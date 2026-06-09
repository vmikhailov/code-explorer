using System.Reflection;
using System.Text.Json;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Database;

namespace CodeExplorer.Core.Mcp;

public class CodeExplorerRepository(MemgraphClient dbClient)
{
    private async Task<string> ExecuteAndFormatQueryAsync(string query, object? parameters = null)
    {
        var resultJson = await dbClient.ExecuteQueryAsync(query, parameters);
        using var doc = JsonDocument.Parse(resultJson);

        return JsonSerializer.Serialize(new { results = doc.RootElement },
            new JsonSerializerOptions { WriteIndented = true });
    }

    private static string CleanPathForComparison(string? path)
    {
        if (string.IsNullOrEmpty(path)) return string.Empty;
        var clean = path.Replace('\\', '/');
        var driveMatch = System.Text.RegularExpressions.Regex.Match(clean, @"^[A-Za-z]:");
        if (driveMatch.Success)
        {
            clean = clean.Substring(driveMatch.Length);
        }
        if (clean.StartsWith("/host", StringComparison.OrdinalIgnoreCase))
        {
            clean = clean.Substring(5);
        }
        return clean.Trim('/').ToLowerInvariant();
    }

    private async Task<string?> GetWorkspaceIdAsync(string? workspacePath)
    {
        if (string.IsNullOrEmpty(workspacePath)) return null;
        var normalized = PathTools.NormalizeToHostPath(workspacePath);
        var normalizedAlt = normalized.Contains('/') ? normalized.Replace('/', '\\') : normalized.Replace('\\', '/');

        // 1. Try exact (case-insensitive) match via database query first
        var query = Queries.Get("get_workspace_id");
        var resultJson = await dbClient.ExecuteQueryAsync(query, new Dictionary<string, object?>
        {
            ["path"] = normalized,
            ["altPath"] = normalizedAlt
        });
        using var doc = JsonDocument.Parse(resultJson);
        if (doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() > 0)
        {
            var row = doc.RootElement[0];
            if (row.TryGetProperty("id", out var idProp))
            {
                if (idProp.ValueKind == JsonValueKind.String)
                {
                    var idVal = idProp.GetString();
                    if (!string.IsNullOrEmpty(idVal)) return idVal;
                }
                else if (idProp.ValueKind == JsonValueKind.Number)
                {
                    return idProp.GetInt64().ToString();
                }
            }
        }

        // 2. Fetch all workspaces to perform suffix/crossover path matching in C#
        var allQuery = Queries.Get("get_all_workspaces");
        var allResult = await dbClient.ExecuteQueryAsync(allQuery);
        using var allDoc = JsonDocument.Parse(allResult);
        if (allDoc.RootElement.ValueKind == JsonValueKind.Array)
        {
            var arrayLength = allDoc.RootElement.GetArrayLength();
            
            // Fallback 2a: If there's exactly one workspace in the database, use it
            if (arrayLength == 1)
            {
                var singleRow = allDoc.RootElement[0];
                if (singleRow.TryGetProperty("id", out var fallbackIdProp))
                {
                    if (fallbackIdProp.ValueKind == JsonValueKind.String)
                    {
                        var fallbackId = fallbackIdProp.GetString();
                        if (!string.IsNullOrEmpty(fallbackId)) return fallbackId;
                    }
                    else if (fallbackIdProp.ValueKind == JsonValueKind.Number)
                    {
                        return fallbackIdProp.GetInt64().ToString();
                    }
                }
            }

            // Fallback 2b: Try suffix cleaning match
            var inputCleaned = CleanPathForComparison(workspacePath);
            foreach (var row in allDoc.RootElement.EnumerateArray())
            {
                string? dbPath = null;
                if (row.TryGetProperty("path", out var pathProp) && pathProp.ValueKind == JsonValueKind.String)
                {
                    dbPath = pathProp.GetString();
                }

                if (dbPath != null && CleanPathForComparison(dbPath) == inputCleaned)
                {
                    if (row.TryGetProperty("id", out var idProp))
                    {
                        if (idProp.ValueKind == JsonValueKind.String)
                        {
                            var idVal = idProp.GetString();
                            if (!string.IsNullOrEmpty(idVal)) return idVal;
                        }
                        else if (idProp.ValueKind == JsonValueKind.Number)
                        {
                            return idProp.GetInt64().ToString();
                        }
                    }
                }
            }
        }

        throw new InvalidOperationException($"Workspace at path '{workspacePath}' is not indexed yet. Please run ingest/index first.");
    }

    public async Task<string> GetArchitectureMapAsync(string? projectName, string? workspacePath)
    {
        string query;
        var parameters = new Dictionary<string, object>();

        if (!string.IsNullOrEmpty(projectName))
        {
            var wsId = await GetWorkspaceIdAsync(workspacePath);
            parameters["projectName"] = projectName;
            
            var prefixFilter = "";
            if (wsId != null)
            {
                parameters["wsIdPrefix"] = wsId + ":";
                prefixFilter = "WHERE p.id STARTS WITH $wsIdPrefix ";
            }

            query = Queries.Get("get_architecture_map_project").Replace("{prefixFilter}", prefixFilter);
        }
        else
        {
            if (!string.IsNullOrEmpty(workspacePath))
            {
                var normalized = PathTools.NormalizeToHostPath(workspacePath);
                var normalizedAlt = normalized.Contains('/') ? normalized.Replace('/', '\\') : normalized.Replace('\\', '/');
                parameters["workspacePath"] = normalized;
                parameters["altWorkspacePath"] = normalizedAlt;

                query = Queries.Get("get_architecture_map_workspace");
            }
            else
            {
                query = Queries.Get("get_architecture_map_all");
            }
        }

        return await ExecuteAndFormatQueryAsync(query, parameters);
    }

    public async Task<string> GetProjectDependenciesAsync(string? projectFilter, string? workspacePath)
    {
        var wsId = await GetWorkspaceIdAsync(workspacePath);
        string query;
        var parameters = new Dictionary<string, object>();

        if (wsId != null)
        {
            parameters["wsIdPrefix"] = wsId + ":";
            if (!string.IsNullOrEmpty(projectFilter))
            {
                parameters["projectFilter"] = projectFilter;
                query = Queries.Get("get_project_dependencies_filtered");
            }
            else
            {
                query = Queries.Get("get_project_dependencies_all");
            }
        }
        else
        {
            if (!string.IsNullOrEmpty(projectFilter))
            {
                parameters["projectFilter"] = projectFilter;
                query = Queries.Get("get_project_dependencies_filtered_no_ws");
            }
            else
            {
                query = Queries.Get("get_project_dependencies_all_no_ws");
            }
        }

        return await ExecuteAndFormatQueryAsync(query, parameters);
    }

    public async Task<string> GetFileOutlineAsync(string filePath, string? workspacePath)
    {
        var wsId = await GetWorkspaceIdAsync(workspacePath);
        string query;
        var parameters = new Dictionary<string, object>
        {
            ["filePath"] = filePath
        };
        if (wsId != null)
        {
            parameters["wsIdPrefix"] = wsId + ":";
            query = Queries.Get("get_file_outline");
        }
        else
        {
            query = Queries.Get("get_file_outline_no_ws");
        }
        return await ExecuteAndFormatQueryAsync(query, parameters);
    }

    public async Task<string> FindSymbolAsync(string name, string? symbolType, string? workspacePath)
    {
        var wsId = await GetWorkspaceIdAsync(workspacePath);
        string query;
        var parameters = new Dictionary<string, object>
        {
            ["name"] = name
        };

        var prefixClause = wsId != null ? " AND n.id STARTS WITH $wsIdPrefix" : "";
        if (wsId != null)
        {
            parameters["wsIdPrefix"] = wsId + ":";
        }

        if (symbolType == "Function")
        {
            query = Queries.Get("find_symbol_function").Replace("{prefixClause}", prefixClause);
        }
        else if (symbolType == "Class")
        {
            query = Queries.Get("find_symbol_class").Replace("{prefixClause}", prefixClause);
        }
        else if (symbolType == "Interface")
        {
            query = Queries.Get("find_symbol_interface").Replace("{prefixClause}", prefixClause);
        }
        else
        {
            query = Queries.Get("find_symbol_all").Replace("{prefixClause}", prefixClause);
        }

        return await ExecuteAndFormatQueryAsync(query, parameters);
    }

    public async Task<string> GetCallChainAsync(string startFunction, string endFunction, int maxDepth, string? workspacePath)
    {
        var wsId = await GetWorkspaceIdAsync(workspacePath);
        var depth = Math.Max(1, Math.Min(10, maxDepth));
        string query;
        var parameters = new Dictionary<string, object>
        {
            ["startFunction"] = startFunction,
            ["endFunction"] = endFunction
        };

        if (wsId != null)
        {
            parameters["wsIdPrefix"] = wsId + ":";
            query = Queries.Get("get_call_chain").Replace("{depth}", depth.ToString());
        }
        else
        {
            query = Queries.Get("get_call_chain_no_ws").Replace("{depth}", depth.ToString());
        }
        return await ExecuteAndFormatQueryAsync(query, parameters);
    }

    public async Task<string> ResolveCallTargetAsync(string interfaceName, string methodName, string? workspacePath)
    {
        var wsId = await GetWorkspaceIdAsync(workspacePath);
        string query;
        var parameters = new Dictionary<string, object>
        {
            ["interfaceName"] = interfaceName,
            ["methodName"] = methodName
        };

        if (wsId != null)
        {
            parameters["wsIdPrefix"] = wsId + ":";
            query = Queries.Get("resolve_call_target");
        }
        else
        {
            query = Queries.Get("resolve_call_target_no_ws");
        }
        return await ExecuteAndFormatQueryAsync(query, parameters);
    }

    public async Task<string> AnalyzeCodeImpactAsync(string symbolName, string? workspacePath)
    {
        var wsId = await GetWorkspaceIdAsync(workspacePath);
        string query;
        var parameters = new Dictionary<string, object>
        {
            ["symbolName"] = symbolName
        };

        if (wsId != null)
        {
            parameters["wsIdPrefix"] = wsId + ":";
            query = Queries.Get("analyze_code_impact");
        }
        else
        {
            query = Queries.Get("analyze_code_impact_no_ws");
        }
        return await ExecuteAndFormatQueryAsync(query, parameters);
    }

    public async Task<string> InspectDataLineageAsync(string tableName, string? workspacePath)
    {
        var wsId = await GetWorkspaceIdAsync(workspacePath);
        string query;
        var parameters = new Dictionary<string, object>
        {
            ["tableName"] = tableName
        };

        if (wsId != null)
        {
            parameters["wsIdPrefix"] = wsId + ":";
            query = Queries.Get("inspect_data_lineage");
        }
        else
        {
            query = Queries.Get("inspect_data_lineage_no_ws");
        }
        return await ExecuteAndFormatQueryAsync(query, parameters);
    }

    public async Task<string> GetProjectEntryPointsAsync(string projectName, string? workspacePath)
    {
        var wsId = await GetWorkspaceIdAsync(workspacePath);
        string query;
        var parameters = new Dictionary<string, object>
        {
            ["projectName"] = projectName
        };

        if (wsId != null)
        {
            parameters["wsIdPrefix"] = wsId + ":";
            query = Queries.Get("get_project_entry_points");
        }
        else
        {
            query = Queries.Get("get_project_entry_points_no_ws");
        }
        return await ExecuteAndFormatQueryAsync(query, parameters);
    }

    public async Task<string> FindRefactoringOpportunitiesAsync(string projectName, string metricType, string? workspacePath)
    {
        var wsId = await GetWorkspaceIdAsync(workspacePath);
        var results = new List<object>();

        var prefixClause = wsId != null ? " WHERE p.id STARTS WITH $wsIdPrefix " : "";
        var parameters = new Dictionary<string, object> { ["projectName"] = projectName };
        if (wsId != null)
        {
            parameters["wsIdPrefix"] = wsId + ":";
        }

        if (metricType == "dead_code" || metricType == "all")
        {
            var deadCodeQuery = Queries.Get("find_refactor_dead_code").Replace("{prefixClause}", prefixClause);

            var res = await dbClient.ExecuteQueryAsync(deadCodeQuery, parameters);
            using var doc = JsonDocument.Parse(res);

            foreach (var item in doc.RootElement.EnumerateArray())
            {
                results.Add(item.Clone());
            }
        }

        if (metricType == "god_objects" || metricType == "all")
        {
            var godObjectsQuery = Queries.Get("find_refactor_god_objects").Replace("{prefixClause}", prefixClause);

            var res = await dbClient.ExecuteQueryAsync(godObjectsQuery, parameters);
            using var doc = JsonDocument.Parse(res);

            foreach (var item in doc.RootElement.EnumerateArray())
            {
                results.Add(item.Clone());
            }
        }

        return JsonSerializer.Serialize(new { results }, new JsonSerializerOptions { WriteIndented = true });
    }

    public async Task<string> ExecuteCustomReadCypherAsync(string query, string? workspacePath)
    {
        var lowerQuery = query.ToLowerInvariant();

        if (lowerQuery.Contains("create") || lowerQuery.Contains("delete") || lowerQuery.Contains("set") ||
            lowerQuery.Contains("merge") || lowerQuery.Contains("remove") || lowerQuery.Contains("drop") ||
            lowerQuery.Contains("detach"))
        {
            throw new InvalidOperationException("Security violation: Mutating queries are not allowed.");
        }

        var wsId = await GetWorkspaceIdAsync(workspacePath);
        var parameters = new Dictionary<string, object?>();

        if (wsId != null)
        {
            var wsIdPrefix = wsId + ":";
            // Check if the query references either parameter
            if (!query.Contains("$workspaceId") && !query.Contains("$workspaceIdPrefix"))
            {
                throw new InvalidOperationException(
                    "Security/scoping violation: Custom Cypher queries in a scoped workspace must filter nodes by workspace. " +
                    "Please include a WHERE clause constraining matched nodes, e.g.: WHERE n.id STARTS WITH $workspaceIdPrefix");
            }
            parameters["workspaceId"] = wsId;
            parameters["workspaceIdPrefix"] = wsIdPrefix;
        }

        return await ExecuteAndFormatQueryAsync(query, parameters);
    }

    public async Task<string> GetWorkspaceContentAsync(string? workspacePath, string? type)
    {
        string query;
        var parameters = new Dictionary<string, object?>();

        if (!string.IsNullOrEmpty(workspacePath))
        {
            var resolvedPath = PathTools.TranslateHostPathToContainerPath(workspacePath);
            var absolutePath = Path.GetFullPath(resolvedPath!).Replace('\\', '/');
            parameters["workspacePath"] = absolutePath;
            parameters["type"] = string.IsNullOrEmpty(type) ? null : type;

            query = Queries.Get("get_workspace_content");
        }
        else
        {
            parameters["type"] = string.IsNullOrEmpty(type) ? null : type;

            query = Queries.Get("get_workspace_content_no_ws");
        }

        return await dbClient.ExecuteQueryAsync(query, parameters);
    }

    public async Task<string> ExecuteRawQueryAsync(string query, Dictionary<string, object?>? parameters = null)
    {
        return await dbClient.ExecuteQueryAsync(query, parameters);
    }

    public async Task<string> GetTaxonomyAsync(string? workspacePath)
    {
        var wsId = await GetWorkspaceIdAsync(workspacePath);
        string query;
        string propQuery;
        var parameters = new Dictionary<string, object?>();

        if (wsId != null)
        {
            var wsIdPrefix = wsId + ":";
            parameters["wsIdPrefix"] = wsIdPrefix;
            query = Queries.Get("get_taxonomy_nodes");
            propQuery = Queries.Get("get_taxonomy_properties");
        }
        else
        {
            query = Queries.Get("get_taxonomy_nodes_no_ws");
            propQuery = Queries.Get("get_taxonomy_properties_no_ws");
        }

        var resultJson = await dbClient.ExecuteQueryAsync(query, parameters);
        var parsedTriplets = JsonSerializer.Deserialize<List<Dictionary<string, string>>>(resultJson) ?? [];

        var propJson = await dbClient.ExecuteQueryAsync(propQuery, parameters);
        var parsedProperties = JsonSerializer.Deserialize<List<Dictionary<string, string>>>(propJson) ?? [];

        var taxonomy = BuildTaxonomy(parsedTriplets, parsedProperties);
        return JsonSerializer.Serialize(new { taxonomy }, new JsonSerializerOptions { WriteIndented = true });
    }

    public async Task<string> FetchCodeSnippetsAsync(string nodesJson, string? workspacePath)
    {
        return await FetchCodeSnippetsDirectlyAsync(nodesJson, workspacePath);
    }

    private static readonly Dictionary<string, (string CapitalizedKind, Type NodeType)> KindMapping = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Workspace"] = ("Workspace", typeof(WorkspaceNode)),
        ["WorkspaceFolder"] = ("WorkspaceFolder", typeof(WorkspaceFolderNode)),
        ["Project"] = ("Project", typeof(ProjectNode)),
        ["ProjectFolder"] = ("ProjectFolder", typeof(ProjectFolderNode)),
        ["Package"] = ("Package", typeof(PackageNode)),
        ["Dependencies"] = ("Dependencies", typeof(DependenciesNode)),
        ["Files"] = ("Files", typeof(FilesNode)),
        ["DataBases"] = ("DataBases", typeof(DataBasesNode)),
        ["ApisInUse"] = ("ApisInUse", typeof(ApisInUseNode)),
        ["CloudServices"] = ("CloudServices", typeof(CloudServicesNode)),
        ["DB"] = ("DB", typeof(DbNode)),
        ["ApiInUse"] = ("ApiInUse", typeof(ApiInUseNode)),
        ["CloudService"] = ("CloudService", typeof(CloudServiceNode)),
        ["File"] = ("File", typeof(FileNode)),
        ["Class"] = ("Class", typeof(ClassNode)),
        ["Interface"] = ("Interface", typeof(InterfaceNode)),
        ["Function"] = ("Function", typeof(FunctionNode)),
        ["Variable"] = ("Variable", typeof(VariableNode)),
        ["GitSettings"] = ("GitSettings", typeof(GitSettingsNode)),
        ["EntryPoint"] = ("EntryPoint", typeof(EntryPointNode)),
        ["EntryPoints"] = ("EntryPoints", typeof(EntryPointsNode))
    };

    private static readonly Dictionary<string, string> PropertyDescriptions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["id"] = "A unique identifier for the node.",
        ["name"] = "The name of the entity.",
        ["path"] = "The path of the folder or file relative to its parent container.",
        ["project_type"] = "The language/signature identifier (e.g. 'csharp', 'go', 'python', 'typescript').",
        ["version"] = "The package version.",
        ["type"] = "The package type or entity type.",
        ["symbol"] = "A globally unique ID for this symbol scope.",
        ["start_line"] = "The starting line number (1-indexed) of the declaration.",
        ["end_line"] = "The ending line number (1-indexed) of the declaration.",
        ["start_col"] = "The starting column number of the declaration.",
        ["end_col"] = "The ending column number of the declaration.",
        ["file_path"] = "The relative path of the declaring file.",
        ["branch"] = "The currently checked-out branch name.",
        ["origin_url"] = "The remote origin repository URL.",
        ["user_name"] = "The git user name.",
        ["user_email"] = "The git user email address.",
        ["protocol"] = "The communication protocol (e.g. 'http', 'ws', 'event').",
        ["route_or_topic"] = "The routing path or message topic."
    };

    private class TemplateInfo
    {
        public string Purpose { get; set; } = "";
        public List<string> Relationships { get; set; } = [];
    }

    private static readonly Dictionary<string, TemplateInfo> Templates = LoadTemplates();

    private static Dictionary<string, TemplateInfo> LoadTemplates()
    {
        try
        {
            var json = Queries.GetOntologyTemplates();
            return JsonSerializer.Deserialize<Dictionary<string, TemplateInfo>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static string ToSnakeCase(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        if (input == "Id") return "id";
        
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < input.Length; i++)
        {
            char c = input[i];
            if (i > 0 && char.IsUpper(c))
            {
                sb.Append('_');
            }
            sb.Append(char.ToLowerInvariant(c));
        }
        return sb.ToString();
    }

    public string GetNodeDefinition(string kind)
    {
        if (string.IsNullOrEmpty(kind)) return "Invalid node kind.";
        var kindLower = kind.Trim().ToLowerInvariant();

        if (!KindMapping.TryGetValue(kindLower, out var map))
        {
            return $"Unknown node kind: '{kind}'. Active ontological kinds in CodeExplorer are: 'Workspace', 'WorkspaceFolder', 'ProjectFolder', 'Project', 'Files', 'DataBases', 'ApisInUse', 'CloudServices', 'File', 'Class', 'Function', 'Variable', 'Package', 'Dependencies', 'EntryPoints', 'EntryPoint', 'ApiInUse', 'CloudService', 'DB', 'GitSettings'.";
        }

        var (capitalizedKind, nodeType) = map;
        
        var propertiesMarkdownList = new List<string>();
        var properties = nodeType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        foreach (var prop in properties)
        {
            if (prop.Name is "Children" or "References" or "Extensions" or "Kind")
            {
                continue;
            }

            var propType = prop.PropertyType;
            var isNullable = propType.IsGenericType && propType.GetGenericTypeDefinition() == typeof(Nullable<>);
            var underlyingType = isNullable ? Nullable.GetUnderlyingType(propType)! : propType;

            var typeStr = underlyingType == typeof(string) ? "string" :
                          underlyingType == typeof(int) || underlyingType == typeof(long) ? "integer" :
                          underlyingType == typeof(bool) ? "boolean" :
                          underlyingType.Name.ToLowerInvariant();

            var snakeName = ToSnakeCase(prop.Name);
            var description = PropertyDescriptions.TryGetValue(snakeName, out var desc) ? desc : "";
            
            var formattedDesc = string.IsNullOrEmpty(description) ? "" : $": {description}";
            propertiesMarkdownList.Add($"  - `{snakeName}` ({typeStr}){formattedDesc}");
        }

        propertiesMarkdownList = propertiesMarkdownList
            .OrderBy(p => p.Contains("`name`") ? 0 : p.Contains("`path`") ? 1 : 2)
            .ThenBy(p => p)
            .ToList();

        var propertiesMarkdown = string.Join("\n", propertiesMarkdownList);

        var purpose = "Node of kind " + capitalizedKind + ".";
        var relationshipsMarkdown = "  - None defined.";

        if (Templates.TryGetValue(capitalizedKind, out var template))
        {
            purpose = template.Purpose;
            if (template.Relationships.Count > 0)
            {
                relationshipsMarkdown = string.Join("\n", template.Relationships.Select(r => $"  - {r}"));
            }
        }

        return $"### Kind: {capitalizedKind}\n" +
               $"**Purpose**: {purpose}\n" +
               $"**Key Properties**:\n{propertiesMarkdown}\n" +
               $"**Relationships**:\n{relationshipsMarkdown}";
    }
    private async Task<string> FetchCodeSnippetsDirectlyAsync(string nodesJson, string? hostWorkspacePath)
    {
        List<McpRAGNode>? nodes = null;

        try
        {
            nodes = JsonSerializer.Deserialize<List<McpRAGNode>>(nodesJson);
        }
        catch
        {
            try
            {
                var single = JsonSerializer.Deserialize<McpRAGNode>(nodesJson);
                if (single != null) nodes = [single];
            }
            catch
            {
                try
                {
                    var nestedNodes = JsonSerializer.Deserialize<List<NestedMcpRAGNode>>(nodesJson);

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

        var workspaceRoot = Environment.GetEnvironmentVariable("WORKSPACE_ROOT");

        if (string.IsNullOrEmpty(workspaceRoot))
        {
            workspaceRoot = PathTools.TranslateHostPathToContainerPath(hostWorkspacePath);

            if (string.IsNullOrEmpty(workspaceRoot))
            {
                var current = Directory.GetCurrentDirectory();

                while (!string.IsNullOrEmpty(current))
                {
                    if (File.Exists(Path.Combine(current, "CodeExplorer.sln")))
                    {
                        workspaceRoot = current;
                        break;
                    }

                    current = Path.GetDirectoryName(current);
                }
            }
        }

        var output = new List<string>();

        foreach (var node in nodes)
        {
            if (string.IsNullOrEmpty(node.file_path) || node.start_line == null || node.end_line == null)
            {
                continue;
            }

            var relativePath = PathTools.GetRelativePath(node.file_path, hostWorkspacePath);

            var joinedPath = Path.Combine(workspaceRoot!, relativePath);
            var absPath = Path.GetFullPath(joinedPath);
            var absRoot = Path.GetFullPath(workspaceRoot!);

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
                var lines = await File.ReadAllLinesAsync(absPath);
                var sIdx = Math.Max(0, node.start_line.Value);
                if (sIdx > lines.Length) sIdx = lines.Length;

                var eIdx = Math.Min(lines.Length, node.end_line.Value + 1);
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

    public static object BuildTaxonomy(
        List<Dictionary<string, string>> triplets,
        List<Dictionary<string, string>> properties)
    {
        var nodes =
            new Dictionary<string, (List<string> properties, HashSet<(string relationship, string target)> outgoing,
                HashSet<(string relationship, string source)> incoming)>(StringComparer.OrdinalIgnoreCase);

        foreach (var prop in properties)
        {
            if (prop.TryGetValue("label", out var label) && prop.TryGetValue("key", out var key))
            {
                if (!nodes.ContainsKey(label)) nodes[label] = ([], [], []);
                nodes[label].properties.Add(key);
            }
        }

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

        var result = new List<object>();

        foreach (var kvp in nodes.OrderBy(k => k.Key))
        {
            result.Add(new
            {
                label = kvp.Key,
                properties = kvp.Value.properties.OrderBy(p => p).ToList(),
                outgoing =
                    kvp.Value.outgoing.OrderBy(x => x.relationship).Select(x => new { x.relationship, x.target })
                        .ToList(),
                incoming = kvp.Value.incoming.OrderBy(x => x.relationship)
                    .Select(x => new { x.relationship, x.source }).ToList()
            });
        }

        return result;
    }
}
