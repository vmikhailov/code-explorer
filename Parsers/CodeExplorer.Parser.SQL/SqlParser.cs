using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CodeExplorer.Database;
using CodeExplorer.Common;
using TreeSitter;
using Node = CodeExplorer.Database.Node;

namespace CodeExplorer.Parser;

public class SqlParser : IProjectParser, IFileParser
{
    public string LanguageName => "sql";

    public string ProjectType => "sql";

    public IReadOnlyCollection<string> ExcludedFolders => Array.Empty<string>();

    public bool CanParse(string fileExtension)
    {
        return fileExtension.Equals(".sql", StringComparison.OrdinalIgnoreCase);
    }

    public bool IsProjectDirectory(string directoryPath, string[] filesInDirectory)
    {
        return filesInDirectory.Any(f => Path.GetExtension(f).Equals(".sql", StringComparison.OrdinalIgnoreCase));
    }

    public string? MapNodeType(TreeSitter.Node node)
    {
        // Custom parser does not use TreeSitter Nodes
        return null;
    }

    public string? ExtractIdentifier(TreeSitter.Node node)
    {
        // Custom parser does not use TreeSitter Nodes
        return null;
    }

    public void CollectReferences(TreeSitter.Node node, string scopeSymbolId, List<Reference> references)
    {
        // Custom parser does not use TreeSitter Nodes
    }

    public Task<ProducedPackageInfo?> GetProducedPackageAsync(string projectDirectory)
    {
        return Task.FromResult<ProducedPackageInfo?>(null);
    }

    public Task<ProjectDependencyInfo> ParseDependenciesAsync(string projectDirectory)
    {
        return Task.FromResult(new ProjectDependencyInfo(new List<string>(), new List<ProducedPackageInfo>()));
    }

    public bool UsesTreeSitter => false;

    public async Task ParseCustomAsync(string filePath, string parentNodeId, ParsingContext ctx)
    {
        var sqlText = await File.ReadAllTextAsync(filePath);

        // 1. Clean SQL comments to avoid false matches
        var withoutBlockComments = Regex.Replace(sqlText, @"/\*.*?\*/", "", RegexOptions.Singleline);
        var cleanSql = Regex.Replace(withoutBlockComments, @"--.*$", "", RegexOptions.Multiline);

        // 2. Identify Database (DB)
        var dbMatch = Regex.Match(cleanSql, @"CREATE\s+DATABASE\s+([a-zA-Z0-9_\[\]""#@]+)", RegexOptions.IgnoreCase);
        string dbNodeId;
        string dbName;
        if (dbMatch.Success)
        {
            dbName = dbMatch.Groups[1].Value.Trim('[', ']', '"');
            dbNodeId = $"db:{dbName.ToLowerInvariant()}";
        }
        else
        {
            var dirName = Path.GetFileName(Path.GetDirectoryName(filePath));
            if (string.IsNullOrEmpty(dirName)) dirName = "DefaultDB";
            dbName = dirName;
            dbNodeId = $"db:{dbName.ToLowerInvariant()}";
        }

        var dbNode = new Node(dbNodeId, OntologyConstants.NodeLabels.DB, new Dictionary<string, object>
        {
            ["name"] = dbName,
            ["path"] = Path.GetRelativePath(ctx.AbsoluteWorkspacePath, filePath).Replace('\\', '/')
        });
        await ctx.EnqueueUploadNodesAsync(new List<Node> { dbNode });
        ctx.IncrementNodeKind(OntologyConstants.NodeLabels.DB);
        ctx.AddNodesCount(1);

        var dbRel = new Relationship(parentNodeId, dbNodeId, OntologyConstants.Relationships.Contains);
        await ctx.EnqueueUploadRelationshipsAsync(new List<Relationship> { dbRel });
        ctx.AddRelsCount(1);

        // 3. Identify Schema (DataSet)
        var schemaMatches = Regex.Matches(cleanSql, @"CREATE\s+SCHEMA\s+([a-zA-Z0-9_\[\]""#@]+)", RegexOptions.IgnoreCase);
        var datasets = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in schemaMatches)
        {
            var schemaName = match.Groups[1].Value.Trim('[', ']', '"');
            var schemaNodeId = $"{dbNodeId}:dataset:{schemaName.ToLowerInvariant()}";
            datasets[schemaName] = schemaNodeId;

            var schemaNode = new Node(schemaNodeId, OntologyConstants.NodeLabels.DataSet, new Dictionary<string, object>
            {
                ["name"] = schemaName,
                ["path"] = Path.GetRelativePath(ctx.AbsoluteWorkspacePath, filePath).Replace('\\', '/')
            });
            await ctx.EnqueueUploadNodesAsync(new List<Node> { schemaNode });
            ctx.IncrementNodeKind(OntologyConstants.NodeLabels.DataSet);
            ctx.AddNodesCount(1);

            var schemaRel = new Relationship(dbNodeId, schemaNodeId, OntologyConstants.Relationships.Contains);
            await ctx.EnqueueUploadRelationshipsAsync(new List<Relationship> { schemaRel });
            ctx.AddRelsCount(1);
        }

        // 4. Identify Tables
        var tableMatches = Regex.Matches(cleanSql, @"CREATE\s+TABLE\s+([a-zA-Z0-9_\.\[\]""#@]+)", RegexOptions.IgnoreCase);
        var tables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in tableMatches)
        {
            var rawTableName = match.Groups[1].Value;
            var parts = rawTableName.Split('.');
            string schemaName = "dbo";
            string tableName = rawTableName;
            if (parts.Length > 1)
            {
                schemaName = parts[0].Trim('[', ']', '"');
                tableName = parts[1].Trim('[', ']', '"');
            }
            else
            {
                tableName = rawTableName.Trim('[', ']', '"');
            }

            var schemaNodeId = $"{dbNodeId}:dataset:{schemaName.ToLowerInvariant()}";
            if (!datasets.ContainsKey(schemaName))
            {
                datasets[schemaName] = schemaNodeId;
                var schemaNode = new Node(schemaNodeId, OntologyConstants.NodeLabels.DataSet, new Dictionary<string, object>
                {
                    ["name"] = schemaName,
                    ["path"] = Path.GetRelativePath(ctx.AbsoluteWorkspacePath, filePath).Replace('\\', '/')
                });
                await ctx.EnqueueUploadNodesAsync(new List<Node> { schemaNode });
                ctx.IncrementNodeKind(OntologyConstants.NodeLabels.DataSet);
                ctx.AddNodesCount(1);

                var schemaRel = new Relationship(dbNodeId, schemaNodeId, OntologyConstants.Relationships.Contains);
                await ctx.EnqueueUploadRelationshipsAsync(new List<Relationship> { schemaRel });
                ctx.AddRelsCount(1);
            }

            var tableNodeId = $"{schemaNodeId}:table:{tableName.ToLowerInvariant()}";
            tables[tableName] = tableNodeId;
            tables[rawTableName] = tableNodeId;

            var tableNode = new Node(tableNodeId, OntologyConstants.NodeLabels.Table, new Dictionary<string, object>
            {
                ["name"] = tableName,
                ["path"] = Path.GetRelativePath(ctx.AbsoluteWorkspacePath, filePath).Replace('\\', '/')
            });
            await ctx.EnqueueUploadNodesAsync(new List<Node> { tableNode });
            ctx.IncrementNodeKind(OntologyConstants.NodeLabels.Table);
            ctx.AddNodesCount(1);

            var tableRel = new Relationship(schemaNodeId, tableNodeId, OntologyConstants.Relationships.Contains);
            await ctx.EnqueueUploadRelationshipsAsync(new List<Relationship> { tableRel });
            ctx.AddRelsCount(1);

            // Register global table symbol
            ctx.AddGlobalSymbol(OntologyConstants.NodeLabels.Table, tableName, tableNodeId);
            if (parts.Length > 1)
            {
                ctx.AddGlobalSymbol(OntologyConstants.NodeLabels.Table, rawTableName, tableNodeId);
            }
        }

        // 5. Identify Procedures / Functions
        var procMatches = Regex.Matches(cleanSql, @"CREATE\s+(?:PROCEDURE|PROC|FUNCTION)\s+([a-zA-Z0-9_\.\[\]""#@]+)", RegexOptions.IgnoreCase);
        var procedures = new List<(string Name, string RawName, string Id, int StartIndex)>();

        for (int i = 0; i < procMatches.Count; i++)
        {
            var match = procMatches[i];
            var rawProcName = match.Groups[1].Value;
            var parts = rawProcName.Split('.');
            string schemaName = "dbo";
            string procName = rawProcName;
            if (parts.Length > 1)
            {
                schemaName = parts[0].Trim('[', ']', '"');
                procName = parts[1].Trim('[', ']', '"');
            }
            else
            {
                procName = rawProcName.Trim('[', ']', '"');
            }

            var schemaNodeId = $"{dbNodeId}:dataset:{schemaName.ToLowerInvariant()}";
            if (!datasets.ContainsKey(schemaName))
            {
                datasets[schemaName] = schemaNodeId;
                var schemaNode = new Node(schemaNodeId, OntologyConstants.NodeLabels.DataSet, new Dictionary<string, object>
                {
                    ["name"] = schemaName,
                    ["path"] = Path.GetRelativePath(ctx.AbsoluteWorkspacePath, filePath).Replace('\\', '/')
                });
                await ctx.EnqueueUploadNodesAsync(new List<Node> { schemaNode });
                ctx.IncrementNodeKind(OntologyConstants.NodeLabels.DataSet);
                ctx.AddNodesCount(1);

                var schemaRel = new Relationship(dbNodeId, schemaNodeId, OntologyConstants.Relationships.Contains);
                await ctx.EnqueueUploadRelationshipsAsync(new List<Relationship> { schemaRel });
                ctx.AddRelsCount(1);
            }

            var procNodeId = $"{schemaNodeId}:procedure:{procName.ToLowerInvariant()}";
            procedures.Add((procName, rawProcName, procNodeId, match.Index));

            var procNode = new Node(procNodeId, OntologyConstants.NodeLabels.Procedure, new Dictionary<string, object>
            {
                ["name"] = procName,
                ["path"] = Path.GetRelativePath(ctx.AbsoluteWorkspacePath, filePath).Replace('\\', '/')
            });
            await ctx.EnqueueUploadNodesAsync(new List<Node> { procNode });
            ctx.IncrementNodeKind(OntologyConstants.NodeLabels.Procedure);
            ctx.AddNodesCount(1);

            var procRel = new Relationship(schemaNodeId, procNodeId, OntologyConstants.Relationships.Contains);
            await ctx.EnqueueUploadRelationshipsAsync(new List<Relationship> { procRel });
            ctx.AddRelsCount(1);

            // Register global procedure symbol
            ctx.AddGlobalSymbol(OntologyConstants.NodeLabels.Procedure, procName, procNodeId);
            if (parts.Length > 1)
            {
                ctx.AddGlobalSymbol(OntologyConstants.NodeLabels.Procedure, rawProcName, procNodeId);
            }
        }

        // 6. Analyze Procedure Bodies for References & Calls
        for (int i = 0; i < procedures.Count; i++)
        {
            var currentProc = procedures[i];
            int start = currentProc.StartIndex;
            int end = (i + 1 < procedures.Count) ? procedures[i + 1].StartIndex : cleanSql.Length;
            var bodyText = cleanSql.Substring(start, end - start);

            // A. Detect calls to other procedures (EXEC / EXECUTE)
            var execMatches = Regex.Matches(bodyText, @"EXEC(?:UTE)?\s+([a-zA-Z0-9_\.\[\]""#@]+)", RegexOptions.IgnoreCase);
            foreach (Match execMatch in execMatches)
            {
                var targetProcRaw = execMatch.Groups[1].Value;
                var targetProcParts = targetProcRaw.Split('.');
                var targetProcName = targetProcParts.Length > 1 ? targetProcParts[1].Trim('[', ']', '"') : targetProcRaw.Trim('[', ']', '"');
                
                var reference = new Reference(currentProc.Id, targetProcName, OntologyConstants.Relationships.Calls);
                ctx.AddGlobalReferences(new[] { reference });
            }

            // B. Detect local table dependencies (DependsOn)
            foreach (var tableKvp in tables)
            {
                var tableName = tableKvp.Key;
                var tableId = tableKvp.Value;

                var pattern = $@"\b{Regex.Escape(tableName)}\b";
                if (Regex.IsMatch(bodyText, pattern, RegexOptions.IgnoreCase))
                {
                    var depRel = new Relationship(currentProc.Id, tableId, OntologyConstants.Relationships.DependsOn);
                    await ctx.EnqueueUploadRelationshipsAsync(new List<Relationship> { depRel });
                    ctx.AddRelsCount(1);
                }
            }

            // C. Register potential external table dependencies for deferred global resolution
            var words = Regex.Matches(bodyText, @"\b[a-zA-Z0-9_]+\b");
            var uniqueWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (Match wm in words)
            {
                uniqueWords.Add(wm.Value);
            }

            var tableReferences = new List<Reference>();
            foreach (var word in uniqueWords)
            {
                if (!tables.ContainsKey(word))
                {
                    tableReferences.Add(new Reference(currentProc.Id, word, OntologyConstants.Relationships.DependsOn));
                }
            }
            if (tableReferences.Count > 0)
            {
                ctx.AddGlobalReferences(tableReferences);
            }
        }
    }
}
