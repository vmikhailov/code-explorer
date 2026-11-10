using CodeExplorer.Core.Mcp;
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
                parameters["workspaceId"] = "ws";
            }

            if (cypherQuery.Contains("$workspaceIdPrefix", StringComparison.OrdinalIgnoreCase) ||
                cypherQuery.Contains("$wsIdPrefix", StringComparison.OrdinalIgnoreCase))
            {
                parameters["workspaceIdPrefix"] = "ws:";
                parameters["wsIdPrefix"] = "ws:";
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

            Console.WriteLine(ToonYamlSerializer.SerializeYaml(result));
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
