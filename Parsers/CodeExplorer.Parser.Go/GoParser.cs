using TreeSitter;

namespace CodeExplorer.Parser;

public class GoParser : ILanguageParser
{
    public string LanguageName => "go";

    public string ProjectType => "go";

    public System.Collections.Generic.IReadOnlyCollection<string> ExcludedFolders => new[] { "vendor" };

    public bool CanParse(string fileExtension)
    {
        return fileExtension.Equals(".go", StringComparison.OrdinalIgnoreCase);
    }

    public bool IsProjectDirectory(string directoryPath, string[] filesInDirectory)
    {
        foreach (var file in filesInDirectory)
        {
            var fileName = System.IO.Path.GetFileName(file).ToLowerInvariant();
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
        var goModPath = System.IO.Path.Combine(projectDirectory, "go.mod");
        if (!System.IO.File.Exists(goModPath)) return null;

        try
        {
            var lines = await System.IO.File.ReadAllLinesAsync(goModPath);
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
}
