using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.CSharp.Libraries;

public class RestSharpLibraryParser : ILibraryParser
{
    public string Name => "RestSharp";
    public string Id => "restsharp";
    public string Type => "api";
    public IReadOnlyList<string> SupportedPatterns => ["RestSharp"];
    public bool IsImplemented => true;

    private static readonly HashSet<string> RestClientCallMethods =
    [
        "GetAsync", "PostAsync", "PutAsync", "DeleteAsync", "PatchAsync",
        "GetJsonAsync", "PostJsonAsync", "PutJsonAsync", "DeleteJsonAsync"
    ];

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (IsRestSharpTarget(node)) return OntologyConstants.NodeLabels.ExternalService;
        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx)
    {
        if (IsRestSharpTarget(node)) return ExtractRestSharpTarget(node);
        return null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx) { }

    private static bool IsRestSharpTarget(Node node)
    {
        // 1. Check object creation: new RestRequest(...) or new RestClient(...)
        if (node.Type == "object_creation_expression")
        {
            var typeNode = node.GetChildForField("type")
                           ?? node.Children.FirstOrDefault(c => c.Type is "type_identifier" or "identifier" or "generic_name");
            if (typeNode != null)
            {
                var typeName = typeNode.Text;
                if (typeName is "RestRequest" or "RestClient")
                {
                    return true;
                }
            }
        }

        // 2. Direct HTTP calls on RestClient: client.GetAsync("...") where first arg is a string
        if (node.Type == "invocation_expression")
        {
            var func = node.GetChildForField("function") ?? (node.Children.Count > 0 ? node.Children[0] : null);
            if (func != null && func.Type == "member_access_expression")
            {
                var nameChild = func.GetChildForField("name");
                if (nameChild != null && RestClientCallMethods.Contains(nameChild.Text))
                {
                    var expr = func.GetChildForField("expression");
                    if (expr != null && expr.Text.ToLowerInvariant().Contains("client"))
                    {
                        var argList = node.Children.FirstOrDefault(c => c.Type == "argument_list");
                        var firstArg = argList?.Children.FirstOrDefault(c => c.Type == "argument");
                        if (firstArg != null)
                        {
                            var valNode = firstArg.Children.FirstOrDefault();
                            if (valNode != null && (valNode.Type.Contains("string") || valNode.Type == "binary_expression"))
                            {
                                return true;
                            }
                        }
                    }
                }
            }
        }

        return false;
    }

    private static string? ExtractRestSharpTarget(Node node)
    {
        if (node.Type == "object_creation_expression")
        {
            var typeNode = node.GetChildForField("type")
                           ?? node.Children.FirstOrDefault(c => c.Type is "type_identifier" or "identifier" or "generic_name");
            var typeName = typeNode?.Text;

            var argList = node.Children.FirstOrDefault(c => c.Type == "argument_list");
            if (argList != null)
            {
                var firstArg = argList.Children.FirstOrDefault(c => c.Type == "argument");
                if (firstArg != null)
                {
                    var valNode = firstArg.Children.FirstOrDefault();
                    if (valNode != null)
                    {
                        var text = ExtractText(valNode);
                        if (!string.IsNullOrEmpty(text))
                        {
                            if (typeName == "RestClient")
                            {
                                return NormalizeUrl(text);
                            }
                            if (typeName == "RestRequest")
                            {
                                if (Uri.TryCreate(text, UriKind.Absolute, out var uri))
                                {
                                    return $"{uri.Scheme}:{uri.Host}{uri.AbsolutePath}";
                                }
                                return text.StartsWith("/") ? text : "/" + text;
                            }
                        }
                    }
                }
            }
        }
        else if (node.Type == "invocation_expression")
        {
            var argList = node.Children.FirstOrDefault(c => c.Type == "argument_list");
            var firstArg = argList?.Children.FirstOrDefault(c => c.Type == "argument");
            if (firstArg != null)
            {
                var valNode = firstArg.Children.FirstOrDefault();
                if (valNode != null)
                {
                    var text = ExtractText(valNode);
                    if (!string.IsNullOrEmpty(text))
                    {
                        return NormalizeUrl(text);
                    }
                }
            }
        }

        return "http:restsharp-service";
    }

    private static string? ExtractText(Node? node)
    {
        if (!node.IsValid()) return null;

        if (node.Type.Contains("string"))
        {
            return node.Text.Trim('"');
        }
        if (node.Type == "binary_expression")
        {
            var left = node.GetChildForField("left") ?? (node.Children.Count > 0 ? node.Children[0] : null);
            if (left != null && left.Type.Contains("string"))
            {
                return left.Text.Trim('"');
            }
        }
        if (node.Type == "interpolated_string_expression")
        {
            return node.Text.Trim('$', '"');
        }
        if (node.Type == "object_creation_expression")
        {
            var argList = node.Children.FirstOrDefault(c => c.Type == "argument_list");
            var firstArg = argList?.Children.FirstOrDefault(c => c.Type == "argument");
            var val = firstArg?.Children.FirstOrDefault();
            if (val != null) return ExtractText(val);
        }
        if (node.Type == "identifier")
        {
            var resolved = TryResolveVariableUri(node, node.Text);
            if (!string.IsNullOrEmpty(resolved)) return resolved;
        }
        return null;
    }

    private static string? TryResolveVariableUri(Node node, string varName)
    {
        var current = node.Parent;
        while (current != null && current.Id != IntPtr.Zero)
        {
            if (current.Type is "block" or "method_declaration" or "local_function_statement" or "compilation_unit" or "class_declaration")
            {
                var uri = FindUriInScope(current, varName, 0);
                if (!string.IsNullOrEmpty(uri)) return uri;
            }
            current = current.Parent;
        }
        return null;
    }

    private static string? FindUriInScope(Node scopeNode, string targetVar, int depth)
    {
        if (depth > 5) return null;

        foreach (var child in scopeNode.Children)
        {
            if (child.Type is "local_declaration_statement" or "variable_declaration" or "field_declaration" or "global_statement")
            {
                var decls = FindNodesOfType(child, "variable_declarator");
                foreach (var decl in decls)
                {
                    var nameNode = decl.GetChildForField("name") ?? decl.Children.FirstOrDefault(c => c.Type is "identifier");
                    if (nameNode.IsValid() && nameNode.Text == targetVar)
                    {
                        var valueNode = decl.GetChildForField("value");
                        if (!valueNode.IsValid())
                        {
                            var eqClause = decl.Children.FirstOrDefault(c => c.Type == "equals_value_clause");
                            if (eqClause != null && eqClause.Children.Count > 1)
                            {
                                valueNode = eqClause.Children[1];
                            }
                            else if (decl.Children.Count >= 3 && decl.Children[1].Text == "=")
                            {
                                valueNode = decl.Children[2];
                            }
                        }

                        if (valueNode.IsValid())
                        {
                            var extracted = ExtractText(valueNode);
                            if (!string.IsNullOrEmpty(extracted)) return extracted;
                        }
                    }
                }
            }
        }

        return null;
    }

    private static List<Node> FindNodesOfType(Node root, string targetType)
    {
        var result = new List<Node>();
        void Recurse(Node current)
        {
            if (current.Type == targetType)
            {
                result.Add(current);
                return;
            }
            foreach (var child in current.Children)
            {
                Recurse(child);
            }
        }
        Recurse(root);
        return result;
    }

    private static string NormalizeUrl(string text)
    {
        text = text.Trim().Trim('"');
        if (Uri.TryCreate(text, UriKind.Absolute, out var uri))
        {
            return $"{uri.Scheme}:{uri.Host}{uri.AbsolutePath}";
        }
        return text.StartsWith("/") ? text : $"http:{text}";
    }
}