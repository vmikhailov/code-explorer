using System.Text.Json;
using CodeExplorer.Common;
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
                responseResult = new
                {
                    tools = new object[]
                    {
                        new
                        {
                            name = "find_symbol",
                            description = "Finds coordinates (symbol ID and file path) of a function or class.",
                            inputSchema = new
                            {
                                type = "object",
                                properties = new
                                {
                                    name = new
                                    {
                                        type = "string",
                                        description = "The name of the function or class to search for."
                                    },
                                    type = new
                                    {
                                        type = "string",
                                        description = "Optional. Filter by symbol type: 'Function' or 'Class'.",
                                        @enum = new[] { "Function", "Class" }
                                    }
                                },
                                required = new[] { "name" }
                            }
                        },
                        new
                        {
                            name = "get_project_structure",
                            description = "Retrieves files and their declared classes in a specific project (module).",
                            inputSchema = new
                            {
                                type = "object",
                                properties = new
                                {
                                    projectName = new
                                    {
                                        type = "string",
                                        description = "The name of the project (e.g. folder name) to view."
                                    }
                                },
                                required = new[] { "projectName" }
                            }
                        },
                        new
                        {
                            name = "get_call_chain",
                            description = "Traces the calling sequence of functions from a start function to an end function.",
                            inputSchema = new
                            {
                                type = "object",
                                properties = new
                                {
                                    startFunction = new
                                    {
                                        type = "string",
                                        description = "The unique fullName (symbol ID) of the starting function."
                                    },
                                    endFunction = new
                                    {
                                        type = "string",
                                        description = "The unique fullName (symbol ID) of the target ending function."
                                    },
                                    maxDepth = new
                                    {
                                        type = "integer",
                                        description = "Optional. Maximum call depth to trace (default 5, max 10)."
                                    }
                                },
                                required = new[] { "startFunction", "endFunction" }
                            }
                        },
                        new
                        {
                            name = "resolve_interface",
                            description = "Lists all concrete implementations and methods of a specified interface contract.",
                            inputSchema = new
                            {
                                type = "object",
                                properties = new
                                {
                                    interfaceName = new
                                    {
                                        type = "string",
                                        description = "The name of the interface class to resolve."
                                    }
                                },
                                required = new[] { "interfaceName" }
                            }
                        },
                        new
                        {
                            name = "get_impact_zone",
                            description = "Analyzes which code elements or files directly depend on or call the target symbol.",
                            inputSchema = new
                            {
                                type = "object",
                                properties = new
                                {
                                    symbolName = new
                                    {
                                        type = "string",
                                        description = "The unique fullName (symbol ID) of the class or function to analyze."
                                    }
                                },
                                required = new[] { "symbolName" }
                            }
                        },
                        new
                        {
                            name = "find_dead_code",
                            description = "Finds orphan functions within a project that have no incoming call references.",
                            inputSchema = new
                            {
                                type = "object",
                                properties = new
                                {
                                    projectName = new
                                    {
                                        type = "string",
                                        description = "The name of the project to scan."
                                    }
                                },
                                required = new[] { "projectName" }
                            }
                        },
                        new
                        {
                            name = "execute_custom_cypher",
                            description = "Runs a custom read-only Cypher query against Memgraph for complex, custom analysis.",
                            inputSchema = new
                            {
                                type = "object",
                                properties = new
                                {
                                    query = new
                                    {
                                        type = "string",
                                        description = "The read-only Cypher query to execute."
                                    }
                                },
                                required = new[] { "query" }
                            }
                        },
                        new
                        {
                            name = "fetch_code_snippets",
                            description = "Reads precise surgical source code snippets from the local filesystem for the specified list of nodes.",
                            inputSchema = new
                            {
                                type = "object",
                                properties = new
                                {
                                    nodes_json = new
                                    {
                                        type = "string",
                                        description = "A JSON string containing an array of node definitions. Each node object should contain properties: 'file_path', 'start_line' (0-indexed integer), and 'end_line' (0-indexed integer)."
                                    }
                                },
                                required = new[] { "nodes_json" }
                            }
                        },
                        new
                        {
                             name = "get_node_definition",
                             description = "Retrieves the structural purpose, meaning, properties, and relationships of a specified database node kind/label in the CodeExplorer ontology (e.g. 'Workspace', 'WorkspaceFolder', 'ProjectFolder', 'Project', 'File', 'Class', 'Function', 'Variable', 'Package').",
                             inputSchema = new
                             {
                                 type = "object",
                                 properties = new
                                 {
                                     kind = new
                                     {
                                         type = "string",
                                         description = "The database node label/kind to query (e.g. 'Workspace', 'WorkspaceFolder', 'ProjectFolder', 'Project', 'File', 'Class', 'Function', 'Variable', 'Package')."
                                     }
                                 },
                                required = new[] { "kind" }
                            }
                        }
                    }
                };
                break;

            case "tools/call":
                var paramsEl = root.GetProperty("params");
                var toolName = paramsEl.GetProperty("name").GetString();
                var args = paramsEl.GetProperty("arguments");

                responseResult = toolName switch
                {
                    "find_symbol" => await HandleFindSymbolAsync(args),
                    "get_project_structure" => await HandleGetProjectStructureAsync(args),
                    "get_call_chain" => await HandleGetCallChainAsync(args),
                    "resolve_interface" => await HandleResolveInterfaceAsync(args),
                    "get_impact_zone" => await HandleGetImpactZoneAsync(args),
                    "find_dead_code" => await HandleFindDeadCodeAsync(args),
                    "execute_custom_cypher" => await HandleExecuteCustomCypherAsync(args),
                    "fetch_code_snippets" => HandleFetchCodeSnippets(args),
                    "get_node_definition" => HandleGetNodeDefinition(args),
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
            return new
            {
                content = new[]
                {
                    new { type = "text", text = resultJson }
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

    private async Task<object> HandleFindSymbolAsync(JsonElement args)
    {
        if (!args.TryGetProperty("name", out var nameEl) || nameEl.ValueKind != JsonValueKind.String)
        {
            return new { isError = true, content = new[] { new { type = "text", text = "Missing or invalid 'name' argument." } } };
        }
        var name = nameEl.GetString()!;
        string? type = null;
        if (args.TryGetProperty("type", out var typeEl) && typeEl.ValueKind == JsonValueKind.String)
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

    private async Task<object> HandleGetProjectStructureAsync(JsonElement args)
    {
        if (!args.TryGetProperty("projectName", out var projectEl) || projectEl.ValueKind != JsonValueKind.String)
        {
            return new { isError = true, content = new[] { new { type = "text", text = "Missing or invalid 'projectName' argument." } } };
        }
        var projectName = projectEl.GetString()!;
        var query = "MATCH (p:Project {name: $projectName})<-[:CONTAINS*1..]-(w:Workspace) " +
                    "MATCH fileDir = (w)-[:CONTAINS*1..]->(f:File) " +
                    "WHERE (p)-[:CONTAINS*1..]->(f) " +
                    "MATCH (f)-[:CONTAINS]->(c:Class) " +
                    "RETURN w.path + '/' + reduce(s = '', x IN nodes(fileDir)[1..size(nodes(fileDir))-1] | s + CASE WHEN x.path = '' THEN '' ELSE x.path + '/' END) + f.path AS file, collect(c.name) AS classes";
        var parameters = new Dictionary<string, object> { ["projectName"] = projectName };
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

    private async Task<object> HandleResolveInterfaceAsync(JsonElement args)
    {
        if (!args.TryGetProperty("interfaceName", out var interfaceEl) || interfaceEl.ValueKind != JsonValueKind.String)
        {
            return new { isError = true, content = new[] { new { type = "text", text = "Missing or invalid 'interfaceName' argument." } } };
        }
        var interfaceName = interfaceEl.GetString()!;
        var query = "MATCH (i:Interface {name: $interfaceName})<-[:IMPLEMENTS]-(impl:Class)-[:CONTAINS]->(f:Function) " +
                    "RETURN impl.name AS className, f.name AS methodName";
        var parameters = new Dictionary<string, object> { ["interfaceName"] = interfaceName };
        return await ExecuteAndFormatQueryAsync(query, parameters);
    }

    private async Task<object> HandleGetImpactZoneAsync(JsonElement args)
    {
        if (!args.TryGetProperty("symbolName", out var symbolEl) || symbolEl.ValueKind != JsonValueKind.String)
        {
            return new { isError = true, content = new[] { new { type = "text", text = "Missing or invalid 'symbolName' argument." } } };
        }
        var symbolName = symbolEl.GetString()!;
        var query = "MATCH (target {symbol: $symbolName})<-[:USES_TYPE|CALLS]-(dependent) " +
                    "OPTIONAL MATCH (f:File)-[:CONTAINS*1..]->(dependent) " +
                    "OPTIONAL MATCH fileDir = (w:Workspace)-[:CONTAINS*1..]->(f) " +
                    "RETURN labels(dependent)[0] AS depType, dependent.name AS depName, " +
                    "CASE WHEN f IS NOT NULL AND w IS NOT NULL " +
                    "     THEN w.path + '/' + reduce(s = '', x IN nodes(fileDir)[1..size(nodes(fileDir))-1] | s + CASE WHEN x.path = '' THEN '' ELSE x.path + '/' END) + f.path " +
                    "     ELSE null " +
                    "END AS filePath";
        var parameters = new Dictionary<string, object> { ["symbolName"] = symbolName };
        return await ExecuteAndFormatQueryAsync(query, parameters);
    }

    private async Task<object> HandleFindDeadCodeAsync(JsonElement args)
    {
        if (!args.TryGetProperty("projectName", out var projectEl) || projectEl.ValueKind != JsonValueKind.String)
        {
            return new { isError = true, content = new[] { new { type = "text", text = "Missing or invalid 'projectName' argument." } } };
        }
        var projectName = projectEl.GetString()!;
        var query = "MATCH (p:Project {name: $projectName})<-[:CONTAINS*1..]-(w:Workspace) " +
                    "MATCH fileDir = (w)-[:CONTAINS*1..]->(file:File) " +
                    "WHERE (p)-[:CONTAINS*1..]->(file) " +
                    "MATCH (file)-[:CONTAINS*1..]->(f:Function) " +
                    "OPTIONAL MATCH (caller)-[:CALLS]->(f) " +
                    "WITH w, fileDir, file, f, caller " +
                    "WHERE caller IS NULL " +
                    "RETURN f.name AS unusedFunction, " +
                    "w.path + '/' + reduce(s = '', x IN nodes(fileDir)[1..size(nodes(fileDir))-1] | s + CASE WHEN x.path = '' THEN '' ELSE x.path + '/' END) + file.path AS file";
        var parameters = new Dictionary<string, object> { ["projectName"] = projectName };
        return await ExecuteAndFormatQueryAsync(query, parameters);
    }

    private async Task<object> HandleExecuteCustomCypherAsync(JsonElement args)
    {
        if (!args.TryGetProperty("query", out var queryEl) || queryEl.ValueKind != JsonValueKind.String)
        {
            return new { isError = true, content = new[] { new { type = "text", text = "Missing or invalid 'query' argument." } } };
        }
        var query = queryEl.GetString()!;
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
}
