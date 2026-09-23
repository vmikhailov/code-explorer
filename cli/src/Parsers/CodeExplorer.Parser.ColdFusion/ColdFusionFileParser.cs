using System.Text.RegularExpressions;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.ColdFusion;

public class ColdFusionFileParser : IFileParser
{
    public string LanguageName => "coldfusion";

    public bool UsesTreeSitter => false;

    public IReadOnlyList<ILibraryParser> LibraryParsers => [];

    public bool CanParse(string fileExtension)
    {
        return fileExtension.Equals(".cfc", StringComparison.OrdinalIgnoreCase) ||
               fileExtension.Equals(".cfm", StringComparison.OrdinalIgnoreCase) ||
               fileExtension.Equals(".cfml", StringComparison.OrdinalIgnoreCase);
    }

    public BaseParserVisitor CreateVisitor(
        Node rootNode,
        List<ILibraryParser> activeLibraryParsers,
        string relativePath,
        string absoluteWorkspacePath,
        IFileParser fileParser,
        LibraryTrieRegistry libraryRegistry)
    {
        throw new NotSupportedException("ColdFusion parser uses high-resilience native C# token analysis.");
    }

    public ImportType ResolveImportType(string importPath, string filePath, string? absoluteWorkspacePath)
    {
        return ImportType.Internal;
    }

    public async Task<SyntaxTree> ParseAsync(string filePath, string parentNodeId, string workspaceId, string absoluteWorkspacePath)
    {
        var relativePath = Path.GetRelativePath(absoluteWorkspacePath, filePath).Replace('\\', '/');
        var fileName = Path.GetFileName(filePath);
        var fileNodeId = $"{workspaceId}:file:{relativePath}";

        var fileNode = new FileNode(fileNodeId, fileName, relativePath, filePath);
        var content = await File.ReadAllTextAsync(filePath);

        var isCfc = filePath.EndsWith(".cfc", StringComparison.OrdinalIgnoreCase);
        var defaultDs = ColdFusionProjectParser.GetDefaultDatasource(Path.GetDirectoryName(filePath) ?? "") ?? "default";

        var rawImports = new List<RawImport>();
        var rawTypeBindings = new List<RawTypeBinding>();

        // 1. Component Extraction (for .cfc files)
        TypeNode? componentNode = null;
        if (isCfc)
        {
            var componentName = Path.GetFileNameWithoutExtension(filePath);
            var componentId = $"{workspaceId}:cfc:{relativePath.Replace('/', '.').Replace('\\', '.')}";

            string? extendsName = null;
            var compTagMatch = Regex.Match(content, @"<\s*cfcomponent\b([^>]*)>", RegexOptions.IgnoreCase);
            if (compTagMatch.Success)
            {
                var attrs = compTagMatch.Groups[1].Value;
                var extMatch = Regex.Match(attrs, @"\bextends\s*=\s*['""]([^'""]+)['""]", RegexOptions.IgnoreCase);
                if (extMatch.Success) extendsName = extMatch.Groups[1].Value.Trim();
            }
            else
            {
                var scriptCompMatch = Regex.Match(content, @"\bcomponent\b(?:\s+extends\s*=\s*['""]?([a-zA-Z0-9_\.]+)['""]?)?", RegexOptions.IgnoreCase);
                if (scriptCompMatch.Success && scriptCompMatch.Groups[1].Success)
                {
                    extendsName = scriptCompMatch.Groups[1].Value.Trim();
                }
            }

            var extensions = extendsName != null ? new Dictionary<string, string> { ["extends"] = extendsName } : null;
            componentNode = new TypeNode(componentId, componentName, componentName, relativePath, relativePath, 1, 1, 0, 0, "class", extensions);
            fileNode.Children.Add(componentNode);
        }
        else
        {
            // For .cfm web pages (not starting with '_' underscore partial template convention), expose as HTTP endpoint
            if (!fileName.StartsWith('_') && !fileName.Equals("Application.cfm", StringComparison.OrdinalIgnoreCase))
            {
                var route = $"/{relativePath}";
                var endpointId = $"{workspaceId}:endpoint:GET:{route.ToLowerInvariant()}";
                var endpointNode = new EndpointNode(
                    endpointId,
                    $"GET {route}",
                    relativePath,
                    "GET",
                    route,
                    "HTTP"
                );
                fileNode.Children.Add(endpointNode);
            }
        }

        // 2. Function & Remote Endpoint Extraction
        ParseFunctionsAndEndpoints(content, fileNode, componentNode, relativePath, workspaceId);

        // 3. Database Queries (<cfquery> & queryExecute)
        ParseQueriesAndDatabases(content, fileNode, relativePath, defaultDs, workspaceId);

        // 4. Stored Procedures (<cfstoredproc>)
        ParseStoredProcedures(content, fileNode, relativePath, defaultDs, workspaceId);

        // 5. External Services (HTTP & WebServices)
        ParseExternalServices(content, fileNode, relativePath, workspaceId);

        // 6. Calls and Inclusions
        ParseInclusionsAndCalls(content, relativePath, rawImports, rawTypeBindings);

        return new SyntaxTree(filePath, relativePath, null, null, null, fileNode, this, rawImports, [], rawTypeBindings);
    }

    private static void ParseFunctionsAndEndpoints(
        string content,
        FileNode fileNode,
        TypeNode? componentNode,
        string relativePath,
        string workspaceId)
    {
        var componentName = componentNode?.Name ?? Path.GetFileNameWithoutExtension(relativePath);

        // A) Tag-based <cffunction>
        var funcMatches = Regex.Matches(content, @"<\s*cffunction\b([^>]*?)>(?:(.*?)</\s*cffunction\s*>)?", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        foreach (Match match in funcMatches)
        {
            var attrs = match.Groups[1].Value;
            var nameMatch = Regex.Match(attrs, @"\bname\s*=\s*['""]([^'""]+)['""]", RegexOptions.IgnoreCase);
            if (!nameMatch.Success) continue;

            var funcName = nameMatch.Groups[1].Value.Trim();
            var accessMatch = Regex.Match(attrs, @"\baccess\s*=\s*['""]([^'""]+)['""]", RegexOptions.IgnoreCase);
            var access = accessMatch.Success ? accessMatch.Groups[1].Value.Trim().ToLowerInvariant() : "public";

            var funcId = $"{workspaceId}:func:{relativePath}:{funcName}";
            var funcNode = new FunctionNode(funcId, funcName, funcName, relativePath, relativePath, 1, 1, 0, 0);

            if (componentNode != null)
            {
                componentNode.Children.Add(funcNode);
            }
            else
            {
                fileNode.Children.Add(funcNode);
            }

            if (access == "remote")
            {
                var route = $"/{componentName}/{funcName}";
                var endpointId = $"{workspaceId}:endpoint:POST:{route.ToLowerInvariant()}";
                var endpointNode = new EndpointNode(
                    endpointId,
                    $"POST {route}",
                    relativePath,
                    "POST",
                    route,
                    "REST"
                );
                fileNode.Children.Add(endpointNode);
            }
        }

        // B) Script-based functions
        var scriptFuncMatches = Regex.Matches(content, @"(?:(remote|public|private|package)\s+)?(?:[a-zA-Z0-9_\.]+\s+)?function\s+([a-zA-Z0-9_]+)\s*\(", RegexOptions.IgnoreCase);
        foreach (Match match in scriptFuncMatches)
        {
            var access = match.Groups[1].Success ? match.Groups[1].Value.Trim().ToLowerInvariant() : "public";
            var funcName = match.Groups[2].Value.Trim();

            // Check if already captured by tag-based parser
            var funcId = $"{workspaceId}:func:{relativePath}:{funcName}";
            if (fileNode.Children.Any(c => c.Id == funcId) || (componentNode != null && componentNode.Children.Any(c => c.Id == funcId)))
            {
                continue;
            }

            var funcNode = new FunctionNode(funcId, funcName, funcName, relativePath, relativePath, 1, 1, 0, 0);
            if (componentNode != null)
            {
                componentNode.Children.Add(funcNode);
            }
            else
            {
                fileNode.Children.Add(funcNode);
            }

            if (access == "remote")
            {
                var route = $"/{componentName}/{funcName}";
                var endpointId = $"{workspaceId}:endpoint:POST:{route.ToLowerInvariant()}";
                var endpointNode = new EndpointNode(
                    endpointId,
                    $"POST {route}",
                    relativePath,
                    "POST",
                    route,
                    "REST"
                );
                fileNode.Children.Add(endpointNode);
            }
        }
    }

    private static void ParseQueriesAndDatabases(
        string content,
        FileNode fileNode,
        string relativePath,
        string defaultDs,
        string workspaceId)
    {
        var dbNodes = new Dictionary<string, DatabaseNode>(StringComparer.OrdinalIgnoreCase);

        // A) Tag-based <cfquery>
        var queryMatches = Regex.Matches(content, @"<\s*cfquery\b([^>]*)>(.*?)</\s*cfquery\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        int queryIdx = 0;

        foreach (Match match in queryMatches)
        {
            queryIdx++;
            var attrs = match.Groups[1].Value;
            var sqlBody = match.Groups[2].Value;

            // Skip dbtype="query" (in-memory queries on recordsets)
            if (Regex.IsMatch(attrs, @"\bdbtype\s*=\s*['""]query['""]", RegexOptions.IgnoreCase))
            {
                continue;
            }

            var dsMatch = Regex.Match(attrs, @"\bdatasource\s*=\s*['""]?([^'"">\s]+)['""]?", RegexOptions.IgnoreCase);
            var dsName = CleanDatasourceName(dsMatch.Success ? dsMatch.Groups[1].Value : null, defaultDs);

            var nameMatch = Regex.Match(attrs, @"\bname\s*=\s*['""]([^'""]+)['""]", RegexOptions.IgnoreCase);
            var queryName = nameMatch.Success ? nameMatch.Groups[1].Value.Trim() : $"query_{queryIdx}";

            // Sanitize SQL
            var cleanSql = CleanColdFusionSql(sqlBody);
            var queryId = $"{workspaceId}:query:{relativePath}:{queryName}";

            var queryNode = NestedSqlParser.ParseNestedSql(cleanSql, queryId, relativePath);
            if (queryNode != null)
            {
                // Ensure Database node is associated with the explicit datasource
                EnsureDatasourceHierarchy(queryNode, dsName, relativePath, workspaceId, dbNodes);
                fileNode.Children.Add(queryNode);
            }
        }

        // B) Script-based queryExecute
        var scriptQueryMatches = Regex.Matches(content, @"queryExecute\s*\(\s*(['""][\s\S]*?['""])\s*(?:,\s*(\[[^\]]*\]|\{[^\}]*\}))?(?:,\s*(\{[^\}]*\}))?\s*\)", RegexOptions.IgnoreCase);
        foreach (Match match in scriptQueryMatches)
        {
            queryIdx++;
            var sqlLiteral = match.Groups[1].Value;
            var optionsStruct = match.Groups[3].Success ? match.Groups[3].Value : "";

            var dsMatch = Regex.Match(optionsStruct, @"datasource\s*[:=]\s*['""]?([^'"">\s,]+)['""]?", RegexOptions.IgnoreCase);
            var dsName = CleanDatasourceName(dsMatch.Success ? dsMatch.Groups[1].Value : null, defaultDs);

            var cleanSql = CleanColdFusionSql(sqlLiteral.Trim('\'', '"'));
            var queryId = $"{workspaceId}:query:{relativePath}:queryExecute_{queryIdx}";

            var queryNode = NestedSqlParser.ParseNestedSql(cleanSql, queryId, relativePath);
            if (queryNode != null)
            {
                EnsureDatasourceHierarchy(queryNode, dsName, relativePath, workspaceId, dbNodes);
                fileNode.Children.Add(queryNode);
            }
        }

        // Add collected Database nodes to fileNode.Children
        foreach (var dbNode in dbNodes.Values)
        {
            if (!fileNode.Children.Any(c => c.Id == dbNode.Id))
            {
                fileNode.Children.Add(dbNode);
            }
        }
    }

    private static void ParseStoredProcedures(
        string content,
        FileNode fileNode,
        string relativePath,
        string defaultDs,
        string workspaceId)
    {
        var procMatches = Regex.Matches(content, @"<\s*cfstoredproc\b([^>]*)>", RegexOptions.IgnoreCase);
        foreach (Match match in procMatches)
        {
            var attrs = match.Groups[1].Value;
            var procMatch = Regex.Match(attrs, @"\bprocedure\s*=\s*['""]([^'""]+)['""]", RegexOptions.IgnoreCase);
            if (!procMatch.Success) continue;

            var procName = procMatch.Groups[1].Value.Trim();
            var dsMatch = Regex.Match(attrs, @"\bdatasource\s*=\s*['""]?([^'"">\s]+)['""]?", RegexOptions.IgnoreCase);
            var dsName = CleanDatasourceName(dsMatch.Success ? dsMatch.Groups[1].Value : null, defaultDs);

            var dbNodeId = $"{workspaceId}:db:{dsName.ToLowerInvariant()}";
            var dbNode = fileNode.Children.OfType<DatabaseNode>().FirstOrDefault(d => d.Id == dbNodeId);
            if (dbNode == null)
            {
                dbNode = new DatabaseNode(dbNodeId, dsName, relativePath, "relational");
                fileNode.Children.Add(dbNode);
            }

            var procId = $"{dbNode.Id}:proc:{procName.ToLowerInvariant()}";
            var procNode = new ProcedureNode(procId, procName, relativePath);
            dbNode.Children.Add(procNode);
        }
    }

    private static void ParseExternalServices(
        string content,
        FileNode fileNode,
        string relativePath,
        string workspaceId)
    {
        var extCount = 0;

        // A) <cfhttp url="..." method="...">
        var httpMatches = Regex.Matches(content, @"<\s*cfhttp\b([^>]*)>", RegexOptions.IgnoreCase);
        foreach (Match match in httpMatches)
        {
            extCount++;
            var attrs = match.Groups[1].Value;
            var urlMatch = Regex.Match(attrs, @"\burl\s*=\s*['""]([^'""]+)['""]", RegexOptions.IgnoreCase);
            var methodMatch = Regex.Match(attrs, @"\bmethod\s*=\s*['""]([^'""]+)['""]", RegexOptions.IgnoreCase);

            var url = urlMatch.Success ? urlMatch.Groups[1].Value.Trim() : "http-call";
            var method = methodMatch.Success ? methodMatch.Groups[1].Value.Trim().ToUpperInvariant() : "GET";

            var cleanUrl = CleanUrl(url);
            var serviceId = $"{workspaceId}:externalservice:http:{relativePath}:{extCount}";
            var extExtensions = new Dictionary<string, string> { ["method"] = method };
            var extNode = new ExternalServiceNode(serviceId, $"{method} {cleanUrl}", "http", cleanUrl, relativePath, extExtensions);
            fileNode.Children.Add(extNode);
        }

        // B) SOAP / WSDL WebService: createObject("webservice", "http://...wsdl")
        var soapMatches = Regex.Matches(content, @"createObject\s*\(\s*['""]webservice['""]\s*,\s*['""]([^'""]+)['""]", RegexOptions.IgnoreCase);
        foreach (Match match in soapMatches)
        {
            extCount++;
            var wsdlUrl = match.Groups[1].Value.Trim();
            var serviceId = $"{workspaceId}:externalservice:soap:{relativePath}:{extCount}";
            var extNode = new ExternalServiceNode(serviceId, $"SOAP {wsdlUrl}", "soap", wsdlUrl, relativePath);
            fileNode.Children.Add(extNode);
        }

        // C) <cfinvoke webservice="...">
        var wsdlInvokeMatches = Regex.Matches(content, @"<\s*cfinvoke\b[^>]*\bwebservice\s*=\s*['""]([^'""]+)['""]", RegexOptions.IgnoreCase);
        foreach (Match match in wsdlInvokeMatches)
        {
            extCount++;
            var wsdlUrl = match.Groups[1].Value.Trim();
            var serviceId = $"{workspaceId}:externalservice:soap:{relativePath}:{extCount}";
            var extNode = new ExternalServiceNode(serviceId, $"SOAP {wsdlUrl}", "soap", wsdlUrl, relativePath);
            fileNode.Children.Add(extNode);
        }
    }

    private static void ParseInclusionsAndCalls(
        string content,
        string relativePath,
        List<RawImport> rawImports,
        List<RawTypeBinding> rawTypeBindings)
    {
        // 1. <cfinclude template="...">
        var includeMatches = Regex.Matches(content, @"<\s*cfinclude\b[^>]*\btemplate\s*=\s*['""]([^'""]+)['""]", RegexOptions.IgnoreCase);
        foreach (Match match in includeMatches)
        {
            var template = match.Groups[1].Value.Trim();
            rawImports.Add(new RawImport(template, relativePath, ImportType.Internal));
        }

        // 2. <cfmodule template="...">
        var moduleMatches = Regex.Matches(content, @"<\s*cfmodule\b[^>]*\btemplate\s*=\s*['""]([^'""]+)['""]", RegexOptions.IgnoreCase);
        foreach (Match match in moduleMatches)
        {
            var template = match.Groups[1].Value.Trim();
            rawImports.Add(new RawImport(template, relativePath, ImportType.Internal));
        }

        // 3. createObject("component", "path.to.Component")
        var createObjMatches = Regex.Matches(content, @"createObject\s*\(\s*['""]component['""]\s*,\s*['""]([^'""]+)['""]\s*\)", RegexOptions.IgnoreCase);
        foreach (Match match in createObjMatches)
        {
            var componentPath = match.Groups[1].Value.Trim();
            rawTypeBindings.Add(new RawTypeBinding(componentPath, componentPath, relativePath, ""));
        }

        // 4. <cfinvoke component="...">
        var invokeMatches = Regex.Matches(content, @"<\s*cfinvoke\b[^>]*\bcomponent\s*=\s*['""]([^'""]+)['""]", RegexOptions.IgnoreCase);
        foreach (Match match in invokeMatches)
        {
            var componentPath = match.Groups[1].Value.Trim();
            rawTypeBindings.Add(new RawTypeBinding(componentPath, componentPath, relativePath, ""));
        }
    }

    private static string CleanColdFusionSql(string sql)
    {
        // Replace <cfqueryparam ...> with ?
        var clean = Regex.Replace(sql, @"<\s*cfqueryparam\b[^>]*>", "?", RegexOptions.IgnoreCase);
        // Replace CF variable interpolations #arguments.val# or #val# with @var
        clean = Regex.Replace(clean, @"#[a-zA-Z0-9_\.]+#", "@param");
        // Remove conditional logic tags but keep their inner content
        clean = Regex.Replace(clean, @"</?\s*cf(?:if|else|elseif)\b[^>]*>", " ", RegexOptions.IgnoreCase);
        return clean.Trim();
    }

    private static string CleanDatasourceName(string? rawDs, string defaultDs)
    {
        if (string.IsNullOrWhiteSpace(rawDs)) return defaultDs;
        var clean = rawDs.Trim('"', '\'', ' ');
        if (Regex.IsMatch(clean, @"^#(?:this|application)\.(?:datasource|dsn)#$", RegexOptions.IgnoreCase))
        {
            return defaultDs;
        }
        clean = clean.Trim('#');
        return string.IsNullOrWhiteSpace(clean) ? defaultDs : clean;
    }

    private static string CleanUrl(string url)
    {
        // Strip query parameters or dynamic expressions for cleaner service identification
        var idx = url.IndexOf('?');
        return idx > 0 ? url[..idx] : url;
    }

    private static void EnsureDatasourceHierarchy(
        QueryNode queryNode,
        string dsName,
        string relativePath,
        string workspaceId,
        Dictionary<string, DatabaseNode> dbNodes)
    {
        var dbKey = dsName.ToLowerInvariant();
        if (!dbNodes.TryGetValue(dbKey, out var targetDb))
        {
            var dbId = $"{workspaceId}:db:{dbKey}";
            targetDb = new DatabaseNode(dbId, dsName, relativePath, "relational");
            dbNodes[dbKey] = targetDb;
        }

        // Migrate all Tables under QueryNode into targetDb
        foreach (var child in queryNode.Children.ToList())
        {
            if (child is DatabaseNode existingDb)
            {
                foreach (var dbChild in existingDb.Children.ToList())
                {
                    if (dbChild is DataSetNode dataset)
                    {
                        var targetDataset = targetDb.Children.OfType<DataSetNode>().FirstOrDefault(d => d.Name.Equals(dataset.Name, StringComparison.OrdinalIgnoreCase));
                        if (targetDataset == null)
                        {
                            targetDataset = new DataSetNode($"{targetDb.Id}:dataset:{dataset.Name.ToLowerInvariant()}", dataset.Name, relativePath);
                            targetDb.Children.Add(targetDataset);
                        }

                        foreach (var table in dataset.Children.OfType<TableNode>())
                        {
                            if (string.IsNullOrWhiteSpace(table.Name)) continue;
                            if (!targetDataset.Children.OfType<TableNode>().Any(t => t.Name.Equals(table.Name, StringComparison.OrdinalIgnoreCase)))
                            {
                                var tableId = $"{targetDataset.Id}:table:{table.Name.ToLowerInvariant()}";
                                targetDataset.Children.Add(new TableNode(tableId, table.Name, relativePath));
                            }
                        }
                    }
                    else if (dbChild is TableNode table)
                    {
                        if (string.IsNullOrWhiteSpace(table.Name)) continue;
                        if (!targetDb.Children.OfType<TableNode>().Any(t => t.Name.Equals(table.Name, StringComparison.OrdinalIgnoreCase)))
                        {
                            var tableId = $"{targetDb.Id}:table:{table.Name.ToLowerInvariant()}";
                            targetDb.Children.Add(new TableNode(tableId, table.Name, relativePath));
                        }
                    }
                }
                queryNode.Children.Remove(existingDb);
            }
        }

        if (!queryNode.Children.Any(c => c.Id == targetDb.Id))
        {
            queryNode.Children.Add(targetDb);
        }
    }
}
