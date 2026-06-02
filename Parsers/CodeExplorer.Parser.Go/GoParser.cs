using System;
using TreeSitter;

namespace CodeExplorer.Parser;

public class GoParser : ILanguageParser
{
    public string LanguageName => "go";

    public bool CanParse(string fileExtension)
    {
        return fileExtension.Equals(".go", StringComparison.OrdinalIgnoreCase);
    }

    public string? MapNodeType(string nodeType)
    {
        return nodeType switch
        {
            "type_spec" or
            "type_declaration" or
            "struct_type" or
            "interface_type" => "Class",

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
}
