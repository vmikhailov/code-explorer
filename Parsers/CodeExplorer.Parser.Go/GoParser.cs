using TreeSitter;
using CodeExplorer.Common;

namespace CodeExplorer.Parser;

public class GoParser : IProjectParser, IFileParser
{
    public string LanguageName => "go";

    public string ProjectType => "go";

    public IReadOnlyCollection<string> ExcludedFolders => new[] { "vendor" };

    public bool CanParse(string fileExtension)
    {
        return fileExtension.Equals(".go", StringComparison.OrdinalIgnoreCase);
    }

    public bool IsProjectDirectory(string directoryPath, string[] filesInDirectory)
    {
        foreach (var file in filesInDirectory)
        {
            var fileName = Path.GetFileName(file).ToLowerInvariant();
            if (fileName == "go.mod")
            {
                return true;
            }
        }
        return false;
    }

    public string? MapNodeType(Node node)
    {
        if (node.Type == "type_spec")
        {
            foreach (var child in node.Children)
            {
                if (child.Type == "interface_type")
                {
                    return "Interface";
                }
            }
            return "Class";
        }

        return node.Type switch
        {
            "type_declaration" or
            "struct_type" => "Class",

            "interface_type" => "Interface",

            "function_declaration" or
            "method_declaration" => "Function",

            "parameter_declaration" or
            "const_spec" or
            "var_spec" or
            "field_declaration" => "Variable",

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
        if (expr.Type == "selector_expression")
        {
            var fieldChild = expr.GetChildForField("field");
            if (fieldChild != null && fieldChild.Id != IntPtr.Zero) return fieldChild.Text;
        }
        return null;
    }

    public async Task<ProducedPackageInfo?> GetProducedPackageAsync(string projectDirectory)
    {
        var goModPath = Path.Combine(projectDirectory, "go.mod");
        if (!File.Exists(goModPath)) return null;

        try
        {
            var lines = await File.ReadAllLinesAsync(goModPath);
            var moduleLine = lines.FirstOrDefault(l => l.Trim().StartsWith("module "));
            if (moduleLine != null)
            {
                var modName = moduleLine.Trim().Substring("module ".Length).Trim();
                if (!string.IsNullOrEmpty(modName))
                {
                    return new ProducedPackageInfo(modName, "1.0.0", "go");
                }
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

        var goModPath = Path.Combine(projectDirectory, "go.mod");
        if (!File.Exists(goModPath))
        {
            return new ProjectDependencyInfo(localProjectPaths, externalPackages);
        }

        try
        {
            var lines = await File.ReadAllLinesAsync(goModPath);
            var inRequireBlock = false;

            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();
                if (string.IsNullOrEmpty(line)) continue;

                // Handle single-line require
                if (line.StartsWith("require ") && !line.EndsWith("("))
                {
                    var content = line.Substring("require ".Length).Trim();
                    var parts = content.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 1)
                    {
                        var name = parts[0];
                        var version = parts.Length >= 2 ? parts[1] : "1.0.0";
                        externalPackages.Add(new ProducedPackageInfo(name, version, "go"));
                    }
                }
                else if (line.StartsWith("require ("))
                {
                    inRequireBlock = true;
                }
                else if (line == ")")
                {
                    inRequireBlock = false;
                }
                else if (inRequireBlock)
                {
                    // Line inside require block
                    var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 1)
                    {
                        var name = parts[0];
                        var version = parts.Length >= 2 ? parts[1] : "1.0.0";
                        externalPackages.Add(new ProducedPackageInfo(name, version, "go"));
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
