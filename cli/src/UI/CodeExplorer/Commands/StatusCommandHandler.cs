using System.Text.Json;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Mcp;
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

            await using var client = new SqliteGraphClient(ws.DbPath);

            // Workspace node info
            string wsName = new DirectoryInfo(ws.RootDirectory).Name;
            DateTime? lastIndexedAt = null;
            var wsResult = await client.ExecuteQueryAsync("MATCH (w:Workspace) RETURN w.name AS name, w.path AS path, w.indexed_at AS indexed_at LIMIT 1");
            using var wsDoc = JsonDocument.Parse(wsResult);
            if (wsDoc.RootElement.ValueKind == JsonValueKind.Array && wsDoc.RootElement.GetArrayLength() > 0)
            {
                var row = wsDoc.RootElement[0];
                if (row.TryGetProperty("name", out var np) && np.ValueKind == JsonValueKind.String)
                {
                    wsName = np.GetString() ?? wsName;
                }
                if (row.TryGetProperty("indexed_at", out var ip) && ip.ValueKind == JsonValueKind.String &&
                    DateTime.TryParse(ip.GetString(), out var dt))
                {
                    lastIndexedAt = dt.ToLocalTime();
                }
            }

            var lastUpdated = lastIndexedAt ?? fileInfo.LastWriteTime;
            var lastUpdatedText = FormatRelativeTime(lastUpdated);

            // Projects info
            var projResult = await client.ExecuteQueryAsync("MATCH (p:Project) RETURN p.name AS name, p.project_type AS language, p.layer AS layer, p.role AS role ORDER BY p.name");
            using var projDoc = JsonDocument.Parse(projResult);
            var projects = new List<(string Name, string Language, string Layer, string Role)>();
            if (projDoc.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var row in projDoc.RootElement.EnumerateArray())
                {
                    var pName = row.TryGetProperty("name", out var n) ? n.GetString() ?? "unknown" : "unknown";
                    var pLang = row.TryGetProperty("language", out var l) ? l.GetString() ?? "unknown" : "unknown";
                    var pLayer = row.TryGetProperty("layer", out var lyr) ? lyr.GetString() ?? "" : "";
                    var pRole = row.TryGetProperty("role", out var r) ? r.GetString() ?? "" : "";
                    projects.Add((pName, pLang, pLayer, pRole));
                }
            }

            // Node count
            var countResult = await client.ExecuteQueryAsync("MATCH (n) RETURN count(n) AS nodeCount");
            long nodeCount = 0;
            using var countDoc = JsonDocument.Parse(countResult);
            if (countDoc.RootElement.ValueKind == JsonValueKind.Array && countDoc.RootElement.GetArrayLength() > 0)
            {
                if (countDoc.RootElement[0].TryGetProperty("nodeCount", out var nc) && nc.TryGetInt64(out var cnt))
                    nodeCount = cnt;
            }

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
                    last_updated = lastUpdated.ToString("o"),
                    last_updated_human = lastUpdatedText,
                    total_nodes = nodeCount,
                    projects = projects.Select(p => new { name = p.Name, language = p.Language, layer = p.Layer, role = p.Role }),
                    built_in_queries_count = builtInCount,
                    custom_queries = savedQueries.Select(q => new { name = q.Name, description = q.Description })
                };
                Console.WriteLine(JsonSerializer.Serialize(statusObj, new JsonSerializerOptions { WriteIndented = true }));
                return 0;
            }

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"Workspace: {wsName}");
            Console.ResetColor();
            Console.WriteLine($"  Root:      {ws.RootDirectory}");
            Console.WriteLine($"  Database:  {ws.DbPath} ({sizeMb:F1} MB)");
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
            Console.WriteLine($"  Built-in:  {builtInCount} queries available (run 'ce queries' to list)");
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
