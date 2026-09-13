using System.Text.Json;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Mcp.Models;

namespace CodeExplorer.Core.Mcp;

public class CodeExplorerRepository(
    IGraphClient dbClient,
    ProjectQueryManager? queryManager = null,
    string? defaultWorkspacePath = null)
{
    private readonly ProjectQueryManager _queryManager = queryManager ?? new();
    public string? DefaultWorkspacePath { get; } = defaultWorkspacePath;

    private async Task<string> ExecuteAndFormatQueryAsync(string query, object? parameters = null)
    {
        var resultJson = await dbClient.ExecuteQueryAsync(query, parameters);
        using var doc = JsonDocument.Parse(resultJson);

        return JsonSerializer.Serialize(new { results = doc.RootElement },
            new JsonSerializerOptions { WriteIndented = true });
    }



    public async Task ClearAllAsync()
    {
        await dbClient.ClearDatabaseAsync();
    }

    public async Task<bool> ClearWorkspaceAsync(string workspaceIdOrPath)
    {
        return await dbClient.ClearWorkspaceAsync(workspaceIdOrPath);
    }

    public async Task<(List<string> Cleared, List<string> NotFound)> ClearWorkspacesAsync(IEnumerable<string> workspaces)
    {
        var cleared = new List<string>();
        var notFound = new List<string>();
        foreach (var ws in workspaces)
        {
            if (await dbClient.ClearWorkspaceAsync(ws))
            {
                cleared.Add(ws);
            }
            else
            {
                notFound.Add(ws);
            }
        }
        return (cleared, notFound);
    }

    public async Task<string> GetArchitectureMapAsync(string? projectName = null, string? workspacePath = null)
    {
        string resultJson;
        if (!string.IsNullOrEmpty(projectName))
        {
            var query = Queries.Get("get_architecture_map_project");
            resultJson = await ExecuteAndFormatQueryAsync(query, new Dictionary<string, object> { ["projectName"] = projectName });
        }
        else
        {
            resultJson = await ExecuteAndFormatQueryAsync(Queries.Get("get_architecture_map_workspace"), new Dictionary<string, object>());
        }

        try
        {
            var effectiveWsPath = await ResolveWorkspacePathAsync(workspacePath);
            var savedQueries = _queryManager.ListQueries(effectiveWsPath);
            if (savedQueries.Count > 0)
            {
                using var doc = JsonDocument.Parse(resultJson);
                return JsonSerializer.Serialize(new
                {
                    results = doc.RootElement.GetProperty("results"),
                    saved_project_queries = savedQueries.Select(q => new
                    {
                        name = q.Name,
                        description = q.Description,
                        parameters = q.Parameters,
                        returns = q.Returns,
                        tags = q.Tags
                    })
                }, new JsonSerializerOptions { WriteIndented = true });
            }
        }
        catch
        {
            // Do not fail architecture map if project query listing fails
        }

        return resultJson;
    }

    public async Task<string> GetProjectDependenciesAsync(string? projectFilter = null, string? workspacePath = null)
    {
        if (!string.IsNullOrEmpty(projectFilter))
        {
            var query = Queries.Get("get_project_dependencies_filtered");
            return await ExecuteAndFormatQueryAsync(query, new Dictionary<string, object> { ["projectFilter"] = projectFilter });
        }
        else
        {
            var query = Queries.Get("get_project_dependencies_all");
            return await ExecuteAndFormatQueryAsync(query, new Dictionary<string, object>());
        }
    }

    public async Task<string> GetFileOutlineAsync(string filePath, string? workspacePath = null)
    {
        var query = Queries.Get("get_file_outline");
        var parameters = new Dictionary<string, object>
        {
            ["filePath"] = filePath
        };
        return await ExecuteAndFormatQueryAsync(query, parameters);
    }

    public async Task<string> FindSymbolAsync(string name, string? symbolType = null, string? workspacePath = null)
    {
        var parameters = new Dictionary<string, object>
        {
            ["name"] = name
        };

        string query;
        if (symbolType == "Function")
        {
            query = Queries.Get("find_symbol_function");
        }
        else if (symbolType == "Class")
        {
            query = Queries.Get("find_symbol_class");
        }
        else if (symbolType == "Interface")
        {
            query = Queries.Get("find_symbol_interface");
        }
        else
        {
            query = Queries.Get("find_symbol_all");
        }

        return await ExecuteAndFormatQueryAsync(query, parameters);
    }

    public async Task<string> GetCallChainAsync(string startFunction, string endFunction, int maxDepth = 5, string? workspacePath = null)
    {
        var depth = Math.Max(1, Math.Min(10, maxDepth));
        var query = Queries.Get("get_call_chain").Replace("{depth}", depth.ToString());
        var parameters = new Dictionary<string, object>
        {
            ["startFunction"] = startFunction,
            ["endFunction"] = endFunction
        };
        return await ExecuteAndFormatQueryAsync(query, parameters);
    }

    public async Task<string> ResolveCallTargetAsync(string interfaceName, string methodName, string? workspacePath = null)
    {
        var query = Queries.Get("resolve_call_target");
        var parameters = new Dictionary<string, object>
        {
            ["interfaceName"] = interfaceName,
            ["methodName"] = methodName
        };
        return await ExecuteAndFormatQueryAsync(query, parameters);
    }

    public async Task<string> AnalyzeCodeImpactAsync(string symbolName, string? workspacePath = null)
    {
        var query = Queries.Get("analyze_code_impact");
        var parameters = new Dictionary<string, object>
        {
            ["symbolName"] = symbolName
        };
        return await ExecuteAndFormatQueryAsync(query, parameters);
    }

    public async Task<string> InspectDataLineageAsync(string tableName, string? workspacePath = null)
    {
        var query = Queries.Get("inspect_data_lineage");
        var parameters = new Dictionary<string, object>
        {
            ["tableName"] = tableName
        };
        return await ExecuteAndFormatQueryAsync(query, parameters);
    }

    public async Task<string> GetProjectEntryPointsAsync(string projectName, string? workspacePath = null)
    {
        var query = Queries.Get("get_project_entry_points");
        var parameters = new Dictionary<string, object>
        {
            ["projectName"] = projectName
        };
        return await ExecuteAndFormatQueryAsync(query, parameters);
    }

    private async Task AppendMetricResultsAsync(List<object> results, string queryKey, Dictionary<string, object> parameters)
    {
        var query = Queries.Get(queryKey);
        var res = await dbClient.ExecuteQueryAsync(query, parameters);
        using var doc = JsonDocument.Parse(res);
        foreach (var item in doc.RootElement.EnumerateArray())
        {
            results.Add(item.Clone());
        }
    }

    public async Task<string> FindRefactoringOpportunitiesAsync(string projectName, string metricType = "all", string? workspacePath = null)
    {
        var results = new List<object>();
        var parameters = new Dictionary<string, object> { ["projectName"] = projectName };

        if (metricType is "dead_code" or "all")
            await AppendMetricResultsAsync(results, "find_refactor_dead_code", parameters);

        if (metricType is "god_objects" or "all")
            await AppendMetricResultsAsync(results, "find_refactor_god_objects", parameters);

        return JsonSerializer.Serialize(new { results }, new JsonSerializerOptions { WriteIndented = true });
    }

    public async Task<string> ExecuteCustomReadCypherAsync(string query, string? workspacePath = null)
    {
        var lowerQuery = query.ToLowerInvariant();

        if (lowerQuery.Contains("create") || lowerQuery.Contains("delete") || lowerQuery.Contains("set") ||
            lowerQuery.Contains("merge") || lowerQuery.Contains("remove") || lowerQuery.Contains("drop") ||
            lowerQuery.Contains("detach"))
        {
            throw new InvalidOperationException("Security violation: Mutating queries are not allowed.");
        }

        return await ExecuteAndFormatQueryAsync(query, new Dictionary<string, object?>());
    }

    public async Task<string> GetWorkspaceContentAsync(string? workspacePath = null, string? type = null)
    {
        var parameters = new Dictionary<string, object?>
        {
            ["type"] = string.IsNullOrEmpty(type) ? null : type
        };
        var query = Queries.Get("get_workspace_content");
        return await dbClient.ExecuteQueryAsync(query, parameters);
    }

    public async Task<string> ExecuteRawQueryAsync(string query, Dictionary<string, object?>? parameters = null)
    {
        return await dbClient.ExecuteQueryAsync(query, parameters);
    }

    public async Task<string> GetTaxonomyAsync(string? workspacePath = null)
    {
        var parameters = new Dictionary<string, object?>();
        var query = Queries.Get("get_taxonomy_nodes");
        var propQuery = Queries.Get("get_taxonomy_properties");

        var resultJson = await dbClient.ExecuteQueryAsync(query, parameters);
        var parsedTriplets = JsonSerializer.Deserialize<List<Dictionary<string, string>>>(resultJson) ?? [];

        var propJson = await dbClient.ExecuteQueryAsync(propQuery, parameters);
        var parsedProperties = JsonSerializer.Deserialize<List<Dictionary<string, string>>>(propJson) ?? [];

        var taxonomy = BuildTaxonomy(parsedTriplets, parsedProperties);
        return JsonSerializer.Serialize(new { taxonomy }, new JsonSerializerOptions { WriteIndented = true });
    }

    public async Task<string> FetchCodeSnippetsAsync(string nodesJson, string? workspacePath = null)
    {
        return await FetchCodeSnippetsDirectlyAsync(nodesJson, workspacePath);
    }

    public string GetNodeDefinition(string kind)
    {
        return OntologyRegistry.GetNodeDefinition(kind);
    }
    private static List<McpRAGNode>? ParseRagNodes(string nodesJson)
    {
        try
        {
            return JsonSerializer.Deserialize<List<McpRAGNode>>(nodesJson);
        }
        catch
        {
            try
            {
                var single = JsonSerializer.Deserialize<McpRAGNode>(nodesJson);
                if (single != null) return [single];
            }
            catch
            {
                var nestedNodes = JsonSerializer.Deserialize<List<NestedMcpRAGNode>>(nodesJson);
                return nestedNodes?.Where(n => n.props != null).Select(n => n.props!).ToList();
            }
        }
        return null;
    }

    private static string? ResolveWorkspaceRoot(string? hostWorkspacePath)
    {
        var workspaceRoot = Environment.GetEnvironmentVariable("WORKSPACE_ROOT");
        if (!string.IsNullOrEmpty(workspaceRoot)) return workspaceRoot;

        if (!string.IsNullOrEmpty(hostWorkspacePath)) return hostWorkspacePath;

        var current = Directory.GetCurrentDirectory();
        while (!string.IsNullOrEmpty(current))
        {
            if (File.Exists(Path.Combine(current, "CodeExplorer.slnx")) || File.Exists(Path.Combine(current, "CodeExplorer.sln")))
            {
                return current;
            }
            current = Path.GetDirectoryName(current);
        }
        return null;
    }

    private static string FormatCodeSnippet(string[] lines, McpRAGNode node)
    {
        var sIdx = Math.Min(Math.Max(0, node.start_line!.Value), lines.Length);
        var eIdx = Math.Max(sIdx, Math.Min(lines.Length, node.end_line!.Value + 1));
        var snippet = string.Join("\n", lines.Skip(sIdx).Take(eIdx - sIdx));

        var ext = Path.GetExtension(node.file_path!).ToLower().TrimStart('.');
        var lang = ext switch
        {
            "ts" or "tsx" => "typescript",
            "js" or "jsx" => "javascript",
            "cs" => "csharp",
            _ => ext
        };

        return $"### File: `{node.file_path}` (Lines {sIdx + 1}-{eIdx})\n```{lang}\n{snippet}\n```";
    }

    private static async Task<string> ExtractFileSnippetAsync(McpRAGNode node, string? workspaceRoot, string? hostWorkspacePath)
    {
        var relativePath = PathTools.GetRelativePath(node.file_path!, hostWorkspacePath);
        var joinedPath = Path.Combine(workspaceRoot ?? "", relativePath);
        var absPath = Path.GetFullPath(joinedPath);
        var absRoot = Path.GetFullPath(workspaceRoot ?? "");

        if (!absPath.StartsWith(absRoot))
            return $"### Access Denied: `{node.file_path}` is outside the workspace root.";

        if (!File.Exists(absPath))
            return $"### File Not Found: `{node.file_path}`";

        try
        {
            var lines = await File.ReadAllLinesAsync(absPath);
            return FormatCodeSnippet(lines, node);
        }
        catch (Exception ex)
        {
            return $"### Error reading `{node.file_path}`: {ex.Message}";
        }
    }

    private async Task<string> FetchCodeSnippetsDirectlyAsync(string nodesJson, string? hostWorkspacePath)
    {
        List<McpRAGNode>? nodes;
        try
        {
            nodes = ParseRagNodes(nodesJson);
        }
        catch (Exception ex)
        {
            return $"Error parsing nodes JSON: {ex.Message}";
        }

        if (nodes == null || nodes.Count == 0) return "No valid code contexts retrieved.";

        var workspaceRoot = ResolveWorkspaceRoot(hostWorkspacePath);
        var output = new List<string>();

        foreach (var node in nodes)
        {
            if (string.IsNullOrEmpty(node.file_path) || node.start_line == null || node.end_line == null)
                continue;

            output.Add(await ExtractFileSnippetAsync(node, workspaceRoot, hostWorkspacePath));
        }

        return output.Count == 0 ? "No valid code contexts retrieved." : string.Join("\n\n", output);
    }

    private static void PopulateTaxonomyProperties(
        Dictionary<string, (List<string> properties, HashSet<(string relationship, string target)> outgoing, HashSet<(string relationship, string source)> incoming)> nodes,
        List<Dictionary<string, string>> properties)
    {
        foreach (var prop in properties)
        {
            if (prop.TryGetValue("label", out var label) && prop.TryGetValue("key", out var key))
            {
                if (!nodes.ContainsKey(label)) nodes[label] = ([], [], []);
                nodes[label].properties.Add(key);
            }
        }
    }

    private static void PopulateTaxonomyTriplets(
        Dictionary<string, (List<string> properties, HashSet<(string relationship, string target)> outgoing, HashSet<(string relationship, string source)> incoming)> nodes,
        List<Dictionary<string, string>> triplets)
    {
        foreach (var triplet in triplets)
        {
            if (triplet.TryGetValue("fromLabel", out var from) && triplet.TryGetValue("relType", out var rel) &&
                triplet.TryGetValue("toLabel", out var to))
            {
                if (!nodes.ContainsKey(from)) nodes[from] = ([], [], []);
                if (!nodes.ContainsKey(to)) nodes[to] = ([], [], []);

                nodes[from].outgoing.Add((rel, to));
                nodes[to].incoming.Add((rel, from));
            }
        }
    }

    public static object BuildTaxonomy(
        List<Dictionary<string, string>> triplets,
        List<Dictionary<string, string>> properties)
    {
        var nodes = new Dictionary<string, (List<string> properties, HashSet<(string relationship, string target)> outgoing,
            HashSet<(string relationship, string source)> incoming)>(StringComparer.OrdinalIgnoreCase);

        PopulateTaxonomyProperties(nodes, properties);
        PopulateTaxonomyTriplets(nodes, triplets);

        return nodes.OrderBy(k => k.Key).Select(kvp => new
        {
            label = kvp.Key,
            properties = kvp.Value.properties.OrderBy(p => p).ToList(),
            outgoing = kvp.Value.outgoing.OrderBy(x => x.relationship).Select(x => new { x.relationship, x.target }).ToList(),
            incoming = kvp.Value.incoming.OrderBy(x => x.relationship).Select(x => new { x.relationship, x.source }).ToList()
        }).ToList();
    }

    public async Task<string> ResolveWorkspacePathAsync(string? workspacePath)
    {
        if (!string.IsNullOrWhiteSpace(workspacePath))
        {
            return Path.GetFullPath(workspacePath);
        }

        if (!string.IsNullOrWhiteSpace(DefaultWorkspacePath))
        {
            return Path.GetFullPath(DefaultWorkspacePath);
        }

        var found = WorkspaceLocator.Find();
        if (found != null)
        {
            return found.RootDirectory;
        }

        try
        {
            var allResult = await dbClient.ExecuteQueryAsync("MATCH (w:Workspace) RETURN w.path AS path LIMIT 1;");
            using var allDoc = JsonDocument.Parse(allResult);
            if (allDoc.RootElement.ValueKind == JsonValueKind.Array && allDoc.RootElement.GetArrayLength() > 0)
            {
                var row = allDoc.RootElement[0];
                if (row.TryGetProperty("path", out var pathProp) && pathProp.ValueKind == JsonValueKind.String)
                {
                    var p = pathProp.GetString();
                    if (!string.IsNullOrEmpty(p)) return Path.GetFullPath(p);
                }
            }
        }
        catch
        {
            // Fallback
        }

        return Directory.GetCurrentDirectory();
    }

    public async Task<string> ListProjectQueriesAsync(string? workspacePath)
    {
        var effectiveWsPath = await ResolveWorkspacePathAsync(workspacePath);
        var queries = _queryManager.ListQueries(effectiveWsPath);
        return JsonSerializer.Serialize(new
        {
            workspace_path = effectiveWsPath,
            queries_count = queries.Count,
            queries = queries.Select(q => new
            {
                name = q.Name,
                description = q.Description,
                parameters = q.Parameters,
                returns = q.Returns,
                tags = q.Tags
            })
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    public async Task<string> SaveProjectQueryAsync(
        string name,
        string description,
        string cypher,
        string? parametersJson,
        string? returns,
        string? tags,
        string? workspacePath)
    {
        var effectiveWsPath = await ResolveWorkspacePathAsync(workspacePath);

        var paramList = new List<ProjectQueryParameter>();
        if (!string.IsNullOrWhiteSpace(parametersJson))
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<List<ProjectQueryParameter>>(parametersJson);
                if (parsed != null) paramList = parsed;
            }
            catch (Exception ex)
            {
                throw new ArgumentException($"Failed to parse parametersJson: {ex.Message}", nameof(parametersJson), ex);
            }
        }

        var tagList = string.IsNullOrWhiteSpace(tags)
            ? []
            : tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

        var metadata = new ProjectQueryMetadata
        {
            Name = name,
            Description = description,
            Parameters = paramList,
            Returns = returns,
            Tags = tagList
        };

        var saved = _queryManager.SaveQuery(effectiveWsPath, name, cypher, metadata);
        return JsonSerializer.Serialize(new
        {
            success = true,
            message = $"Project query '{saved.Name}' successfully validated and saved to .codeexplorer/queries/",
            query = new
            {
                name = saved.Name,
                description = saved.Description,
                parameters = saved.Parameters,
                returns = saved.Returns,
                tags = saved.Tags,
                cypher_file = saved.CypherPath,
                metadata_file = saved.MetadataPath
            }
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    public async Task<string> ExecuteProjectQueryAsync(string name, string? parametersJson, string? workspacePath)
    {
        var effectiveWsPath = await ResolveWorkspacePathAsync(workspacePath);
        var queryItem = _queryManager.GetQuery(effectiveWsPath, name);
        if (queryItem == null)
        {
            throw new FileNotFoundException(
                $"Project query '{name}' not found in '{_queryManager.GetQueriesDirectory(effectiveWsPath)}'. " +
                "Use 'list_project_queries' to see available queries or 'save_project_query' to register a new one.");
        }

        var paramDict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(parametersJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(parametersJson);
                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in doc.RootElement.EnumerateObject())
                    {
                        paramDict[prop.Name] = prop.Value.ValueKind switch
                        {
                            JsonValueKind.String => prop.Value.GetString(),
                            JsonValueKind.Number => prop.Value.TryGetInt64(out var l) ? l : prop.Value.GetDouble(),
                            JsonValueKind.True => true,
                            JsonValueKind.False => false,
                            JsonValueKind.Null => null,
                            _ => prop.Value.GetRawText()
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                throw new ArgumentException($"Failed to parse parametersJson: {ex.Message}", nameof(parametersJson), ex);
            }
        }

        foreach (var p in queryItem.Parameters)
        {
            if (p.Required && (!paramDict.ContainsKey(p.Name) || paramDict[p.Name] == null))
            {
                throw new ArgumentException($"Required parameter '{p.Name}' is missing for query '{name}'.");
            }
        }

        if (queryItem.Cypher.Contains("$workspaceId") && !paramDict.ContainsKey("workspaceId"))
        {
            paramDict["workspaceId"] = "workspace";
        }

        if (queryItem.Cypher.Contains("$workspaceIdPrefix") && !paramDict.ContainsKey("workspaceIdPrefix"))
        {
            paramDict["workspaceIdPrefix"] = "workspace:";
        }

        return await ExecuteAndFormatQueryAsync(queryItem.Cypher, paramDict);
    }
}
