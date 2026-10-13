using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Cypher.Ast;
using CodeExplorer.Cypher.Compiler;
using CodeExplorer.Cypher.Linq;
using CodeExplorer.Cypher.Parser;
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeExplorer.Core.Database;

public partial class SqliteGraphClient
{
    public async Task<Dictionary<(string Kind, string Name), string>> LoadSymbolsByNamesAsync(
        IEnumerable<string> names,
        string workspaceId,
        CancellationToken cancellationToken = default)
    {
        var distinctNames = names
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (distinctNames.Count == 0) return [];

        var widPrefix = string.IsNullOrEmpty(workspaceId) ? "" : (workspaceId.EndsWith(':') ? workspaceId : workspaceId + ":");
        var namesJson = JsonSerializer.Serialize(distinctNames);
        var result = new Dictionary<(string Kind, string Name), string>();

        await _lock.WaitAsync(cancellationToken);
        try
        {
            await using var cmdInit = _conn.CreateCommand();
            cmdInit.Parameters.AddWithValue("@namesJson", namesJson);
            cmdInit.CommandText = """
                                  DROP TABLE IF EXISTS temp_target_names;
                                  CREATE TEMP TABLE temp_target_names (val TEXT PRIMARY KEY);
                                  INSERT OR IGNORE INTO temp_target_names (val) SELECT value FROM json_each(@namesJson);
                                  """;
            await cmdInit.ExecuteNonQueryAsync(cancellationToken);

            await using var cmd = _conn.CreateCommand();
            cmd.Parameters.AddWithValue("@widPrefix", widPrefix);

            cmd.CommandText = """
                              SELECT n.id, n.kind, json_extract(n.properties, '$.name') AS name,
                                     json_extract(p.properties, '$.name') AS parent_name
                              FROM temp_target_names t
                              JOIN nodes n ON json_extract(n.properties, '$.name') = t.val
                              LEFT JOIN edges e ON e.to_id = n.id AND e.kind IN ('HAS_METHOD', 'CONTAINS')
                              LEFT JOIN nodes p ON p.id = e.from_id AND p.kind = 'Type'
                              WHERE n.kind IN ('Type', 'Function', 'Procedure', 'Table', 'EntryPoint', 'Endpoint')
                                AND (length(@widPrefix) = 0 OR n.id LIKE @widPrefix || '%');
                              """;

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                var id = reader.GetString(0);
                var kind = reader.GetString(1);
                var name = reader.IsDBNull(2) ? "" : reader.GetString(2);
                var parentName = reader.IsDBNull(3) ? null : reader.GetString(3);

                if (!string.IsNullOrEmpty(name))
                {
                    result[(kind, name)] = id;

                    if (kind == "Endpoint" || kind == "EntryPoint")
                    {
                        result[(kind, name.Replace(":", " "))] = id;
                    }

                    if (kind == "Function" && !string.IsNullOrEmpty(parentName))
                    {
                        result[(kind, $"{parentName}.{name}")] = id;
                    }
                }
            }
        }
        finally
        {
            try
            {
                await using var cmdClean = _conn.CreateCommand();
                cmdClean.CommandText = "DROP TABLE IF EXISTS temp_target_names;";
                await cmdClean.ExecuteNonQueryAsync(CancellationToken.None);
            }
            catch
            {
                // Best effort cleanup
            }

            _lock.Release();
        }

        return result;
    }

    public async Task<Dictionary<string, List<string>>> LoadImplementationsForTypesAsync(
        IEnumerable<string> typeNames,
        string workspaceId,
        CancellationToken cancellationToken = default)
    {
        var distinctTypes = typeNames
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (distinctTypes.Count == 0) return [];

        var widPrefix = string.IsNullOrEmpty(workspaceId) ? "" : (workspaceId.EndsWith(':') ? workspaceId : workspaceId + ":");
        var typesJson = JsonSerializer.Serialize(distinctTypes);
        var result = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        await _lock.WaitAsync(cancellationToken);
        try
        {
            await using var cmdInit = _conn.CreateCommand();
            cmdInit.Parameters.AddWithValue("@typesJson", typesJson);
            cmdInit.CommandText = """
                                  DROP TABLE IF EXISTS temp_target_types;
                                  CREATE TEMP TABLE temp_target_types (val TEXT PRIMARY KEY);
                                  INSERT OR IGNORE INTO temp_target_types (val) SELECT value FROM json_each(@typesJson);
                                  """;
            await cmdInit.ExecuteNonQueryAsync(cancellationToken);

            await using var cmd = _conn.CreateCommand();
            cmd.Parameters.AddWithValue("@widPrefix", widPrefix);

            cmd.CommandText = """
                              SELECT json_extract(t_target.properties, '$.name') AS iface_name,
                                     json_extract(t_impl.properties, '$.name') AS impl_name
                              FROM temp_target_types tt
                              JOIN nodes t_target ON json_extract(t_target.properties, '$.name') = tt.val AND t_target.kind = 'Type'
                              JOIN edges e ON e.to_id = t_target.id AND e.kind = 'IMPLEMENTS'
                              JOIN nodes t_impl ON t_impl.id = e.from_id AND t_impl.kind = 'Type'
                              WHERE (length(@widPrefix) = 0 OR e.from_id LIKE @widPrefix || '%');
                              """;

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                var iface = reader.IsDBNull(0) ? null : reader.GetString(0);
                var impl = reader.IsDBNull(1) ? null : reader.GetString(1);

                if (!string.IsNullOrEmpty(iface) && !string.IsNullOrEmpty(impl))
                {
                    if (!result.TryGetValue(iface, out var list))
                    {
                        list = [];
                        result[iface] = list;
                    }

                    if (!list.Contains(impl))
                    {
                        list.Add(impl);
                    }
                }
            }
        }
        finally
        {
            try
            {
                await using var cmdClean = _conn.CreateCommand();
                cmdClean.CommandText = "DROP TABLE IF EXISTS temp_target_types;";
                await cmdClean.ExecuteNonQueryAsync(CancellationToken.None);
            }
            catch
            {
                // Best effort cleanup
            }

            _lock.Release();
        }

        return result;
    }

    public async Task<(List<EndpointNode> Endpoints, List<EntryPointNode> EntryPoints, List<ExternalServiceNode> ExternalServices, List<ProjectNode> Projects)> LoadLateBindingCandidatesAsync(
        string workspaceId,
        bool needEndpoints,
        bool needExternalServices,
        CancellationToken cancellationToken = default)
    {
        var endpoints = new List<EndpointNode>();
        var entryPoints = new List<EntryPointNode>();
        var externalServices = new List<ExternalServiceNode>();
        var projects = new List<ProjectNode>();

        if (!needEndpoints && !needExternalServices)
        {
            return (endpoints, entryPoints, externalServices, projects);
        }

        var widPrefix = string.IsNullOrEmpty(workspaceId) ? "" : (workspaceId.EndsWith(':') ? workspaceId : workspaceId + ":");

        await _lock.WaitAsync(cancellationToken);
        try
        {
            await using var cmd = _conn.CreateCommand();

            cmd.Parameters.AddWithValue("@widPrefix", widPrefix);
            cmd.Parameters.AddWithValue("@needEndpoints", needEndpoints ? 1 : 0);
            cmd.Parameters.AddWithValue("@needExternalServices", needExternalServices ? 1 : 0);

            cmd.CommandText = """
                              SELECT id, kind, properties
                              FROM nodes
                              WHERE (length(@widPrefix) = 0 OR id LIKE @widPrefix || '%')
                                AND (
                                  (@needEndpoints = 1 AND kind IN ('Endpoint', 'EntryPoint'))
                                  OR
                                  (@needExternalServices = 1 AND kind = 'ExternalService')
                                  OR
                                  kind = 'Project'
                                );
                              """;

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                var id = reader.GetString(0);
                var kind = reader.GetString(1);
                var propsJson = reader.GetString(2);

                try
                {
                    using var doc = JsonDocument.Parse(propsJson);

                    var root = doc.RootElement;

                    if (kind == "Endpoint")
                    {
                        var name = root.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";

                        var filePath = root.TryGetProperty("path", out var p)
                            ? p.GetString() ?? ""
                            : (root.TryGetProperty("file_path", out var fp) ? fp.GetString() ?? "" : "");

                        var httpMethod = root.TryGetProperty("http_method", out var hm)
                            ? hm.GetString() ?? ""
                            : "";
                        var route = root.TryGetProperty("route_template", out var rt)
                            ? rt.GetString() ?? ""
                            : (root.TryGetProperty("route", out var r) ? r.GetString() ?? "" : "");
                        endpoints.Add(new EndpointNode(id, name, filePath, httpMethod, route));
                    }
                    else if (kind == "EntryPoint")
                    {
                        var name = root.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                        var path = root.TryGetProperty("path", out var p)
                            ? p.GetString() ?? ""
                            : (root.TryGetProperty("file_path", out var fp) ? fp.GetString() ?? "" : "");
                        var type = root.TryGetProperty("entry_type", out var et)
                            ? et.GetString() ?? ""
                            : (root.TryGetProperty("type", out var t) ? t.GetString() ?? "" : "");
                        entryPoints.Add(new EntryPointNode(id, name, path, type));
                    }
                    else if (kind == "ExternalService")
                    {
                        var name = root.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";

                        var protocol = root.TryGetProperty("protocol", out var pr)
                            ? pr.GetString() ?? ""
                            : (root.TryGetProperty("service_type", out var st) ? st.GetString() ?? "" : "");

                        var domain = root.TryGetProperty("domain_or_service", out var d)
                            ? d.GetString() ?? ""
                            : (root.TryGetProperty("domain", out var dom) ? dom.GetString() ?? "" : (root.TryGetProperty("service", out var s) ? s.GetString() ?? "" : ""));

                        var path = root.TryGetProperty("path", out var p)
                            ? p.GetString() ?? ""
                            : (root.TryGetProperty("target_path", out var tp) ? tp.GetString() ?? "" : "");
                        externalServices.Add(new ExternalServiceNode(id, name, protocol, domain, path));
                    }
                    else if (kind == "Project")
                    {
                        var name = root.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                        var path = root.TryGetProperty("path", out var p) ? p.GetString() ?? "" : "";
                        var projectType = root.TryGetProperty("project_type", out var pt)
                            ? pt.GetString() ?? ""
                            : (root.TryGetProperty("language", out var l) ? l.GetString() ?? "" : "");
                        var role = root.TryGetProperty("role", out var r) ? r.GetString() ?? "Service" : "Service";
                        var isLib = root.TryGetProperty("is_library", out var il) && il.GetBoolean();
                        projects.Add(new ProjectNode(id, name, path, projectType, role, isLib));
                    }
                }
                catch
                {
                    // Ignore malformed node properties
                }
            }
        }
        finally
        {
            _lock.Release();
        }

        return (endpoints, entryPoints, externalServices, projects);
    }

    public async Task<PostIndexGraphData> LoadPostIndexGraphDataAsync(string widPrefix)
    {
        await _lock.WaitAsync();
        try
        {
            // 1. Sinks (ExternalService, DB, Database, Query, CloudService)
            var sinks = new Dictionary<string, string>();
            var sinkDomains = new Dictionary<string, string?>();
            await using (var cmd = _conn.CreateCommand())
            {
                cmd.CommandText = "SELECT id, kind, COALESCE(json_extract(properties, '$.domain_or_service'), json_extract(properties, '$.name')) FROM nodes WHERE kind IN ('ExternalService', 'DB', 'Database', 'Query', 'CloudService');";

                await using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var id = reader.GetString(0);
                        var kind = reader.GetString(1);
                        var domain = reader.IsDBNull(2) ? null : reader.GetString(2);
                        sinks[id] = kind;
                        sinkDomains[id] = domain;
                    }
                }
            }

            // 2. CALLS edges (including reversed CALLED_BY, QUERIED_BY, and USES_DB)
            var callsAdjacency = new Dictionary<string, List<string>>();
            await using (var cmd = _conn.CreateCommand())
            {
                cmd.CommandText = """
                    SELECT from_id, to_id FROM edges WHERE kind IN ('CALLS', 'CALLS_ENDPOINT', 'USES_DB')
                    UNION
                    SELECT to_id, from_id FROM edges WHERE kind IN ('CALLED_BY', 'QUERIED_BY');
                    """;

                await using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var from = reader.GetString(0);
                        var to = reader.GetString(1);

                        if (!callsAdjacency.TryGetValue(from, out var list))
                        {
                            list = [];
                            callsAdjacency[from] = list;
                        }

                        list.Add(to);
                    }
                }
            }

            // 3. IMPLEMENTS, IMPLEMENTED_BY, and EXPOSED_BY edges
            var implements = new Dictionary<string, List<string>>(); // epId -> list of fnIds
            await using (var cmd = _conn.CreateCommand())
            {
                cmd.CommandText = "SELECT from_id, to_id, kind FROM edges WHERE kind IN ('IMPLEMENTS', 'IMPLEMENTED_BY', 'EXPOSED_BY');";

                await using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var from = reader.GetString(0);
                        var to = reader.GetString(1);
                        var kind = reader.GetString(2);
                        var epId = kind == "IMPLEMENTS" ? to : from;
                        var fnId = kind == "IMPLEMENTS" ? from : to;

                        if (!implements.TryGetValue(epId, out var list))
                        {
                            list = [];
                            implements[epId] = list;
                        }

                        if (!list.Contains(fnId))
                        {
                            list.Add(fnId);
                        }
                    }
                }
            }

            // 4. EntryPoints
            var entryPointIds = new List<string>();
            await using (var cmd = _conn.CreateCommand())
            {
                cmd.CommandText = "SELECT id FROM nodes WHERE kind IN ('EntryPoint', 'Endpoint') AND (length(@widPrefix) = 0 OR id LIKE @widPrefix || '%');";
                cmd.Parameters.AddWithValue("@widPrefix", widPrefix);

                await using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        entryPointIds.Add(reader.GetString(0));
                    }
                }
            }

            // 5. Callers (Function / Method)
            var callers = new List<string>();
            await using (var cmd = _conn.CreateCommand())
            {
                cmd.CommandText = "SELECT id FROM nodes WHERE kind IN ('Function', 'Method') AND (length(@widPrefix) = 0 OR id LIKE @widPrefix || '%');";
                cmd.Parameters.AddWithValue("@widPrefix", widPrefix);

                await using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        callers.Add(reader.GetString(0));
                    }
                }
            }

            // 6. Project to EntryPoints
            var projectToEntryPoints = new Dictionary<string, List<string>>();
            var epSet = new HashSet<string>(entryPointIds);
            await using (var cmd = _conn.CreateCommand())
            {
                cmd.CommandText = """
                    SELECT p.id, e.to_id
                    FROM nodes p
                    JOIN edges e ON e.from_id = p.id AND e.kind IN ('CONTAINS', 'EXPOSES')
                    WHERE p.kind = 'Project' AND (length(@widPrefix) = 0 OR p.id LIKE @widPrefix || '%')
                    UNION
                    SELECT p.id, e2.to_id
                    FROM nodes p
                    JOIN edges e1 ON e1.from_id = p.id AND e1.kind IN ('CONTAINS', 'EXPOSES')
                    JOIN edges e2 ON e2.from_id = e1.to_id AND e2.kind IN ('CONTAINS', 'EXPOSES')
                    WHERE p.kind = 'Project' AND (length(@widPrefix) = 0 OR p.id LIKE @widPrefix || '%')
                    UNION
                    SELECT p.id, e.to_id
                    FROM nodes p
                    JOIN edges b ON b.to_id = p.id AND b.kind = 'BELONGS_TO'
                    JOIN edges e ON e.from_id = b.from_id AND e.kind IN ('CONTAINS', 'EXPOSES')
                    WHERE p.kind = 'Project' AND (length(@widPrefix) = 0 OR p.id LIKE @widPrefix || '%');
                    """;
                cmd.Parameters.AddWithValue("@widPrefix", widPrefix);

                await using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var projId = reader.GetString(0);
                        var toId = reader.GetString(1);
                        if (!epSet.Contains(toId)) continue;

                        if (!projectToEntryPoints.TryGetValue(projId, out var epList))
                        {
                            epList = [];
                            projectToEntryPoints[projId] = epList;
                        }

                        if (!epList.Contains(toId))
                        {
                            epList.Add(toId);
                        }
                    }
                }
            }

            return new PostIndexGraphData(callsAdjacency, sinks, sinkDomains, callers, implements, entryPointIds, projectToEntryPoints);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SavePostIndexResultsAsync(string widPrefix, PostIndexAnalysisResult result)
    {
        var sw = Stopwatch.StartNew();
        await _lock.WaitAsync();
        try
        {
            // 1. Delete old TRANSITIVELY_CALLS and ATTRIBUTED_TO for workspace
            await using var tx = (SqliteTransaction)await _conn.BeginTransactionAsync();

            await using (var delCmd = _conn.CreateCommand())
            {
                delCmd.Transaction = tx;

                delCmd.CommandText =
                    "DELETE FROM edges WHERE kind = 'TRANSITIVELY_CALLS' AND (length(@widPrefix) = 0 OR from_id LIKE @widPrefix || '%');";
                delCmd.Parameters.AddWithValue("@widPrefix", widPrefix);
                await delCmd.ExecuteNonQueryAsync();

                delCmd.CommandText =
                    "DELETE FROM edges WHERE kind = 'ATTRIBUTED_TO' AND (length(@widPrefix) = 0 OR from_id LIKE @widPrefix || '%');";
                await delCmd.ExecuteNonQueryAsync();
            }

            // 2. Insert TRANSITIVELY_CALLS
            if (result.TransitivelyCalls.Count > 0)
            {
                await using var insCmd = _conn.CreateCommand();

                insCmd.Transaction = tx;

                insCmd.CommandText =
                    "INSERT OR REPLACE INTO edges (from_id, to_id, kind, properties) VALUES (@from, @to, 'TRANSITIVELY_CALLS', @props);";
                var pFrom = insCmd.Parameters.Add("@from", SqliteType.Text);
                var pTo = insCmd.Parameters.Add("@to", SqliteType.Text);
                var pProps = insCmd.Parameters.Add("@props", SqliteType.Text);

                foreach (var rel in result.TransitivelyCalls)
                {
                    pFrom.Value = rel.From;
                    pTo.Value = rel.To;
                    pProps.Value = $"{{\"hops\":{rel.Hops}}}";
                    await insCmd.ExecuteNonQueryAsync();
                }
            }

            // 3. Insert ATTRIBUTED_TO
            if (result.AttributedTo.Count > 0)
            {
                await using var insCmd = _conn.CreateCommand();

                insCmd.Transaction = tx;

                insCmd.CommandText =
                    "INSERT OR REPLACE INTO edges (from_id, to_id, kind, properties) VALUES (@from, @to, 'ATTRIBUTED_TO', @props);";
                var pFrom = insCmd.Parameters.Add("@from", SqliteType.Text);
                var pTo = insCmd.Parameters.Add("@to", SqliteType.Text);
                var pProps = insCmd.Parameters.Add("@props", SqliteType.Text);

                foreach (var rel in result.AttributedTo)
                {
                    pFrom.Value = rel.EpId;
                    pTo.Value = rel.SinkId;

                    var props = new Dictionary<string, object>
                    {
                        ["hops"] = rel.Hops, ["sink_kind"] = rel.SinkKind
                    };
                    pProps.Value = JsonSerializer.Serialize(props);
                    await insCmd.ExecuteNonQueryAsync();
                }
            }

            // 4. Update Project external_apis
            if (result.ProjectExternalApis.Count > 0)
            {
                await using var updCmd = _conn.CreateCommand();

                updCmd.Transaction = tx;

                updCmd.CommandText =
                    "UPDATE nodes SET properties = json_set(properties, '$.external_apis', json(@domains)) WHERE id = @id;";
                var pDomains = updCmd.Parameters.Add("@domains", SqliteType.Text);
                var pId = updCmd.Parameters.Add("@id", SqliteType.Text);

                foreach (var (projId, domains) in result.ProjectExternalApis)
                {
                    pId.Value = projId;
                    pDomains.Value = JsonSerializer.Serialize(domains);
                    await updCmd.ExecuteNonQueryAsync();
                }
            }

            await tx.CommitAsync();
            sw.Stop();

            _logger.LogInformation(
                "[DB:PostIndex] Saved {TcCount} TRANSITIVELY_CALLS, {AttrCount} ATTRIBUTED_TO, and {ApiCount} project external_apis for prefix '{Prefix}' in {ElapsedMs:F1}ms",
                result.TransitivelyCalls.Count, result.AttributedTo.Count, result.ProjectExternalApis.Count,
                widPrefix, sw.Elapsed.TotalMilliseconds);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task UpdateProjectExternalApisAsync(Dictionary<string, List<string>> projectExternalApis)
    {
        if (projectExternalApis.Count == 0) return;
        await _lock.WaitAsync();
        try
        {
            await using var tx = (SqliteTransaction)await _conn.BeginTransactionAsync();

            await using var updCmd = _conn.CreateCommand();

            updCmd.Transaction = tx;

            updCmd.CommandText =
                "UPDATE nodes SET properties = json_set(properties, '$.external_apis', json(@domains)) WHERE id = @id;";
            var pDomains = updCmd.Parameters.Add("@domains", SqliteType.Text);
            var pId = updCmd.Parameters.Add("@id", SqliteType.Text);

            foreach (var (projId, domains) in projectExternalApis)
            {
                pId.Value = projId;
                pDomains.Value = JsonSerializer.Serialize(domains);
                await updCmd.ExecuteNonQueryAsync();
            }

            await tx.CommitAsync();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<Dictionary<string, FileRegistryEntry>> LoadFileRegistryAsync(
        CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            const string sql = "SELECT relative_path AS RelativePath, content_hash AS ContentHash, last_modified_utc AS LastModifiedUtcStr, project_path AS ProjectPath, snapshot_json AS SnapshotJson FROM file_registry;";
            var rows = await _conn.QueryAsync<(string RelativePath, string ContentHash, string LastModifiedUtcStr, string ProjectPath, string? SnapshotJson)>(
                new CommandDefinition(sql, cancellationToken: cancellationToken));

            var result = new Dictionary<string, FileRegistryEntry>(StringComparer.OrdinalIgnoreCase);
            foreach (var (relPath, hash, lastModStr, projPath, snapshotJson) in rows)
            {
                var lastMod = DateTime.TryParse(lastModStr, null,
                    System.Globalization.DateTimeStyles.RoundtripKind, out var dt)
                    ? dt
                    : DateTime.MinValue;
                result[relPath] = new FileRegistryEntry(hash, lastMod, projPath, snapshotJson);
            }

            return result;
        }
        finally
        {
            _lock.Release();
        }
    }

    public Task SaveFileRegistryEntriesAsync(
        IEnumerable<(string RelativePath, string ContentHash, DateTime LastModifiedUtc, string ProjectPath)> entries,
        CancellationToken cancellationToken = default) =>
        SaveFileRegistryEntriesAsync(entries.Select(e => (e.RelativePath, e.ContentHash, e.LastModifiedUtc, e.ProjectPath, (string?)null)), cancellationToken);

    public async Task SaveFileRegistryEntriesAsync(
        IEnumerable<(string RelativePath, string ContentHash, DateTime LastModifiedUtc, string ProjectPath, string? SnapshotJson)> entries,
        CancellationToken cancellationToken = default)
    {
        var entryList = entries.Select(e => new
        {
            relPath = e.RelativePath,
            hash = e.ContentHash,
            lastMod = e.LastModifiedUtc.ToString("o"),
            projPath = e.ProjectPath,
            snapshotJson = e.SnapshotJson
        }).ToList();
        if (entryList.Count == 0) return;

        await _lock.WaitAsync(cancellationToken);
        try
        {
            await using var tx = await _conn.BeginTransactionAsync(cancellationToken);

            const string sql = """
                               INSERT INTO file_registry (relative_path, content_hash, last_modified_utc, project_path, snapshot_json)
                               VALUES (@relPath, @hash, @lastMod, @projPath, @snapshotJson)
                               ON CONFLICT(relative_path) DO UPDATE SET
                                   content_hash = excluded.content_hash,
                                   last_modified_utc = excluded.last_modified_utc,
                                   project_path = excluded.project_path,
                                   snapshot_json = coalesce(excluded.snapshot_json, file_registry.snapshot_json);
                               """;

            await _conn.ExecuteAsync(new CommandDefinition(sql, entryList, transaction: tx, cancellationToken: cancellationToken));
            await tx.CommitAsync(cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task DeleteFileRegistryEntriesAsync(
        IEnumerable<string> relativePaths,
        CancellationToken cancellationToken = default)
    {
        var pathList = relativePaths.Select(p => new { relPath = p }).ToList();
        if (pathList.Count == 0) return;

        await _lock.WaitAsync(cancellationToken);
        try
        {
            await using var tx = await _conn.BeginTransactionAsync(cancellationToken);

            const string sql = "DELETE FROM file_registry WHERE relative_path = @relPath;";
            await _conn.ExecuteAsync(new CommandDefinition(sql, pathList, transaction: tx, cancellationToken: cancellationToken));

            await tx.CommitAsync(cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }
}
