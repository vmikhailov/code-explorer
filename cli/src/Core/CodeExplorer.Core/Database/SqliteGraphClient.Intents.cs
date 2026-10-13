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
                           json_extract(properties, '$.intent_domain') AS intent_domain,
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

    public async Task<List<IntentCandidate>> LoadIntentCandidatesAsync(
        string workspaceId,
        int? limit = null,
        CancellationToken cancellationToken = default)
    {
        var effectiveLimit = limit;
        if (!effectiveLimit.HasValue && int.TryParse(Environment.GetEnvironmentVariable("CODE_INTENT_MAX_FILES"), out var envLimit) && envLimit > 0)
        {
            effectiveLimit = envLimit;
        }

        var limitClause = effectiveLimit.HasValue ? "LIMIT @limit" : "";
        var results = new List<IntentCandidate>();

        await _lock.WaitAsync(cancellationToken);
        try
        {
            await using var cmd = _conn.CreateCommand();

            cmd.CommandTimeout = CommandTimeoutSeconds;

            if (effectiveLimit.HasValue)
            {
                cmd.Parameters.AddWithValue("@limit", effectiveLimit.Value);
            }

            // 1. Architectural candidates (classes/interfaces/files)
            cmd.CommandText = $"""
                               SELECT id, kind,
                                      COALESCE(json_extract(properties, '$.name'), '') AS name,
                                      COALESCE(json_extract(properties, '$.path'), json_extract(properties, '$.file_path'), '') AS rel_path,
                                      COALESCE(json_extract(properties, '$.full_path'), '') AS full_path
                               FROM nodes
                               WHERE kind IN ('Type', 'File')
                                 AND (
                                     name LIKE '%Service%' OR name LIKE '%Controller%' OR name LIKE '%Repository%'
                                     OR name LIKE '%Handler%' OR name LIKE '%Consumer%' OR name LIKE '%Manager%'
                                     OR name LIKE '%Producer%' OR name LIKE '%Listener%' OR name LIKE '%Worker%'
                                     OR name LIKE '%Job%' OR name LIKE '%Processor%' OR name LIKE '%Store%'
                                     OR name LIKE '%Command%' OR name LIKE '%Query%' OR name LIKE '%Entity%'
                                     OR name LIKE '%DTO%' OR name LIKE '%Model%' OR name LIKE '%Aggregate%'
                                     OR name LIKE '%Policy%' OR name LIKE '%Factory%'
                                     OR rel_path LIKE '%.service.%' OR rel_path LIKE '%.controller.%'
                                     OR rel_path LIKE '%.repository.%' OR rel_path LIKE '%.handler.%'
                                     OR rel_path LIKE '%.consumer.%' OR rel_path LIKE '%.manager.%'
                                     OR rel_path LIKE '%/services/%' OR rel_path LIKE '%/controllers/%'
                                     OR rel_path LIKE '%/repositories/%' OR rel_path LIKE '%/handlers/%'
                                     OR rel_path LIKE '%/consumers/%' OR rel_path LIKE '%/domain/%'
                                     OR rel_path LIKE '%/entities/%' OR rel_path LIKE '%/commands/%'
                                     OR rel_path LIKE '%/queries/%'
                                 )
                                 AND rel_path NOT LIKE '%.spec.%'
                                 AND rel_path NOT LIKE '%.test.%'
                                 AND rel_path NOT LIKE '%/tests/%'
                                 AND rel_path NOT LIKE '%/test/%'
                                 AND rel_path NOT LIKE '%/obj/%'
                                 AND rel_path NOT LIKE '%/bin/%'
                                 AND rel_path NOT LIKE '%/node_modules/%'
                                 AND rel_path NOT LIKE '%.sql'
                               {limitClause};
                               """;

            await using (var reader = await cmd.ExecuteReaderAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    results.Add(new IntentCandidate(reader.GetString(0), reader.GetString(1), reader.GetString(2),
                        reader.GetString(3), reader.IsDBNull(4) ? null : reader.GetString(4)));
                }
            }

            // 2. Fallback if no specific architectural keywords were found: take top Types
            if (results.Count == 0)
            {
                cmd.CommandText = $"""
                                   SELECT id, kind,
                                          COALESCE(json_extract(properties, '$.name'), '') AS name,
                                          COALESCE(json_extract(properties, '$.path'), json_extract(properties, '$.file_path'), '') AS rel_path,
                                          COALESCE(json_extract(properties, '$.full_path'), '') AS full_path
                                   FROM nodes
                                   WHERE kind = 'Type'
                                     AND rel_path NOT LIKE '%.spec.%'
                                     AND rel_path NOT LIKE '%.test.%'
                                     AND rel_path NOT LIKE '%/tests/%'
                                     AND rel_path NOT LIKE '%/test/%'
                                     AND rel_path NOT LIKE '%/obj/%'
                                     AND rel_path NOT LIKE '%/bin/%'
                                     AND rel_path NOT LIKE '%/node_modules/%'
                                   {limitClause};
                                   """;

                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

                while (await reader.ReadAsync(cancellationToken))
                {
                    results.Add(new IntentCandidate(reader.GetString(0), reader.GetString(1),
                        reader.GetString(2), reader.GetString(3),
                        reader.IsDBNull(4) ? null : reader.GetString(4)));
                }
            }
        }
        finally
        {
            _lock.Release();
        }

        return results;
    }

    public async Task SaveIntentPredictionsAsync(
        string workspaceId,
        List<CodeIntentPredictionResult> predictions,
        CancellationToken cancellationToken = default)
    {
        if (predictions.Count == 0) return;

        var widPrefix = string.IsNullOrEmpty(workspaceId) ? "" : (workspaceId.EndsWith(':') ? workspaceId : workspaceId + ":");

        await _lock.WaitAsync(cancellationToken);
        try
        {
            // 1. Update node properties with intent fields
            await using var tx = (SqliteTransaction)await _conn.BeginTransactionAsync(cancellationToken);

            await using (var updCmd = _conn.CreateCommand())
            {
                updCmd.Transaction = tx;
                updCmd.CommandTimeout = CommandTimeoutSeconds;

                updCmd.CommandText = """
                                     UPDATE nodes SET properties = json_set(
                                         properties,
                                         '$.intent_domain', @domain,
                                         '$.intent_layer', @layer,
                                         '$.intent_pattern', @pattern,
                                         '$.intent_operation', @operation,
                                         '$.intent_capability', @capability,
                                         '$.intent_summary', @summary,
                                         '$.is_pure_domain', @isPureDomain,
                                         '$.target_entities', json(@targetEntities),
                                         '$.emitted_events', json(@emittedEvents)
                                     ) WHERE id = @id;
                                     """;

                var pId = updCmd.Parameters.Add("@id", SqliteType.Text);
                var pDomain = updCmd.Parameters.Add("@domain", SqliteType.Text);
                var pLayer = updCmd.Parameters.Add("@layer", SqliteType.Text);
                var pPattern = updCmd.Parameters.Add("@pattern", SqliteType.Text);
                var pOp = updCmd.Parameters.Add("@operation", SqliteType.Text);
                var pCap = updCmd.Parameters.Add("@capability", SqliteType.Text);
                var pSummary = updCmd.Parameters.Add("@summary", SqliteType.Text);
                var pPure = updCmd.Parameters.Add("@isPureDomain", SqliteType.Integer);
                var pEntities = updCmd.Parameters.Add("@targetEntities", SqliteType.Text);
                var pEvents = updCmd.Parameters.Add("@emittedEvents", SqliteType.Text);

                foreach (var pred in predictions)
                {
                    pId.Value = pred.Id;
                    pDomain.Value = (object?)pred.Domain ?? DBNull.Value;
                    pLayer.Value = (object?)pred.Layer ?? DBNull.Value;
                    pPattern.Value = (object?)pred.Pattern ?? DBNull.Value;
                    pOp.Value = (object?)pred.OperationType ?? DBNull.Value;
                    pCap.Value = (object?)pred.CapabilityTag ?? DBNull.Value;
                    pSummary.Value = (object?)pred.IntentSummary ?? DBNull.Value;
                    pPure.Value = pred.IsPureDomain.HasValue ? (pred.IsPureDomain.Value ? 1 : 0) : DBNull.Value;
                    pEntities.Value = JsonSerializer.Serialize(pred.TargetEntities ?? []);
                    pEvents.Value = JsonSerializer.Serialize(pred.EmittedEvents ?? []);

                    await updCmd.ExecuteNonQueryAsync(cancellationToken);
                }
            }

            // 2. Materialize Domain nodes and BELONGS_TO_DOMAIN relationships
            var distinctDomains = predictions.Where(p => !string.IsNullOrWhiteSpace(p.Domain))
                .Select(p => p.Domain!.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            if (distinctDomains.Count > 0)
            {
                await using var domCmd = _conn.CreateCommand();

                domCmd.Transaction = tx;
                domCmd.CommandTimeout = CommandTimeoutSeconds;

                domCmd.CommandText = """
                                     INSERT INTO nodes (id, kind, properties)
                                     VALUES (@id, 'Domain', @props)
                                     ON CONFLICT(id) DO NOTHING;
                                     """;
                var pDomId = domCmd.Parameters.Add("@id", SqliteType.Text);
                var pDomProps = domCmd.Parameters.Add("@props", SqliteType.Text);

                foreach (var dom in distinctDomains)
                {
                    var domId = $"{widPrefix}dom:{dom.ToLowerInvariant()}";

                    var domProps = JsonSerializer.Serialize(new Dictionary<string, object>
                    {
                        ["id"] = domId, ["name"] = dom
                    });

                    pDomId.Value = domId;
                    pDomProps.Value = domProps;
                    await domCmd.ExecuteNonQueryAsync(cancellationToken);
                }

                await using var edgeCmd = _conn.CreateCommand();

                edgeCmd.Transaction = tx;
                edgeCmd.CommandTimeout = CommandTimeoutSeconds;

                edgeCmd.CommandText = """
                                      INSERT OR IGNORE INTO edges (from_id, to_id, kind, properties)
                                      VALUES (@fromId, @toId, 'BELONGS_TO_DOMAIN', '{}');
                                      """;
                var pFromId = edgeCmd.Parameters.Add("@fromId", SqliteType.Text);
                var pToId = edgeCmd.Parameters.Add("@toId", SqliteType.Text);

                foreach (var pred in predictions.Where(p => !string.IsNullOrWhiteSpace(p.Domain)))
                {
                    var domId = $"{widPrefix}dom:{pred.Domain!.Trim().ToLowerInvariant()}";
                    pFromId.Value = pred.Id;
                    pToId.Value = domId;
                    await edgeCmd.ExecuteNonQueryAsync(cancellationToken);
                }
            }

            await tx.CommitAsync(cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<List<IntentRecord>> LoadExistingIntentsAsync(
        string workspaceId,
        CancellationToken cancellationToken = default)
    {
        var results = new List<IntentRecord>();
        await _lock.WaitAsync(cancellationToken);
        try
        {
            await using var cmd = _conn.CreateCommand();

            cmd.CommandTimeout = CommandTimeoutSeconds;

            cmd.CommandText = """
                              SELECT file_path, workspace_id, file_id, content_hash, last_modified_utc,
                                     domain, layer, pattern, operation_type, capability_tag,
                                     intent_summary, target_entities, emitted_events, is_pure_domain,
                                     error_count, last_error, analyzed_at_utc
                              FROM intents
                              WHERE (@workspaceId = '' 
                                     OR workspace_id = @workspaceId COLLATE NOCASE 
                                     OR REPLACE(workspace_id, '\', '/') = REPLACE(@workspaceId, '\', '/') COLLATE NOCASE);
                              """;
            cmd.Parameters.AddWithValue("@workspaceId", workspaceId ?? "");

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                var filePath = reader.GetString(0);
                var wsId = reader.GetString(1);
                var fileId = reader.GetString(2);
                var contentHash = reader.GetString(3);
                var lastMod = DateTime.TryParse(reader.GetString(4), out var dt) ? dt : DateTime.UtcNow;
                var domain = reader.IsDBNull(5) ? null : reader.GetString(5);
                var layer = reader.IsDBNull(6) ? null : reader.GetString(6);
                var pattern = reader.IsDBNull(7) ? null : reader.GetString(7);
                var opType = reader.IsDBNull(8) ? null : reader.GetString(8);
                var capTag = reader.IsDBNull(9) ? null : reader.GetString(9);
                var summary = reader.IsDBNull(10) ? null : reader.GetString(10);

                var targetEntities = reader.IsDBNull(11)
                    ? null
                    : JsonSerializer.Deserialize<List<string>>(reader.GetString(11));

                var emittedEvents = reader.IsDBNull(12)
                    ? null
                    : JsonSerializer.Deserialize<List<string>>(reader.GetString(12));
                var isPure = reader.IsDBNull(13) ? (bool?)null : reader.GetInt32(13) == 1;
                var errorCount = reader.GetInt32(14);
                var lastError = reader.IsDBNull(15) ? null : reader.GetString(15);

                var analyzedAt = reader.IsDBNull(16)
                    ? (DateTime?)null
                    : (DateTime.TryParse(reader.GetString(16), out var adt) ? adt : null);

                results.Add(new IntentRecord(filePath, wsId, fileId, contentHash, lastMod, domain, layer,
                    pattern, opType, capTag, summary, targetEntities, emittedEvents, isPure, errorCount,
                    lastError, analyzedAt));
            }
        }
        finally
        {
            _lock.Release();
        }

        return results;
    }

    public async Task SaveIntentRecordAsync(
        IntentRecord record,
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
                                  domain, layer, pattern, operation_type, capability_tag,
                                  intent_summary, target_entities, emitted_events, is_pure_domain,
                                  error_count, last_error, analyzed_at_utc
                              ) VALUES (
                                  @filePath, @workspaceId, @fileId, @contentHash, @lastModifiedUtc,
                                  @domain, @layer, @pattern, @operationType, @capabilityTag,
                                  @intentSummary, @targetEntities, @emittedEvents, @isPureDomain,
                                  0, NULL, @analyzedAtUtc
                              )
                              ON CONFLICT(file_path) DO UPDATE SET
                                  workspace_id = excluded.workspace_id,
                                  file_id = excluded.file_id,
                                  content_hash = excluded.content_hash,
                                  last_modified_utc = excluded.last_modified_utc,
                                  domain = excluded.domain,
                                  layer = excluded.layer,
                                  pattern = excluded.pattern,
                                  operation_type = excluded.operation_type,
                                  capability_tag = excluded.capability_tag,
                                  intent_summary = excluded.intent_summary,
                                  target_entities = excluded.target_entities,
                                  emitted_events = excluded.emitted_events,
                                  is_pure_domain = excluded.is_pure_domain,
                                  error_count = 0,
                                  last_error = NULL,
                                  analyzed_at_utc = excluded.analyzed_at_utc;
                              """;

            cmd.Parameters.AddWithValue("@filePath", record.FilePath);
            cmd.Parameters.AddWithValue("@workspaceId", record.WorkspaceId);
            cmd.Parameters.AddWithValue("@fileId", record.FileId);
            cmd.Parameters.AddWithValue("@contentHash", record.ContentHash);
            cmd.Parameters.AddWithValue("@lastModifiedUtc", record.LastModifiedUtc.ToString("o"));
            cmd.Parameters.AddWithValue("@domain", (object?)record.Domain ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@layer", (object?)record.Layer ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@pattern", (object?)record.Pattern ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@operationType", (object?)record.OperationType ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@capabilityTag", (object?)record.CapabilityTag ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@intentSummary", (object?)record.IntentSummary ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@targetEntities", JsonSerializer.Serialize(record.TargetEntities ?? []));
            cmd.Parameters.AddWithValue("@emittedEvents", JsonSerializer.Serialize(record.EmittedEvents ?? []));

            cmd.Parameters.AddWithValue("@isPureDomain",
                record.IsPureDomain.HasValue ? (record.IsPureDomain.Value ? 1 : 0) : DBNull.Value);

            cmd.Parameters.AddWithValue("@analyzedAtUtc",
                record.AnalyzedAtUtc?.ToString("o") ?? DateTime.UtcNow.ToString("o"));

            await cmd.ExecuteNonQueryAsync(cancellationToken);
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

    public async Task PurgeIntentsByPathsAsync(
        List<string> filePaths,
        CancellationToken cancellationToken = default)
    {
        if (filePaths == null || filePaths.Count == 0) return;

        await _lock.WaitAsync(cancellationToken);
        try
        {
            await using var tx = (SqliteTransaction)await _conn.BeginTransactionAsync(cancellationToken);

            foreach (var chunk in filePaths.Chunk(500))
            {
                await using var cmd = _conn.CreateCommand();

                cmd.Transaction = tx;
                cmd.CommandTimeout = CommandTimeoutSeconds;

                var paramNames = new List<string>();
                for (var i = 0; i < chunk.Length; i++)
                {
                    var pName = $"@p{i}";
                    paramNames.Add(pName);
                    cmd.Parameters.AddWithValue(pName, chunk[i]);
                }

                var inClause = string.Join(", ", paramNames);
                cmd.CommandText = $"""
                                   DELETE FROM intents WHERE file_path IN ({inClause});
                                   DELETE FROM edges
                                   WHERE kind = 'BELONGS_TO_DOMAIN'
                                     AND from_id IN (
                                         SELECT id FROM nodes
                                         WHERE json_extract(properties, '$.path') IN ({inClause})
                                            OR json_extract(properties, '$.file_path') IN ({inClause})
                                            OR id IN ({inClause})
                                     );
                                   UPDATE nodes
                                   SET properties = json_remove(
                                       properties,
                                       '$.intent_domain',
                                       '$.intent_layer',
                                       '$.intent_pattern',
                                       '$.intent_operation',
                                       '$.intent_capability',
                                       '$.intent_summary',
                                       '$.is_pure_domain',
                                       '$.target_entities',
                                       '$.emitted_events'
                                   )
                                   WHERE json_extract(properties, '$.path') IN ({inClause})
                                      OR json_extract(properties, '$.file_path') IN ({inClause})
                                      OR id IN ({inClause});
                                   """;
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }

            await tx.CommitAsync(cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<int> ApplyCachedIntentsToGraphAsync(
        string workspaceId,
        CancellationToken cancellationToken = default)
    {
        var widPrefix = string.IsNullOrEmpty(workspaceId) ? "" : (workspaceId.EndsWith(':') ? workspaceId : workspaceId + ":");
        var updatedNodes = 0;

        await _lock.WaitAsync(cancellationToken);
        try
        {
            // 1. Update matching nodes (File or Type nodes matching file_path or file_id)
            await using var tx = (SqliteTransaction)await _conn.BeginTransactionAsync(cancellationToken);

            await using (var updCmd = _conn.CreateCommand())
            {
                updCmd.Transaction = tx;
                updCmd.CommandTimeout = CommandTimeoutSeconds;

                // 0. Ensure no raw .sql scripts are lingering in intents or domain edges
                updCmd.CommandText = """
                                     DELETE FROM intents WHERE file_path LIKE '%.sql';
                                     DELETE FROM edges
                                     WHERE kind = 'BELONGS_TO_DOMAIN'
                                       AND from_id IN (
                                           SELECT id FROM nodes
                                           WHERE json_extract(properties, '$.path') LIKE '%.sql'
                                              OR json_extract(properties, '$.file_path') LIKE '%.sql'
                                              OR id LIKE '%.sql'
                                       );
                                     """;
                await updCmd.ExecuteNonQueryAsync(cancellationToken);

                updCmd.CommandText = """
                                     UPDATE nodes
                                     SET properties = json_set(
                                         properties,
                                         '$.intent_domain', i.domain,
                                         '$.intent_layer', i.layer,
                                         '$.intent_pattern', i.pattern,
                                         '$.intent_operation', i.operation_type,
                                         '$.intent_capability', i.capability_tag,
                                         '$.intent_summary', i.intent_summary,
                                         '$.is_pure_domain', i.is_pure_domain,
                                         '$.target_entities', json(i.target_entities),
                                         '$.emitted_events', json(i.emitted_events)
                                     )
                                     FROM intents i
                                     WHERE (nodes.id = i.file_id 
                                            OR json_extract(nodes.properties, '$.path') = i.file_path 
                                            OR json_extract(nodes.properties, '$.file_path') = i.file_path)
                                       AND i.domain IS NOT NULL;
                                     """;
                updatedNodes = await updCmd.ExecuteNonQueryAsync(cancellationToken);
            }

            // 2. Materialize Domain nodes from distinct domains in intents
            await using (var domCmd = _conn.CreateCommand())
            {
                domCmd.Transaction = tx;
                domCmd.CommandTimeout = CommandTimeoutSeconds;

                domCmd.CommandText = """
                                     INSERT INTO nodes (id, kind, properties)
                                     SELECT DISTINCT
                                         @widPrefix || 'dom:' || lower(domain) AS id,
                                         'Domain' AS kind,
                                         json_object('id', @widPrefix || 'dom:' || lower(domain), 'name', domain) AS properties
                                     FROM intents
                                     WHERE domain IS NOT NULL AND trim(domain) != ''
                                     ON CONFLICT(id) DO NOTHING;
                                     """;
                domCmd.Parameters.AddWithValue("@widPrefix", widPrefix);
                await domCmd.ExecuteNonQueryAsync(cancellationToken);
            }

            // 3. Create BELONGS_TO_DOMAIN edges from nodes to Domain nodes
            await using (var edgeCmd = _conn.CreateCommand())
            {
                edgeCmd.Transaction = tx;
                edgeCmd.CommandTimeout = CommandTimeoutSeconds;

                edgeCmd.CommandText = """
                                      INSERT OR IGNORE INTO edges (from_id, to_id, kind, properties)
                                      SELECT DISTINCT
                                          n.id AS from_id,
                                          @widPrefix || 'dom:' || lower(i.domain) AS to_id,
                                          'BELONGS_TO_DOMAIN' AS kind,
                                          '{}' AS properties
                                      FROM nodes n
                                      JOIN intents i ON (n.id = i.file_id 
                                                         OR json_extract(n.properties, '$.path') = i.file_path 
                                                         OR json_extract(n.properties, '$.file_path') = i.file_path)
                                      WHERE i.domain IS NOT NULL AND trim(i.domain) != '';
                                      """;
                edgeCmd.Parameters.AddWithValue("@widPrefix", widPrefix);
                await edgeCmd.ExecuteNonQueryAsync(cancellationToken);
            }

            // 4. Create EXPOSES_DOMAIN edges from Project/App nodes to Domain nodes
            await using (var projDomCmd = _conn.CreateCommand())
            {
                projDomCmd.Transaction = tx;
                projDomCmd.CommandTimeout = CommandTimeoutSeconds;

                projDomCmd.CommandText = """
                                         INSERT OR IGNORE INTO edges (from_id, to_id, kind, properties)
                                         SELECT DISTINCT
                                             p.id AS from_id,
                                             @widPrefix || 'dom:' || lower(i.domain) AS to_id,
                                             'EXPOSES_DOMAIN' AS kind,
                                             '{}' AS properties
                                         FROM edges e
                                         JOIN nodes p ON e.to_id = p.id AND (p.kind = 'Project' OR p.kind = 'App')
                                         JOIN nodes n ON e.from_id = n.id
                                         JOIN intents i ON (n.id = i.file_id 
                                                            OR json_extract(n.properties, '$.path') = i.file_path 
                                                            OR json_extract(n.properties, '$.file_path') = i.file_path)
                                         WHERE e.kind = 'BELONGS_TO'
                                           AND i.domain IS NOT NULL AND trim(i.domain) != '';
                                         """;
                projDomCmd.Parameters.AddWithValue("@widPrefix", widPrefix);
                await projDomCmd.ExecuteNonQueryAsync(cancellationToken);
            }

            // 5. Connect fine-grained subdomains sharing a project boundary via SUBDOMAIN_OF
            await using (var subDomCmd = _conn.CreateCommand())
            {
                subDomCmd.Transaction = tx;
                subDomCmd.CommandTimeout = CommandTimeoutSeconds;

                subDomCmd.CommandText = """
                                        INSERT OR IGNORE INTO edges (from_id, to_id, kind, properties)
                                        SELECT DISTINCT
                                            e_sub.to_id AS from_id,
                                            e_base.to_id AS to_id,
                                            'SUBDOMAIN_OF' AS kind,
                                            '{}' AS properties
                                        FROM edges e_sub
                                        JOIN edges e_base ON e_sub.from_id = e_base.from_id 
                                                         AND e_sub.kind = 'EXPOSES_DOMAIN' 
                                                         AND e_base.kind = 'EXPOSES_DOMAIN'
                                        JOIN nodes d_sub ON e_sub.to_id = d_sub.id AND d_sub.kind = 'Domain'
                                        JOIN nodes d_base ON e_base.to_id = d_base.id AND d_base.kind = 'Domain'
                                        WHERE d_sub.id != d_base.id
                                          AND length(json_extract(d_base.properties, '$.name')) >= 4
                                          AND length(json_extract(d_sub.properties, '$.name')) > length(json_extract(d_base.properties, '$.name'))
                                          AND (lower(json_extract(d_sub.properties, '$.name')) LIKE lower(json_extract(d_base.properties, '$.name')) || '%'
                                               OR lower(json_extract(d_sub.properties, '$.name')) LIKE '%' || lower(json_extract(d_base.properties, '$.name')));
                                        """;
                await subDomCmd.ExecuteNonQueryAsync(cancellationToken);
            }

            // 6. Clean up orphaned Domain nodes and edges that have no active intents
            await using (var purgeCmd = _conn.CreateCommand())
            {
                purgeCmd.Transaction = tx;
                purgeCmd.CommandTimeout = CommandTimeoutSeconds;

                purgeCmd.CommandText = """
                                       DELETE FROM edges
                                       WHERE kind = 'BELONGS_TO_DOMAIN'
                                         AND from_id NOT IN (
                                             SELECT file_id FROM intents WHERE file_id IS NOT NULL
                                             UNION
                                             SELECT id FROM nodes WHERE json_extract(properties, '$.intent_domain') IS NOT NULL
                                             UNION
                                             SELECT id FROM nodes WHERE kind IN ('Project', 'Service', 'App', 'Worker')
                                         );

                                       DELETE FROM edges
                                       WHERE kind = 'BELONGS_TO_DOMAIN'
                                         AND to_id NOT IN (
                                             SELECT @widPrefix || 'dom:' || lower(domain)
                                             FROM intents
                                             WHERE domain IS NOT NULL AND trim(domain) != ''
                                             UNION
                                             SELECT @widPrefix || 'dom:' || lower(json_extract(properties, '$.intent_domain'))
                                             FROM nodes
                                             WHERE json_extract(properties, '$.intent_domain') IS NOT NULL 
                                               AND trim(json_extract(properties, '$.intent_domain')) != ''
                                         );

                                       DELETE FROM edges
                                       WHERE kind = 'EXPOSES_DOMAIN'
                                         AND to_id NOT IN (
                                             SELECT @widPrefix || 'dom:' || lower(domain)
                                             FROM intents
                                             WHERE domain IS NOT NULL AND trim(domain) != ''
                                             UNION
                                             SELECT @widPrefix || 'dom:' || lower(json_extract(properties, '$.intent_domain'))
                                             FROM nodes
                                             WHERE json_extract(properties, '$.intent_domain') IS NOT NULL 
                                               AND trim(json_extract(properties, '$.intent_domain')) != ''
                                         );

                                       DELETE FROM nodes
                                       WHERE kind = 'Domain'
                                         AND id NOT IN (
                                             SELECT @widPrefix || 'dom:' || lower(domain)
                                             FROM intents
                                             WHERE domain IS NOT NULL AND trim(domain) != ''
                                             UNION
                                             SELECT @widPrefix || 'dom:' || lower(json_extract(properties, '$.intent_domain'))
                                             FROM nodes
                                             WHERE json_extract(properties, '$.intent_domain') IS NOT NULL 
                                               AND trim(json_extract(properties, '$.intent_domain')) != ''
                                         );
                                       """;
                purgeCmd.Parameters.AddWithValue("@widPrefix", widPrefix);
                await purgeCmd.ExecuteNonQueryAsync(cancellationToken);
            }

            await tx.CommitAsync(cancellationToken);
        }
        finally
        {
            _lock.Release();
        }

        return updatedNodes;
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

    public async Task<List<DomainInfrastructureLinkRecord>> LoadDomainInfrastructureLinksAsync(
        string workspaceId,
        CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            const string sql = """
                               SELECT 
                                   i.domain AS Domain,
                                   n_infra.id AS InfraId,
                                   COALESCE(json_extract(n_infra.properties, '$.name'), n_infra.id) AS InfraName,
                                   n_infra.kind AS InfraKind,
                                   e.kind AS EdgeKind
                               FROM edges e
                               JOIN nodes n ON e.from_id = n.id
                               JOIN nodes n_infra ON e.to_id = n_infra.id
                               JOIN intents i ON (n.id = i.file_id 
                                                  OR json_extract(n.properties, '$.path') = i.file_path 
                                                  OR json_extract(n.properties, '$.file_path') = i.file_path)
                               WHERE (@workspaceId = '' OR i.workspace_id = @workspaceId COLLATE NOCASE OR REPLACE(i.workspace_id, '\', '/') = REPLACE(@workspaceId, '\', '/') COLLATE NOCASE)
                                 AND i.domain IS NOT NULL AND trim(i.domain) != ''
                                 AND n_infra.kind IN ('Database', 'Topic')
                               GROUP BY i.domain, n_infra.id, n_infra.kind, e.kind;
                               """;

            var rows = await _conn.QueryAsync<(string Domain, string InfraId, string InfraName, string InfraKind, string EdgeKind)>(
                new CommandDefinition(sql, new { workspaceId = workspaceId ?? "" }, commandTimeout: CommandTimeoutSeconds, cancellationToken: cancellationToken));
            return
            [
                .. rows.Select(r =>
                    new DomainInfrastructureLinkRecord(r.Domain, r.InfraId, r.InfraName, r.InfraKind, r.EdgeKind))
            ];
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[SqliteGraphClient] Note on querying domain infrastructure links: {Message}", ex.Message);
            return [];
        }
        finally
        {
            _lock.Release();
        }
    }
}
