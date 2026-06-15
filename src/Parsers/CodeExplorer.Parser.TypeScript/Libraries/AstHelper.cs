using System;
using System.Linq;
using System.Text.RegularExpressions;
using TreeSitter;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public static class AstHelper
{
    public static string? ResolveStringOrTemplate(Node? argNode)
    {
        if (argNode == null || argNode.Id == IntPtr.Zero) return null;

        if (argNode.Type == "string" || argNode.Type == "template_string" || argNode.Type == "string_literal" || argNode.Type == "interpreted_string_literal")
        {
            var text = argNode.Text.Trim('\'', '"', '`');
            return Regex.Replace(text, @"\$\{[^}]+\}", "*");
        }

        if (argNode.Type == "identifier")
        {
            var varName = argNode.Text;
            var val = FindVariableInitializerInAst(argNode, varName);
            if (val != null)
            {
                return Regex.Replace(val, @"\$\{[^}]+\}", "*");
            }
        }

        return null;
    }

    private static string? FindVariableInitializerInAst(Node node, string varName)
    {
        var curr = node.Parent;
        while (curr != null && curr.Id != IntPtr.Zero)
        {
            if (curr.Type is "statement_block" or "program")
            {
                foreach (var child in curr.Children)
                {
                    if (child.Type is "lexical_declaration" or "variable_declaration")
                    {
                        foreach (var decl in child.Children.Where(c => c.Type == "variable_declarator"))
                        {
                            var nameNode = decl.GetChildForField("name");
                            if (nameNode != null && nameNode.Text == varName)
                            {
                                var valNode = decl.GetChildForField("value");
                                if (valNode != null && valNode.Id != IntPtr.Zero)
                                {
                                    return valNode.Text.Trim('\'', '"', '`');
                                }
                            }
                        }
                    }
                }
            }
            curr = curr.Parent;
        }
        return null;
    }
}
