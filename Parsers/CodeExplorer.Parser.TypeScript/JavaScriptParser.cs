using TreeSitter;

namespace CodeExplorer.Parser;

public class JavaScriptParser : IProjectParser, IFileParser
{
    public string LanguageName => "javascript";

    public string ProjectType => "javascript";

    public IReadOnlyCollection<string> ExcludedFolders => new[] { "node_modules", "dist", "build", ".next", "out" };

    public bool CanParse(string fileExtension)
    {
        return fileExtension.Equals(".js", StringComparison.OrdinalIgnoreCase) ||
               fileExtension.Equals(".jsx", StringComparison.OrdinalIgnoreCase);
    }

    public bool IsProjectDirectory(string directoryPath, string[] filesInDirectory)
    {
        foreach (var file in filesInDirectory)
        {
            var fileName = Path.GetFileName(file).ToLowerInvariant();
            if (fileName == "package.json" || fileName == "jsconfig.json" || fileName == "tsconfig.json")
            {
                return true;
            }
        }
        return false;
    }

    public string? MapNodeType(Node node)
    {
        return node.Type switch
        {
            "class_declaration" => "Class",
            "interface_declaration" => "Interface",

            "method_definition" or
            "function_declaration" or
            "arrow_function" => "Function",

            "variable_declarator" or
            "formal_parameters" or
            "property_signature" => "Variable",

            _ => null
        };
    }

    public string? ExtractIdentifier(Node node)
    {
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

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references)
    {
        if (node.Type == "call_expression")
        {
            var callName = FindCallName(node);
            if (!string.IsNullOrEmpty(callName))
            {
                references.Add(new Reference(scopeSymbolId, callName, "CALLS"));
            }
        }
        else if (node.Type == "extends_clause" || node.Type == "implements_clause")
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
    public Task ParseCustomAsync(string filePath, string parentNodeId, ParsingContext ctx) => throw new NotSupportedException();
}
