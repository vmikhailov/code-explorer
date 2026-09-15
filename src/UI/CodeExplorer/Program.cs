using System.Reflection;
using System.Text.Json;
using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Diagrams;
using CodeExplorer.Core.Mcp;
using CodeExplorer.Core.Mcp.Models;
using CodeExplorer.Core.Parser;
using CodeExplorer.Options;
using CodeExplorer.Parser.CSharp;
using CodeExplorer.Parser.Go;
using CodeExplorer.Parser.Java;
using CodeExplorer.Parser.Python;
using CodeExplorer.Parser.SQL;
using CodeExplorer.Parser.TypeScript;
using CommandLine;
using CommandLineParser = CommandLine.Parser;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CodeExplorer;

public class Program
{
    public static WebApplication? App { get; private set; }

    public static async Task<int> Main(string[] args)
    {
        if (args == null || args.Length == 0)
        {
            ShowWelcomeAndHelp();
            return 0;
        }

        WorkspaceIndexer.Register(new CSharpParser());
        WorkspaceIndexer.Register(new JavaParser());
        WorkspaceIndexer.Register(new GoParser());
        WorkspaceIndexer.Register(new PythonParser());
        WorkspaceIndexer.Register(new TypeScriptParser());
        WorkspaceIndexer.Register(new JavaScriptParser());
        WorkspaceIndexer.Register(new SqlParser());

        return await CommandLineParser.Default
            .ParseArguments<
                InitOptions,
                ScanOptions,
                IndexOptions,
                StatusOptions,
                InfoOptions,
                ClearOptions,
                QueriesOptions,
                QueryOptions,
                McpOptions,
                IngestOptions,
                ExportOptions>(args)
            .MapResult(
                (InitOptions opts) => HandleInitAsync(opts),
                (ScanOptions opts) => HandleScanAsync(opts),
                (IndexOptions opts) => HandleScanAsync(opts),
                (StatusOptions opts) => HandleStatusAsync(opts),
                (InfoOptions opts) => HandleStatusAsync(opts),
                (ClearOptions opts) => HandleClearAsync(opts),
                (QueriesOptions opts) => HandleQueryAsync(opts),
                (QueryOptions opts) => HandleQueryAsync(opts),
                (McpOptions opts) => HandleMcpAsync(opts),
                (IngestOptions opts) => HandleIngestAsync(opts),
                (ExportOptions opts) => HandleExportAsync(opts),
                _ => Task.FromResult(1));
    }

    private static async Task<int> HandleInitAsync(InitOptions opts)
    {
        var targetDir = Path.GetFullPath(opts.Dir ?? Directory.GetCurrentDirectory());
        var name = string.IsNullOrWhiteSpace(opts.Name) ? new DirectoryInfo(targetDir).Name : opts.Name.Trim();

        var existing = WorkspaceLocator.Find(targetDir);
        if (existing != null && existing.RootDirectory.Equals(targetDir, StringComparison.OrdinalIgnoreCase) && !opts.Force)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"Workspace already exists at '{existing.RootDirectory}'. Use -f or --force to reinitialize.");
            Console.ResetColor();
            return 1;
        }

        var ws = WorkspaceLocator.Initialize(targetDir, name);

        // Initialize DB schema & seed Workspace node
        await using (var client = new SqliteGraphClient(ws.DbPath))
        {
            await client.ExecuteWriteAsync(
                "INSERT INTO nodes (id, kind, properties) VALUES ('workspace', 'Workspace', json_object('id', 'workspace', 'name', @name, 'path', @path)) " +
                "ON CONFLICT(id) DO UPDATE SET properties = json_object('id', 'workspace', 'name', @name, 'path', @path);",
                new Dictionary<string, object?> { ["name"] = name, ["path"] = ws.RootDirectory });
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"✓ Initialized CodeExplorer workspace '{name}'");
        Console.ResetColor();
        Console.WriteLine($"  Root:     {ws.RootDirectory}");
        Console.WriteLine($"  Database: {ws.DbPath}");
        Console.WriteLine($"  Queries:  {ws.QueriesDirectory}");
        Console.WriteLine($"\nNext: run 'ce scan' to index your codebase.");
        return 0;
    }

    private static async Task<int> HandleExportAsync(ExportOptions opts)
    {
        var targetDir = Path.GetFullPath(opts.Dir ?? Directory.GetCurrentDirectory());
        var ws = WorkspaceLocator.Find(targetDir);
        if (ws == null)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Error: No CodeExplorer workspace found at '{targetDir}'. Run 'ce init' first.");
            Console.ResetColor();
            return 1;
        }

        await using var client = new SqliteGraphClient(ws.DbPath);
        var diagram = await DiagramExporter.ExportAsync(client, opts.Format, opts.Type, opts.Project);

        if (!string.IsNullOrWhiteSpace(opts.Output))
        {
            var outPath = Path.GetFullPath(opts.Output);
            var dir = Path.GetDirectoryName(outPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            await File.WriteAllTextAsync(outPath, diagram);
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"✓ Exported {opts.Format.ToUpperInvariant()} diagram to '{outPath}'");
            Console.ResetColor();
        }
        else
        {
            Console.WriteLine(diagram);
        }

        return 0;
    }

    private static async Task<int> HandleScanAsync(ScanOptions opts)
    {
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder
                .SetMinimumLevel(LogLevel.Information)
                .AddShortConsole();
        });

        var logger = loggerFactory.CreateLogger<Program>();
        var indexerLogger = loggerFactory.CreateLogger<WorkspaceIndexer>();
        var clientLogger = loggerFactory.CreateLogger<SqliteGraphClient>();

        try
        {
            var targetPath = Path.GetFullPath(opts.Path ?? Directory.GetCurrentDirectory());
            var ws = WorkspaceLocator.FindOrThrow(targetPath);

            logger.LogInformation("Workspace: {WorkspaceRoot} ({DbPath})", ws.RootDirectory, ws.DbPath);
            logger.LogInformation("Scanning:  {TargetPath}...", targetPath);

            await using var client = new SqliteGraphClient(ws.DbPath, clientLogger);

            if (opts.Clear)
            {
                logger.LogInformation("Clearing existing data for {TargetPath}...", targetPath);
                await client.ClearWorkspaceAsync(targetPath);
            }

            var indexer = new WorkspaceIndexer(client, indexerLogger);
            var (nodesCount, relsCount, nodesByKind) = await indexer.IndexAsync(targetPath, ws.RootDirectory, clear: false);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"\n✓ Successfully indexed {nodesCount} nodes and {relsCount} relationships!");
            Console.ResetColor();

            Console.WriteLine("Nodes breakdown:");
            foreach (var (kind, count) in nodesByKind.OrderByDescending(x => x.Value))
            {
                Console.WriteLine($"  - {kind,-20}: {count,6}");
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine($"Scan Error: {ex.Message}");
            Console.ResetColor();
            return 1;
        }
    }

    private static async Task<int> HandleStatusAsync(StatusOptions opts)
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
            var wsResult = await client.ExecuteQueryAsync("MATCH (w:Workspace) RETURN w.name AS name, w.path AS path LIMIT 1");
            using var wsDoc = JsonDocument.Parse(wsResult);
            if (wsDoc.RootElement.ValueKind == JsonValueKind.Array && wsDoc.RootElement.GetArrayLength() > 0)
            {
                var row = wsDoc.RootElement[0];
                if (row.TryGetProperty("name", out var np) && np.ValueKind == JsonValueKind.String)
                {
                    wsName = np.GetString() ?? wsName;
                }
            }

            // Projects info
            var projResult = await client.ExecuteQueryAsync("MATCH (p:Project) RETURN p.name AS name, p.project_type AS language ORDER BY p.name");
            using var projDoc = JsonDocument.Parse(projResult);
            var projects = new List<(string Name, string Language)>();
            if (projDoc.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var row in projDoc.RootElement.EnumerateArray())
                {
                    var pName = row.TryGetProperty("name", out var n) ? n.GetString() ?? "unknown" : "unknown";
                    var pLang = row.TryGetProperty("language", out var l) ? l.GetString() ?? "unknown" : "unknown";
                    projects.Add((pName, pLang));
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
                    total_nodes = nodeCount,
                    projects = projects.Select(p => new { name = p.Name, language = p.Language }),
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
            Console.WriteLine($"  Nodes:     {nodeCount:N0}");

            Console.WriteLine($"\nProjects ({projects.Count}):");
            if (projects.Count == 0)
            {
                Console.WriteLine("  (No projects indexed yet. Run 'ce scan' to index.)");
            }
            else
            {
                foreach (var (pName, pLang) in projects)
                {
                    Console.WriteLine($"  ✓ {pName,-30} [{pLang}]");
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

    private static async Task<int> HandleClearAsync(ClearOptions opts)
    {
        try
        {
            var ws = WorkspaceLocator.FindOrThrow(opts.Dir);
            await using var client = new SqliteGraphClient(ws.DbPath);

            if (!string.IsNullOrWhiteSpace(opts.Path))
            {
                var targetPath = Path.GetFullPath(opts.Path);
                Console.WriteLine($"Clearing data for path '{targetPath}' from workspace '{ws.RootDirectory}'...");
                var cleared = await client.ClearWorkspaceAsync(targetPath);
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine(cleared ? "✓ Cleared path data successfully." : "Path not found in database.");
                Console.ResetColor();
                return 0;
            }

            if (!opts.Yes)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write($"Are you sure you want to clear the entire graph database at '{ws.DbPath}'? (y/N): ");
                Console.ResetColor();
                var answer = Console.ReadLine();
                if (!string.Equals(answer?.Trim(), "y", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(answer?.Trim(), "yes", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("Operation cancelled.");
                    return 0;
                }
            }

            await client.ClearDatabaseAsync();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"✓ Cleared entire database at '{ws.DbPath}'.");
            Console.ResetColor();
            return 0;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine($"Clear Error: {ex.Message}");
            Console.ResetColor();
            return 1;
        }
    }

    private static async Task<int> HandleQueryAsync(QueryOptions opts)
    {
        try
        {
            if (opts.List || (string.IsNullOrWhiteSpace(opts.Query) &&
                             string.IsNullOrWhiteSpace(opts.FilePath) &&
                             string.IsNullOrWhiteSpace(opts.QueryName) &&
                             string.IsNullOrWhiteSpace(opts.ShowQueryName)))
            {
                return PrintAllQueries(opts);
            }

            if (!string.IsNullOrWhiteSpace(opts.ShowQueryName))
            {
                return ShowQuerySource(opts);
            }

            var ws = WorkspaceLocator.Find(opts.Dir);

            string cypherQuery;
            if (!string.IsNullOrWhiteSpace(opts.QueryName))
            {
                var qName = opts.QueryName.Trim();
                var custom = ws != null ? new ProjectQueryManager().GetQuery(ws.RootDirectory, qName) : null;
                if (custom != null)
                {
                    cypherQuery = custom.Cypher;
                }
                else if (Queries.Exists(qName))
                {
                    cypherQuery = Queries.Get(qName);
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.Error.WriteLine($"Error: Query '{qName}' not found in custom or built-in queries.");
                    Console.ResetColor();
                    Console.WriteLine("Run 'ce queries' to view all available queries.");
                    return 1;
                }
            }
            else if (!string.IsNullOrWhiteSpace(opts.FilePath))
            {
                if (!File.Exists(opts.FilePath))
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.Error.WriteLine($"Error: Query file not found: '{opts.FilePath}'.");
                    Console.ResetColor();
                    return 1;
                }
                cypherQuery = await File.ReadAllTextAsync(opts.FilePath);
            }
            else if (!string.IsNullOrWhiteSpace(opts.Query))
            {
                cypherQuery = opts.Query;
            }
            else
            {
                return PrintAllQueries(opts);
            }

            string dbPath;
            if (!string.IsNullOrWhiteSpace(opts.DbPath))
            {
                dbPath = Path.GetFullPath(opts.DbPath);
            }
            else
            {
                var currentWs = ws ?? WorkspaceLocator.FindOrThrow(opts.Dir);
                dbPath = currentWs.DbPath;
            }

            if (!File.Exists(dbPath))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Error.WriteLine($"Error: Graph database not found at '{dbPath}'. Run 'ce scan' first to index.");
                Console.ResetColor();
                return 1;
            }

            await using var client = new SqliteGraphClient(dbPath);

            var parameters = new Dictionary<string, object?>();
            if (cypherQuery.Contains("{prefixFilter}"))
            {
                cypherQuery = cypherQuery.Replace("{prefixFilter}", "");
            }
            if (cypherQuery.Contains("{prefixClause}"))
            {
                cypherQuery = cypherQuery.Replace("{prefixClause}", "");
            }
            if (cypherQuery.Contains("{depth}"))
            {
                cypherQuery = cypherQuery.Replace("{depth}", "5");
            }
            if (cypherQuery.Contains("$workspaceId", StringComparison.OrdinalIgnoreCase))
            {
                parameters["workspaceId"] = "workspace";
            }
            if (cypherQuery.Contains("$workspaceIdPrefix", StringComparison.OrdinalIgnoreCase) ||
                cypherQuery.Contains("$wsIdPrefix", StringComparison.OrdinalIgnoreCase))
            {
                parameters["workspaceIdPrefix"] = "workspace:";
                parameters["wsIdPrefix"] = "workspace:";
            }

            if (opts.Params != null)
            {
                foreach (var paramStr in opts.Params)
                {
                    if (string.IsNullOrWhiteSpace(paramStr)) continue;
                    var eqIdx = paramStr.IndexOf('=');
                    if (eqIdx > 0)
                    {
                        var key = paramStr.Substring(0, eqIdx).Trim().TrimStart('$');
                        var valStr = paramStr.Substring(eqIdx + 1).Trim();
                        if (long.TryParse(valStr, out var longVal))
                        {
                            parameters[key] = longVal;
                        }
                        else if (double.TryParse(valStr, System.Globalization.CultureInfo.InvariantCulture, out var dblVal))
                        {
                            parameters[key] = dblVal;
                        }
                        else if (bool.TryParse(valStr, out var boolVal))
                        {
                            parameters[key] = boolVal;
                        }
                        else
                        {
                            parameters[key] = valStr;
                        }
                    }
                }
            }

            var resultJson = await client.ExecuteQueryAsync(cypherQuery, parameters);

            bool isJsonFormat = opts.Json || string.Equals(opts.Format, "json", StringComparison.OrdinalIgnoreCase);
            if (isJsonFormat)
            {
                Console.WriteLine(resultJson);
                return 0;
            }

            // Print formatted table
            using var queryDoc = JsonDocument.Parse(resultJson);
            if (queryDoc.RootElement.ValueKind == JsonValueKind.Array)
            {
                var rows = queryDoc.RootElement.EnumerateArray().ToList();
                if (rows.Count == 0)
                {
                    Console.WriteLine("(0 rows returned)");
                    return 0;
                }

                var columns = rows[0].EnumerateObject().Select(p => p.Name).ToList();
                var maxLimit = opts.NoTruncate ? int.MaxValue : 80;
                var colWidths = columns.ToDictionary(c => c, c => c.Length);
                bool wasTruncated = false;

                static string FormatCell(JsonElement elem)
                {
                    if (elem.ValueKind == JsonValueKind.Null || elem.ValueKind == JsonValueKind.Undefined)
                        return "";
                    if (elem.ValueKind == JsonValueKind.String)
                        return elem.GetString() ?? "";
                    if (elem.ValueKind == JsonValueKind.Array || elem.ValueKind == JsonValueKind.Object)
                        return JsonSerializer.Serialize(elem);
                    return elem.ToString();
                }

                var tableData = new List<Dictionary<string, string>>(rows.Count);
                foreach (var row in rows)
                {
                    var rowData = new Dictionary<string, string>();
                    foreach (var col in columns)
                    {
                        var rawStr = row.TryGetProperty(col, out var p) ? FormatCell(p) : "";
                        var singleLine = System.Text.RegularExpressions.Regex.Replace(rawStr, @"[\r\n\t]+", " ").Trim();
                        rowData[col] = singleLine;

                        var displayLen = opts.NoTruncate ? singleLine.Length : Math.Min(singleLine.Length, maxLimit);
                        if (displayLen > colWidths[col])
                        {
                            colWidths[col] = displayLen;
                        }
                    }
                    tableData.Add(rowData);
                }

                // Print header
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine(string.Join(" | ", columns.Select(c => c.PadRight(colWidths[c]))));
                Console.WriteLine(string.Join("-+-", columns.Select(c => new string('-', colWidths[c]))));
                Console.ResetColor();

                // Print rows
                foreach (var row in tableData)
                {
                    var line = string.Join(" | ", columns.Select(c =>
                    {
                        var valStr = row[c];
                        if (!opts.NoTruncate && valStr.Length > maxLimit)
                        {
                            wasTruncated = true;
                            valStr = valStr[..(maxLimit - 3)] + "...";
                        }
                        return valStr.PadRight(colWidths[c]);
                    }));
                    Console.WriteLine(line);
                }

                Console.WriteLine($"\n({rows.Count} rows)");

                if (wasTruncated)
                {
                    Console.ForegroundColor = ConsoleColor.DarkYellow;
                    Console.WriteLine("\n[!] Output was truncated in table view.");
                    Console.ResetColor();
                    Console.WriteLine("    Run with -j or --json to view full structured JSON data.");
                    Console.WriteLine("    Run with --no-truncate to display full table columns without cutoff.");
                }
            }
            else
            {
                Console.WriteLine(resultJson);
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine($"Query Error: {ex.Message}");
            Console.ResetColor();
            return 1;
        }
    }

    private static int PrintAllQueries(QueryOptions opts)
    {
        var ws = WorkspaceLocator.Find(opts.Dir);
        var builtInQueries = Queries.GetBuiltInQueries();
        var customQueries = ws != null
            ? new ProjectQueryManager().ListQueries(ws.RootDirectory)
            : [];

        if (string.Equals(opts.Format, "json", StringComparison.OrdinalIgnoreCase))
        {
            var jsonObject = new
            {
                workspace = ws?.RootDirectory,
                built_in = builtInQueries.Select(q => new
                {
                    name = q.Name,
                    category = q.Category,
                    description = q.Description
                }),
                custom = customQueries.Select(q => new
                {
                    name = q.Name,
                    description = q.Description,
                    parameters = q.Parameters,
                    path = q.CypherPath
                })
            };
            Console.WriteLine(JsonSerializer.Serialize(jsonObject, new JsonSerializerOptions { WriteIndented = true }));
            return 0;
        }

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("============================================================");
        Console.WriteLine(" CodeExplorer Queries");
        Console.WriteLine("============================================================");
        Console.ResetColor();

        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine("\nBuilt-in Queries:");
        Console.ResetColor();

        var byCategory = builtInQueries.GroupBy(q => q.Category).OrderBy(g => g.Key);
        foreach (var group in byCategory)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"\n  [{group.Key}]");
            Console.ResetColor();

            foreach (var q in group)
            {
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write($"    {q.Name,-36}");
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine(q.Description);
                Console.ResetColor();
            }
        }

        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine($"\nCustom Workspace Queries ({customQueries.Count}):");
        Console.ResetColor();

        if (ws == null)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("  (No .codeexplorer workspace detected in current directory)");
            Console.ResetColor();
        }
        else if (customQueries.Count == 0)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"  (None saved yet in {ws.QueriesDirectory})");
            Console.ResetColor();
        }
        else
        {
            foreach (var q in customQueries)
            {
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write($"    {q.Name,-36}");
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine(q.Description);
                Console.ResetColor();
            }
        }

        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine("Usage:");
        Console.ResetColor();
        Console.WriteLine("  ce query -n <name>             Execute query by name");
        Console.WriteLine("  ce query --show <name>         View Cypher source code of query");
        Console.WriteLine("  ce query \"<cypher>\"            Execute custom ad-hoc Cypher query");
        Console.WriteLine("  ce query -f <path.cypher>      Execute query from a file\n");

        return 0;
    }

    private static int ShowQuerySource(QueryOptions opts)
    {
        var queryName = opts.ShowQueryName!.Trim();
        var ws = WorkspaceLocator.Find(opts.Dir);

        ProjectQueryItem? custom = null;
        if (ws != null)
        {
            custom = new ProjectQueryManager().GetQuery(ws.RootDirectory, queryName);
        }

        if (custom != null)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"// Custom Query: {custom.Name} ({custom.CypherPath})");
            if (!string.IsNullOrWhiteSpace(custom.Description))
            {
                Console.WriteLine($"// Description: {custom.Description}");
            }
            Console.ResetColor();
            Console.WriteLine(custom.Cypher);
            return 0;
        }

        if (Queries.Exists(queryName))
        {
            var cypher = Queries.Get(queryName);
            var builtIn = Queries.GetBuiltInQueries().FirstOrDefault(q => q.Name.Equals(queryName, StringComparison.OrdinalIgnoreCase));
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"// Built-in Query: {queryName}{(builtIn != null ? $" [{builtIn.Category}] - {builtIn.Description}" : "")}");
            Console.ResetColor();
            Console.WriteLine(cypher);
            return 0;
        }

        Console.ForegroundColor = ConsoleColor.Red;
        Console.Error.WriteLine($"Error: Query '{queryName}' not found in built-in or custom queries.");
        Console.ResetColor();
        Console.WriteLine("Run 'ce queries' to list all available queries.");
        return 1;
    }

    private static async Task<int> HandleMcpAsync(McpOptions opts)
    {
        WorkspaceInfo? ws = null;
        string? dbPath = null;
        if (!string.IsNullOrWhiteSpace(opts.DbPath))
        {
            dbPath = Path.GetFullPath(opts.DbPath);
            ws = WorkspaceLocator.FindFromDbPath(dbPath);
        }
        else
        {
            ws = WorkspaceLocator.FindWithFallbacks(opts.Root);
            if (ws != null)
            {
                dbPath = ws.DbPath;
                WorkspaceLocator.RecordActiveWorkspace(ws.RootDirectory);
            }
        }

        var isStandby = string.IsNullOrEmpty(dbPath) || !File.Exists(dbPath);
        if (isStandby && string.IsNullOrEmpty(dbPath))
        {
            dbPath = "Data Source=:memory:;Mode=Memory;Cache=Shared";
        }

        await using var client = new SqliteGraphClient(dbPath!);
        var wsRoot = ws?.RootDirectory;

        if (opts.Port > 0)
        {
            await RunMcpWebServerAsync(client, opts.Port, wsRoot, isStandby, opts.Quiet);
        }
        else
        {
            await RunMcpStdioHostAsync(client, wsRoot, isStandby, opts.Quiet);
        }

        return 0;
    }


    private static async Task<int> HandleIngestAsync(IngestOptions opts)
    {
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder
                .SetMinimumLevel(LogLevel.Information)
                .AddShortConsole();
        });

        var logger = loggerFactory.CreateLogger<Program>();
        var indexerLogger = loggerFactory.CreateLogger<WorkspaceIndexer>();
        var clientLogger = loggerFactory.CreateLogger<SqliteGraphClient>();

        try
        {
            logger.LogInformation("Scanning and parsing directory: {Directory}...", opts.Dir);
            await using var client = new SqliteGraphClient(opts.DbPath, clientLogger);

            if (opts.ClearAll)
            {
                logger.LogInformation("Performing a global database clear...");
                await client.ClearDatabaseAsync();
            }

            var indexer = new WorkspaceIndexer(client, indexerLogger);

            var (nodesCount, relsCount, nodesByKind) =
                await indexer.IndexAsync(opts.Dir, opts.Dir, opts.Clear && !opts.ClearAll);

            logger.LogInformation("Parsed and uploaded {NodesCount} nodes and {RelationshipsCount} relationships successfully!", nodesCount, relsCount);
            logger.LogInformation("Nodes breakdown by kind:");

            foreach (var (kind, count) in nodesByKind)
            {
                logger.LogInformation("  - {Kind}: {Count}", kind, count);
            }

            return 0;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ingestion Error: {Message}", ex.Message);
            return 1;
        }
    }

    private static async Task RunMcpWebServerAsync(SqliteGraphClient client, int port, string? workspaceRoot = null, bool isStandby = false, bool quiet = false)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        if (!quiet)
        {
            builder.Logging.AddFilter("Microsoft", LogLevel.Warning);
            builder.Logging.AddFilter("System", LogLevel.Warning);
            builder.Logging.AddShortConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);
        }
        else
        {
            builder.Logging.SetMinimumLevel(LogLevel.None);
        }

        ConfigureWebServices(builder.Services, client, workspaceRoot);

        var app = builder.Build();
        App = app;
        client.Logger = app.Services.GetRequiredService<ILogger<SqliteGraphClient>>();
        ConfigureWebPipeline(app, quiet);

        app.Urls.Add($"http://0.0.0.0:{port}");
        if (!quiet)
        {
            if (isStandby)
            {
                app.Logger.LogWarning("[MCP Standby] Server started in Standby Mode (no workspace bound). Specify 'workspacePath' on tool calls or set WORKSPACE_ROOT environment variable.");
            }
            else
            {
                app.Logger.LogInformation("Starting CodeExplorer MCP HTTP Service on http://localhost:{Port} (endpoint: /mcp)...", port);
            }
        }
        await app.RunAsync();
    }

    private static void ConfigureWebServices(IServiceCollection services, SqliteGraphClient client, string? workspaceRoot = null)
    {
        services.AddCors(options => options.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));
        RegisterCommonServices(services, client, workspaceRoot);

#pragma warning disable MCP9004, MCPEXP002
        services.AddMcpServer().WithHttpTransport(o =>
        {
            o.Stateless = false;
            o.EnableLegacySse = true;
        }).WithTools<McpGraphHandler>();
#pragma warning restore MCP9004, MCPEXP002
    }

    private static void ConfigureWebPipeline(WebApplication app, bool quiet = false)
    {
        if (!quiet)
        {
            app.Use(async (context, next) =>
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var path = context.Request.Path.Value ?? "";
                var method = context.Request.Method;

                try
                {
                    await next();
                }
                finally
                {
                    sw.Stop();
                    var contentType = context.Response.ContentType ?? string.Empty;
                    if (!contentType.Contains("text/event-stream", StringComparison.OrdinalIgnoreCase))
                    {
                        app.Logger.LogInformation("[MCP] {Method} {Path} completed with {StatusCode} in {ElapsedMs:F1}ms",
                            method, path, context.Response.StatusCode, sw.Elapsed.TotalMilliseconds);
                    }
                }
            });
        }

        app.UseCors();
        app.MapGet("/", () => Results.Ok(new { service = "CodeExplorer MCP", transport = "http", endpoint = "/mcp" }));
        app.MapMcp("/mcp");
    }

    private static async Task RunMcpStdioHostAsync(SqliteGraphClient client, string? workspaceRoot = null, bool isStandby = false, bool quiet = false)
    {
        var builder = Host.CreateApplicationBuilder();
        if (!quiet)
        {
            builder.Logging.AddFilter("Microsoft", LogLevel.Warning);
            builder.Logging.AddFilter("System", LogLevel.Warning);
            builder.Logging.AddShortConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);
        }
        else
        {
            builder.Logging.ClearProviders();
            builder.Logging.SetMinimumLevel(LogLevel.None);
        }

        RegisterCommonServices(builder.Services, client, workspaceRoot);
        builder.Services.AddMcpServer().WithStdioServerTransport().WithTools<McpGraphHandler>();

        var host = builder.Build();
        client.Logger = host.Services.GetRequiredService<ILogger<SqliteGraphClient>>();
        if (isStandby && !quiet)
        {
            var logger = host.Services.GetRequiredService<ILogger<Program>>();
            logger.LogWarning("[MCP Standby] Server started in Standby Mode (no workspace bound). Specify 'workspacePath' on tool calls or set WORKSPACE_ROOT environment variable.");
        }
        await host.RunAsync();
    }

    private static void RegisterCommonServices(IServiceCollection services, SqliteGraphClient client, string? workspaceRoot = null)
    {
        services.AddSingleton<IGraphClient>(client);
        services.AddSingleton<ProjectQueryManager>();
        services.AddSingleton(sp => new CodeExplorerRepository(
            sp.GetRequiredService<IGraphClient>(),
            sp.GetRequiredService<ProjectQueryManager>(),
            workspaceRoot
        ));
        services.AddSingleton<WorkspaceIndexer>();
        services.AddHttpContextAccessor();
    }

    private static string GetAppVersion()
    {
        var asm = typeof(Program).Assembly;
        var infoVer = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(infoVer))
        {
            return infoVer.Split('+')[0];
        }
        var ver = asm.GetName().Version;
        return ver != null ? $"{ver.Major}.{ver.Minor}.{ver.Build}" : "1.0.0";
    }

    private static void ShowWelcomeAndHelp()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("CodeExplorer (ce) - High-performance graph-based code intelligence & MCP server");
        Console.ResetColor();
        Console.WriteLine($"Version: {GetAppVersion()}\n");

        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine("USAGE:");
        Console.ResetColor();
        Console.WriteLine("  ce <command> [options]\n");

        // Workspace detection
        var ws = WorkspaceLocator.Find();
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine("CURRENT WORKSPACE:");
        Console.ResetColor();
        if (ws != null)
        {
            var dbExists = File.Exists(ws.DbPath);
            var dbSize = dbExists ? new FileInfo(ws.DbPath).Length / (1024.0 * 1024.0) : 0.0;
            var customQueriesCount = Directory.Exists(ws.QueriesDirectory)
                ? Directory.GetFiles(ws.QueriesDirectory, "*.cypher").Length
                : 0;

            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("  [OK] ");
            Console.ResetColor();
            Console.WriteLine($"Found at '{ws.RootDirectory}'");
            Console.WriteLine($"       Database: {ws.DbPath} ({(dbExists ? $"{dbSize:F1} MB" : "not indexed yet")})");
            Console.WriteLine($"       Queries:  {ws.QueriesDirectory} ({customQueriesCount} custom query files)");
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.Write("  [!]  ");
            Console.ResetColor();
            Console.WriteLine("No .codeexplorer workspace detected in current directory or any parent.");
            Console.WriteLine("       Run 'ce init [name]' to initialize a workspace here.");
        }
        Console.WriteLine();

        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine("QUICK START WORKFLOW:");
        Console.ResetColor();
        Console.WriteLine("  1. ce init [name]       Initialize a .codeexplorer workspace in current directory");
        Console.WriteLine("  2. ce scan [path]       Index code topology, AST, dependencies and semantic graph");
        Console.WriteLine("  3. ce status            View workspace health, indexed projects, and statistics");
        Console.WriteLine("  4. ce queries           List all available built-in and custom Cypher queries");
        Console.WriteLine("  5. ce query \"<cypher>\"  Execute Cypher query directly against the code graph");
        Console.WriteLine("  6. ce mcp               Start MCP server (stdio) for Cursor, Claude Desktop, etc.\n");

        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine("AVAILABLE COMMANDS:");
        Console.ResetColor();
        Console.WriteLine("  init                    Initialize a new .codeexplorer workspace");
        Console.WriteLine("  scan (alias: index)     Scan and index a directory into the nearest workspace");
        Console.WriteLine("  status (alias: info)    Show workspace summary, projects, node kinds, and statistics");
        Console.WriteLine("  clear                   Clear indexed data from the workspace database");
        Console.WriteLine("  queries                 List all available built-in and workspace custom queries");
        Console.WriteLine("  query                   Run a read-only Cypher query against the knowledge graph");
        Console.WriteLine("  mcp                     Run Model Context Protocol server (stdio default, or --port)");
        Console.WriteLine();

        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine("EXAMPLES:");
        Console.ResetColor();
        Console.WriteLine("  ce init MyProject");
        Console.WriteLine("  ce scan ./src");
        Console.WriteLine("  ce status");
        Console.WriteLine("  ce queries");
        Console.WriteLine("  ce query -n get_architecture_map_workspace");
        Console.WriteLine("  ce query --show get_architecture_map_workspace");
        Console.WriteLine("  ce query \"MATCH (p:Project) RETURN p.name, p.project_type\"");
        Console.WriteLine("  ce query --file my_query.cypher");
        Console.WriteLine("  ce mcp");
        Console.WriteLine("  ce clear ./src/old-module\n");

        Console.WriteLine("For options and arguments on any specific command, run:");
        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.WriteLine("  ce <command> --help\n");
        Console.ResetColor();
    }
}
