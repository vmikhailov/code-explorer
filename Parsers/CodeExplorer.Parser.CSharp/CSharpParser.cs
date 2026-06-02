using System;
using TreeSitter;

namespace CodeExplorer.Parser;

public class CSharpParser : ILanguageParser
{
    public string LanguageName => "c-sharp";

    public bool CanParse(string fileExtension)
    {
        return fileExtension.Equals(".cs", StringComparison.OrdinalIgnoreCase);
    }

    public string? MapNodeType(string nodeType)
    {
        return nodeType switch
        {
            "class_declaration" or
            "interface_declaration" or
            "struct_declaration" or
            "record_declaration" => "Class",

            "method_declaration" or
            "function_declaration" or
            "constructor_declaration" or
            "local_function_statement" => "Function",

            "variable_declarator" or
            "parameter" or
            "property_declaration" or
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
        if (node.Type == "invocation_expression")
        {
            var callName = FindCallName(node);
            if (!string.IsNullOrEmpty(callName))
            {
                references.Add(new Reference(scopeSymbolId, callName, "CALLS"));
            }
        }
        else if (node.Type == "base_list")
        {
            foreach (var child in node.Children)
            {
                if (child.Type.Contains("identifier") || child.Type.Contains("name"))
                {
                    var baseName = child.Text;
                    var refKind = baseName.StartsWith('I') && baseName.Length > 1 && char.IsUpper(baseName[1])
                        ? "IMPLEMENTS"
                        : "INHERITS_FROM";
                    references.Add(new Reference(scopeSymbolId, baseName, refKind));
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
        if (expr.Type == "member_access_expression")
        {
            var nameChild = expr.GetChildForField("name");
            if (nameChild != null && nameChild.Id != IntPtr.Zero) return nameChild.Text;
        }
        return null;
    }
}
