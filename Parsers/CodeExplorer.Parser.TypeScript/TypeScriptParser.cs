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

    public IReadOnlyCollection<string> ExcludedFolders => new[] { "node_modules", "dist", "build", ".next", "out" };

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
            if (obj != null && (obj.Text.Contains("app") || obj.Text.Contains("router") || obj.Text.Contains("express")))
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

        if (func.Type == "identifier" && func.Text == "fetch") return true;

        if (func.Type == "member_expression")
        {
            var obj = func.GetChildForField("object");
            if (obj != null && obj.Text == "axios")
            {
                var prop = func.GetChildForField("property");
                if (prop != null)
                {
                    var method = prop.Text;
                    return method is "get" or "post" or "put" or "delete" or "request";
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

    private static Node? GetNextNamedSibling(Node node)
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

        if (idx >= 0)
        {
            for (int i = idx + 1; i < children.Count; i++)
            {
                var sibling = children[i];
                if (sibling.Type == "method_definition")
                {
                    return sibling;
                }
                if (sibling.Type == "class_declaration")
                {
                    return sibling;
                }
            }
        }
        return null;
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
}
