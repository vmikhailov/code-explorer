using System.Text.Json;
using CodeExplorer.Core.Common;

namespace CodeExplorer.Core.Mcp;

public partial class CodeExplorerRepository
{
    private static string SanitizeMermaidId(string text)
    {
        return text.Replace(".", "_").Replace("-", "_").Replace(":", "_").Replace("/", "_").Replace(" ", "_");
    }

    private static string FormatDependenciesAll(string rawJson, string format, int limit, string type = "all")
    {
        using var doc = JsonDocument.Parse(rawJson);

        if (format.Equals("json", StringComparison.OrdinalIgnoreCase))
        {
            return JsonSerializer.Serialize(new { results = doc.RootElement }, CompactJsonOptions);
        }

        if (format.Equals("yaml", StringComparison.OrdinalIgnoreCase))
        {
            return ToonYamlSerializer.SerializeYaml(doc.RootElement);
        }

        if (format.Equals("toon", StringComparison.OrdinalIgnoreCase))
        {
            return ToonYamlSerializer.SerializeToon(doc.RootElement);
        }

        var isRuntimeOnly = type.Equals("runtime", StringComparison.OrdinalIgnoreCase);

        if (format.Equals("mermaid", StringComparison.OrdinalIgnoreCase))
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("```mermaid");
            sb.AppendLine("graph TD");
            var count = 0;

            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in doc.RootElement.EnumerateArray())
                {
                    var proj = item.TryGetProperty("project", out var p) ? p.GetString() : null;
                    var dep = item.TryGetProperty("dependency", out var d) ? d.GetString() : null;

                    if (!string.IsNullOrEmpty(proj) && !string.IsNullOrEmpty(dep))
                    {
                        if (isRuntimeOnly && (proj.Contains("Test", StringComparison.OrdinalIgnoreCase) ||
                                              dep.Contains("Test", StringComparison.OrdinalIgnoreCase)))
                            continue;

                        if (count++ >= limit) break;

                        sb.AppendLine(
                            $"    {SanitizeMermaidId(proj)}[\"{proj}\"] --> {SanitizeMermaidId(dep)}[\"{dep}\"]");
                    }
                }
            }

            if (count == 0)
            {
                sb.AppendLine("    %% No project dependencies found");
            }

            sb.Append("```");
            return sb.ToString();
        }

        // Markdown format (default)
        var md = new System.Text.StringBuilder();
        md.AppendLine($"### Project Dependencies (Filter: {type})\n");
        var grouped = new Dictionary<string, List<string>>();
        var total = 0;

        if (doc.RootElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                var proj = item.TryGetProperty("project", out var p) ? p.GetString() : null;
                var dep = item.TryGetProperty("dependency", out var d) ? d.GetString() : null;

                if (!string.IsNullOrEmpty(proj) && !string.IsNullOrEmpty(dep))
                {
                    if (isRuntimeOnly && (proj.Contains("Test", StringComparison.OrdinalIgnoreCase) ||
                                          dep.Contains("Test", StringComparison.OrdinalIgnoreCase)))
                        continue;

                    if (total++ >= limit) break;

                    if (!grouped.TryGetValue(proj, out var list))
                    {
                        list = [];
                        grouped[proj] = list;
                    }

                    list.Add(dep);
                }
            }
        }

        if (grouped.Count == 0)
        {
            md.AppendLine("No matching project dependencies found in workspace.");
        }
        else
        {
            foreach (var (proj, deps) in grouped)
            {
                md.AppendLine($"- **{proj}** -> {string.Join(", ", deps)}");
            }
        }

        return md.ToString().TrimEnd();
    }

    private static string FormatDependenciesFiltered(string rawJson, string format, string projectFilter, string type = "all")
    {
        using var doc = JsonDocument.Parse(rawJson);

        if (format.Equals("json", StringComparison.OrdinalIgnoreCase))
        {
            return JsonSerializer.Serialize(new { results = doc.RootElement }, CompactJsonOptions);
        }

        if (format.Equals("yaml", StringComparison.OrdinalIgnoreCase))
        {
            return ToonYamlSerializer.SerializeYaml(doc.RootElement);
        }

        if (format.Equals("toon", StringComparison.OrdinalIgnoreCase))
        {
            return ToonYamlSerializer.SerializeToon(doc.RootElement);
        }

        var outgoing = new List<string>();
        var incoming = new List<string>();

        if (doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() > 0)
        {
            var row = doc.RootElement[0];

            if (row.TryGetProperty("outgoingDependencies", out var outProp) &&
                outProp.ValueKind == JsonValueKind.Array)
            {
                outgoing =
                [
                    .. outProp.EnumerateArray().Select(x => x.GetString() ?? "").Where(x => !string.IsNullOrEmpty(x))
                ];
            }

            if (row.TryGetProperty("incomingDependencies", out var inProp) &&
                inProp.ValueKind == JsonValueKind.Array)
            {
                incoming =
                [
                    .. inProp.EnumerateArray().Select(x => x.GetString() ?? "").Where(x => !string.IsNullOrEmpty(x))
                ];
            }
        }

        if (type.Equals("runtime", StringComparison.OrdinalIgnoreCase))
        {
            outgoing =
            [
                .. outgoing.Where(x =>
                    !x.Contains("Test", StringComparison.OrdinalIgnoreCase) &&
                    !x.Contains("Mock", StringComparison.OrdinalIgnoreCase))
            ];

            incoming =
            [
                .. incoming.Where(x =>
                    !x.Contains("Test", StringComparison.OrdinalIgnoreCase) &&
                    !x.Contains("Mock", StringComparison.OrdinalIgnoreCase))
            ];
        }

        if (format.Equals("mermaid", StringComparison.OrdinalIgnoreCase))
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("```mermaid");
            sb.AppendLine("graph TD");
            var pId = SanitizeMermaidId(projectFilter);

            foreach (var o in outgoing)
            {
                sb.AppendLine($"    {pId}[\"{projectFilter}\"] --> {SanitizeMermaidId(o)}[\"{o}\"]");
            }

            foreach (var i in incoming)
            {
                sb.AppendLine($"    {SanitizeMermaidId(i)}[\"{i}\"] --> {pId}[\"{projectFilter}\"]");
            }

            if (outgoing.Count == 0 && incoming.Count == 0)
            {
                sb.AppendLine($"    {pId}[\"{projectFilter}\"]");
                sb.AppendLine("    %% No connected dependencies");
            }

            sb.Append("```");
            return sb.ToString();
        }

        // Markdown format
        var md = new System.Text.StringBuilder();
        md.AppendLine($"### Dependencies for Project: `{projectFilter}` (Filter: {type})\n");

        md.AppendLine(
            $"- **Outgoing Dependencies** ({outgoing.Count}): {(outgoing.Count > 0 ? string.Join(", ", outgoing) : "none")}");

        md.AppendLine(
            $"- **Incoming Dependencies** ({incoming.Count}): {(incoming.Count > 0 ? string.Join(", ", incoming) : "none")}");
        return md.ToString().TrimEnd();
    }


    public async Task<string> GetProjectDependenciesAsync(string? projectFilter = null, string format = "markdown", int limit = 50, string type = "all", string? workspacePath = null, CancellationToken cancellationToken = default)
    {
        var client = await ResolveClientAsync(workspacePath);
        if (await IsEmptyStandbyAsync(client))
        {
            return GetStandbyMessage(format);
        }

        if (!string.IsNullOrEmpty(projectFilter))
        {
            var query = Queries.Get("get_project_dependencies_filtered");
            var rawJson = await client.ExecuteQueryAsync(query, new Dictionary<string, object> { ["projectFilter"] = projectFilter }, cancellationToken);
            return FormatDependenciesFiltered(rawJson, format, projectFilter, type);
        }
        else
        {
            var query = Queries.Get("get_project_dependencies_all");
            var rawJson = await client.ExecuteQueryAsync(query, new Dictionary<string, object>(), cancellationToken);
            return FormatDependenciesAll(rawJson, format, limit, type);
        }
    }

    private static string FormatFileOutline(string rawJson, string format, string filePath)
    {
        using var doc = JsonDocument.Parse(rawJson);

        if (format.Equals("json", StringComparison.OrdinalIgnoreCase))
        {
            return JsonSerializer.Serialize(new { results = doc.RootElement }, CompactJsonOptions);
        }

        if (format.Equals("yaml", StringComparison.OrdinalIgnoreCase))
        {
            return ToonYamlSerializer.SerializeYaml(doc.RootElement);
        }

        if (format.Equals("toon", StringComparison.OrdinalIgnoreCase))
        {
            return ToonYamlSerializer.SerializeToon(doc.RootElement);
        }

        if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() == 0)
        {
            return $"No symbols found in outline for '{filePath}'.";
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"### File Outline: `{filePath}`\n");

        foreach (var item in doc.RootElement.EnumerateArray())
        {
            var type = item.TryGetProperty("type", out var t) && t.ValueKind == JsonValueKind.String
                ? t.GetString() ?? "Symbol"
                : "Symbol";

            var name = item.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String
                ? n.GetString() ?? ""
                : "";

            var startLine = item.TryGetProperty("startLine", out var sl) && sl.ValueKind == JsonValueKind.Number &&
                            sl.TryGetInt64(out var slVal)
                ? slVal
                : 0;

            var endLine = item.TryGetProperty("endLine", out var el) && el.ValueKind == JsonValueKind.Number &&
                          el.TryGetInt64(out var elVal)
                ? elVal
                : 0;

            var linesStr = startLine > 0
                ? (endLine > startLine ? $" (L{startLine}-{endLine})" : $" (L{startLine})")
                : "";
            sb.AppendLine($"- **{type}** `{name}`{linesStr}");
        }

        return sb.ToString().TrimEnd();
    }

    public async Task<string> GetFileOutlineAsync(string filePath, string format = "markdown", string? workspacePath = null, CancellationToken cancellationToken = default)
    {
        var client = await ResolveClientAsync(workspacePath);
        if (await IsEmptyStandbyAsync(client))
        {
            return GetStandbyMessage(format);
        }

        var normalizedPath = filePath.Trim().Replace('\\', '/').TrimStart('/');
        var query = Queries.Get("get_file_outline");
        var parameters = new Dictionary<string, object>
        {
            ["filePath"] = normalizedPath
        };
        var rawJson = await client.ExecuteQueryAsync(query, parameters, cancellationToken);
        return FormatFileOutline(rawJson, format, normalizedPath);
    }


    public async Task<string> InspectDataLineageAsync(string tableName, string? workspacePath = null, CancellationToken cancellationToken = default)
    {
        var query = Queries.Get("inspect_data_lineage");
        var parameters = new Dictionary<string, object>
        {
            ["tableName"] = tableName
        };
        return await ExecuteAndFormatQueryAsync(query, parameters, workspacePath, cancellationToken);
    }

    public async Task<string> GetProjectEntryPointsAsync(string projectName, string? workspacePath = null, CancellationToken cancellationToken = default)
    {
        var query = Queries.Get("get_project_entry_points");
        var parameters = new Dictionary<string, object>
        {
            ["projectName"] = projectName
        };
        return await ExecuteAndFormatQueryAsync(query, parameters, workspacePath, cancellationToken);
    }

    private async Task AppendMetricResultsAsync(List<object> results, string queryKey, Dictionary<string, object> parameters, string? workspacePath = null, CancellationToken cancellationToken = default)
    {
        var client = await ResolveClientAsync(workspacePath);
        var query = Queries.Get(queryKey);
        var res = await client.ExecuteQueryAsync(query, parameters, cancellationToken);

        using var doc = JsonDocument.Parse(res);

        foreach (var item in doc.RootElement.EnumerateArray())
        {
            results.Add(item.Clone());
        }
    }

    public async Task<string> FindRefactoringOpportunitiesAsync(string projectName, string metricType = "all", string? workspacePath = null, CancellationToken cancellationToken = default)
    {
        var results = new List<object>();
        var parameters = new Dictionary<string, object> { ["projectName"] = projectName };

        if (metricType is "dead_code" or "all")
            await AppendMetricResultsAsync(results, "find_refactor_dead_code", parameters, workspacePath, cancellationToken);

        if (metricType is "god_objects" or "all")
            await AppendMetricResultsAsync(results, "find_refactor_god_objects", parameters, workspacePath, cancellationToken);

        return JsonSerializer.Serialize(new { results }, CompactJsonOptions);
    }


    public async Task<string> GetTaxonomyAsync(string? workspacePath = null, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"taxonomy:{workspacePath ?? ""}";
        if (_macroCache.TryGetValue(cacheKey, out var cachedTaxonomy))
        {
            return cachedTaxonomy;
        }

        var client = await ResolveClientAsync(workspacePath);
        if (await IsEmptyStandbyAsync(client))
        {
            return StandbyMessageJson;
        }

        var parameters = new Dictionary<string, object?>();
        var query = Queries.Get("get_taxonomy_nodes");
        var propQuery = Queries.Get("get_taxonomy_properties");

        var resultJson = await client.ExecuteQueryAsync(query, parameters, cancellationToken);
        var parsedTriplets = JsonSerializer.Deserialize<List<Dictionary<string, string>>>(resultJson) ?? [];

        var propJson = await client.ExecuteQueryAsync(propQuery, parameters, cancellationToken);
        var parsedProperties = JsonSerializer.Deserialize<List<Dictionary<string, string>>>(propJson) ?? [];

        var taxonomy = BuildTaxonomy(parsedTriplets, parsedProperties);
        var finalJson = JsonSerializer.Serialize(new { taxonomy }, CompactJsonOptions);
        _macroCache[cacheKey] = finalJson;
        return finalJson;
    }


    public async Task<string> FetchCodeSnippetsAsync(string nodesJson, string? workspacePath = null, CancellationToken cancellationToken = default)
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
                return nestedNodes?.Select(n => n.props).OfType<McpRAGNode>().ToList();
            }
        }
        return null;
    }

    private static string? ResolveWorkspaceRoot(string? hostWorkspacePath)
    {
        var workspaceRoot = Environment.GetEnvironmentVariable("WORKSPACE_ROOT");
        if (!string.IsNullOrEmpty(workspaceRoot)) return workspaceRoot;

        if (!string.IsNullOrEmpty(hostWorkspacePath)) return hostWorkspacePath;

        return WorkspaceLocator.Find()?.RootDirectory;
    }

    private static string FormatCodeSnippet(string[] lines, McpRAGNode node)
    {
        var startLine = node.start_line ?? 0;
        var endLine = node.end_line ?? (lines.Length > 0 ? lines.Length - 1 : 0);
        var sIdx = Math.Min(Math.Max(0, startLine), lines.Length);
        var eIdx = Math.Max(sIdx, Math.Min(lines.Length, endLine + 1));
        var snippet = string.Join("\n", lines.Skip(sIdx).Take(eIdx - sIdx));

        var filePath = node.file_path ?? string.Empty;
        var ext = Path.GetExtension(filePath).ToLower().TrimStart('.');
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
        if (string.IsNullOrEmpty(node.file_path)) return "### Invalid node: missing file_path.";
        var relativePath = PathTools.GetRelativePath(node.file_path, hostWorkspacePath);
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

}
