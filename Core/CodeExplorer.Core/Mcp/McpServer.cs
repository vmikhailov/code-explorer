using System.IO;
using System.Text.Json;
using CodeExplorer.Database;

namespace CodeExplorer.Mcp;

public class McpServer(MemgraphClient dbClient)
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
                    "get_architecture_map" => await HandleGetArchitectureMapAsync(args),
                    "get_project_dependencies" => await HandleGetProjectDependenciesAsync(args),
                    "get_file_outline" => await HandleGetFileOutlineAsync(args),
                    "find_symbol" => await HandleFindSymbolAsync(args),
                    "get_call_chain" => await HandleGetCallChainAsync(args),
                    "resolve_call_target" => await HandleResolveCallTargetAsync(args),
                    "analyze_code_impact" => await HandleAnalyzeCodeImpactAsync(args),
                    "inspect_data_lineage" => await HandleInspectDataLineageAsync(args),
                    "get_project_entry_points" => await HandleGetProjectEntryPointsAsync(args),
                    "find_refactoring_opportunities" => await HandleFindRefactoringOpportunitiesAsync(args),
                    "execute_custom_read_cypher" => await HandleExecuteCustomReadCypherAsync(args),
                    "fetch_code_snippets" => HandleFetchCodeSnippets(args),
                    "get_taxonomy" => await HandleGetTaxonomyAsync(args),
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

    private async Task<object> ExecuteAndFormatQueryAsync(string query, object? parameters = null)
    {
        try
        {
            var resultJson = await dbClient.ExecuteQueryAsync(query, parameters);
            using var doc = JsonDocument.Parse(resultJson);
            var wrappedJson = JsonSerializer.Serialize(new { results = doc.RootElement }, new JsonSerializerOptions { WriteIndented = true });

            return new
            {
                content = new[]
                {
                    new { type = "text", text = wrappedJson }
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

    private async Task<object> HandleGetArchitectureMapAsync(JsonElement args)
    {
        string query;
        var parameters = new Dictionary<string, object>();
        if (args.TryGetProperty("projectName", out var projEl) && projEl.ValueKind == JsonValueKind.String)
        {
            parameters["projectName"] = projEl.GetString()!;
            query = "MATCH (p:Project {name: $projectName}) " +
                    "OPTIONAL MATCH (p)-[:USES_DB]->(db:DB) " +
                    "OPTIONAL MATCH (p)-[:CONTAINS*1..]->(pf:ProjectFolder) " +
                    "RETURN p.name AS project, p.project_type AS type, db.name AS dbName, collect(DISTINCT pf.name) AS folders";
        }
        else
        {
            query = "MATCH (w:Workspace) " +
                    "OPTIONAL MATCH (w)-[:CONTAINS*1..]->(wf:WorkspaceFolder) " +
                    "OPTIONAL MATCH (w)-[:CONTAINS*1..]->(p:Project) " +
                    "OPTIONAL MATCH (p)-[:USES_DB]->(db:DB) " +
                    "RETURN w.name AS workspace, w.path AS path, collect(DISTINCT wf.name) AS workspaceFolders, collect(DISTINCT p.name) AS projects, collect(DISTINCT db.name) AS dbNames";
        }
        return await ExecuteAndFormatQueryAsync(query, parameters);
    }

    private async Task<object> HandleGetProjectDependenciesAsync(JsonElement args)
    {
        string query;
        var parameters = new Dictionary<string, object>();
        if (args.TryGetProperty("projectFilter", out var filterEl) && filterEl.ValueKind == JsonValueKind.String)
        {
            parameters["projectFilter"] = filterEl.GetString()!;
            query = "MATCH (p:Project {name: $projectFilter}) " +
                    "OPTIONAL MATCH (p)-[:DEPENDS_ON]->(out) " +
                    "OPTIONAL MATCH (in)-[:DEPENDS_ON]->(p) " +
                    "RETURN p.name AS project, collect(DISTINCT out.name) AS outgoingDependencies, collect(DISTINCT in.name) AS incomingDependencies";
        }
        else
        {
            query = "MATCH (p:Project)-[:DEPENDS_ON]->(dep) " +
                    "RETURN p.name AS project, dep.name AS dependency, labels(dep)[0] AS dependencyType";
        }
        return await ExecuteAndFormatQueryAsync(query, parameters);
    }

    private async Task<object> HandleGetFileOutlineAsync(JsonElement args)
    {
        if (!args.TryGetProperty("filePath", out var pathEl) || pathEl.ValueKind != JsonValueKind.String)
        {
            return new { isError = true, content = new[] { new { type = "text", text = "Missing or invalid 'filePath' argument." } } };
        }
        var filePath = pathEl.GetString()!;
        var query = "MATCH (f:File) WHERE f.path ENDS_WITH $filePath OR f.file_path = $filePath " +
                    "OPTIONAL MATCH (f)-[:CONTAINS*1..]->(child) " +
                    "WHERE child:Class OR child:Interface OR child:Function OR child:Variable OR child:Query " +
                    "RETURN child.name AS name, labels(child)[0] AS type, child.start_line AS startLine, child.end_line AS endLine, child.symbol AS symbol " +
                    "ORDER BY child.start_line";
        var parameters = new Dictionary<string, object> { ["filePath"] = filePath };
        return await ExecuteAndFormatQueryAsync(query, parameters);
    }

    private async Task<object> HandleFindSymbolAsync(JsonElement args)
    {
        if (!args.TryGetProperty("name", out var nameEl) || nameEl.ValueKind != JsonValueKind.String)
        {
            return new { isError = true, content = new[] { new { type = "text", text = "Missing or invalid 'name' argument." } } };
        }
        var name = nameEl.GetString()!;
        string? type = null;
        if (args.TryGetProperty("symbolType", out var typeEl) && typeEl.ValueKind == JsonValueKind.String)
        {
            type = typeEl.GetString();
        }

        string query;
        var parameters = new Dictionary<string, object> { ["name"] = name };

        if (type == "Function")
        {
            query = "MATCH (n:Function) WHERE n.name CONTAINS $name " +
                    "OPTIONAL MATCH (f:File)-[:CONTAINS*1..]->(n) " +
                    "OPTIONAL MATCH fileDir = (w:Workspace)-[:CONTAINS*1..]->(f) " +
                    "RETURN 'Function' AS type, n.name AS name, n.symbol AS fullName, " +
                    "CASE WHEN f IS NOT NULL AND w IS NOT NULL " +
                    "     THEN w.path + '/' + reduce(s = '', x IN nodes(fileDir)[1..size(nodes(fileDir))-1] | s + CASE WHEN x.path = '' THEN '' ELSE x.path + '/' END) + f.path " +
                    "     ELSE n.file_path " +
                    "END AS filePath LIMIT 10";
        }
        else if (type == "Class")
        {
            query = "MATCH (n:Class) WHERE n.name CONTAINS $name " +
                    "OPTIONAL MATCH (f:File)-[:CONTAINS*1..]->(n) " +
                    "OPTIONAL MATCH fileDir = (w:Workspace)-[:CONTAINS*1..]->(f) " +
                    "RETURN 'Class' AS type, n.name AS name, n.symbol AS fullName, " +
                    "CASE WHEN f IS NOT NULL AND w IS NOT NULL " +
                    "     THEN w.path + '/' + reduce(s = '', x IN nodes(fileDir)[1..size(nodes(fileDir))-1] | s + CASE WHEN x.path = '' THEN '' ELSE x.path + '/' END) + f.path " +
                    "     ELSE n.file_path " +
                    "END AS filePath LIMIT 10";
        }
        else if (type == "Interface")
        {
            query = "MATCH (n:Interface) WHERE n.name CONTAINS $name " +
                    "OPTIONAL MATCH (f:File)-[:CONTAINS*1..]->(n) " +
                    "OPTIONAL MATCH fileDir = (w:Workspace)-[:CONTAINS*1..]->(f) " +
                    "RETURN 'Interface' AS type, n.name AS name, n.symbol AS fullName, " +
                    "CASE WHEN f IS NOT NULL AND w IS NOT NULL " +
                    "     THEN w.path + '/' + reduce(s = '', x IN nodes(fileDir)[1..size(nodes(fileDir))-1] | s + CASE WHEN x.path = '' THEN '' ELSE x.path + '/' END) + f.path " +
                    "     ELSE n.file_path " +
                    "END AS filePath LIMIT 10";
        }
        else
        {
            query = "MATCH (n) WHERE (n:Function OR n:Class OR n:Interface) AND n.name CONTAINS $name " +
                    "OPTIONAL MATCH (f:File)-[:CONTAINS*1..]->(n) " +
                    "OPTIONAL MATCH fileDir = (w:Workspace)-[:CONTAINS*1..]->(f) " +
                    "RETURN labels(n)[0] AS type, n.name AS name, n.symbol AS fullName, " +
                    "CASE WHEN f IS NOT NULL AND w IS NOT NULL " +
                    "     THEN w.path + '/' + reduce(s = '', x IN nodes(fileDir)[1..size(nodes(fileDir))-1] | s + CASE WHEN x.path = '' THEN '' ELSE x.path + '/' END) + f.path " +
                    "     ELSE n.file_path " +
                    "END AS filePath LIMIT 10";
        }

        return await ExecuteAndFormatQueryAsync(query, parameters);
    }

    private async Task<object> HandleGetCallChainAsync(JsonElement args)
    {
        if (!args.TryGetProperty("startFunction", out var startEl) || startEl.ValueKind != JsonValueKind.String ||
            !args.TryGetProperty("endFunction", out var endEl) || endEl.ValueKind != JsonValueKind.String)
        {
            return new { isError = true, content = new[] { new { type = "text", text = "Missing or invalid 'startFunction' or 'endFunction' argument." } } };
        }
        var startFunction = startEl.GetString()!;
        var endFunction = endEl.GetString()!;

        int depth = 5;
        if (args.TryGetProperty("maxDepth", out var depthEl) && depthEl.ValueKind == JsonValueKind.Number)
        {
            depth = Math.Max(1, Math.Min(10, depthEl.GetInt32()));
        }

        var query = $"MATCH path = (src:Function {{symbol: $startFunction}})-[:CALLS*1..{depth}]->(tgt:Function {{symbol: $endFunction}}) " +
                    "RETURN nodes(path) AS chain";
        var parameters = new Dictionary<string, object> 
        { 
            ["startFunction"] = startFunction, 
            ["endFunction"] = endFunction 
        };
        return await ExecuteAndFormatQueryAsync(query, parameters);
    }

    private async Task<object> HandleResolveCallTargetAsync(JsonElement args)
    {
        if (!args.TryGetProperty("interfaceName", out var interfaceEl) || interfaceEl.ValueKind != JsonValueKind.String ||
            !args.TryGetProperty("methodName", out var methodEl) || methodEl.ValueKind != JsonValueKind.String)
        {
            return new { isError = true, content = new[] { new { type = "text", text = "Missing or invalid 'interfaceName' or 'methodName' argument." } } };
        }
        var interfaceName = interfaceEl.GetString()!;
        var methodName = methodEl.GetString()!;

        var query = "MATCH (i:Interface {name: $interfaceName})<-[:IMPLEMENTS]-(impl:Class)-[:CONTAINS]->(f:Function {name: $methodName}) " +
                    "RETURN impl.name AS className, f.name AS methodName, f.symbol AS methodSymbol, f.file_path AS filePath, f.start_line AS startLine";
        var parameters = new Dictionary<string, object>
        {
            ["interfaceName"] = interfaceName,
            ["methodName"] = methodName
        };
        return await ExecuteAndFormatQueryAsync(query, parameters);
    }

    private async Task<object> HandleAnalyzeCodeImpactAsync(JsonElement args)
    {
        if (!args.TryGetProperty("symbolName", out var symbolEl) || symbolEl.ValueKind != JsonValueKind.String)
        {
            return new { isError = true, content = new[] { new { type = "text", text = "Missing or invalid 'symbolName' argument." } } };
        }
        var symbolName = symbolEl.GetString()!;
        var query = "MATCH (target) WHERE (target:Class OR target:Interface OR target:Function) AND (target.symbol = $symbolName OR target.name = $symbolName) " +
                    "MATCH (target)<-[:USES_TYPE|CALLS]-(dependent) " +
                    "OPTIONAL MATCH (f:File)-[:CONTAINS*1..]->(dependent) " +
                    "OPTIONAL MATCH fileDir = (w:Workspace)-[:CONTAINS*1..]->(f) " +
                    "RETURN labels(dependent)[0] AS dependentType, dependent.name AS dependentName, dependent.symbol AS dependentSymbol, " +
                    "CASE WHEN f IS NOT NULL AND w IS NOT NULL " +
                    "     THEN w.path + '/' + reduce(s = '', x IN nodes(fileDir)[1..size(nodes(fileDir))-1] | s + CASE WHEN x.path = '' THEN '' ELSE x.path + '/' END) + f.path " +
                    "     ELSE null " +
                    "END AS filePath";
        var parameters = new Dictionary<string, object> { ["symbolName"] = symbolName };
        return await ExecuteAndFormatQueryAsync(query, parameters);
    }

    private async Task<object> HandleInspectDataLineageAsync(JsonElement args)
    {
        if (!args.TryGetProperty("tableName", out var tableEl) || tableEl.ValueKind != JsonValueKind.String)
        {
            return new { isError = true, content = new[] { new { type = "text", text = "Missing or invalid 'tableName' argument." } } };
        }
        var tableName = tableEl.GetString()!;
        var query = "MATCH (t:Table {name: $tableName}) " +
                    "OPTIONAL MATCH (q:Query)-[:DEPENDS_ON]->(t) " +
                    "OPTIONAL MATCH (parent)-[:CONTAINS]->(q) " +
                    "OPTIONAL MATCH (caller)-[:CALLS|DEPENDS_ON*0..]->(parent) " +
                    "RETURN t.name AS tableName, q.name AS queryName, q.query_text AS queryText, q.path AS filePath, " +
                    "collect(DISTINCT parent.name) AS parentName, labels(parent)[0] AS parentType, " +
                    "collect(DISTINCT caller.name) AS callingSymbols";
        var parameters = new Dictionary<string, object> { ["tableName"] = tableName };
        return await ExecuteAndFormatQueryAsync(query, parameters);
    }

    private async Task<object> HandleGetProjectEntryPointsAsync(JsonElement args)
    {
        if (!args.TryGetProperty("projectName", out var projectEl) || projectEl.ValueKind != JsonValueKind.String)
        {
            return new { isError = true, content = new[] { new { type = "text", text = "Missing or invalid 'projectName' argument." } } };
        }
        var projectName = projectEl.GetString()!;
        var query = "MATCH (p:Project {name: $projectName})-[:CONTAINS*1..]->(f:File) " +
                    "MATCH (f)-[:CONTAINS*1..]->(func:Function) " +
                    "WHERE f.path CONTAINS 'Controller' OR f.path CONTAINS 'Endpoint' OR f.path CONTAINS 'Handler' OR f.path CONTAINS 'Resolver' " +
                    "OR func.name STARTS WITH 'On' OR func.name STARTS WITH 'Handle' " +
                    "OPTIONAL MATCH (class:Class)-[:CONTAINS]->(func) " +
                    "RETURN func.name AS entryPoint, func.symbol AS symbol, class.name AS className, f.path AS filePath, func.start_line AS startLine";
        var parameters = new Dictionary<string, object> { ["projectName"] = projectName };
        return await ExecuteAndFormatQueryAsync(query, parameters);
    }

    private async Task<object> HandleFindRefactoringOpportunitiesAsync(JsonElement args)
    {
        if (!args.TryGetProperty("projectName", out var projectEl) || projectEl.ValueKind != JsonValueKind.String)
        {
            return new { isError = true, content = new[] { new { type = "text", text = "Missing or invalid 'projectName' argument." } } };
        }
        var projectName = projectEl.GetString()!;
        var metricType = "all";
        if (args.TryGetProperty("metricType", out var metricEl) && metricEl.ValueKind == JsonValueKind.String)
        {
            metricType = metricEl.GetString()!;
        }

        var results = new List<object>();

        if (metricType == "dead_code" || metricType == "all")
        {
            var deadCodeQuery = "MATCH (p:Project {name: $projectName})-[:CONTAINS*1..]->(f:File) " +
                                "MATCH (f)-[:CONTAINS*1..]->(item) " +
                                "WHERE (item:Function OR item:Class) " +
                                "OPTIONAL MATCH (caller:Entity)-[:CALLS|USES_TYPE]->(item) " +
                                "WITH f, item, caller " +
                                "WHERE caller IS NULL " +
                                "RETURN item.name AS name, labels(item)[0] AS type, f.path AS filePath, 'dead_code' AS anomalyType, item.symbol AS symbol LIMIT 50";
            
            var parameters = new Dictionary<string, object> { ["projectName"] = projectName };
            var res = await dbClient.ExecuteQueryAsync(deadCodeQuery, parameters);
            using var doc = JsonDocument.Parse(res);
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                results.Add(item.Clone());
            }
        }

        if (metricType == "god_objects" || metricType == "all")
        {
            var godObjectsQuery = "MATCH (p:Project {name: $projectName})-[:CONTAINS*1..]->(f:File) " +
                                  "MATCH (f)-[:CONTAINS*1..]->(c:Class) " +
                                  "MATCH (c)-[:CONTAINS]->(member) " +
                                  "WITH c, f, count(member) AS memberCount " +
                                  "WHERE memberCount > 15 " +
                                  "RETURN c.name AS name, 'Class' AS type, f.path AS filePath, 'god_object' AS anomalyType, memberCount AS metricValue, c.symbol AS symbol " +
                                  "ORDER BY memberCount DESC LIMIT 20";
            
            var parameters = new Dictionary<string, object> { ["projectName"] = projectName };
            var res = await dbClient.ExecuteQueryAsync(godObjectsQuery, parameters);
            using var doc = JsonDocument.Parse(res);
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                results.Add(item.Clone());
            }
        }

        var wrappedJson = JsonSerializer.Serialize(new { results }, new JsonSerializerOptions { WriteIndented = true });
        return new
        {
            content = new[]
            {
                new { type = "text", text = wrappedJson }
            }
        };
    }

    private async Task<object> HandleExecuteCustomReadCypherAsync(JsonElement args)
    {
        if (!args.TryGetProperty("query", out var queryEl) || queryEl.ValueKind != JsonValueKind.String)
        {
            return new { isError = true, content = new[] { new { type = "text", text = "Missing or invalid 'query' argument." } } };
        }
        var query = queryEl.GetString()!;
        
        var lowerQuery = query.ToLowerInvariant();
        if (lowerQuery.Contains("create") || lowerQuery.Contains("delete") || lowerQuery.Contains("set") || 
            lowerQuery.Contains("merge") || lowerQuery.Contains("remove") || lowerQuery.Contains("drop") || lowerQuery.Contains("detach"))
        {
            return new { isError = true, content = new[] { new { type = "text", text = "Security violation: Mutating queries are not allowed." } } };
        }

        return await ExecuteAndFormatQueryAsync(query);
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

    private object HandleGetNodeDefinition(JsonElement args)
    {
        if (!args.TryGetProperty("kind", out var kindEl) || kindEl.ValueKind != JsonValueKind.String)
        {
            return new { isError = true, content = new[] { new { type = "text", text = "Missing or invalid 'kind' argument." } } };
        }
        var kind = kindEl.GetString()!.Trim();
        var kindLower = kind.ToLowerInvariant();

        string text = kindLower switch
        {
            "workspace" => 
                "### Kind: Workspace\n" +
                "**Purpose**: Represents the absolute root of the workspace directory hierarchy.\n" +
                "**Key Properties**:\n" +
                "  - `name` (string): The workspace root folder name.\n" +
                "  - `path` (string): The absolute filesystem path of the workspace.\n" +
                "**Relationships**:\n" +
                "  - `(Workspace)-[:CONTAINS]->(WorkspaceFolder)`\n" +
                "  - `(Workspace)-[:CONTAINS]->(Project)`\n" +
                "  - `(Workspace)-[:CONTAINS]->(File)` (if a source file sits at the root directory)",

            "workspacefolder" =>
                "### Kind: WorkspaceFolder\n" +
                "**Purpose**: Represents a subdirectory inside a Workspace, housing projects or other folders outside projects. Cannot contain files directly (files outside projects are ignored).\n" +
                "**Key Properties**:\n" +
                "  - `name` (string): The folder name.\n" +
                "  - `path` (string): The local folder name relative to its immediate parent container.\n" +
                "**Relationships**:\n" +
                "  - `(Workspace|WorkspaceFolder)-[:CONTAINS]->(WorkspaceFolder)`\n" +
                "  - `(WorkspaceFolder)-[:CONTAINS]->(Project)`",

            "project" =>
                "### Kind: Project\n" +
                "**Purpose**: Represents a buildable/compilable module or package directory (e.g. C# project, Go module, TS library, Python package).\n" +
                "**Key Properties**:\n" +
                "  - `name` (string): The project name.\n" +
                "  - `path` (string): The local project folder name relative to its parent container (empty string at root).\n" +
                "  - `project_type` (string): The language/signature identifier (e.g., 'csharp', 'go', 'python', 'typescript').\n" +
                "**Relationships**:\n" +
                "  - `(Workspace|WorkspaceFolder)-[:CONTAINS]->(Project)`\n" +
                "  - `(Project)-[:CONTAINS]->(ProjectFolder)`\n" +
                "  - `(Project)-[:CONTAINS]->(File)`\n" +
                "  - `(Project)-[:DEPENDS_ON]->(Project)`\n" +
                "  - `(Project)-[:DEPENDS_ON]->(Package)`",

            "projectfolder" =>
                "### Kind: ProjectFolder\n" +
                "**Purpose**: Represents a subdirectory inside a Project, containing files and other project folders.\n" +
                "**Key Properties**:\n" +
                "  - `name` (string): The folder name.\n" +
                "  - `path` (string): The local folder name relative to its immediate parent container.\n" +
                "**Relationships**:\n" +
                "  - `(Project|ProjectFolder)-[:CONTAINS]->(ProjectFolder)`\n" +
                "  - `(ProjectFolder)-[:CONTAINS]->(File)`",

            "package" =>
                "### Kind: Package\n" +
                "**Purpose**: Represents an external dependency package or workspace package referenced or produced by projects.\n" +
                "**Key Properties**:\n" +
                "  - `name` (string): The package name (e.g. 'neo4j.driver', 'react', 'CodeExplorer.Core').\n" +
                "  - `version` (string): The package version.\n" +
                "  - `type` (string): The package type identifier ('nuget', 'npm', 'go').\n" +
                "**Relationships**:\n" +
                "  - `(Project)-[:DEPENDS_ON]->(Package)`\n" +
                "  - `(Package)-[:IMPLEMENTED_BY]->(Project)`",

            "file" =>
                "### Kind: File\n" +
                "**Purpose**: Represents a source code file containing parsable content.\n" +
                "**Key Properties**:\n" +
                "  - `name` (string): The filename basename.\n" +
                "  - `path` (string): The filename relative to its immediate parent container folder.\n" +
                "**Relationships**:\n" +
                "  - `(Project|ProjectFolder)-[:CONTAINS]->(File)`\n" +
                "  - `(File)-[:CONTAINS]->(Class)`\n" +
                "  - `(File)-[:CONTAINS]->(Interface)`\n" +
                "  - `(File)-[:CONTAINS]->(Function)`",

            "class" =>
                "### Kind: Class\n" +
                "**Purpose**: Represents a parsed OOP class, struct, or concrete type definition.\n" +
                "**Key Properties**:\n" +
                "  - `name` (string): The name of the class.\n" +
                "  - `symbol` (string): A globally unique ID for this symbol scope.\n" +
                "  - `start_line` / `end_line` (integer): The bounds of the class definition.\n" +
                "  - `file_path` (string): The relative path of the declaring file.\n" +
                "**Relationships**:\n" +
                "  - `(File)-[:CONTAINS]->(Class)`\n" +
                "  - `(Class)-[:USES_TYPE]->(Class|Interface)`\n" +
                "  - `(Class)-[:IMPLEMENTS]->(Interface)`\n" +
                "  - `(Class)-[:INHERITS_FROM]->(Class)`",

            "interface" =>
                "### Kind: Interface\n" +
                "**Purpose**: Represents a parsed OOP interface contract (e.g. C# interface, Go interface, TypeScript interface).\n" +
                "**Key Properties**:\n" +
                "  - `name` (string): The name of the interface.\n" +
                "  - `symbol` (string): A globally unique ID for this symbol scope.\n" +
                "  - `start_line` / `end_line` (integer): The bounds of the interface definition.\n" +
                "  - `file_path` (string): The relative path of the declaring file.\n" +
                "**Relationships**:\n" +
                "  - `(File)-[:CONTAINS]->(Interface)`\n" +
                "  - `(Class)-[:IMPLEMENTS]->(Interface)`\n" +
                "  - `(Interface)-[:INHERITS_FROM]->(Interface)`",

            "function" =>
                "### Kind: Function\n" +
                "**Purpose**: Represents a parsed method, function, subroutine, or procedure.\n" +
                "**Key Properties**:\n" +
                "  - `name` (string): The name of the function.\n" +
                "  - `symbol` (string): A globally unique ID for this symbol scope.\n" +
                "  - `start_line` / `end_line` (integer): The bounds of the function definition.\n" +
                "  - `file_path` (string): The relative path of the declaring file.\n" +
                "**Relationships**:\n" +
                "  - `(File|Class|Interface)-[:CONTAINS]->(Function)`\n" +
                "  - `(Function)-[:CALLS]->(Function)`\n" +
                "  - `(Function)-[:USES_TYPE]->(Class|Interface)`",

            "variable" =>
                "### Kind: Variable\n" +
                "**Purpose**: Represents a declared field, variable, parameter, or property parsed from the AST.\n" +
                "**Key Properties**:\n" +
                "  - `name` (string): The name of the variable.\n" +
                "  - `symbol` (string): A globally unique ID for this symbol scope.\n" +
                "  - `start_line` / `end_line` (integer): The bounds of the variable declaration.\n" +
                "  - `file_path` (string): The relative path of the declaring file.\n" +
                "**Relationships**:\n" +
                "  - `(Class|Interface|Function)-[:CONTAINS]->(Variable)`",

            _ => $"Unknown node kind: '{kind}'. Active ontological kinds in CodeExplorer are: 'Workspace', 'WorkspaceFolder', 'ProjectFolder', 'Project', 'File', 'Class', 'Function', 'Variable', 'Package'."
        };

        return new
        {
            content = new[]
            {
                new { type = "text", text }
            }
        };
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



    private async Task<object> HandleGetTaxonomyAsync(JsonElement args)
    {
        try
        {
            var query = "MATCH (n)-[r]->(m) WITH DISTINCT labels(n)[0] AS fromLabel, type(r) AS relType, labels(m)[0] AS toLabel RETURN fromLabel, relType, toLabel";
            var resultJson = await dbClient.ExecuteQueryAsync(query);
            var parsedTriplets = JsonSerializer.Deserialize<List<Dictionary<string, string>>>(resultJson) ?? new();

            var propQuery = "MATCH (n) UNWIND labels(n) AS label UNWIND keys(n) AS key RETURN DISTINCT label, key";
            var propJson = await dbClient.ExecuteQueryAsync(propQuery);
            var parsedProperties = JsonSerializer.Deserialize<List<Dictionary<string, string>>>(propJson) ?? new();

            var taxonomy = BuildTaxonomy(parsedTriplets, parsedProperties);

            return new
            {
                content = new[]
                {
                    new { type = "text", text = JsonSerializer.Serialize(new { taxonomy }, new JsonSerializerOptions { WriteIndented = true }) }
                }
            };
        }
        catch (Exception ex)
        {
            return new
            {
                isError = true,
                content = new[] { new { type = "text", text = ex.Message } }
            };
        }
    }

    internal static object BuildTaxonomy(List<Dictionary<string, string>> triplets, List<Dictionary<string, string>> properties)
    {
        var nodes = new Dictionary<string, (List<string> properties, HashSet<(string relationship, string target)> outgoing, HashSet<(string relationship, string source)> incoming)>(StringComparer.OrdinalIgnoreCase);

        foreach (var prop in properties)
        {
            if (prop.TryGetValue("label", out var label) && prop.TryGetValue("key", out var key))
            {
                if (!nodes.ContainsKey(label)) nodes[label] = (new List<string>(), new(), new());
                nodes[label].properties.Add(key);
            }
        }

        foreach (var triplet in triplets)
        {
            if (triplet.TryGetValue("fromLabel", out var from) &&
                triplet.TryGetValue("relType", out var rel) &&
                triplet.TryGetValue("toLabel", out var to))
            {
                if (!nodes.ContainsKey(from)) nodes[from] = (new List<string>(), new(), new());
                if (!nodes.ContainsKey(to)) nodes[to] = (new List<string>(), new(), new());

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
                outgoing = kvp.Value.outgoing.OrderBy(x => x.relationship).Select(x => new { relationship = x.relationship, target = x.target }).ToList(),
                incoming = kvp.Value.incoming.OrderBy(x => x.relationship).Select(x => new { relationship = x.relationship, source = x.source }).ToList()
            });
        }

        return result;
    }
}
