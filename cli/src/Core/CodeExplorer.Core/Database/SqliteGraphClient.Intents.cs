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
    public Task<List<ProjectSignature>> LoadProjectSignaturesAsync(
        string workspaceId,
        CancellationToken cancellationToken = default) =>
        LoadProjectSignaturesAsync(workspaceId, workloadsOnly: false, cancellationToken);

    public async Task<List<ProjectSignature>> LoadProjectSignaturesAsync(
        string workspaceId,
        bool workloadsOnly,
        CancellationToken cancellationToken = default)
    {
        var projects = new List<(string Id, string Name, string Path, string Role, string? Domain, string? Summary)>();
        var results = new List<ProjectSignature>();

        await _lock.WaitAsync(cancellationToken);
        try
        {
            // 1. Fetch all projects and their architectural roles
            await using (var cmd = _conn.CreateCommand())
            {
                cmd.CommandTimeout = CommandTimeoutSeconds;
                cmd.CommandText = """
                    SELECT id,
                           COALESCE(json_extract(properties, '$.name'), '') AS name,
                           COALESCE(json_extract(properties, '$.path'), '') AS path,
                           COALESCE(json_extract(properties, '$.role'), json_extract(properties, '$.primary_role'), '') AS role,
                           COALESCE(
                               json_extract(properties, '$.intent_domain'),
                               json_extract(properties, '$.domain'),
                               (SELECT json_extract(d.properties, '$.name')
                                FROM edges e
                                JOIN nodes d ON e.to_id = d.id AND d.kind = 'Domain'
                                WHERE e.from_id = nodes.id AND e.kind IN ('EXPOSES_DOMAIN', 'BELONGS_TO_DOMAIN')
                                LIMIT 1)
                           ) AS intent_domain,
                           json_extract(properties, '$.intent_summary') AS intent_summary
                    FROM nodes
                    WHERE kind = 'Project'
                    ORDER BY length(path) DESC;
                    """;

                await using (var reader = await cmd.ExecuteReaderAsync(cancellationToken))
                {
                    while (await reader.ReadAsync(cancellationToken))
                    {
                        var id = reader.GetString(0);
                        var name = reader.GetString(1);
                        var path = reader.GetString(2);
                        var role = reader.GetString(3);
                        var domain = reader.IsDBNull(4) ? null : reader.GetString(4);
                        var summary = reader.IsDBNull(5) ? null : reader.GetString(5);
                        if (string.IsNullOrWhiteSpace(path)) path = name;
                        path = path.Replace('\\', '/').Trim('/');
                        projects.Add((id, name, path, role, domain, summary));
                    }
                }
            }

            if (projects.Count == 0) return results;

            // 2. Fetch architectural nodes (Endpoints, Tables, Topics, Types / classes / interfaces)
            var endpointsByProj = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            var tablesByProj = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            var topicsByProj = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            var typesByProj = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

            await using (var cmd = _conn.CreateCommand())
            {
                cmd.CommandTimeout = CommandTimeoutSeconds;
                cmd.CommandText = """
                    SELECT kind,
                           COALESCE(json_extract(properties, '$.name'), '') AS name,
                           COALESCE(json_extract(properties, '$.path'), json_extract(properties, '$.file_path'), '') AS path,
                           COALESCE(json_extract(properties, '$.method'), '') AS method
                    FROM nodes
                    WHERE kind IN ('Endpoint', 'Table', 'Topic', 'Type', 'class', 'interface', 'struct', 'record');
                    """;

                await using (var reader = await cmd.ExecuteReaderAsync(cancellationToken))
                {
                    while (await reader.ReadAsync(cancellationToken))
                    {
                        var kind = reader.GetString(0);
                        var name = reader.GetString(1);
                        var path = reader.GetString(2).Replace('\\', '/').Trim('/');
                        var method = reader.GetString(3);
                        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(path)) continue;

                        // Match to most specific project path
                        string? matchedProject = null;

                        foreach (var p in projects)
                        {
                            if (path.StartsWith(p.Path, StringComparison.OrdinalIgnoreCase))
                            {
                                matchedProject = p.Name;
                                break;
                            }
                        }

                        if (matchedProject == null) continue;

                        if (kind == "Endpoint")
                        {
                            var epName = !string.IsNullOrEmpty(method) ? $"{method} {name}".Trim() : name;

                            if (!endpointsByProj.TryGetValue(matchedProject, out var epSet))
                            {
                                epSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                                endpointsByProj[matchedProject] = epSet;
                            }

                            epSet.Add(epName);
                        }
                        else if (kind == "Table")
                        {
                            if (!tablesByProj.TryGetValue(matchedProject, out var tblSet))
                            {
                                tblSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                                tablesByProj[matchedProject] = tblSet;
                            }

                            tblSet.Add(name);
                        }
                        else if (kind == "Topic")
                        {
                            if (!topicsByProj.TryGetValue(matchedProject, out var topSet))
                            {
                                topSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                                topicsByProj[matchedProject] = topSet;
                            }

                            topSet.Add(name);
                        }
                        else if (kind is "Type" or "class" or "interface" or "struct" or "record")
                        {
                            var lowerPath = path.ToLowerInvariant();
                            var lowerName = name.ToLowerInvariant();

                            if (lowerPath.Contains("test") || lowerPath.Contains("mock") ||
                                lowerPath.Contains("fake") || lowerPath.Contains("benchmark")) continue;

                            if (lowerName.EndsWith("tests") || lowerName.EndsWith("test") ||
                                lowerName.EndsWith("fixture") || lowerName.EndsWith("mock")) continue;

                            var isDomainSignal = lowerPath.Contains("domain") || lowerPath.Contains("entities") ||
                                                 lowerPath.Contains("models") || lowerPath.Contains("aggregates") ||
                                                 lowerPath.Contains("services") || lowerPath.Contains("contracts") ||
                                                 lowerPath.Contains("core") || lowerName.EndsWith("entity") ||
                                                 lowerName.EndsWith("model") || lowerName.EndsWith("aggregate") ||
                                                 lowerName.EndsWith("repository") || lowerName.EndsWith("service") ||
                                                 lowerName.EndsWith("handler") || lowerName.EndsWith("calculator") ||
                                                 lowerName.EndsWith("rule") || lowerName.EndsWith("policy") ||
                                                 lowerName.EndsWith("manager") || lowerName.EndsWith("engine") ||
                                                 lowerName.EndsWith("dto") || lowerName.EndsWith("record") ||
                                                 lowerName.EndsWith("config");

                            if (isDomainSignal)
                            {
                                if (!typesByProj.TryGetValue(matchedProject, out var typSet))
                                {
                                    typSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                                    typesByProj[matchedProject] = typSet;
                                }

                                typSet.Add(name);
                            }
                        }
                    }
                }
            }

            // 2.1 Fetch connected Tables and Topics via graph edges (USES_DB, ACCESSES_TABLE, PUBLISHES, SUBSCRIBES, QUERIED_BY)
            try
            {
                await using var edgeCmd = _conn.CreateCommand();

                edgeCmd.CommandTimeout = CommandTimeoutSeconds;

                edgeCmd.CommandText = """
                                      SELECT COALESCE(json_extract(p.properties, '$.name'), '') AS proj_name,
                                             e.kind AS edge_kind,
                                             COALESCE(json_extract(target.properties, '$.name'), '') AS target_name
                                      FROM edges e
                                      JOIN nodes p ON p.id = e.from_id AND p.kind IN ('Project', 'Service', 'App', 'Worker')
                                      JOIN nodes target ON target.id = e.to_id AND target.kind IN ('Table', 'Topic')
                                      WHERE e.kind IN ('USES_DB', 'ACCESSES_TABLE', 'PUBLISHES', 'SUBSCRIBES')
                                      UNION
                                      SELECT COALESCE(json_extract(p.properties, '$.name'), '') AS proj_name,
                                             e.kind AS edge_kind,
                                             COALESCE(json_extract(target.properties, '$.name'), '') AS target_name
                                      FROM edges e
                                      JOIN nodes target ON target.id = e.from_id AND target.kind = 'Table'
                                      JOIN nodes p ON p.id = e.to_id AND p.kind IN ('Project', 'Service', 'App', 'Worker')
                                      WHERE e.kind = 'QUERIED_BY';
                                      """;

                await using var edgeReader = await edgeCmd.ExecuteReaderAsync(cancellationToken);

                while (await edgeReader.ReadAsync(cancellationToken))
                {
                    var projName = edgeReader.GetString(0);
                    var edgeKind = edgeReader.GetString(1);
                    var targetName = edgeReader.GetString(2);
                    if (string.IsNullOrEmpty(projName) || string.IsNullOrEmpty(targetName)) continue;

                    if (edgeKind is "USES_DB" or "ACCESSES_TABLE" or "QUERIED_BY")
                    {
                        if (!tablesByProj.TryGetValue(projName, out var tblSet))
                        {
                            tblSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                            tablesByProj[projName] = tblSet;
                        }

                        tblSet.Add(targetName);
                    }
                    else if (edgeKind is "PUBLISHES" or "SUBSCRIBES")
                    {
                        if (!topicsByProj.TryGetValue(projName, out var topSet))
                        {
                            topSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                            topicsByProj[projName] = topSet;
                        }

                        topSet.Add(targetName);
                    }
                }
            }
            catch
            {
                // Non-fatal if edges table is not yet fully populated
            }

            // 2.2 Fetch Project-to-Project relationships (DEPENDS_ON, SERVICE_CALL)
            var libsByProj = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            var inboundByProj = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            var outboundByProj = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

            try
            {
                await using var edgeCmd = _conn.CreateCommand();

                edgeCmd.CommandTimeout = CommandTimeoutSeconds;
                edgeCmd.CommandText = """
                                      SELECT COALESCE(json_extract(from_p.properties, '$.name'), '') AS from_proj,
                                             e.kind AS edge_kind,
                                             COALESCE(json_extract(to_p.properties, '$.name'), '') AS to_proj,
                                             COALESCE(json_extract(to_p.properties, '$.role'), json_extract(to_p.properties, '$.primary_role'), '') AS to_role
                                      FROM edges e
                                      JOIN nodes from_p ON from_p.id = e.from_id AND from_p.kind IN ('Project', 'Service', 'App', 'Worker')
                                      JOIN nodes to_p ON to_p.id = e.to_id AND to_p.kind IN ('Project', 'Service', 'App', 'Worker', 'Library')
                                      WHERE e.kind IN ('DEPENDS_ON', 'SERVICE_CALL');
                                      """;

                await using var reader = await edgeCmd.ExecuteReaderAsync(cancellationToken);

                while (await reader.ReadAsync(cancellationToken))
                {
                    var fromProj = reader.GetString(0);
                    var edgeKind = reader.GetString(1);
                    var toProj = reader.GetString(2);
                    var toRole = reader.GetString(3);

                    if (string.IsNullOrEmpty(fromProj) || string.IsNullOrEmpty(toProj)) continue;

                    if (edgeKind == "DEPENDS_ON")
                    {
                        if (toRole is "SharedLibrary" or "Library")
                        {
                            if (!libsByProj.TryGetValue(fromProj, out var libSet))
                            {
                                libSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                                libsByProj[fromProj] = libSet;
                            }
                            libSet.Add(toProj);
                        }
                        else
                        {
                            // Dependency on another service is an outbound call/integration
                            if (!outboundByProj.TryGetValue(fromProj, out var outSet))
                            {
                                outSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                                outboundByProj[fromProj] = outSet;
                            }
                            outSet.Add(toProj);

                            if (!inboundByProj.TryGetValue(toProj, out var inSet))
                            {
                                inSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                                inboundByProj[toProj] = inSet;
                            }
                            inSet.Add(fromProj);
                        }
                    }
                    else if (edgeKind == "SERVICE_CALL")
                    {
                        if (!outboundByProj.TryGetValue(fromProj, out var outSet))
                        {
                            outSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                            outboundByProj[fromProj] = outSet;
                        }
                        outSet.Add(toProj);

                        if (!inboundByProj.TryGetValue(toProj, out var inSet))
                        {
                            inSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                            inboundByProj[toProj] = inSet;
                        }
                        inSet.Add(fromProj);
                    }
                }
            }
            catch
            {
                // Non-fatal if edges table is not yet fully populated
            }

            // 3. Determine target projects: filter to workloads if requested and available
            var hasWorkloads = projects.Any(p => p.Role is "Service" or "Worker" or "FrontendApp" or "App");
            var targetProjects = (workloadsOnly && hasWorkloads)
                ? [.. projects.Where(p => p.Role is "Service" or "Worker" or "FrontendApp" or "App")]
                : projects;

            // 4. Assemble ProjectSignature objects
            foreach (var p in targetProjects)
            {
                var endpoints = endpointsByProj.TryGetValue(p.Name, out var ep) ? ep.OrderBy(x => x).Take(15).ToList() : new List<string>();
                var tables = tablesByProj.TryGetValue(p.Name, out var tb) ? new HashSet<string>(tb, StringComparer.OrdinalIgnoreCase) : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var topics = topicsByProj.TryGetValue(p.Name, out var tp) ? tp.OrderBy(x => x).Take(10).ToList() : new List<string>();
                var types = typesByProj.TryGetValue(p.Name, out var ty) ? new HashSet<string>(ty, StringComparer.OrdinalIgnoreCase) : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                var referencedLibs = libsByProj.TryGetValue(p.Name, out var rl) ? rl.OrderBy(x => x).ToList() : new List<string>();
                var inbounds = inboundByProj.TryGetValue(p.Name, out var ib) ? ib.OrderBy(x => x).Take(10).ToList() : new List<string>();
                var outbounds = outboundByProj.TryGetValue(p.Name, out var ob) ? ob.OrderBy(x => x).Take(10).ToList() : new List<string>();

                // Graph Traversal: Pull domain types and tables from referenced shared libraries!
                foreach (var libName in referencedLibs)
                {
                    if (typesByProj.TryGetValue(libName, out var libTypes))
                    {
                        foreach (var lt in libTypes.Take(8))
                        {
                            types.Add(lt);
                        }
                    }
                    if (tablesByProj.TryGetValue(libName, out var libTables))
                    {
                        foreach (var tbl in libTables.Take(5))
                        {
                            tables.Add(tbl);
                        }
                    }
                }

                results.Add(new ProjectSignature(
                    p.Id,
                    p.Name,
                    p.Path,
                    endpoints, [.. tables.OrderBy(x => x).Take(15)],
                    topics, [.. types.OrderBy(x => x).Take(20)],
                    p.Domain,
                    p.Summary,
                    Role: p.Role,
                    ReferencedLibraries: referencedLibs,
                    InboundCallers: inbounds,
                    OutboundCalls: outbounds));
            }
        }
        finally
        {
            _lock.Release();
        }

        return results;
    }

    public async Task SaveProjectIntentsAsync(
        string workspaceId,
        Dictionary<string, (string Domain, string Summary, List<string> Capabilities)> projectIntents,
        CancellationToken cancellationToken = default)
    {
        if (projectIntents.Count == 0) return;

        await _lock.WaitAsync(cancellationToken);
        try
        {
            await using var tx = (SqliteTransaction)await _conn.BeginTransactionAsync(cancellationToken);

            try
            {
                foreach (var (projectName, (domain, summary, capabilities)) in projectIntents)
                {
                    if (string.IsNullOrWhiteSpace(domain)) continue;

                    // 1. Update Project and Service nodes matching this project name
                    await using (var cmd = _conn.CreateCommand())
                    {
                        cmd.Transaction = tx;
                        cmd.CommandTimeout = CommandTimeoutSeconds;

                        cmd.CommandText = """
                                          UPDATE nodes
                                          SET properties = json_set(
                                              json_set(
                                                  json_set(properties, '$.intent_domain', @domain),
                                                  '$.intent_summary', @summary
                                              ),
                                              '$.intent_capabilities', json(@capabilities)
                                          )
                                          WHERE kind IN ('Project', 'Service', 'App', 'Worker')
                                            AND (
                                                lower(json_extract(properties, '$.name')) = lower(@pname)
                                                OR lower(json_extract(properties, '$.clean_name')) = lower(@pname)
                                                OR lower(json_extract(properties, '$.raw_name')) = lower(@pname)
                                                OR lower(id) = lower(@pname)
                                                OR lower(id) LIKE ('%/' || lower(@pname))
                                                OR lower(id) LIKE ('%:' || lower(@pname))
                                            );
                                          """;
                        cmd.Parameters.AddWithValue("@domain", domain);
                        cmd.Parameters.AddWithValue("@summary", summary ?? "");
                        cmd.Parameters.AddWithValue("@capabilities", JsonSerializer.Serialize(capabilities ?? []));
                        cmd.Parameters.AddWithValue("@pname", projectName);
                        await cmd.ExecuteNonQueryAsync(cancellationToken);
                    }

                    // 2. Ensure Domain node exists and link Project/Service to Domain
                    var widPrefix = string.IsNullOrEmpty(workspaceId) ? "" : (workspaceId.EndsWith(':') ? workspaceId : workspaceId + ":");
                    var domainId = $"{widPrefix}dom:{domain.ToLowerInvariant()}";

                    await using (var cmd = _conn.CreateCommand())
                    {
                        cmd.Transaction = tx;
                        cmd.CommandTimeout = CommandTimeoutSeconds;

                        cmd.CommandText = """
                                          INSERT OR IGNORE INTO nodes(id, kind, properties)
                                          VALUES (@id, 'Domain', json_object('id', @id, 'name', @domain));
                                          """;
                        cmd.Parameters.AddWithValue("@id", domainId);
                        cmd.Parameters.AddWithValue("@domain", domain);
                        await cmd.ExecuteNonQueryAsync(cancellationToken);
                    }

                    // Remove any prior BELONGS_TO_DOMAIN edges for this project to prevent domain fragmentation
                    await using (var delCmd = _conn.CreateCommand())
                    {
                        delCmd.Transaction = tx;
                        delCmd.CommandTimeout = CommandTimeoutSeconds;
                        delCmd.CommandText = """
                                             DELETE FROM edges
                                             WHERE kind = 'BELONGS_TO_DOMAIN'
                                               AND from_id IN (
                                                   SELECT id FROM nodes
                                                   WHERE kind IN ('Project', 'Service', 'App', 'Worker')
                                                     AND (
                                                         lower(json_extract(properties, '$.name')) = lower(@pname)
                                                         OR lower(json_extract(properties, '$.clean_name')) = lower(@pname)
                                                         OR lower(json_extract(properties, '$.raw_name')) = lower(@pname)
                                                         OR lower(id) = lower(@pname)
                                                         OR lower(id) LIKE ('%/' || lower(@pname))
                                                         OR lower(id) LIKE ('%:' || lower(@pname))
                                                     )
                                               );
                                             """;
                        delCmd.Parameters.AddWithValue("@pname", projectName);
                        await delCmd.ExecuteNonQueryAsync(cancellationToken);
                    }

                    await using (var cmd = _conn.CreateCommand())
                    {
                        cmd.Transaction = tx;
                        cmd.CommandTimeout = CommandTimeoutSeconds;

                        cmd.CommandText = """
                                          INSERT OR IGNORE INTO edges(from_id, to_id, kind, properties)
                                          SELECT id, @domainId, 'BELONGS_TO_DOMAIN', '{}'
                                          FROM nodes
                                          WHERE kind IN ('Project', 'Service', 'App', 'Worker')
                                            AND (
                                                lower(json_extract(properties, '$.name')) = lower(@pname)
                                                OR lower(json_extract(properties, '$.clean_name')) = lower(@pname)
                                                OR lower(json_extract(properties, '$.raw_name')) = lower(@pname)
                                                OR lower(id) = lower(@pname)
                                                OR lower(id) LIKE ('%/' || lower(@pname))
                                                OR lower(id) LIKE ('%:' || lower(@pname))
                                            );
                                          """;
                        cmd.Parameters.AddWithValue("@domainId", domainId);
                        cmd.Parameters.AddWithValue("@pname", projectName);
                        await cmd.ExecuteNonQueryAsync(cancellationToken);
                    }

                    // 3. Link Tables, Types, and Endpoints under this project to Domain
                    await using (var cmd = _conn.CreateCommand())
                    {
                        cmd.Transaction = tx;
                        cmd.CommandTimeout = CommandTimeoutSeconds;

                        cmd.CommandText = """
                                          INSERT OR IGNORE INTO edges(from_id, to_id, kind, properties)
                                          SELECT n.id, @domainId, 'BELONGS_TO_DOMAIN', '{}'
                                          FROM nodes n
                                          JOIN nodes p ON p.kind IN ('Project', 'Service', 'App', 'Worker')
                                          WHERE (
                                              lower(json_extract(p.properties, '$.name')) = lower(@pname)
                                              OR lower(json_extract(p.properties, '$.clean_name')) = lower(@pname)
                                              OR lower(json_extract(p.properties, '$.raw_name')) = lower(@pname)
                                              OR lower(p.id) = lower(@pname)
                                              OR lower(p.id) LIKE ('%/' || lower(@pname))
                                              OR lower(p.id) LIKE ('%:' || lower(@pname))
                                          )
                                          AND n.kind IN ('Endpoint', 'Table', 'Topic', 'Type', 'class', 'interface', 'struct', 'record')
                                          AND (
                                              json_extract(n.properties, '$.path') LIKE (json_extract(p.properties, '$.path') || '%')
                                              OR json_extract(n.properties, '$.file_path') LIKE (json_extract(p.properties, '$.path') || '%')
                                          );
                                          """;
                        cmd.Parameters.AddWithValue("@domainId", domainId);
                        cmd.Parameters.AddWithValue("@pname", projectName);
                        await cmd.ExecuteNonQueryAsync(cancellationToken);
                    }
                }

                // 4. Prune orphan Domain nodes that have no incoming BELONGS_TO_DOMAIN edges
                await using (var pruneCmd = _conn.CreateCommand())
                {
                    pruneCmd.Transaction = tx;
                    pruneCmd.CommandTimeout = CommandTimeoutSeconds;
                    pruneCmd.CommandText = """
                                           DELETE FROM nodes
                                           WHERE kind = 'Domain'
                                             AND id NOT IN (SELECT to_id FROM edges WHERE kind = 'BELONGS_TO_DOMAIN');
                                           """;
                    await pruneCmd.ExecuteNonQueryAsync(cancellationToken);
                }

                await tx.CommitAsync(cancellationToken);
            }
            catch
            {
                await tx.RollbackAsync(cancellationToken);
                throw;
            }
        }
        finally
        {
            _lock.Release();
        }
    }


    public async Task IncrementIntentErrorAsync(
        string filePath,
        string workspaceId,
        string fileId,
        string contentHash,
        DateTime lastModifiedUtc,
        string error,
        CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            await using var cmd = _conn.CreateCommand();

            cmd.CommandTimeout = CommandTimeoutSeconds;

            cmd.CommandText = """
                              INSERT INTO intents (
                                  file_path, workspace_id, file_id, content_hash, last_modified_utc,
                                  error_count, last_error
                              ) VALUES (
                                  @filePath, @workspaceId, @fileId, @contentHash, @lastModifiedUtc,
                                  1, @lastError
                              )
                              ON CONFLICT(file_path) DO UPDATE SET
                                  content_hash = excluded.content_hash,
                                  last_modified_utc = excluded.last_modified_utc,
                                  error_count = intents.error_count + 1,
                                  last_error = excluded.last_error;
                              """;

            cmd.Parameters.AddWithValue("@filePath", filePath);
            cmd.Parameters.AddWithValue("@workspaceId", workspaceId);
            cmd.Parameters.AddWithValue("@fileId", fileId);
            cmd.Parameters.AddWithValue("@contentHash", contentHash);
            cmd.Parameters.AddWithValue("@lastModifiedUtc", lastModifiedUtc.ToString("o"));
            cmd.Parameters.AddWithValue("@lastError", (object?)error ?? DBNull.Value);

            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task ResetIntentErrorsAsync(
        string workspaceId,
        CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            await using var cmd = _conn.CreateCommand();

            cmd.CommandTimeout = CommandTimeoutSeconds;

            cmd.CommandText =
                "UPDATE intents SET error_count = 0, last_error = NULL WHERE (@workspaceId = '' OR workspace_id = @workspaceId COLLATE NOCASE OR REPLACE(workspace_id, '\\', '/') = REPLACE(@workspaceId, '\\', '/') COLLATE NOCASE);";
            cmd.Parameters.AddWithValue("@workspaceId", workspaceId ?? "");
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task ClearIntentsAsync(
        string workspaceId,
        CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            await using var cmd = _conn.CreateCommand();

            cmd.CommandTimeout = CommandTimeoutSeconds;

            cmd.CommandText = """
                              DELETE FROM intents WHERE (@workspaceId = '' OR workspace_id = @workspaceId COLLATE NOCASE OR REPLACE(workspace_id, '\\', '/') = REPLACE(@workspaceId, '\\', '/') COLLATE NOCASE);
                              UPDATE nodes
                              SET properties = json_remove(properties, '$.intent_domain', '$.intent_summary', '$.intent_capabilities')
                              WHERE json_extract(properties, '$.intent_domain') IS NOT NULL;
                              """;
            cmd.Parameters.AddWithValue("@workspaceId", workspaceId ?? "");
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }


    public async Task<List<CrossDomainInteractionRecord>> LoadCrossDomainInteractionsAsync(
        string workspaceId,
        CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            const string sql = """
                               SELECT 
                                   i1.domain AS SourceDomain,
                                   i2.domain AS TargetDomain,
                                   e.kind AS EdgeKind,
                                   COUNT(*) AS InteractionCount
                               FROM edges e
                               JOIN nodes n1 ON e.from_id = n1.id
                               JOIN nodes n2 ON e.to_id = n2.id
                               JOIN intents i1 ON (n1.id = i1.file_id 
                                                  OR json_extract(n1.properties, '$.path') = i1.file_path 
                                                  OR json_extract(n1.properties, '$.file_path') = i1.file_path)
                               JOIN intents i2 ON (n2.id = i2.file_id 
                                                  OR json_extract(n2.properties, '$.path') = i2.file_path 
                                                  OR json_extract(n2.properties, '$.file_path') = i2.file_path)
                               WHERE (@workspaceId = '' OR i1.workspace_id = @workspaceId COLLATE NOCASE OR REPLACE(i1.workspace_id, '\', '/') = REPLACE(@workspaceId, '\', '/') COLLATE NOCASE)
                                 AND (@workspaceId = '' OR i2.workspace_id = @workspaceId COLLATE NOCASE OR REPLACE(i2.workspace_id, '\', '/') = REPLACE(@workspaceId, '\', '/') COLLATE NOCASE)
                                 AND e.kind IN ('CALLS', 'CALLS_ENDPOINT', 'USES_API', 'PUBLISHES', 'SUBSCRIBES', 'TRIGGERS', 'CONSUMES', 'USES_DB')
                                 AND i1.domain IS NOT NULL AND trim(i1.domain) != ''
                                 AND i2.domain IS NOT NULL AND trim(i2.domain) != ''
                                 AND lower(i1.domain) != lower(i2.domain)
                               GROUP BY i1.domain, i2.domain, e.kind;
                               """;

            var rows = await _conn.QueryAsync<(string SourceDomain, string TargetDomain, string EdgeKind, int InteractionCount)>(
                new CommandDefinition(sql, new { workspaceId = workspaceId ?? "" }, commandTimeout: CommandTimeoutSeconds, cancellationToken: cancellationToken));
            return
            [
                .. rows.Select(r =>
                    new CrossDomainInteractionRecord(r.SourceDomain, r.TargetDomain, r.EdgeKind, r.InteractionCount))
            ];
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[SqliteGraphClient] Note on querying cross-domain interactions: {Message}", ex.Message);
            return [];
        }
        finally
        {
            _lock.Release();
        }
    }

}
