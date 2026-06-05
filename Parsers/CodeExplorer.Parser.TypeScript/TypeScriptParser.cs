using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.TypeScript;

public class TypeScriptParser : IProjectParser, IFileParser
{

    public string LanguageName => "typescript";

    public string ProjectType => "typescript";

    public IReadOnlyCollection<string> ExcludedFolders => ["node_modules", "dist", "build", ".next", "out"];

    public IEnumerable<ILibraryParser> LibraryParsers { get; } =
    [
        new Libraries.AxiosLibraryParser(),
        new Libraries.ElasticsearchTsLibraryParser(),
        new Libraries.InfluxDbLibraryParser(),
        new Libraries.KnexLibraryParser(),
        new Libraries.MongodbLibraryParser(),
        new Libraries.MongooseLibraryParser(),
        new Libraries.Mysql2LibraryParser(),
        new Libraries.Neo4jLibraryParser(),
        new Libraries.PgLibraryParser(),
        new Libraries.RedisLibraryParser(),
        new Libraries.SequelizeLibraryParser(),
        new Libraries.Sqlite3LibraryParser(),
        new Libraries.TypeOrmLibraryParser()
    ];

    public TypeScriptParser()
    {
        _analyzer = new TypeScriptSemanticAnalyzer(LibraryParsers);
    }

    public bool CanParse(string fileExtension)
    {
        return fileExtension.Equals(".ts", StringComparison.OrdinalIgnoreCase) ||
               fileExtension.Equals(".tsx", StringComparison.OrdinalIgnoreCase);
    }

    public bool IsProjectDirectory(string directoryPath, string[] filesInDirectory)
    {
        foreach (var file in filesInDirectory)
        {
            var fileName = Path.GetFileName(file).ToLowerInvariant();
            if (fileName == "package.json" || fileName == "tsconfig.json")
            {
                return true;
            }
        }
        return false;
    }

    public string? MapNodeType(Node node)
    {
        if (IsTsDecoratorEntryPoint(node) || IsExpressRoute(node))
        {
            return "EntryPoint";
        }

        if (IsTsHttpClientCall(node))
        {
            return "ExternalService";
        }

        if (node.Type is "string" or "template_string")
        {
            if (NestedSqlParser.TryParseSql(node.Text, out _, out _))
            {
                return "Query";
            }
        }

        return node.Type switch
        {
            "class_declaration" or
            "class_expression" or
            "enum_declaration" => "Class",

            "interface_declaration" or
            "type_alias_declaration" => "Interface",

            "method_definition" or
            "function_declaration" or
            "function_expression" or
            "generator_function_declaration" or
            "arrow_function" => "Function",

            "variable_declarator" or
            "formal_parameters" or
            "property_signature" or
            "public_field_definition" => "Variable",

            _ => null
        };
    }

    public string? ExtractIdentifier(Node node)
    {
        if (IsTsDecoratorEntryPoint(node))
        {
            return ExtractTsDecoratorRoute(node);
        }

        if (IsExpressRoute(node))
        {
            return ExtractExpressRoute(node);
        }

        if (IsTsHttpClientCall(node))
        {
            return ExtractTsHttpClientTarget(node);
        }

        if (node.Type is "string" or "template_string")
        {
            if (NestedSqlParser.TryParseSql(node.Text, out var firstWord, out _))
            {
                return $"{firstWord} Query";
            }
        }

        if (node.Type is "arrow_function" or "function_expression")
        {
            var parent = node.Parent;
            if (parent != null && parent.Id != IntPtr.Zero)
            {
                if (parent.Type == "variable_declarator")
                {
                    var parentNameNode = parent.GetChildForField("name");
                    if (parentNameNode != null && parentNameNode.Id != IntPtr.Zero)
                    {
                        return parentNameNode.Text;
                    }
                    var firstIdent = parent.Children.FirstOrDefault(c => c.Type == "identifier");
                    if (firstIdent != null && firstIdent.Id != IntPtr.Zero)
                    {
                        return firstIdent.Text;
                    }
                }
                else if (parent.Type == "assignment_expression")
                {
                    var leftNode = parent.GetChildForField("left");
                    if (leftNode != null && leftNode.Id != IntPtr.Zero)
                    {
                        return leftNode.Text;
                    }
                }
            }
        }

        var nameNode = node.GetChildForField("name");
        if (nameNode != null && nameNode.Id != IntPtr.Zero)
        {
            return nameNode.Text;
        }

        // Fallback: search for first-level identifier or variable_name
        foreach (var child in node.Children)
        {
            if (child.Type is "identifier" or "variable_name")
            {
                return child.Text;
            }
        }

        // Fallback: search first-level recursively for contains("name")
        foreach (var child in node.Children)
        {
            if (child.Type.Contains("name"))
            {
                return child.Text;
            }
        }

        return null;
    }

    private static bool IsTsDecoratorEntryPoint(Node node)
    {
        if (node.Type != "decorator") return false;
        var call = node.Children.FirstOrDefault(c => c.Type == "call_expression");
        if (call == null) return false;
        var func = call.GetChildForField("function");
        if (func == null || (func.Id == IntPtr.Zero && call.Children.Count > 0)) func = call.Children[0];
        if (func == null || func.Id == IntPtr.Zero) return false;
        var name = func.Text;
        return name is "Controller" or "Get" or "Post" or "Put" or "Delete" or "Patch" or "SubscribeMessage";
    }

    private static string? ExtractTsDecoratorRoute(Node node)
    {
        var call = node.Children.FirstOrDefault(c => c.Type == "call_expression");
        if (call == null) return null;
        var func = call.GetChildForField("function");
        if (func == null || (func.Id == IntPtr.Zero && call.Children.Count > 0)) func = call.Children[0];
        if (func == null || func.Id == IntPtr.Zero) return null;
        var name = func.Text;

        var args = call.Children.FirstOrDefault(c => c.Type == "arguments");
        string routeVal = "/";
        if (args != null && args.Children.Count > 2)
        {
            var firstArg = args.Children.FirstOrDefault(c => c.Type is "string" or "template_string");
            if (firstArg != null)
            {
                routeVal = firstArg.Text.Trim('\'', '"', '`');
            }
        }

        if (name == "SubscribeMessage")
        {
            return $"ws:{routeVal}";
        }
        var method = name == "Controller" ? "GET" : name.ToUpperInvariant();
        return $"{method}:{routeVal}";
    }

    private static bool IsExpressRoute(Node node)
    {
        if (node.Type != "call_expression") return false;
        var func = node.GetChildForField("function");
        if (func == null || (func.Id == IntPtr.Zero && node.Children.Count > 0)) func = node.Children[0];
        if (func == null || func.Id == IntPtr.Zero) return false;

        if (func.Type == "member_expression")
        {
            var obj = func.GetChildForField("object");
            if (obj != null && (obj.Text == "app" || obj.Text == "router" || obj.Text == "express"))
            {
                var prop = func.GetChildForField("property");
                if (prop != null && prop.Id != IntPtr.Zero)
                {
                    var method = prop.Text;
                    return method is "get" or "post" or "put" or "delete";
                }
            }
        }
        return false;
    }

    private static string? ExtractExpressRoute(Node node)
    {
        var func = node.GetChildForField("function");
        if (func == null || (func.Id == IntPtr.Zero && node.Children.Count > 0)) func = node.Children[0];
        if (func == null || func.Id == IntPtr.Zero) return null;
        var prop = func.GetChildForField("property");
        if (prop == null) return null;
        var method = prop.Text.ToUpperInvariant();

        var args = node.Children.FirstOrDefault(c => c.Type == "arguments");
        string routeVal = "/";
        if (args != null)
        {
            var firstArg = args.Children.FirstOrDefault(c => c.Type is "string" or "template_string");
            if (firstArg != null)
            {
                routeVal = firstArg.Text.Trim('\'', '"', '`');
            }
        }
        return $"{method}:{routeVal}";
    }

    private static bool IsTsHttpClientCall(Node node)
    {
        if (node.Type != "call_expression") return false;

        var func = node.GetChildForField("function");
        if (func == null || (func.Id == IntPtr.Zero && node.Children.Count > 0)) func = node.Children[0];
        if (func == null || func.Id == IntPtr.Zero) return false;

        if (func.Type == "identifier")
        {
            return func.Text is "fetch" or "nodeFetch" or "got" or "superagent";
        }

        if (func.Type == "member_expression")
        {
            var obj = func.GetChildForField("object");
            if (obj != null)
            {
                var objName = obj.Text;
                var prop = func.GetChildForField("property");
                if (prop != null)
                {
                    var propName = prop.Text;
                    if (objName is "got" or "superagent" or "request")
                    {
                        return propName is "get" or "post" or "put" or "delete" or "request" or "patch" or "head";
                    }
                    if (objName is "http" or "https")
                    {
                        return propName is "get" or "request" or "post";
                    }
                }
            }
        }
        return false;
    }

    private static string? ExtractTsHttpClientTarget(Node node)
    {
        var args = node.Children.FirstOrDefault(c => c.Type == "arguments");
        if (args != null)
        {
            var firstArg = args.Children.FirstOrDefault(c => c.Type is "string" or "template_string");
            if (firstArg != null)
            {
                var text = firstArg.Text.Trim('\'', '"', '`');
                if (text.Contains("://"))
                {
                    try
                    {
                        var uri = new Uri(text);
                        return $"http:{uri.Host}";
                    }
                    catch
                    {
                    }
                }
                return $"http:{text}";
            }
        }
        return "http:unknown-service";
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references)
    {
        TryDetectCalls(node, scopeSymbolId, references);
        TryDetectInheritsFromAndImplements(node, scopeSymbolId, references);
        if (node.Type is "string" or "template_string")
        {
            NestedSqlParser.TryDetectSqlDependencies(node.Text, scopeSymbolId, references);
        }

        // If this is a method_definition preceded by an EntryPoint decorator, link via IMPLEMENTS
        if (node.Type == "method_definition")
        {
            var prevSibling = GetPreviousNamedSibling(node);
            if (prevSibling != null && prevSibling.Type == "decorator" && IsTsDecoratorEntryPoint(prevSibling))
            {
                var route = ExtractTsDecoratorRoute(prevSibling);
                if (!string.IsNullOrEmpty(route))
                {
                    references.Add(new Reference(scopeSymbolId, route.Replace(":", " "), OntologyConstants.Relationships.Implements));
                }
            }
        }
    }

    private static Node? GetPreviousNamedSibling(Node node)
    {
        var parent = node.Parent;
        if (parent == null || parent.Id == IntPtr.Zero) return null;

        var children = parent.Children;
        int idx = -1;
        for (int i = 0; i < children.Count; i++)
        {
            if (children[i].Id == node.Id)
            {
                idx = i;
                break;
            }
        }

        return idx > 0 ? children[idx - 1] : null;
    }

    private void TryDetectCalls(Node node, string scopeSymbolId, List<Reference> references)
    {
        if (node.Type == "call_expression")
        {
            var callName = FindCallName(node);
            if (!string.IsNullOrEmpty(callName))
            {
                references.Add(new Reference(scopeSymbolId, callName, "CALLS"));
            }
        }
    }

    private void TryDetectInheritsFromAndImplements(Node node, string scopeSymbolId, List<Reference> references)
    {
        if (node.Type == "extends_clause" || node.Type == "implements_clause")
        {
            var kind = node.Type == "implements_clause" ? "IMPLEMENTS" : "INHERITS_FROM";
            foreach (var child in node.Children)
            {
                if (child.Type.Contains("identifier") || child.Type.Contains("name"))
                {
                    references.Add(new Reference(scopeSymbolId, child.Text, kind));
                }
            }
        }
    }

    private static string? FindCallName(Node callNode)
    {
        var expr = callNode.GetChildForField("function");
        if (expr != null && expr.Id == IntPtr.Zero && callNode.Children.Count > 0)
        {
            expr = callNode.Children[0];
        }
        if (expr == null || expr.Id == IntPtr.Zero) return null;

        if (expr.Type == "identifier")
        {
            return expr.Text;
        }
        if (expr.Type == "member_expression")
        {
            var propChild = expr.GetChildForField("property");
            if (propChild != null && propChild.Id != IntPtr.Zero) return propChild.Text;
        }
        return null;
    }

    public async Task<ProducedPackageInfo?> GetProducedPackageAsync(string projectDirectory)
    {
        var packageJsonPath = Path.Combine(projectDirectory, "package.json");
        if (!File.Exists(packageJsonPath)) return null;

        try
        {
            var content = await File.ReadAllTextAsync(packageJsonPath);
            using var doc = System.Text.Json.JsonDocument.Parse(content);
            var root = doc.RootElement;

            // Check private attribute for npm publishing
            if (root.TryGetProperty("private", out var privateProp))
            {
                if (privateProp.ValueKind == System.Text.Json.JsonValueKind.True ||
                    (privateProp.ValueKind == System.Text.Json.JsonValueKind.String &&
                     string.Equals(privateProp.GetString(), "true", StringComparison.OrdinalIgnoreCase)))
                {
                    return null;
                }
            }

            if (root.TryGetProperty("name", out var nameProp) && nameProp.ValueKind == System.Text.Json.JsonValueKind.String)
            {
                var name = nameProp.GetString();
                if (string.IsNullOrEmpty(name)) return null;

                var version = "1.0.0";
                if (root.TryGetProperty("version", out var versionProp) && versionProp.ValueKind == System.Text.Json.JsonValueKind.String)
                {
                    version = versionProp.GetString() ?? "1.0.0";
                }

                return new ProducedPackageInfo(name, version, "npm");
            }
        }
        catch
        {
            // Ignore
        }

        return null;
    }

    public async Task<ProjectDependencyInfo> ParseDependenciesAsync(string projectDirectory)
    {
        var localProjectPaths = new List<string>();
        var externalPackages = new List<ProducedPackageInfo>();

        var packageJsonPath = Path.Combine(projectDirectory, "package.json");
        if (!File.Exists(packageJsonPath))
        {
            return new ProjectDependencyInfo(localProjectPaths, externalPackages);
        }

        try
        {
            var content = await File.ReadAllTextAsync(packageJsonPath);
            using var doc = System.Text.Json.JsonDocument.Parse(content);
            var root = doc.RootElement;

            var depProperties = new[] { "dependencies", "devDependencies" };
            foreach (var propName in depProperties)
            {
                if (root.TryGetProperty(propName, out var depsObj) && depsObj.ValueKind == System.Text.Json.JsonValueKind.Object)
                {
                    foreach (var prop in depsObj.EnumerateObject())
                    {
                        var packageName = prop.Name;
                        var packageVersion = prop.Value.GetString() ?? "unknown";

                        // Check if it is a local workspace project reference
                        if (packageVersion.StartsWith("file:") || packageVersion.StartsWith("workspace:"))
                        {
                            var relativePath = packageVersion.Substring(packageVersion.IndexOf(':') + 1);
                            if (!string.IsNullOrEmpty(relativePath))
                            {
                                var referencedDir = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(packageJsonPath)!, relativePath)).Replace('\\', '/');
                                localProjectPaths.Add(referencedDir);
                                continue;
                            }
                        }

                        // Treat as npm package reference
                        externalPackages.Add(new ProducedPackageInfo(packageName, packageVersion, "npm"));
                    }
                }
            }
        }
        catch
        {
            // Ignore
        }

        return new ProjectDependencyInfo(localProjectPaths, externalPackages);
    }

    public bool UsesTreeSitter => true;
    public Task<FileNode> ParseAsync(string filePath, string parentNodeId, ParsingContext ctx)
    {
        var relativePath = Path.GetRelativePath(ctx.AbsoluteWorkspacePath, filePath).Replace('\\', '/');
        return TreeSitterFileParser.ParseFileAsync(filePath, relativePath, parentNodeId, this, ctx);
    }

    private readonly TypeScriptSemanticAnalyzer _analyzer;

    public ISemanticAnalyzer GetSemanticAnalyzer() => _analyzer;

    public void CollectSemanticData(Node node, string filePath, ParsingContext ctx)
    {
        if (node.Type == "import_statement")
        {
            var sourceNode = node.GetChildForField("source");
            if (sourceNode == null || sourceNode.Id == IntPtr.Zero)
            {
                sourceNode = node.Children.FirstOrDefault(c => c.Type == "string");
            }
            if (sourceNode != null && sourceNode.Id != IntPtr.Zero)
            {
                var importPath = sourceNode.Text.Trim('\'', '"');
                ctx.AddRawImport(new RawImport(importPath, filePath));
            }
        }
        else if (node.Type == "call_expression")
        {
            var funcNode = node.GetChildForField("function");
            if (funcNode != null && funcNode.Text == "require")
            {
                var argList = node.GetChildForField("arguments");
                if (argList != null && argList.Children.Count > 1)
                {
                    var firstArg = argList.Children.FirstOrDefault(c => c.Type == "string");
                    if (firstArg != null)
                    {
                        var importPath = firstArg.Text.Trim('\'', '"');
                        ctx.AddRawImport(new RawImport(importPath, filePath));
                    }
                }
            }
        }
        else if (node.Type == "variable_declarator")
        {
            var nameNode = node.GetChildForField("name");
            if (nameNode == null || nameNode.Id == IntPtr.Zero)
            {
                nameNode = node.Children.FirstOrDefault(c => c.Type == "identifier");
            }
            var name = nameNode?.Text;

            if (!string.IsNullOrEmpty(name))
            {
                var valueNode = node.GetChildForField("value");
                string initializerText = valueNode != null && valueNode.Id != IntPtr.Zero ? valueNode.Text : "";
                bool isConstant = IsTypeScriptConstant(node);
                string scope = DetermineTypeScriptScope(node);

                ctx.AddRawVariable(new RawVariable(
                    name,
                    initializerText,
                    scope,
                    isConstant,
                    filePath,
                    node.StartPosition.Row,
                    node.EndPosition.Row,
                    node.StartPosition.Column,
                    node.EndPosition.Column
                ));
            }
        }
        else if (node.Type == "public_field_definition")
        {
            var nameNode = node.GetChildForField("name");
            if (nameNode == null || nameNode.Id == IntPtr.Zero)
            {
                nameNode = node.Children.FirstOrDefault(c => c.Type == "property_identifier");
            }
            var name = nameNode?.Text;

            if (!string.IsNullOrEmpty(name))
            {
                var valueNode = node.GetChildForField("value");
                string initializerText = valueNode != null && valueNode.Id != IntPtr.Zero ? valueNode.Text : "";
                bool isConstant = false;
                string scope = "class";

                ctx.AddRawVariable(new RawVariable(
                    name,
                    initializerText,
                    scope,
                    isConstant,
                    filePath,
                    node.StartPosition.Row,
                    node.EndPosition.Row,
                    node.StartPosition.Column,
                    node.EndPosition.Column
                ));
            }
        }
    }

    private static bool IsTypeScriptConstant(Node node)
    {
        var curr = node.Parent;
        while (curr != null && curr.Id != IntPtr.Zero)
        {
            if (curr.Type == "lexical_declaration")
            {
                return curr.Text.StartsWith("const");
            }
            curr = curr.Parent;
        }
        return false;
    }

    private static string DetermineTypeScriptScope(Node node)
    {
        var curr = node.Parent;
        while (curr != null && curr.Id != IntPtr.Zero)
        {
            if (curr.Type is "class_declaration" or "interface_declaration")
                return "class";
            if (curr.Type is "function_declaration" or "arrow_function" or "method_definition" or "statement_block")
                return "local";
            curr = curr.Parent;
        }
        return "global";
    }
}
