using System.Text.Json;
using CodeExplorer.Core.Common;
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

    public async Task<string> GetArchitectureMapAsync(string? projectName = null)
    {
        string query;
        var parameters = new Dictionary<string, object>();

        if (!string.IsNullOrEmpty(projectName))
        {
            parameters["projectName"] = projectName;

            query = "MATCH (p:Project {name: $projectName}) " + "OPTIONAL MATCH (p)-[:CONTAINS]->(:DataBases)-[:USES_DB]->(db:DB) " +
                    "OPTIONAL MATCH (p)-[:CONTAINS*1..3]->(pf:ProjectFolder) " +
                    "RETURN p.name AS project, p.project_type AS type, db.name AS dbName, collect(DISTINCT pf.name) AS folders";
        }
        else
        {
            query = "MATCH (w:Workspace) " + "OPTIONAL MATCH (w)-[:CONTAINS*1..4]->(wf:WorkspaceFolder) " +
                    "OPTIONAL MATCH (w)-[:CONTAINS*1..4]->(p:Project) " + "OPTIONAL MATCH (p)-[:CONTAINS]->(:DataBases)-[:USES_DB]->(db:DB) " +
                    "RETURN w.name AS workspace, w.path AS path, collect(DISTINCT wf.name) AS workspaceFolders, collect(DISTINCT p.name) AS projects, collect(DISTINCT db.name) AS dbNames";
        }

        return await ExecuteAndFormatQueryAsync(query, parameters);
    }

    public async Task<string> GetProjectDependenciesAsync(string? projectFilter = null)
    {
        string query;
        var parameters = new Dictionary<string, object>();

        if (!string.IsNullOrEmpty(projectFilter))
        {
            parameters["projectFilter"] = projectFilter;

            query = "MATCH (p:Project {name: $projectFilter}) " + "OPTIONAL MATCH (p)-[:DEPENDS_ON]->(out) " +
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

    public async Task<string> GetFileOutlineAsync(string filePath)
    {
        var query = "MATCH (f:File) WHERE f.path ENDS_WITH $filePath OR f.file_path = $filePath " +
                    "OPTIONAL MATCH (f)-[:CONTAINS*1..]->(child) " +
                    "WHERE child:Class OR child:Interface OR child:Function OR child:Variable OR child:Query " +
                    "RETURN child.name AS name, labels(child)[0] AS type, child.start_line AS startLine, child.end_line AS endLine, child.symbol AS symbol " +
                    "ORDER BY child.start_line";
        var parameters = new Dictionary<string, object> { ["filePath"] = filePath };
        return await ExecuteAndFormatQueryAsync(query, parameters);
    }

    public async Task<string> FindSymbolAsync(string name, string? symbolType = null)
    {
        string query;
        var parameters = new Dictionary<string, object> { ["name"] = name };

        if (symbolType == "Function")
        {
            query = "MATCH (n:Function) WHERE n.name CONTAINS $name " +
                    "OPTIONAL MATCH (f:File)-[:CONTAINS*1..]->(n) " +
                    "OPTIONAL MATCH fileDir = (w:Workspace)-[:CONTAINS*1..]->(f) " +
                    "RETURN 'Function' AS type, n.name AS name, n.symbol AS fullName, " +
                    "CASE WHEN f IS NOT NULL AND w IS NOT NULL " +
                    "     THEN w.path + '/' + reduce(s = '', x IN nodes(fileDir)[1..size(nodes(fileDir))-1] | s + CASE WHEN x.path = '' THEN '' ELSE x.path + '/' END) + f.path " +
                    "     ELSE n.file_path " + "END AS filePath LIMIT 10";
        }
        else if (symbolType == "Class")
        {
            query = "MATCH (n:Class) WHERE n.name CONTAINS $name " + "OPTIONAL MATCH (f:File)-[:CONTAINS*1..]->(n) " +
                    "OPTIONAL MATCH fileDir = (w:Workspace)-[:CONTAINS*1..]->(f) " +
                    "RETURN 'Class' AS type, n.name AS name, n.symbol AS fullName, " +
                    "CASE WHEN f IS NOT NULL AND w IS NOT NULL " +
                    "     THEN w.path + '/' + reduce(s = '', x IN nodes(fileDir)[1..size(nodes(fileDir))-1] | s + CASE WHEN x.path = '' THEN '' ELSE x.path + '/' END) + f.path " +
                    "     ELSE n.file_path " + "END AS filePath LIMIT 10";
        }
        else if (symbolType == "Interface")
        {
            query = "MATCH (n:Interface) WHERE n.name CONTAINS $name " +
                    "OPTIONAL MATCH (f:File)-[:CONTAINS*1..]->(n) " +
                    "OPTIONAL MATCH fileDir = (w:Workspace)-[:CONTAINS*1..]->(f) " +
                    "RETURN 'Interface' AS type, n.name AS name, n.symbol AS fullName, " +
                    "CASE WHEN f IS NOT NULL AND w IS NOT NULL " +
                    "     THEN w.path + '/' + reduce(s = '', x IN nodes(fileDir)[1..size(nodes(fileDir))-1] | s + CASE WHEN x.path = '' THEN '' ELSE x.path + '/' END) + f.path " +
                    "     ELSE n.file_path " + "END AS filePath LIMIT 10";
        }
        else
        {
            query = "MATCH (n) WHERE (n:Function OR n:Class OR n:Interface) AND n.name CONTAINS $name " +
                    "OPTIONAL MATCH (f:File)-[:CONTAINS*1..]->(n) " +
                    "OPTIONAL MATCH fileDir = (w:Workspace)-[:CONTAINS*1..]->(f) " +
                    "RETURN labels(n)[0] AS type, n.name AS name, n.symbol AS fullName, " +
                    "CASE WHEN f IS NOT NULL AND w IS NOT NULL " +
                    "     THEN w.path + '/' + reduce(s = '', x IN nodes(fileDir)[1..size(nodes(fileDir))-1] | s + CASE WHEN x.path = '' THEN '' ELSE x.path + '/' END) + f.path " +
                    "     ELSE n.file_path " + "END AS filePath LIMIT 10";
        }

        return await ExecuteAndFormatQueryAsync(query, parameters);
    }

    public async Task<string> GetCallChainAsync(string startFunction, string endFunction, int maxDepth = 5)
    {
        var depth = Math.Max(1, Math.Min(10, maxDepth));

        var query =
            $"MATCH path = (src:Function {{symbol: $startFunction}})-[:CALLS*1..{depth}]->(tgt:Function {{symbol: $endFunction}}) " +
            "RETURN nodes(path) AS chain";

        var parameters = new Dictionary<string, object>
        {
            ["startFunction"] = startFunction, ["endFunction"] = endFunction
        };
        return await ExecuteAndFormatQueryAsync(query, parameters);
    }

    public async Task<string> ResolveCallTargetAsync(string interfaceName, string methodName)
    {
        var query =
            "MATCH (i:Interface {name: $interfaceName})<-[:IMPLEMENTS]-(impl:Class)-[:CONTAINS]->(f:Function {name: $methodName}) " +
            "RETURN impl.name AS className, f.name AS methodName, f.symbol AS methodSymbol, f.file_path AS filePath, f.start_line AS startLine";

        var parameters = new Dictionary<string, object>
        {
            ["interfaceName"] = interfaceName, ["methodName"] = methodName
        };
        return await ExecuteAndFormatQueryAsync(query, parameters);
    }

    public async Task<string> AnalyzeCodeImpactAsync(string symbolName)
    {
        var query =
            "MATCH (target) WHERE (target:Class OR target:Interface OR target:Function) AND (target.symbol = $symbolName OR target.name = $symbolName) " +
            "MATCH (target)<-[:USES_TYPE|CALLS]-(dependent) " +
            "OPTIONAL MATCH (f:File)-[:CONTAINS*1..]->(dependent) " +
            "OPTIONAL MATCH fileDir = (w:Workspace)-[:CONTAINS*1..]->(f) " +
            "RETURN labels(dependent)[0] AS dependentType, dependent.name AS dependentName, dependent.symbol AS dependentSymbol, " +
            "CASE WHEN f IS NOT NULL AND w IS NOT NULL " +
            "     THEN w.path + '/' + reduce(s = '', x IN nodes(fileDir)[1..size(nodes(fileDir))-1] | s + CASE WHEN x.path = '' THEN '' ELSE x.path + '/' END) + f.path " +
            "     ELSE null " + "END AS filePath";
        var parameters = new Dictionary<string, object> { ["symbolName"] = symbolName };
        return await ExecuteAndFormatQueryAsync(query, parameters);
    }

    public async Task<string> InspectDataLineageAsync(string tableName)
    {
        var query = "MATCH (t:Table {name: $tableName}) " + "OPTIONAL MATCH (q:Query)-[:DEPENDS_ON]->(t) " +
                    "OPTIONAL MATCH (parent)-[:CONTAINS]->(q) " +
                    "OPTIONAL MATCH (caller)-[:CALLS|DEPENDS_ON*0..]->(parent) " +
                    "RETURN t.name AS tableName, q.name AS queryName, q.query_text AS queryText, q.path AS filePath, " +
                    "collect(DISTINCT parent.name) AS parentName, labels(parent)[0] AS parentType, " +
                    "collect(DISTINCT caller.name) AS callingSymbols";
        var parameters = new Dictionary<string, object> { ["tableName"] = tableName };
        return await ExecuteAndFormatQueryAsync(query, parameters);
    }

    public async Task<string> GetProjectEntryPointsAsync(string projectName)
    {
        var query = "MATCH (p:Project {name: $projectName})-[:CONTAINS*1..]->(f:File) " +
                    "MATCH (f)-[:CONTAINS*1..]->(func:Function) " +
                    "WHERE f.path CONTAINS 'Controller' OR f.path CONTAINS 'Endpoint' OR f.path CONTAINS 'Handler' OR f.path CONTAINS 'Resolver' " +
                    "OR func.name STARTS WITH 'On' OR func.name STARTS WITH 'Handle' " +
                    "OPTIONAL MATCH (class:Class)-[:CONTAINS]->(func) " +
                    "RETURN func.name AS entryPoint, func.symbol AS symbol, class.name AS className, f.path AS filePath, func.start_line AS startLine";
        var parameters = new Dictionary<string, object> { ["projectName"] = projectName };
        return await ExecuteAndFormatQueryAsync(query, parameters);
    }

    public async Task<string> FindRefactoringOpportunitiesAsync(string projectName, string metricType = "all")
    {
        var results = new List<object>();

        if (metricType == "dead_code" || metricType == "all")
        {
            var deadCodeQuery = "MATCH (p:Project {name: $projectName})-[:CONTAINS*1..]->(f:File) " +
                                "MATCH (f)-[:CONTAINS*1..]->(item) " + "WHERE (item:Function OR item:Class) " +
                                "OPTIONAL MATCH (caller:Entity)-[:CALLS|USES_TYPE]->(item) " + "WITH f, item, caller " +
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
                                  "MATCH (f)-[:CONTAINS*1..]->(c:Class) " + "MATCH (c)-[:CONTAINS]->(member) " +
                                  "WITH c, f, count(member) AS memberCount " + "WHERE memberCount > 15 " +
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

        return JsonSerializer.Serialize(new { results }, new JsonSerializerOptions { WriteIndented = true });
    }

    public async Task<string> ExecuteCustomReadCypherAsync(string query)
    {
        var lowerQuery = query.ToLowerInvariant();

        if (lowerQuery.Contains("create") || lowerQuery.Contains("delete") || lowerQuery.Contains("set") ||
            lowerQuery.Contains("merge") || lowerQuery.Contains("remove") || lowerQuery.Contains("drop") ||
            lowerQuery.Contains("detach"))
        {
            throw new InvalidOperationException("Security violation: Mutating queries are not allowed.");
        }

        return await ExecuteAndFormatQueryAsync(query);
    }

    public async Task<string> GetWorkspaceContentAsync(string? workspacePath, string? type)
    {
        string query;
        var parameters = new Dictionary<string, object?>();

        if (!string.IsNullOrEmpty(workspacePath))
        {
            var resolvedPath = PathTools.TranslateHostPathToContainerPath(workspacePath);
            var absolutePath = Path.GetFullPath(resolvedPath).Replace('\\', '/');
            parameters["workspacePath"] = absolutePath;
            parameters["type"] = string.IsNullOrEmpty(type) ? null : type;

            query = @"
                MATCH (r:Root {path: $workspacePath})-[:CONTAINS*0..]->(n)
                WHERE $type IS NULL OR $type = '' OR any(lbl IN labels(n) WHERE lbl = $type)
                RETURN n LIMIT 1000";
        }
        else
        {
            parameters["type"] = string.IsNullOrEmpty(type) ? null : type;

            query = @"
                MATCH (n)
                WHERE $type IS NULL OR $type = '' OR any(lbl IN labels(n) WHERE lbl = $type)
                RETURN n LIMIT 1000";
        }

        return await dbClient.ExecuteQueryAsync(query, parameters);
    }

    public async Task<string> ExecuteRawQueryAsync(string query, Dictionary<string, object?>? parameters = null)
    {
        return await dbClient.ExecuteQueryAsync(query, parameters);
    }

    public async Task<string> GetTaxonomyAsync()
    {
        var query =
            "MATCH (n)-[r]->(m) WITH DISTINCT labels(n)[0] AS fromLabel, type(r) AS relType, labels(m)[0] AS toLabel RETURN fromLabel, relType, toLabel";
        var resultJson = await dbClient.ExecuteQueryAsync(query);
        var parsedTriplets = JsonSerializer.Deserialize<List<Dictionary<string, string>>>(resultJson) ?? [];

        var propQuery =
            "MATCH (n) WITH DISTINCT labels(n) AS labels, keys(n) AS keys UNWIND labels AS label UNWIND keys AS key RETURN DISTINCT label, key";
        var propJson = await dbClient.ExecuteQueryAsync(propQuery);
        var parsedProperties = JsonSerializer.Deserialize<List<Dictionary<string, string>>>(propJson) ?? [];

        var taxonomy = BuildTaxonomy(parsedTriplets, parsedProperties);
        return JsonSerializer.Serialize(new { taxonomy }, new JsonSerializerOptions { WriteIndented = true });
    }

    public async Task<string> FetchCodeSnippetsAsync(string nodesJson)
    {
        return await FetchCodeSnippetsDirectlyAsync(nodesJson);
    }

    public string GetNodeDefinition(string kind)
    {
        var kindLower = kind.Trim().ToLowerInvariant();

        return kindLower switch
        {
            "workspace" => "### Kind: Workspace\n" +
                           "**Purpose**: Represents the absolute root of the workspace directory hierarchy.\n" +
                           "**Key Properties**:\n" + "  - `name` (string): The workspace root folder name.\n" +
                           "  - `path` (string): The absolute filesystem path of the workspace.\n" +
                           "**Relationships**:\n" + "  - `(Workspace)-[:CONTAINS]->(WorkspaceFolder)`\n" +
                           "  - `(Workspace)-[:CONTAINS]->(Project)`\n" +
                           "  - `(Workspace)-[:CONTAINS]->(File)` (if a source file sits at the root directory)",

            "workspacefolder" => "### Kind: WorkspaceFolder\n" +
                                 "**Purpose**: Represents a subdirectory inside a Workspace, housing projects or other folders outside projects. Cannot contain files directly (files outside projects are ignored).\n" +
                                 "**Key Properties**:\n" + "  - `name` (string): The folder name.\n" +
                                 "  - `path` (string): The local folder name relative to its immediate parent container.\n" +
                                 "**Relationships**:\n" +
                                 "  - `(Workspace|WorkspaceFolder)-[:CONTAINS]->(WorkspaceFolder)`\n" +
                                 "  - `(WorkspaceFolder)-[:CONTAINS]->(Project)`",

            "project" => "### Kind: Project\n" +
                          "**Purpose**: Represents a buildable/compilable module or package directory (e.g. C# project, Go module, TS library, Python package).\n" +
                          "**Key Properties**:\n" + "  - `name` (string): The project name.\n" +
                          "  - `path` (string): The local project folder name relative to its parent container (empty string at root).\n" +
                          "  - `project_type` (string): The language/signature identifier (e.g., 'csharp', 'go', 'python', 'typescript').\n" +
                          "**Relationships**:\n" + "  - `(Workspace|WorkspaceFolder)-[:CONTAINS]->(Project)`\n" +
                          "  - `(Project)-[:CONTAINS]->(Files)`\n" + 
                          "  - `(Project)-[:CONTAINS]->(DataBases)`\n" + 
                          "  - `(Project)-[:CONTAINS]->(ApisInUse)`\n" + 
                          "  - `(Project)-[:CONTAINS]->(CloudServices)`\n" + 
                          "  - `(Project)-[:CONTAINS]->(Dependencies)`\n" + 
                          "  - `(Project)-[:CONTAINS]->(EntryPoints)`\n" + 
                          "  - `(Project)-[:DEPENDS_ON]->(Project)`\n" + "  - `(Project)-[:DEPENDS_ON]->(Package)`",

            "projectfolder" => "### Kind: ProjectFolder\n" +
                               "**Purpose**: Represents a subdirectory inside a Project, containing files and other project folders.\n" +
                               "**Key Properties**:\n" + "  - `name` (string): The folder name.\n" +
                               "  - `path` (string): The local folder name relative to its immediate parent container.\n" +
                               "**Relationships**:\n" + "  - `(Files|ProjectFolder)-[:CONTAINS]->(ProjectFolder)`\n" +
                               "  - `(ProjectFolder)-[:CONTAINS]->(File)`",

            "package" => "### Kind: Package\n" +
                          "**Purpose**: Represents an external dependency package or workspace package referenced or produced by projects.\n" +
                          "**Key Properties**:\n" +
                          "  - `name` (string): The package name (e.g. 'neo4j.driver', 'react', 'CodeExplorer.Core').\n" +
                          "  - `version` (string): The package version.\n" +
                          "  - `type` (string): The package type identifier ('nuget', 'npm', 'go').\n" +
                          "**Relationships**:\n" + "  - `(Dependencies)-[:DEPENDS_ON]->(Package)` (for external dependencies)\n" +
                          "  - `(Project)-[:DEPENDS_ON]->(Package)` (for produced packages)\n" +
                          "  - `(Package)-[:IMPLEMENTED_BY]->(Project)`",

            "dependencies" => "### Kind: Dependencies\n" +
                              "**Purpose**: Represents an intermediate node grouping external packages / third-party dependencies of a project.\n" +
                              "**Key Properties**:\n" +
                              "  - `name` (string): Constant name 'Dependencies'.\n" +
                              "  - `path` (string): The path of the parent project.\n" +
                              "**Relationships**:\n" +
                              "  - `(Project)-[:CONTAINS]->(Dependencies)`\n" +
                              "  - `(Dependencies)-[:DEPENDS_ON]->(Package)`",

            "files" => "### Kind: Files\n" +
                       "**Purpose**: Represents an intermediate node grouping all source code files and folders of a project.\n" +
                       "**Key Properties**:\n" +
                       "  - `name` (string): Constant name 'Files'.\n" +
                       "  - `path` (string): The path of the parent project.\n" +
                       "**Relationships**:\n" +
                       "  - `(Project)-[:CONTAINS]->(Files)`\n" +
                       "  - `(Files)-[:CONTAINS]->(ProjectFolder)`\n" +
                       "  - `(Files)-[:CONTAINS]->(File)`",

            "databases" => "### Kind: DataBases\n" +
                           "**Purpose**: Represents an intermediate node grouping all databases used by a project.\n" +
                           "**Key Properties**:\n" +
                           "  - `name` (string): Constant name 'DataBases'.\n" +
                           "  - `path` (string): The path of the parent project.\n" +
                           "**Relationships**:\n" +
                           "  - `(Project)-[:CONTAINS]->(DataBases)`\n" +
                           "  - `(DataBases)-[:USES_DB]->(DB)`",

            "apisinuse" => "### Kind: ApisInUse\n" +
                           "**Purpose**: Represents an intermediate node grouping all external APIs used by a project.\n" +
                           "**Key Properties**:\n" +
                           "  - `name` (string): Constant name 'ApisInUse'.\n" +
                           "  - `path` (string): The path of the parent project.\n" +
                           "**Relationships**:\n" +
                           "  - `(Project)-[:CONTAINS]->(ApisInUse)`\n" +
                           "  - `(ApisInUse)-[:USES_API]->(ApiInUse)`",

            "cloudservices" => "### Kind: CloudServices\n" +
                               "**Purpose**: Represents an intermediate node grouping all cloud services used by a project.\n" +
                               "**Key Properties**:\n" +
                               "  - `name` (string): Constant name 'CloudServices'.\n" +
                               "  - `path` (string): The path of the parent project.\n" +
                               "**Relationships**:\n" +
                               "  - `(Project)-[:CONTAINS]->(CloudServices)`\n" +
                               "  - `(CloudServices)-[:USES_CLOUD]->(CloudService)`",

            "db" => "### Kind: DB\n" +
                    "**Purpose**: Represents a database server or instance used by the project.\n" +
                    "**Key Properties**:\n" +
                    "  - `name` (string): The name of the database engine (e.g. 'PostgreSQL', 'MongoDB').\n" +
                    "**Relationships**:\n" +
                    "  - `(DataBases)-[:USES_DB]->(DB)`\n" +
                    "  - `(File|Class|Function)-[:USES_DB]->(DB)`",

            "apiinuse" => "### Kind: ApiInUse\n" +
                          "**Purpose**: Represents an external API library or client service used by the project (e.g. NestJS, Axios, HttpClient).\n" +
                          "**Key Properties**:\n" +
                          "  - `name` (string): The name of the API library or service.\n" +
                          "**Relationships**:\n" +
                          "  - `(ApisInUse)-[:USES_API]->(ApiInUse)`\n" +
                          "  - `(File|Class|Function)-[:USES_API]->(ApiInUse)`",

            "cloudservice" => "### Kind: CloudService\n" +
                              "**Purpose**: Represents a cloud provider service used by the project (e.g. AWS S3, Stripe, Firebase).\n" +
                              "**Key Properties**:\n" +
                              "  - `name` (string): The name of the cloud service.\n" +
                              "**Relationships**:\n" +
                              "  - `(CloudServices)-[:USES_CLOUD]->(CloudService)`\n" +
                              "  - `(File|Class|Function)-[:USES_CLOUD]->(CloudService)`",

            "file" => "### Kind: File\n" + "**Purpose**: Represents a source code file containing parsable content.\n" +
                      "**Key Properties**:\n" + "  - `name` (string): The filename basename.\n" +
                      "  - `path` (string): The filename relative to its immediate parent container folder.\n" +
                      "**Relationships**:\n" + "  - `(Files|ProjectFolder)-[:CONTAINS]->(File)`\n" +
                      "  - `(File)-[:CONTAINS]->(Class)`\n" + "  - `(File)-[:CONTAINS]->(Interface)`\n" +
                      "  - `(File)-[:CONTAINS]->(Function)`",

            "class" => "### Kind: Class\n" +
                       "**Purpose**: Represents a parsed OOP class, struct, or concrete type definition.\n" +
                       "**Key Properties**:\n" + "  - `name` (string): The name of the class.\n" +
                       "  - `symbol` (string): A globally unique ID for this symbol scope.\n" +
                       "  - `start_line` / `end_line` (integer): The bounds of the class definition.\n" +
                       "  - `file_path` (string): The relative path of the declaring file.\n" + "**Relationships**:\n" +
                       "  - `(File)-[:CONTAINS]->(Class)`\n" + "  - `(Class)-[:USES_TYPE]->(Class|Interface)`\n" +
                       "  - `(Class)-[:IMPLEMENTS]->(Interface)`\n" + "  - `(Class)-[:INHERITS_FROM]->(Class)`",

            "interface" => "### Kind: Interface\n" +
                           "**Purpose**: Represents a parsed OOP interface contract (e.g. C# interface, Go interface, TypeScript interface).\n" +
                           "**Key Properties**:\n" + "  - `name` (string): The name of the interface.\n" +
                           "  - `symbol` (string): A globally unique ID for this symbol scope.\n" +
                           "  - `start_line` / `end_line` (integer): The bounds of the interface definition.\n" +
                           "  - `file_path` (string): The relative path of the declaring file.\n" +
                           "**Relationships**:\n" + "  - `(File)-[:CONTAINS]->(Interface)`\n" +
                           "  - `(Class)-[:IMPLEMENTS]->(Interface)`\n" +
                           "  - `(Interface)-[:INHERITS_FROM]->(Interface)`",

            "function" => "### Kind: Function\n" +
                          "**Purpose**: Represents a parsed method, function, subroutine, or procedure.\n" +
                          "**Key Properties**:\n" + "  - `name` (string): The name of the function.\n" +
                          "  - `symbol` (string): A globally unique ID for this symbol scope.\n" +
                          "  - `start_line` / `end_line` (integer): The bounds of the function definition.\n" +
                          "  - `file_path` (string): The relative path of the declaring file.\n" +
                          "**Relationships**:\n" + "  - `(File|Class|Interface)-[:CONTAINS]->(Function)`\n" +
                          "  - `(Function)-[:CALLS]->(Function)`\n" +
                          "  - `(Function)-[:USES_TYPE]->(Class|Interface)`",

            "variable" => "### Kind: Variable\n" +
                          "**Purpose**: Represents a declared field, variable, parameter, or property parsed from the AST.\n" +
                          "**Key Properties**:\n" + "  - `name` (string): The name of the variable.\n" +
                          "  - `symbol` (string): A globally unique ID for this symbol scope.\n" +
                          "  - `start_line` / `end_line` (integer): The bounds of the variable declaration.\n" +
                          "  - `file_path` (string): The relative path of the declaring file.\n" +
                          "**Relationships**:\n" + "  - `(Class|Interface|Function)-[:CONTAINS]->(Variable)`",

            "gitsettings" => "### Kind: GitSettings\n" +
                             "**Purpose**: Represents the Git repository configuration settings for the workspace.\n" +
                             "**Key Properties**:\n" +
                             "  - `name` (string): Constant name 'Git Settings'.\n" +
                             "  - `branch` (string): The currently checked-out branch name.\n" +
                             "  - `origin_url` (string): The remote origin repository URL.\n" +
                             "  - `user_name` (string): The git user name.\n" +
                             "  - `user_email` (string): The git user email address.\n" +
                             "**Relationships**:\n" +
                             "  - `(Workspace)-[:CONTAINS]->(GitSettings)`",

            "entrypoint" => "### Kind: EntryPoint\n" +
                            "**Purpose**: Represents an exposed API route, message listener, or application entry point.\n" +
                            "**Key Properties**:\n" +
                            "  - `name` (string): The endpoint name/method/path (e.g. 'GET /api/orders').\n" +
                            "  - `protocol` (string): The communication protocol ('http', 'ws', 'event').\n" +
                            "  - `route_or_topic` (string): The routing path or message topic.\n" +
                            "**Relationships**:\n" +
                            "  - `(EntryPoints)-[:EXPOSES]->(EntryPoint)`\n" +
                            "  - `(EntryPoint)-[:IMPLEMENTED_BY]->(Function)`",

            "entrypoints" => "### Kind: EntryPoints\n" +
                             "**Purpose**: Represents an intermediate node grouping all EntryPoint / API definition nodes of a project.\n" +
                             "**Key Properties**:\n" +
                             "  - `name` (string): Constant name 'EntryPoints'.\n" +
                             "  - `path` (string): The path of the parent project.\n" +
                             "**Relationships**:\n" +
                             "  - `(Project)-[:CONTAINS]->(EntryPoints)`\n" +
                             "  - `(EntryPoints)-[:EXPOSES]->(EntryPoint)`",

             _ =>
                $"Unknown node kind: '{kind}'. Active ontological kinds in CodeExplorer are: 'Workspace', 'WorkspaceFolder', 'ProjectFolder', 'Project', 'Files', 'DataBases', 'ApisInUse', 'CloudServices', 'File', 'Class', 'Function', 'Variable', 'Package', 'Dependencies', 'EntryPoints', 'EntryPoint', 'ApiInUse', 'CloudService', 'DB', 'GitSettings'."
        };
    }

    private async Task<string> FetchCodeSnippetsDirectlyAsync(string nodesJson)
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

        var hostWorkspacePath = "";

        try
        {
            var workspaceResultJson =
                await dbClient.ExecuteQueryAsync("MATCH (w:Workspace) RETURN w.path AS path LIMIT 1");
            using var doc = JsonDocument.Parse(workspaceResultJson);

            if (doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() > 0)
            {
                var row = doc.RootElement[0];

                if (row.TryGetProperty("path", out var pathProp) && pathProp.ValueKind == JsonValueKind.String)
                {
                    hostWorkspacePath = pathProp.GetString() ?? "";
                }
            }
        }
        catch
        {
            // Ignore database query errors and proceed with empty path
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

            var joinedPath = Path.Combine(workspaceRoot, relativePath);
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
