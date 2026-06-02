using System;
using TreeSitter;

namespace CodeExplorer.Parser;

public class PythonParser : ILanguageParser
{
    public string LanguageName => "python";

    public bool CanParse(string fileExtension)
    {
        return fileExtension.Equals(".py", StringComparison.OrdinalIgnoreCase);
    }

    public string? MapNodeType(string nodeType)
    {
        return nodeType switch
        {
            "class_definition" => "Class",

            "function_definition" => "Function",

            "assignment" or
            "parameters" or
            "pattern" => "Variable",

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
        if (node.Type == "call")
        {
            var callName = FindCallName(node);
            if (!string.IsNullOrEmpty(callName))
            {
                references.Add(new Reference(scopeSymbolId, callName, "CALLS"));
            }
        }
        else if (node.Type == "class_definition")
        {
            var superclassesNode = node.GetChildForField("superclasses");
            if (superclassesNode != null && superclassesNode.Id != IntPtr.Zero && superclassesNode.Children.Count > 0)
            {
                foreach (var baseChild in superclassesNode.Children)
                {
                    if (baseChild.Type == "identifier")
                    {
                        references.Add(new Reference(scopeSymbolId, baseChild.Text, "INHERITS_FROM"));
                    }
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
        if (expr.Type == "attribute")
        {
            var attrChild = expr.GetChildForField("attribute");
            if (attrChild != null && attrChild.Id != IntPtr.Zero) return attrChild.Text;
        }
        return null;
    }
}
