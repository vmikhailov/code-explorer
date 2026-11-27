using CodeExplorer.Core.Mcp.Models;
using CodeExplorer.Options;

namespace CodeExplorer.Commands;

public class QueryCommandHandler : BaseCommandHandler
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
                var trimmedQ = opts.Query.Trim();
                if (!trimmedQ.Contains(' ') && !trimmedQ.Contains('\n'))
                {
                    var custom = ws != null ? new ProjectQueryManager().GetQuery(ws.RootDirectory, trimmedQ) : null;
                    if (custom != null)
                    {
                        cypherQuery = custom.Cypher;
                    }
                    else if (Queries.Exists(trimmedQ))
                    {
                        cypherQuery = Queries.Get(trimmedQ);
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.Error.WriteLine($"Error: Query '{trimmedQ}' not found in custom or built-in queries. Run 'ce queries' to list available query names, or pass a valid Cypher query.");
                        Console.ResetColor();
                        return 1;
                    }
                }
                else
                {
                    cypherQuery = opts.Query;
                }
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
                parameters["workspaceId"] = "ws";
            }

            if (cypherQuery.Contains("$workspaceIdPrefix", StringComparison.OrdinalIgnoreCase) ||
                cypherQuery.Contains("$wsIdPrefix", StringComparison.OrdinalIgnoreCase))
            {
                parameters["workspaceIdPrefix"] = "ws:";
                parameters["wsIdPrefix"] = "ws:";
            }

            if (cypherQuery.Contains("$projectName", StringComparison.OrdinalIgnoreCase))
            {
                parameters["projectName"] = "";
            }

            if (cypherQuery.Contains("$projectFilter", StringComparison.OrdinalIgnoreCase))
            {
                parameters["projectFilter"] = "";
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
                        else if (double.TryParse(valStr, System.Globalization.CultureInfo.InvariantCulture,
                                     out var dblVal))
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

            var result = await client.ExecuteCypherAsync(cypherQuery, parameters);
            if (opts.Limit is > 0 && result.Rows.Count > opts.Limit.Value)
            {
                result = new CypherQueryResult(result.Columns, result.Rows.Take(opts.Limit.Value).ToList());
            }

            Console.WriteLine(FormatQueryResult(result, opts.Format));
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

    private const char CommaDelimiter = ',';
    private const char TabDelimiter = '\t';

    internal static string FormatQueryResult(CypherQueryResult result, string? format)
    {
        var fmt = (format ?? "yaml").Trim().ToLowerInvariant();
        return fmt switch
        {
            "json" => FormatJsonResult(result),
            "csv" => FormatDelimitedResult(result, CommaDelimiter),
            "tsv" => FormatDelimitedResult(result, TabDelimiter),
            _ => ToonYamlSerializer.SerializeYaml(result)
        };
    }

    private static string FormatJsonResult(CypherQueryResult result)
    {
        var list = new List<Dictionary<string, object?>>(result.Rows.Count);
        foreach (var row in result.Rows)
        {
            var dict = new Dictionary<string, object?>(result.Columns.Count, StringComparer.Ordinal);
            for (var c = 0; c < result.Columns.Count && c < row.Values.Count; c++)
            {
                var val = row.Values[c];
                dict[result.Columns[c]] = val.Kind switch
                {
                    CypherValueKind.Null => null,
                    CypherValueKind.Boolean => val.AsBool(),
                    CypherValueKind.Int64 => val.AsInt64(),
                    CypherValueKind.Float64 => val.AsDouble(),
                    CypherValueKind.String => val.AsString(),
                    _ => val.ToJsonString()
                };
            }
            list.Add(dict);
        }

        return System.Text.Json.JsonSerializer.Serialize(list, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
    }

    private static string FormatDelimitedResult(CypherQueryResult result, char delimiter)
    {
        if (result.Columns.Count == 0)
        {
            return string.Empty;
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine(string.Join(delimiter, result.Columns.Select(c => EscapeDelimitedCell(c, delimiter))));

        foreach (var row in result.Rows)
        {
            for (var c = 0; c < result.Columns.Count; c++)
            {
                if (c > 0) sb.Append(delimiter);
                var cell = c < row.Values.Count ? FormatCellText(row.Values[c]) : string.Empty;
                sb.Append(EscapeDelimitedCell(cell, delimiter));
            }
            sb.AppendLine();
        }

        return sb.ToString().TrimEnd('\r', '\n');
    }

    private static string FormatCellText(CypherValue val) =>
        val.Kind switch
        {
            CypherValueKind.Null => string.Empty,
            CypherValueKind.String => val.AsString(),
            CypherValueKind.Boolean => val.AsBool() ? "true" : "false",
            CypherValueKind.Int64 => val.AsInt64().ToString(System.Globalization.CultureInfo.InvariantCulture),
            CypherValueKind.Float64 => val.AsDouble().ToString(System.Globalization.CultureInfo.InvariantCulture),
            _ => val.ToJsonString()
        };

    private static string EscapeDelimitedCell(string text, char delimiter)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        if (delimiter == '\t')
        {
            return text.Replace('\t', ' ').Replace("\r", " ").Replace("\n", " ");
        }

        if (text.Contains(delimiter) || text.Contains('"') || text.Contains('\n') || text.Contains('\r'))
        {
            return $"\"{text.Replace("\"", "\"\"")}\"";
        }

        return text;
    }

    public static int PrintAllQueries(QueryOptions opts)
    {
        var ws = WorkspaceLocator.Find(opts.Dir);
        var builtInQueries = Queries.GetBuiltInQueries();
        var customQueries = ws != null
            ? new ProjectQueryManager().ListQueries(ws.RootDirectory)
            : [];

        var queryObj = new
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

        Console.WriteLine(ToonYamlSerializer.SerializeObject(queryObj));
        return 0;
    }

    public static int ShowQuerySource(QueryOptions opts)
    {
        var queryName = (opts.ShowQueryName ?? string.Empty).Trim();
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
