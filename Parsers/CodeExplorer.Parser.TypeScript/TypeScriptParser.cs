using System;
using TreeSitter;

namespace CodeExplorer.Parser;

public class TypeScriptParser : ILanguageParser
{
    public string LanguageName => "typescript";

    public string ProjectType => "typescript";

    public System.Collections.Generic.IReadOnlyCollection<string> ExcludedFolders => new[] { "node_modules", "dist", "build", ".next", "out" };

    public bool CanParse(string fileExtension)
    {
        return fileExtension.Equals(".ts", StringComparison.OrdinalIgnoreCase) ||
               fileExtension.Equals(".tsx", StringComparison.OrdinalIgnoreCase) ||
               fileExtension.Equals(".js", StringComparison.OrdinalIgnoreCase) ||
               fileExtension.Equals(".jsx", StringComparison.OrdinalIgnoreCase);
    }

    public bool IsProjectDirectory(string directoryPath, string[] filesInDirectory)
    {
        foreach (var file in filesInDirectory)
        {
            var fileName = System.IO.Path.GetFileName(file).ToLowerInvariant();
            if (fileName == "package.json" || fileName == "tsconfig.json")
            {
                return true;
            }
        }
        return false;
    }

    public string? MapNodeType(string nodeType)
    {
        return nodeType switch
        {
            "class_declaration" or
            "interface_declaration" => "Class",

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
}
