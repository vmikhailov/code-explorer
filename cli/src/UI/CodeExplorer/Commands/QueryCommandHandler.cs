using System.Text.Json;
using System.Text.RegularExpressions;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Mcp;
using CodeExplorer.Core.Mcp.Models;
using CodeExplorer.Options;

namespace CodeExplorer.Commands;

public static class QueryCommandHandler
{
    public static async Task<int> HandleAsync(QueryOptions opts)
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
                        var key = paramStr[..eqIdx].Trim().TrimStart('$');
                        var valStr = paramStr[(eqIdx + 1)..].Trim();
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
                        var singleLine = Regex.Replace(rawStr, @"[\r\n\t]+", " ").Trim();
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

    public static int PrintAllQueries(QueryOptions opts)
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

    public static int ShowQuerySource(QueryOptions opts)
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
}
