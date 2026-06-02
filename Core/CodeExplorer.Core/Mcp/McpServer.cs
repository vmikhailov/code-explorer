using System.IO;
using System.Text.Json;
using CodeExplorer.Database;

namespace CodeExplorer.Mcp;

public class McpServer(McpGraphRepository graphRepository)
{
    public async Task StartAsync()
    {
        Console.SetError(new StreamWriter(Console.OpenStandardError()) { AutoFlush = true });
        await Console.Error.WriteLineAsync("Starting CodeExplorer C# MCP Server over Stdio...");

        Console.InputEncoding = System.Text.Encoding.UTF8;
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        using var reader = new StreamReader(Console.OpenStandardInput(), System.Text.Encoding.UTF8);
        
        while (true)
        {
            var line = await reader.ReadLineAsync();
            if (line == null) break;

            try
            {
                var response = await ProcessRequestAsync(line);
                if (response != null)
                {
                    Console.WriteLine(response);
                }
            }
            catch (Exception ex)
            {
                await Console.Error.WriteLineAsync($"Error handling MCP request: {ex.Message}");
            }
        }
    }

    public async Task<string?> ProcessRequestAsync(string jsonLine)
    {
        using var doc = JsonDocument.Parse(jsonLine);
        var root = doc.RootElement;

        if (!root.TryGetProperty("id", out var idProp)) return null;

        var id = idProp.ValueKind == JsonValueKind.Number ? idProp.GetInt64() : (object)idProp.GetString()!;
        var method = root.GetProperty("method").GetString();

        object responseResult;

        switch (method)
        {
            case "initialize":
                responseResult = new
                {
                    protocolVersion = "2024-11-05",
                    capabilities = new
                    {
                        tools = new { }
                    },
                    serverInfo = new
                    {
                        name = "CodeExplorer",
                        version = "1.0.0"
                    }
                };
                break;

            case "tools/list":
                try
                {
                    var jsonPath = Path.Combine(AppContext.BaseDirectory, "Mcp", "mcp_tools.json");
                    if (!File.Exists(jsonPath))
                    {
                        jsonPath = Path.Combine(AppContext.BaseDirectory, "mcp_tools.json");
                    }
                    var jsonText = File.ReadAllText(jsonPath);
                    using var toolDoc = JsonDocument.Parse(jsonText);
                    var toolsArray = toolDoc.RootElement.GetProperty("mcp_tools").Clone();

                    responseResult = new
                    {
                        tools = toolsArray
                    };
                }
                catch (Exception ex)
                {
                    await Console.Error.WriteLineAsync($"[McpServer] Error loading mcp_tools.json: {ex.Message}");
                    responseResult = new
                    {
                        tools = Array.Empty<object>()
                    };
                }
                break;

            case "tools/call":
                var paramsEl = root.GetProperty("params");
                var toolName = paramsEl.GetProperty("name").GetString();
                var args = paramsEl.GetProperty("arguments");

                responseResult = toolName switch
                {
                    "get_architecture_map" => await graphRepository.GetArchitectureMapAsync(args),
                    "get_project_dependencies" => await graphRepository.GetProjectDependenciesAsync(args),
                    "get_file_outline" => await graphRepository.GetFileOutlineAsync(args),
                    "find_symbol" => await graphRepository.FindSymbolAsync(args),
                    "get_call_chain" => await graphRepository.GetCallChainAsync(args),
                    "resolve_call_target" => await graphRepository.ResolveCallTargetAsync(args),
                    "analyze_code_impact" => await graphRepository.AnalyzeCodeImpactAsync(args),
                    "inspect_data_lineage" => await graphRepository.InspectDataLineageAsync(args),
                    "get_project_entry_points" => await graphRepository.GetProjectEntryPointsAsync(args),
                    "find_refactoring_opportunities" => await graphRepository.FindRefactoringOpportunitiesAsync(args),
                    "execute_custom_read_cypher" => await graphRepository.ExecuteCustomReadCypherAsync(args),
                    "fetch_code_snippets" => HandleFetchCodeSnippets(args),
                    "get_taxonomy" => await graphRepository.GetTaxonomyAsync(args),
                    _ => new { isError = true, content = new[] { new { type = "text", text = $"Unknown tool: {toolName}" } } }
                };
                break;

            default:
                await Console.Error.WriteLineAsync($"Unsupported method: {method}");
                return null;
        }

        var response = new
        {
            jsonrpc = "2.0",
            id,
            result = responseResult
        };

        return JsonSerializer.Serialize(response);
    }

    private object HandleFetchCodeSnippets(JsonElement args)
    {
        if (!args.TryGetProperty("nodes_json", out var nodesEl) || nodesEl.ValueKind != JsonValueKind.String)
        {
            return new { isError = true, content = new[] { new { type = "text", text = "Missing or invalid 'nodes_json' argument." } } };
        }

        try
        {
            var nodesJSON = nodesEl.GetString()!;
            var result = FetchCodeSnippetsDirectly(nodesJSON);
            return new
            {
                content = new[]
                {
                    new { type = "text", text = result }
                }
            };
        }
        catch (Exception ex)
        {
            return new
            {
                isError = true,
                content = new[]
                {
                    new { type = "text", text = ex.Message }
                }
            };
        }
    }

    private static string FetchCodeSnippetsDirectly(string nodesJSON)
    {
        List<McpRAGNode>? nodes = null;

        try
        {
            nodes = JsonSerializer.Deserialize<List<McpRAGNode>>(nodesJSON);
        }
        catch
        {
            try
            {
                var single = JsonSerializer.Deserialize<McpRAGNode>(nodesJSON);
                if (single != null) nodes = new List<McpRAGNode> { single };
            }
            catch
            {
                try
                {
                    var nestedNodes = JsonSerializer.Deserialize<List<NestedMcpRAGNode>>(nodesJSON);
                    if (nestedNodes != null)
                    {
                        nodes = nestedNodes.Where(n => n.props != null).Select(n => n.props!).ToList();
                    }
                }
                catch (Exception ex)
                {
                    return $"Error parsing nodes JSON: {ex.Message}";
                }
            }
        }

        if (nodes == null || nodes.Count == 0)
        {
            return "No valid code contexts retrieved.";
        }

        const string workspaceRoot = "/Users/slava/Projects/Personal/CodeExplorer";
        var output = new List<string>();

        foreach (var node in nodes)
        {
            if (string.IsNullOrEmpty(node.file_path) || node.start_line == null || node.end_line == null)
            {
                continue;
            }

            var joinedPath = Path.Combine(workspaceRoot, node.file_path);
            var absPath = Path.GetFullPath(joinedPath);
            var absRoot = Path.GetFullPath(workspaceRoot);

            if (!absPath.StartsWith(absRoot))
            {
                output.Add($"### Access Denied: `{node.file_path}` is outside the workspace root.");
                continue;
            }

            if (!File.Exists(absPath))
            {
                output.Add($"### File Not Found: `{node.file_path}`");
                continue;
            }

            try
            {
                var lines = File.ReadAllLines(absPath);
                int sIdx = Math.Max(0, node.start_line.Value);
                if (sIdx > lines.Length) sIdx = lines.Length;

                int eIdx = Math.Min(lines.Length, node.end_line.Value + 1);
                if (eIdx < sIdx) eIdx = sIdx;

                var snippet = string.Join("\n", lines.Skip(sIdx).Take(eIdx - sIdx));

                var ext = Path.GetExtension(node.file_path).ToLower();
                var lang = ext.TrimStart('.');
                lang = lang switch
                {
                    "ts" or "tsx" => "typescript",
                    "js" or "jsx" => "javascript",
                    "cs" => "csharp",
                    _ => lang
                };

                output.Add($"### File: `{node.file_path}` (Lines {sIdx + 1}-{eIdx})\n```{lang}\n{snippet}\n```");
            }
            catch (Exception ex)
            {
                output.Add($"### Error reading `{node.file_path}`: {ex.Message}");
            }
        }

        return output.Count == 0 ? "No valid code contexts retrieved." : string.Join("\n\n", output);
    }

    [Obsolete("Use McpGraphRepository.BuildTaxonomy instead")]
    public static object BuildTaxonomy(List<Dictionary<string, string>> triplets, List<Dictionary<string, string>> properties)
        => McpGraphRepository.BuildTaxonomy(triplets, properties);
}
