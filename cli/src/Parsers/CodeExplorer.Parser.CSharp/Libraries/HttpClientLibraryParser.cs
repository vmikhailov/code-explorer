using System.Text.RegularExpressions;
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
            if (!nameChild.IsValid()) return false;

            var methodName = nameChild.Text;
            var angleIdx = methodName.IndexOf('<');
            if (angleIdx > 0)
            {
                methodName = methodName[..angleIdx].Trim();
            }

            if (!HttpMethods.Contains(methodName))
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

            // Check argument list: first argument must not be pure CancellationToken, unless fluent HTTP builder is used
            var argList = node.FindChildOfType(TreeSitterSyntax.Common.ArgumentList);
            if (argList.IsValid())
            {
                var firstArg = argList.FindChildOfType(TreeSitterSyntax.CSharp.Argument);
                if (firstArg.IsValid())
                {
                    var argText = firstArg.Text.Trim();
                    if (IsCancellationToken(argText))
                    {
                        if (!HasFluentHttpRequestInReceiver(exprChild))
                            return false;
                    }
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

    private static readonly Dictionary<string, string> KnownHttpClientResources = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Tournaments"] = "api/v1/tournaments",
        ["Identity"] = "api/v1",
        ["Player"] = "api/v1/profiles",
        ["Profiles"] = "api/v1/profiles",
        ["Notifications"] = "api/v1/notifications",
        ["Media"] = "api/v1/Media",
        ["Games"] = "api/v1/games",
        ["Teams"] = "api/v1/teams",
        ["Invitations"] = "api/v1/invitations"
    };

    public static string? ExtractTarget(Node node)
    {
        // 1. Try fluent request chain first (from receiver expression)
        var func = node.GetFunctionNode();
        if (func.IsValid() && func.Is(TreeSitterSyntax.CSharp.MemberAccessExpression))
        {
            var expr = func.GetField(TreeSitterSyntax.Fields.Expression);
            var fluentTarget = TryExtractFluentTarget(expr);
            if (!string.IsNullOrEmpty(fluentTarget))
            {
                return NormalizeUrl(fluentTarget);
            }
        }

        // 2. Standard HttpClient arguments
        var argList = node.FindChildOfType(TreeSitterSyntax.Common.ArgumentList);
        if (!argList.IsValid()) return "http:unknown-service";

        var args = argList.FindChildrenOfType(TreeSitterSyntax.CSharp.Argument).ToList();
        if (args.Count == 0) return "http:unknown-service";

        var namedService = TryResolveNamedClientService(node);

        var firstArg = args[0];
        var valNode = firstArg.Children.FirstOrDefault();
        if (valNode.IsValid())
        {
            var text = valNode.Text.Trim('"');

            // Check if route registry has this constant directly
            if (RouteDictionaryRegistry.TryResolve(text, out var rp, out var rs))
            {
                var cleanPath = rp.Split('?')[0];
                var s = rs ?? namedService;
                var combined = !string.IsNullOrEmpty(s) ? $"{s}{cleanPath}" : cleanPath;
                return NormalizeUrl(combined);
            }

            if (valNode.Type.Contains("string") || text.Contains("://") || text.StartsWith("/"))
            {
                if (text.StartsWith("$") || text.StartsWith("@$") || text.StartsWith("$@"))
                {
                    text = text.TrimStart('$', '@').Trim('"');
                }
                var normalized = NormalizeUrl(text);
                if (!string.IsNullOrEmpty(namedService) && normalized.StartsWith('/'))
                {
                    return $"{namedService}{normalized}";
                }
                return normalized;
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
                            if (RouteDictionaryRegistry.TryResolve(nextText, out var nrp, out var nrs))
                            {
                                var cleanPath = nrp.Split('?')[0];
                                var s = nrs ?? namedService;
                                var combined = !string.IsNullOrEmpty(s) ? $"{s}{cleanPath}" : cleanPath;
                                return NormalizeUrl(combined);
                            }

                            if (nextText.StartsWith("$") || nextText.StartsWith("@$") || nextText.StartsWith("$@"))
                            {
                                nextText = nextText.TrimStart('$', '@').Trim('"');
                            }
                            var norm = NormalizeUrl(nextText);
                            if (!string.IsNullOrEmpty(namedService) && norm.StartsWith('/'))
                            {
                                return $"{namedService}{norm}";
                            }
                            return norm;
                        }
                    }
                }
                return "http:unknown-service";
            }

            // Variable identifier like 'request' or 'uri'
            var resolvedUri = TryResolveVariableUri(node, text);
            if (!string.IsNullOrEmpty(resolvedUri))
            {
                var norm = NormalizeUrl(resolvedUri);
                if (!string.IsNullOrEmpty(namedService) && norm.StartsWith('/'))
                {
                    return $"{namedService}{norm}";
                }
                return norm;
            }

            if (text.Contains('/') || text.Contains('.') || text.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                return $"http:{text}";
            }

            return "http:unknown-service";
        }

        return "http:unknown-service";
    }

    private static string? TryResolveNamedClientService(Node node)
    {
        var func = node.GetFunctionNode();
        if (func.IsValid() && func.Is(TreeSitterSyntax.CSharp.MemberAccessExpression))
        {
            var expr = func.GetField(TreeSitterSyntax.Fields.Expression);
            if (expr.IsValid())
            {
                var receiverName = expr.Text;
                var curr = node.Parent;
                while (curr.IsValid())
                {
                    if (curr.IsAny(TreeSitterSyntax.CSharp.Block, TreeSitterSyntax.CSharp.MethodDeclaration, TreeSitterSyntax.CSharp.ClassDeclaration))
                    {
                        var createClientMatch = Regex.Match(curr.Text, receiverName + @"\s*=\s*.*?CreateClient\s*\(\s*[""']([^""']+)[""']");
                        if (createClientMatch.Success)
                        {
                            return createClientMatch.Groups[1].Value;
                        }
                    }
                    curr = curr.Parent;
                }
            }
        }
        return null;
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

    private static bool HasFluentHttpRequestInReceiver(Node? expr)
    {
        var curr = expr;
        while (curr.IsValid())
        {
            if (curr.Is(TreeSitterSyntax.CSharp.InvocationExpression))
            {
                var func = curr.GetFunctionNode();
                if (func.IsValid() && func.Is(TreeSitterSyntax.CSharp.MemberAccessExpression))
                {
                    var name = func.GetField(TreeSitterSyntax.Fields.Name)?.Text;
                    if (name is "GetRequest" or "PostRequest" or "PutRequest" or "DeleteRequest" or "PatchRequest" or "AddUrlSegment" or "AddUriParameter")
                    {
                        return true;
                    }
                }
                curr = func.IsValid() ? func.GetField(TreeSitterSyntax.Fields.Expression) : null;
            }
            else if (curr.Is(TreeSitterSyntax.CSharp.MemberAccessExpression))
            {
                var name = curr.GetField(TreeSitterSyntax.Fields.Name)?.Text;
                if (name != null && (name.Contains("HttpClientResource") || curr.Text.Contains("HttpClientResource")))
                {
                    return true;
                }
                curr = curr.GetField(TreeSitterSyntax.Fields.Expression);
            }
            else if (curr.Is(TreeSitterSyntax.Common.Identifier))
            {
                var initNode = FindVariableInitializer(curr, curr.Text);
                if (initNode.IsValid())
                {
                    return HasFluentHttpRequestInReceiver(initNode);
                }
                break;
            }
            else
            {
                break;
            }
        }
        return false;
    }

    private static string? TryExtractFluentTarget(Node? expr)
    {
        if (!expr.IsValid()) return null;

        var curr = expr;
        if (curr.Is(TreeSitterSyntax.Common.Identifier))
        {
            var initNode = FindVariableInitializer(curr, curr.Text);
            if (initNode.IsValid())
            {
                curr = initNode;
            }
        }

        string? baseRoute = null;
        var segments = new List<string>();

        while (curr.IsValid())
        {
            if (curr.Is(TreeSitterSyntax.CSharp.InvocationExpression))
            {
                var func = curr.GetFunctionNode();
                if (func.IsValid() && func.Is(TreeSitterSyntax.CSharp.MemberAccessExpression))
                {
                    var methodName = func.GetField(TreeSitterSyntax.Fields.Name)?.Text;
                    if (methodName is "AddUrlSegment")
                    {
                        var argList = curr.FindChildOfType(TreeSitterSyntax.Common.ArgumentList);
                        var firstArg = argList.IsValid() ? argList.FindChildOfType(TreeSitterSyntax.CSharp.Argument) : null;
                        if (firstArg.IsValid())
                        {
                            var argVal = firstArg.Children.FirstOrDefault();
                            if (argVal.IsValid())
                            {
                                if (argVal.Type.Contains("string"))
                                {
                                    segments.Add(argVal.Text.Trim('"'));
                                }
                                else
                                {
                                    segments.Add("*");
                                }
                            }
                        }
                    }
                    else if (methodName is "GetRequest" or "PostRequest" or "PutRequest" or "DeleteRequest" or "PatchRequest")
                    {
                        var argList = curr.FindChildOfType(TreeSitterSyntax.Common.ArgumentList);
                        var firstArg = argList.IsValid() ? argList.FindChildOfType(TreeSitterSyntax.CSharp.Argument) : null;
                        if (firstArg.IsValid())
                        {
                            var argVal = firstArg.Children.FirstOrDefault();
                            if (argVal.IsValid())
                            {
                                baseRoute = ResolveResourceArg(argVal);
                            }
                        }
                    }

                    curr = func.GetField(TreeSitterSyntax.Fields.Expression);
                    continue;
                }
            }

            break;
        }

        if (!string.IsNullOrEmpty(baseRoute) || segments.Count > 0)
        {
            segments.Reverse();
            var path = baseRoute ?? "";
            foreach (var seg in segments)
            {
                if (string.IsNullOrEmpty(path))
                    path = seg;
                else
                    path = path.TrimEnd('/') + "/" + seg.TrimStart('/');
            }
            if (!string.IsNullOrEmpty(path))
            {
                return path.StartsWith('/') ? path : "/" + path;
            }
        }

        return null;
    }

    private static string? ResolveResourceArg(Node argVal)
    {
        if (argVal.Type.Contains("string"))
        {
            return argVal.Text.Trim('"');
        }

        var text = argVal.Text;
        if (text.Contains('.'))
        {
            var propName = text[(text.LastIndexOf('.') + 1)..];
            if (KnownHttpClientResources.TryGetValue(propName, out var route))
            {
                return route;
            }
            return $"api/v1/{propName.ToLowerInvariant()}";
        }

        if (KnownHttpClientResources.TryGetValue(text, out var knownRoute))
        {
            return knownRoute;
        }

        return null;
    }

    private static Node? FindVariableInitializer(Node node, string varName)
    {
        var curr = node.Parent;
        while (curr.IsValid())
        {
            if (curr.IsAny(TreeSitterSyntax.CSharp.Block, TreeSitterSyntax.CSharp.MethodDeclaration, TreeSitterSyntax.CSharp.LocalFunctionStatement))
            {
                foreach (var child in curr.Children)
                {
                    if (child.IsAny(TreeSitterSyntax.CSharp.LocalDeclarationStatement, TreeSitterSyntax.CSharp.VariableDeclaration))
                    {
                        var decls = FindNodesOfType(child, TreeSitterSyntax.CSharp.VariableDeclarator);
                        foreach (var decl in decls)
                        {
                            var nameNode = decl.GetField(TreeSitterSyntax.Fields.Name)
                                           ?? decl.FindChildOfType(TreeSitterSyntax.Common.Identifier);
                            if (nameNode.IsValid() && nameNode.Text == varName)
                            {
                                var valueNode = decl.GetField(TreeSitterSyntax.Fields.Value);
                                if (!valueNode.IsValid())
                                {
                                    var eqClause = decl.FindChildOfType(TreeSitterSyntax.CSharp.EqualsValueClause);
                                    if (eqClause.IsValid() && eqClause.Children.Count > 1)
                                    {
                                        valueNode = eqClause.Children[1];
                                    }
                                }
                                if (valueNode.IsValid()) return valueNode;
                            }
                        }
                    }
                }
            }
            curr = curr.Parent;
        }
        return null;
    }

    public static string NormalizeUrl(string text)
    {
        return RouteDictionaryRegistry.NormalizeResolvedUrl(text);
    }
}