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
        if (!node.Is(TreeSitterSyntax.CSharp.InvocationExpression)) return false;
        var func = node.GetFunctionNode();
        if (!func.IsValid()) return false;

        if (func.Is(TreeSitterSyntax.CSharp.MemberAccessExpression))
        {
            var nameChild = func.GetField(TreeSitterSyntax.Fields.Name);
            if (!nameChild.IsValid() || !HttpMethods.Contains(nameChild.Text))
                return false;

            // Check receiver expression
            var exprChild = func.GetField(TreeSitterSyntax.Fields.Expression);
            if (exprChild.IsValid())
            {
                var receiverText = exprChild.Text.ToLowerInvariant();
                foreach (var kw in NonHttpReceiverKeywords)
                {
                    if (receiverText.Contains(kw))
                        return false;
                }
            }

            // Check argument list: first argument must not be pure CancellationToken
            var argList = node.FindChildOfType(TreeSitterSyntax.Common.ArgumentList);
            if (argList.IsValid())
            {
                var firstArg = argList.FindChildOfType(TreeSitterSyntax.CSharp.Argument);
                if (firstArg.IsValid())
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
        var argList = node.FindChildOfType(TreeSitterSyntax.Common.ArgumentList);
        if (!argList.IsValid()) return "http:unknown-service";

        var args = argList.FindChildrenOfType(TreeSitterSyntax.CSharp.Argument).ToList();
        if (args.Count == 0) return "http:unknown-service";

        var firstArg = args[0];
        var valNode = firstArg.Children.FirstOrDefault();
        if (valNode.IsValid())
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
                    if (nextVal.IsValid())
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
        while (current.IsValid())
        {
            if (current.IsAny(TreeSitterSyntax.CSharp.Block, TreeSitterSyntax.CSharp.MethodDeclaration, TreeSitterSyntax.CSharp.LocalFunctionStatement))
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
            if (child.IsAny(TreeSitterSyntax.CSharp.LocalDeclarationStatement, TreeSitterSyntax.CSharp.VariableDeclaration, TreeSitterSyntax.CSharp.UsingStatement))
            {
                var decls = FindNodesOfType(child, TreeSitterSyntax.CSharp.VariableDeclarator);
                foreach (var decl in decls)
                {
                    var nameNode = decl.GetField(TreeSitterSyntax.Fields.Name)
                                   ?? decl.FindChildOfType(TreeSitterSyntax.Common.Identifier);
                    if (nameNode.IsValid() && nameNode.Text == targetVar)
                    {
                        var valueNode = decl.GetField(TreeSitterSyntax.Fields.Value);
                        if (!valueNode.IsValid())
                        {
                            var eqClause = decl.FindChildOfType(TreeSitterSyntax.CSharp.EqualsValueClause);
                            if (eqClause.IsValid() && eqClause.Children.Count > 1)
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

            if (child.Is(TreeSitterSyntax.CSharp.ExpressionStatement))
            {
                var assign = child.FindChildOfType(TreeSitterSyntax.Common.AssignmentExpression);
                if (assign.IsValid())
                {
                    var left = assign.GetField(TreeSitterSyntax.Fields.Left);
                    if (left.IsValid() && left.Text == targetVar)
                    {
                        var right = assign.GetField(TreeSitterSyntax.Fields.Right)
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
        if (!expr.IsValid()) return null;

        if (expr.Type.Contains("string"))
        {
            return expr.Text.Trim('"');
        }

        if (expr.Is(TreeSitterSyntax.CSharp.ObjectCreationExpression))
        {
            var typeNode = expr.GetField(TreeSitterSyntax.Fields.Type)
                           ?? expr.Children.FirstOrDefault(c => c.IsAny(TreeSitterSyntax.CSharp.TypeIdentifier, TreeSitterSyntax.Common.Identifier));
            var typeName = typeNode.IsValid() ? typeNode.Text : "";

            var argList = expr.FindChildOfType(TreeSitterSyntax.Common.ArgumentList);
            if (argList.IsValid())
            {
                var args = argList.FindChildrenOfType(TreeSitterSyntax.CSharp.Argument).ToList();

                if (typeName.Contains("Uri"))
                {
                    var firstArg = args.FirstOrDefault();
                    if (firstArg.IsValid())
                    {
                        var val = firstArg.Children.FirstOrDefault();
                        if (val.IsValid() && val.Type.Contains("string"))
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
                        if (!val.IsValid()) continue;

                        if (val.Type.Contains("string"))
                        {
                            return val.Text.Trim('"');
                        }
                        if (val.Is(TreeSitterSyntax.CSharp.ObjectCreationExpression))
                        {
                            var innerUri = ExtractUriFromExpression(val, scopeNode, depth + 1);
                            if (!string.IsNullOrEmpty(innerUri)) return innerUri;
                        }
                        if (val.Is(TreeSitterSyntax.Common.Identifier) && val.Text != "HttpMethod")
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
        if (node.Is(nodeType))
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