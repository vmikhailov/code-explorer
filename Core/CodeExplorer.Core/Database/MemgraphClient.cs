using System.Text.Json;
using CodeExplorer.Common;
using Neo4j.Driver;

namespace CodeExplorer.Database;

public class MemgraphClient(string boltUrl, string username, string password) : IAsyncDisposable
{
    private readonly IDriver _driver = GraphDatabase.Driver(
        boltUrl,
        string.IsNullOrEmpty(username) ? AuthTokens.None : AuthTokens.Basic(username, password)
    );

    public async Task CreateIndicesAsync()
    {
        var kinds = new[] 
        { 
            OntologyConstants.NodeLabels.Workspace, 
            OntologyConstants.NodeLabels.WorkspaceFolder, 
            OntologyConstants.NodeLabels.ProjectFolder,
            OntologyConstants.NodeLabels.Project, 
            OntologyConstants.NodeLabels.File, 
            OntologyConstants.NodeLabels.Class, 
            OntologyConstants.NodeLabels.Interface,
            OntologyConstants.NodeLabels.Function, 
            OntologyConstants.NodeLabels.Variable, 
            OntologyConstants.NodeLabels.Package,
            OntologyConstants.NodeLabels.DB,
            OntologyConstants.NodeLabels.DataSet,
            OntologyConstants.NodeLabels.Table,
            OntologyConstants.NodeLabels.Procedure,
            OntologyConstants.NodeLabels.Query
        };
        await using var session = _driver.AsyncSession(o => o.WithDefaultAccessMode(AccessMode.Write));
        
        foreach (var kind in kinds)
        {
            var query = $"CREATE INDEX ON :{kind}(id);";
            try
            {
                await session.RunAsync(query);
            }
            catch (Exception ex)
            {
                await Console.Error.WriteLineAsync($"Warning creating index for {kind}: {ex.Message}");
            }
        }

        try
        {
            await session.RunAsync("CREATE INDEX ON :Entity(id);");
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"Warning creating index for Entity: {ex.Message}");
        }

        try
        {
            await session.RunAsync($"CREATE INDEX ON :{OntologyConstants.NodeLabels.Workspace}(path);");
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"Warning creating path index for Workspace: {ex.Message}");
        }
    }

    public async Task ClearDatabaseAsync()
    {
        await using var session = _driver.AsyncSession(o => o.WithDefaultAccessMode(AccessMode.Write));
        await session.ExecuteWriteAsync(async tx =>
        {
            await tx.RunAsync("MATCH (n) DETACH DELETE n");
        });
    }

    public async Task ClearWorkspaceAsync(string workspacePath)
    {
        await using var session = _driver.AsyncSession(o => o.WithDefaultAccessMode(AccessMode.Write));
        await session.ExecuteWriteAsync(async tx =>
        {
            await tx.RunAsync(
                $"MATCH (r:{OntologyConstants.NodeLabels.Workspace} {{path: $workspacePath}})-[:{OntologyConstants.Relationships.Contains}*0..]->(n) DETACH DELETE n",
                new { workspacePath }
            );
        });
    }

    public async Task UploadNodesAsync(List<Node> nodes)
    {
        if (nodes.Count == 0) return;

        var nodesByKind = nodes.GroupBy(n => n.Kind);
        await using var session = _driver.AsyncSession(o => o.WithDefaultAccessMode(AccessMode.Write));

        foreach (var group in nodesByKind)
        {
            var kind = group.Key;
            var batch = new List<Dictionary<string, object>>();

            foreach (var node in group)
            {
                var props = new Dictionary<string, object>
                {
                    ["id"] = node.Id
                };
                foreach (var (k, v) in node.Properties)
                {
                    props[k] = v;
                }
                batch.Add(props);
            }

            var query = $"UNWIND $batch AS row MERGE (n:{kind} {{ id: row.id }}) SET n:Entity, n = row";
            await session.ExecuteWriteAsync(async tx =>
            {
                await tx.RunAsync(query, new { batch });
            });
        }
    }

    public async Task UploadRelationshipsAsync(List<Relationship> rels)
    {
        if (rels.Count == 0) return;

        var relsByKind = rels.GroupBy(r => r.Kind);
        await using var session = _driver.AsyncSession(o => o.WithDefaultAccessMode(AccessMode.Write));

        foreach (var group in relsByKind)
        {
            var kind = group.Key;
            var relList = group.ToList();

            const int batchSize = 1000;
            for (int i = 0; i < relList.Count; i += batchSize)
            {
                var chunk = relList.Skip(i).Take(batchSize).Select(r => new Dictionary<string, object>
                {
                    ["from"] = r.From,
                    ["to"] = r.To,
                    ["properties"] = r.Properties
                }).ToList();

                var query = $"UNWIND $batch AS row MATCH (from:Entity {{ id: row.from }}), (to:Entity {{ id: row.to }}) MERGE (from)-[r:{kind}]->(to) SET r = row.properties";
                await session.ExecuteWriteAsync(async tx =>
                {
                    await tx.RunAsync(query, new { batch = chunk });
                });
            }
        }
    }

    public async Task<string> ExecuteQueryAsync(string query, object? parameters = null)
    {
        var forbidden = new[] { "create ", "merge ", "delete ", "remove ", "drop ", "set ", "detach " };
        var lowerQuery = query.ToLowerInvariant();
        foreach (var word in forbidden)
        {
            if (lowerQuery.Contains(word))
            {
                throw new InvalidOperationException($"Security violation: modifying keyword '{word.Trim()}' is not allowed in sandbox mode.");
            }
        }

        await using var session = _driver.AsyncSession(o => o.WithDefaultAccessMode(AccessMode.Read));
        
        var records = await session.ExecuteReadAsync(async tx =>
        {
            var cursor = parameters != null ? await tx.RunAsync(query, parameters) : await tx.RunAsync(query);
            var results = new List<Dictionary<string, object>>();

            await foreach (var record in cursor)
            {
                var recordMap = new Dictionary<string, object>();
                foreach (var key in record.Keys)
                {
                    recordMap[key] = FormatValue(record[key]);
                }
                results.Add(recordMap);
            }

            return results;
        });

        return JsonSerializer.Serialize(records, new JsonSerializerOptions { WriteIndented = true });
    }

    private static object FormatValue(object val)
    {
        switch (val)
        {
            case INode node:
                return new Dictionary<string, object>
                {
                    ["id"] = node.Properties.TryGetValue("id", out var idVal) ? idVal : node.ElementId,
                    ["labels"] = node.Labels,
                    ["props"] = node.Properties
                };
            case IRelationship rel:
                return new Dictionary<string, object>
                {
                    ["id"] = rel.ElementId,
                    ["type"] = rel.Type,
                    ["start"] = rel.StartNodeElementId,
                    ["end"] = rel.EndNodeElementId,
                    ["props"] = rel.Properties
                };
            case List<object> list:
                return list.Select(FormatValue).ToList();
            case Dictionary<string, object> dict:
                return dict.ToDictionary(k => k.Key, v => FormatValue(v.Value));
            default:
                return val;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _driver.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
