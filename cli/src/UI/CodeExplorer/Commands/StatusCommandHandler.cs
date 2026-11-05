using System.Text.Json;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Database;
using CodeExplorer.Cypher.Linq;
using CodeExplorer.Options;

namespace CodeExplorer.Commands;

public static class StatusCommandHandler
{
    public static async Task<int> HandleAsync(StatusOptions opts)
    {
        try
        {
            var ws = WorkspaceLocator.FindOrThrow(opts.Dir);
            if (!File.Exists(ws.DbPath))
            {
                Console.WriteLine($"Workspace found at '{ws.RootDirectory}', but database has not been initialized yet.");
                Console.WriteLine("Run 'ce scan' to index your codebase.");
                return 0;
            }

            var fileInfo = new FileInfo(ws.DbPath);
            var sizeMb = fileInfo.Length / (1024.0 * 1024.0);

            // Workspace node info
            await using var client = new SqliteGraphClient(ws.DbPath);

            string wsName = new DirectoryInfo(ws.RootDirectory).Name;
            DateTime? lastIndexedAt = null;

            var repo = client.AsRepository();
            var wsNode = await repo.GetWorkspaceAsync();
            if (wsNode != null)
            {
                if (!string.IsNullOrWhiteSpace(wsNode.Name))
                {
                    wsName = wsNode.Name;
                }

                if (wsNode.Extensions != null &&
                    wsNode.Extensions.TryGetValue(OntologyConstants.ExtensionKeys.IndexedAt, out var indexedAtStr) &&
                    DateTime.TryParse(indexedAtStr, out var dt))
                {
                    lastIndexedAt = dt.ToLocalTime();
                }
            }

            var lastUpdated = lastIndexedAt ?? fileInfo.LastWriteTime;
            var lastUpdatedText = FormatRelativeTime(lastUpdated);

            // Projects info
            var projectEntities = await client.Graph.Projects.ToListAsync();
            var projects = projectEntities
                .OrderBy(p => p.Name, StringComparer.Ordinal)
                .Select(p => (
                    Name: !string.IsNullOrEmpty(p.Name) ? p.Name : "unknown",
                    Language: !string.IsNullOrEmpty(p.Language) ? p.Language : "unknown",
                    Layer: p.Layer ?? string.Empty,
                    Role: p.Role ?? string.Empty))
                .ToList();

            // Node count
            var (totalNodes, _) = await client.GetGraphCountsAsync();
            long nodeCount = totalNodes;

            // Queries info
            var queryManager = new ProjectQueryManager();
            var savedQueries = queryManager.ListQueries(ws.RootDirectory);
            var builtInCount = Queries.GetBuiltInQueries().Count;

            if (opts.Json)
            {
                var statusObj = new
                {
                    workspace_name = wsName,
                    root_directory = ws.RootDirectory,
                    database_path = ws.DbPath,
                    database_size_bytes = fileInfo.Length,
                    database_size_mb = Math.Round(sizeMb, 2),
                    schema_version = client.SchemaVersion,
                    current_schema_version = SqliteGraphClient.CurrentSchemaVersion,
                    is_schema_outdated = client.IsSchemaOutdated,
                    was_migrated = client.WasMigratedOnOpen,
                    migrated_from_version = client.MigratedFromVersion,
                    last_updated = lastUpdated.ToString("o"),
                    last_updated_human = lastUpdatedText,
                    total_nodes = nodeCount,
                    projects =
                        projects.Select(p => new
                        {
                            name = p.Name, language = p.Language, layer = p.Layer, role = p.Role
                        }),
                    built_in_queries_count = builtInCount,
                    custom_queries = savedQueries.Select(q =>
                        new { name = q.Name, description = q.Description })
                };

                Console.WriteLine(JsonSerializer.Serialize(statusObj,
                    new JsonSerializerOptions { WriteIndented = true }));
                return 0;
            }

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"Workspace: {wsName}");
            Console.ResetColor();
            Console.WriteLine($"  Root:      {ws.RootDirectory}");
            Console.WriteLine($"  Database:  {ws.DbPath} ({sizeMb:F1} MB)");
            var schemaText = client.SchemaVersion > SqliteGraphClient.CurrentSchemaVersion
                ? $"v{client.SchemaVersion} (newer than CLI v{SqliteGraphClient.CurrentSchemaVersion} - consider upgrading CLI)"
                : (client.IsSchemaOutdated
                    ? $"v{client.SchemaVersion} (outdated, current is v{SqliteGraphClient.CurrentSchemaVersion} - run 'ce scan --clear' to upgrade)"
                    : (client.WasMigratedOnOpen
                        ? $"v{client.SchemaVersion} (upgraded from v{client.MigratedFromVersion})"
                        : $"v{client.SchemaVersion}"));
            Console.WriteLine($"  Schema:    {schemaText}");
            Console.WriteLine($"  Updated:   {lastUpdatedText}");
            Console.WriteLine($"  Nodes:     {nodeCount:N0}");

            Console.WriteLine($"\nProjects ({projects.Count}):");

            if (projects.Count == 0)
            {
                Console.WriteLine("  (No projects indexed yet. Run 'ce scan' to index.)");
            }
            else
            {
                foreach (var (pName, pLang, pLayer, pRole) in projects)
                {
                    var layerTag = pLayer switch
                    {
                        "layer_ingress" => "[Ingress]",
                        "layer_components" => "[Components]",
                        "layer_egress" => "[Egress]",
                        "layer_foundation" => "[Foundation]",
                        "layer_tests" => "[Tests]",
                        _ => string.IsNullOrEmpty(pLayer) ? "" : $"[{pLayer}]"
                    };
                    Console.WriteLine($"  ✓ {pName,-35} [{pLang}] {pRole,-14} {layerTag}");
                }
            }

            Console.WriteLine($"\nQueries:");

            Console.WriteLine(
                $"  Built-in:  {builtInCount} queries available (run 'ce queries' to list)");
            Console.WriteLine($"  Custom:    {savedQueries.Count} saved in .codeexplorer/queries/");

            foreach (var q in savedQueries)
            {
                Console.WriteLine($"    - {q.Name,-25} : {q.Description}");
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine($"Status Error: {ex.Message}");
            Console.ResetColor();
            return 1;
        }
    }

    private static string FormatRelativeTime(DateTime timestamp)
    {
        var diff = DateTime.Now - timestamp;
        if (diff.TotalSeconds < 0 || diff.TotalMinutes < 1) return $"{timestamp:yyyy-MM-dd HH:mm:ss} (just now)";
        if (diff.TotalMinutes < 60) return $"{timestamp:yyyy-MM-dd HH:mm:ss} ({(int)diff.TotalMinutes}m ago)";
        if (diff.TotalHours < 24) return $"{timestamp:yyyy-MM-dd HH:mm:ss} ({(int)diff.TotalHours}h {diff.Minutes}m ago)";
        if (diff.TotalDays < 7) return $"{timestamp:yyyy-MM-dd HH:mm:ss} ({(int)diff.TotalDays}d ago)";
        return $"{timestamp:yyyy-MM-dd HH:mm:ss}";
    }
}
