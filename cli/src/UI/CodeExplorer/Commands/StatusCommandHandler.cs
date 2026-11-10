using CodeExplorer.Core.Common;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Mcp;
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
                Console.WriteLine(ToonYamlSerializer.SerializeObject(new
                {
                    status = "not_initialized",
                    root_directory = ws.RootDirectory,
                    message = "Workspace found, but database has not been initialized yet. Run 'ce scan' to index your codebase."
                }));
                return 0;
            }

            var fileInfo = new FileInfo(ws.DbPath);
            var sizeMb = fileInfo.Length / (1024.0 * 1024.0);

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

                if (!string.IsNullOrWhiteSpace(wsNode.IndexedAt) &&
                    DateTime.TryParse(wsNode.IndexedAt, out var dt))
                {
                    lastIndexedAt = dt.ToLocalTime();
                }
            }

            var lastUpdated = lastIndexedAt ?? fileInfo.LastWriteTime;
            var lastUpdatedText = FormatRelativeTime(lastUpdated);

            var projectEntities = await client.Graph.Projects.ToListAsync();
            var projects = projectEntities
                .OrderBy(p => p.Name, StringComparer.Ordinal)
                .Select(p => (
                    Name: !string.IsNullOrEmpty(p.Name) ? p.Name : "unknown",
                    Language: !string.IsNullOrEmpty(p.Language) ? p.Language : "unknown",
                    Layer: p.Layer ?? string.Empty,
                    Role: p.Role ?? string.Empty))
                .ToList();

            var (totalNodes, _) = await client.GetGraphCountsAsync();
            long nodeCount = totalNodes;

            var queryManager = new ProjectQueryManager();
            var savedQueries = queryManager.ListQueries(ws.RootDirectory);
            var builtInCount = Queries.GetBuiltInQueries().Count;

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
                projects = projects.Select(p => new
                {
                    name = p.Name,
                    language = p.Language,
                    layer = p.Layer,
                    role = p.Role
                }),
                built_in_queries_count = builtInCount,
                custom_queries = savedQueries.Select(q => new
                {
                    name = q.Name,
                    description = q.Description
                })
            };

            Console.WriteLine(ToonYamlSerializer.SerializeObject(statusObj));
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
