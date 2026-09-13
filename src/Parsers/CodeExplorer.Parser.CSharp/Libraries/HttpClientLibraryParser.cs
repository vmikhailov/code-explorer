using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.CSharp.Libraries;

public class HttpClientLibraryParser : ILibraryParser
{
    public string Name => "HttpClient";
    public string Id => "httpclient";
    public string Type => "api";
    public IReadOnlyList<string> SupportedPatterns => ["System.Net.Http"];
    public bool IsImplemented => true;
    public bool IsBuiltIn => true;

    private static readonly HashSet<string> HttpMethods =
    [
        "GetAsync", "PostAsync", "PutAsync", "DeleteAsync", "SendAsync",
        "PostAsJsonAsync", "GetFromJsonAsync", "PatchAsync", "PutAsJsonAsync"
    ];

    private static readonly string[] NonHttpReceiverKeywords =
    [
        "dicom", "channel", "queue", "bus", "stream", "pipe", "socket",
        "reader", "writer", "mediat", "broker", "eventhub", "producer", "consumer"
    ];

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (IsHttpClientCall(node)) return OntologyConstants.NodeLabels.ExternalService;
        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx)
    {
        if (IsHttpClientCall(node)) return ExtractTarget(node);
        return null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx) { }

    public static bool IsHttpClientCall(Node node)
    {
        if (node.Type != "invocation_expression") return false;
        var func = node.GetChildForField("function")
                   ?? (node.Children.Count > 0 ? node.Children[0] : null);
        if (func == null || func.Id == IntPtr.Zero) return false;

        if (func.Type == "member_access_expression")
        {
            var nameChild = func.GetChildForField("name");
            if (nameChild == null || nameChild.Id == IntPtr.Zero || !HttpMethods.Contains(nameChild.Text))
                return false;

            // Check receiver expression
            var exprChild = func.GetChildForField("expression");
            if (exprChild != null && exprChild.Id != IntPtr.Zero)
            {
                var receiverText = exprChild.Text.ToLowerInvariant();
                foreach (var kw in NonHttpReceiverKeywords)
                {
                    if (receiverText.Contains(kw))
                        return false;
                }
            }

            // Check argument list: first argument must not be pure CancellationToken
            var argList = node.Children.FirstOrDefault(c => c.Type == "argument_list");
            if (argList != null)
            {
                var firstArg = argList.Children.FirstOrDefault(c => c.Type == "argument");
                if (firstArg != null)
                {
                    var argText = firstArg.Text.Trim();
                    if (IsCancellationToken(argText))
                        return false;
                }
            }

            return true;
        }
        return false;
    }

    public static bool IsCancellationToken(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        if (text.Contains("CancellationToken", StringComparison.OrdinalIgnoreCase)) return true;
        if (text.EndsWith("Token", StringComparison.OrdinalIgnoreCase)) return true;
        if (text.Equals("ct", StringComparison.OrdinalIgnoreCase) || text.Equals("token", StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    public static string? ExtractTarget(Node node)
    {
        var argList = node.Children.FirstOrDefault(c => c.Type == "argument_list");
        if (argList == null) return "http:unknown-service";

        var args = argList.Children.Where(c => c.Type == "argument").ToList();
        if (args.Count == 0) return "http:unknown-service";

        var firstArg = args[0];
        var valNode = firstArg.Children.FirstOrDefault();
        if (valNode != null)
        {
            var text = valNode.Text.Trim('"');
            if (valNode.Type.Contains("string") || text.Contains("://") || text.StartsWith("/"))
            {
                return NormalizeUrl(text);
            }

            // If argument is a CancellationToken, try subsequent arguments
            if (IsCancellationToken(text))
            {
                for (int i = 1; i < args.Count; i++)
                {
                    var nextVal = args[i].Children.FirstOrDefault();
                    if (nextVal != null)
                    {
                        var nextText = nextVal.Text.Trim('"');
                        if (!string.IsNullOrEmpty(nextText) && !IsCancellationToken(nextText))
                        {
                            return NormalizeUrl(nextText);
                        }
                    }
                }
                return "http:unknown-service";
            }

            // Variable identifier like 'request' or 'uri'
            var resolvedUri = TryResolveVariableUri(node, text);
            if (!string.IsNullOrEmpty(resolvedUri))
            {
                return NormalizeUrl(resolvedUri);
            }

            return $"http:{text}";
        }

        return "http:unknown-service";
    }

    private static string? TryResolveVariableUri(Node node, string varName)
    {
        var current = node.Parent;
        while (current != null && current.Id != IntPtr.Zero)
        {
            if (current.Type is "block" or "method_declaration" or "local_function_statement")
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
        if (depth > 10) return null;

        foreach (var child in scopeNode.Children)
        {
            if (child.Type is "local_declaration_statement" or "variable_declaration" or "using_statement")
            {
                var decls = FindNodesOfType(child, "variable_declarator");
                foreach (var decl in decls)
                {
                    var nameNode = decl.GetChildForField("name")
                                   ?? decl.Children.FirstOrDefault(c => c.Type is "identifier");
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
                            var uri = ExtractUriFromExpression(valueNode, scopeNode, depth);
                            if (!string.IsNullOrEmpty(uri)) return uri;
                        }
                    }
                }
            }

            if (child.Type is "expression_statement")
            {
                var assign = child.Children.FirstOrDefault(c => c.Type == "assignment_expression");
                if (assign != null)
                {
                    var left = assign.GetChildForField("left");
                    if (left.IsValid() && left.Text == targetVar)
                    {
                        var right = assign.GetChildForField("right")
                                    ?? (assign.Children.Count >= 3 ? assign.Children[2] : null);
                        if (right.IsValid())
                        {
                            var uri = ExtractUriFromExpression(right, scopeNode, depth);
                            if (!string.IsNullOrEmpty(uri)) return uri;
                        }
                    }
                }
            }
        }

        return null;
    }

    private static string? ExtractUriFromExpression(Node? expr, Node scopeNode, int depth)
    {
        if (expr == null || !expr.IsValid()) return null;

        if (expr.Type.Contains("string"))
        {
            return expr.Text.Trim('"');
        }

        if (expr.Type == "object_creation_expression")
        {
            var typeNode = expr.GetChildForField("type")
                           ?? expr.Children.FirstOrDefault(c => c.Type is "type_identifier" or "identifier");
            var typeName = typeNode.IsValid() ? typeNode.Text : "";

            var argList = expr.Children.FirstOrDefault(c => c.Type == "argument_list");
            if (argList != null)
            {
                var args = argList.Children.Where(c => c.Type == "argument").ToList();

                if (typeName.Contains("Uri"))
                {
                    var firstArg = args.FirstOrDefault();
                    if (firstArg != null)
                    {
                        var val = firstArg.Children.FirstOrDefault();
                        if (val != null && val.Type.Contains("string"))
                        {
                            return val.Text.Trim('"');
                        }
                    }
                }
                else if (typeName.Contains("HttpRequestMessage"))
                {
                    foreach (var arg in args)
                    {
                        var val = arg.Children.FirstOrDefault();
                        if (val == null) continue;

                        if (val.Type.Contains("string"))
                        {
                            return val.Text.Trim('"');
                        }
                        if (val.Type == "object_creation_expression")
                        {
                            var innerUri = ExtractUriFromExpression(val, scopeNode, depth + 1);
                            if (!string.IsNullOrEmpty(innerUri)) return innerUri;
                        }
                        if (val.Type == "identifier" && val.Text != "HttpMethod")
                        {
                            var resolved = FindUriInScope(scopeNode, val.Text, depth + 1);
                            if (!string.IsNullOrEmpty(resolved)) return resolved;
                        }
                    }
                }
            }
        }

        return null;
    }

    private static List<Node> FindNodesOfType(Node root, string nodeType)
    {
        var list = new List<Node>();
        CollectNodes(root, nodeType, list);
        return list;
    }

    private static void CollectNodes(Node node, string nodeType, List<Node> result)
    {
        if (node.Type == nodeType)
        {
            result.Add(node);
        }
        foreach (var child in node.Children)
        {
            CollectNodes(child, nodeType, result);
        }
    }

    public static string NormalizeUrl(string text)
    {
        text = text.Trim().Trim('"');
        if (Uri.TryCreate(text, UriKind.Absolute, out var uri))
        {
            return $"{uri.Scheme}:{uri.Host}{uri.AbsolutePath}";
        }
        return text;
    }
}