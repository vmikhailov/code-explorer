using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.Java.Libraries;

public class HttpClientJavaLibraryParser : ILibraryParser
{
    public string Type => "api:client";
    public string Name => "Java HTTP Clients (HttpClient / RestTemplate / WebClient / OkHttp / Feign)";
    public string Id => "java-http";

    public IReadOnlyList<string> SupportedPatterns =>
    [
        "java.net.http",
        "org.springframework.web.client",
        "org.springframework.web.reactive.function.client",
        "okhttp3",
        "org.apache.http",
        "org.apache.hc.client5",
        "org.springframework.cloud.openfeign",
        "feign"
    ];

    public bool IsImplemented => true;

    private static readonly HashSet<string> HttpCallMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "send", "sendAsync", "execute", "newCall",
        "getForObject", "getForEntity", "postForObject", "postForEntity", "put", "delete", "exchange",
        "get", "post", "retrieve", "exchangeToMono", "exchangeToFlux"
    };

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (IsHttpClientCall(node))
        {
            return OntologyConstants.NodeLabels.ExternalService;
        }
        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx)
    {
        if (IsHttpClientCall(node))
        {
            return ExtractTarget(node) ?? "HTTP Service";
        }
        return null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
    }

    public static bool IsHttpClientCall(Node node)
    {
        if (node.Is(TreeSitterSyntax.Java.MethodInvocation))
        {
            var name = node.GetChildFieldText(TreeSitterSyntax.Fields.Name);
            if (!string.IsNullOrEmpty(name) && HttpCallMethods.Contains(name))
            {
                return true;
            }
        }

        if (node.IsAny(TreeSitterSyntax.Java.MarkerAnnotation, TreeSitterSyntax.Java.Annotation))
        {
            var nameNode = node.FindChildOfType(TreeSitterSyntax.Java.Identifier)
                           ?? node.FindChildOfType(TreeSitterSyntax.Java.ScopedIdentifier);
            if (nameNode.IsValid() && nameNode.Text.EndsWith("FeignClient", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public static string? ExtractTarget(Node node)
    {
        if (node.IsAny(TreeSitterSyntax.Java.MarkerAnnotation, TreeSitterSyntax.Java.Annotation))
        {
            string? nameVal = null;
            string? pathVal = null;

            var str = node.FindChildOfType(TreeSitterSyntax.Java.StringLiteral);
            if (str.IsValid()) nameVal = str.Text.Trim('"');

            var pairs = new List<Node>();
            CollectNodes(node, TreeSitterSyntax.Java.ElementValuePair, pairs);
            foreach (var pair in pairs)
            {
                var key = pair.FindChildOfType(TreeSitterSyntax.Java.Identifier);
                var val = pair.FindChildOfType(TreeSitterSyntax.Java.StringLiteral);
                if (key.IsValid() && val.IsValid())
                {
                    var keyText = key.Text;
                    var valText = val.Text.Trim('"');
                    if (keyText is "name" or "value")
                    {
                        nameVal = valText;
                    }
                    else if (keyText is "path")
                    {
                        pathVal = valText;
                    }
                    else if (keyText is "url")
                    {
                        return RouteDictionaryRegistry.NormalizeResolvedUrl(valText);
                    }
                }
            }

            if (!string.IsNullOrEmpty(nameVal))
            {
                var combined = !string.IsNullOrEmpty(pathVal) ? $"{nameVal}/{pathVal.TrimStart('/')}" : nameVal;
                return RouteDictionaryRegistry.NormalizeResolvedUrl(combined);
            }
        }

        if (node.Is(TreeSitterSyntax.Java.MethodInvocation))
        {
            var argList = node.FindChildOfType(TreeSitterSyntax.Java.ArgumentList);
            if (argList.IsValid())
            {
                foreach (var child in argList.Children)
                {
                    if (child.IsAny(TreeSitterSyntax.Java.StringLiteral, TreeSitterSyntax.Java.TextBlock))
                    {
                        var text = child.Text.Trim('"');
                        if (text.Contains('/') || text.StartsWith("http"))
                        {
                            return RouteDictionaryRegistry.NormalizeResolvedUrl(text);
                        }
                    }
                    else if (child.Is(TreeSitterSyntax.Java.Identifier))
                    {
                        var varName = child.Text;
                        if (RouteDictionaryRegistry.TryResolve(varName, out var rPath, out var rService))
                        {
                            var cleanPath = rPath.Split('?')[0];
                            return !string.IsNullOrEmpty(rService) ? $"{rService}{cleanPath}" : cleanPath;
                        }

                        var resolved = FindJavaVariableInitializer(child, varName);
                        if (!string.IsNullOrEmpty(resolved))
                        {
                            return RouteDictionaryRegistry.NormalizeResolvedUrl(resolved);
                        }
                    }
                    else if (child.Type is "binary_expression" or TreeSitterSyntax.Common.BinaryExpression)
                    {
                        var left = child.GetField(TreeSitterSyntax.Fields.Left) ?? (child.Children.Count >= 1 ? child.Children[0] : null);
                        if (left.IsValid() && left.IsAny(TreeSitterSyntax.Java.StringLiteral, TreeSitterSyntax.Java.TextBlock))
                        {
                            var text = left.Text.Trim('"');
                            if (text.Contains('/') || text.StartsWith("http"))
                            {
                                return RouteDictionaryRegistry.NormalizeResolvedUrl(text.EndsWith('/') ? text + "*" : text);
                            }
                        }

                        var right = child.GetField(TreeSitterSyntax.Fields.Right) ?? (child.Children.Count >= 3 ? child.Children[2] : null);
                        if (right.IsValid() && right.IsAny(TreeSitterSyntax.Java.StringLiteral, TreeSitterSyntax.Java.TextBlock))
                        {
                            return RouteDictionaryRegistry.NormalizeResolvedUrl(right.Text.Trim('"'));
                        }
                    }
                }
            }
        }

        return "http:unknown-service";
    }

    private static string? FindJavaVariableInitializer(Node node, string varName)
    {
        var curr = node.Parent;
        while (curr.IsValid())
        {
            if (curr.IsAny(TreeSitterSyntax.Java.Block, TreeSitterSyntax.Java.MethodDeclaration, TreeSitterSyntax.Java.ClassBody))
            {
                foreach (var child in curr.Children)
                {
                    if (child.Is(TreeSitterSyntax.Java.LocalVariableDeclaration) || child.Is(TreeSitterSyntax.Java.FieldDeclaration))
                    {
                        var decls = child.Children.Where(c => c.Is(TreeSitterSyntax.Java.VariableDeclarator)).ToList();
                        foreach (var decl in decls)
                        {
                            var nameNode = decl.GetChildForField(TreeSitterSyntax.Fields.Name) ??
                                           decl.Children.FirstOrDefault(c => c.Is(TreeSitterSyntax.Java.Identifier));
                            if (nameNode.IsValid() && nameNode.Text == varName)
                            {
                                var valNode = decl.GetChildForField(TreeSitterSyntax.Fields.Value) ??
                                              (decl.Children.Count >= 3 ? decl.Children[^1] : null);
                                if (valNode.IsValid())
                                {
                                    if (valNode.IsAny(TreeSitterSyntax.Java.StringLiteral, TreeSitterSyntax.Java.TextBlock))
                                    {
                                        return valNode.Text.Trim('"');
                                    }
                                    if (valNode.Type is "binary_expression" or TreeSitterSyntax.Common.BinaryExpression)
                                    {
                                        var left = valNode.GetChildForField(TreeSitterSyntax.Fields.Left) ??
                                                   (valNode.Children.Count > 0 ? valNode.Children[0] : null);
                                        var right = valNode.GetChildForField(TreeSitterSyntax.Fields.Right) ??
                                                    (valNode.Children.Count >= 3 ? valNode.Children[2] : null);

                                        if (left.IsValid() && left.IsAny(TreeSitterSyntax.Java.StringLiteral, TreeSitterSyntax.Java.TextBlock))
                                        {
                                            var leftText = left.Text.Trim('"');
                                            if (right.IsValid() && right.IsAny(TreeSitterSyntax.Java.StringLiteral, TreeSitterSyntax.Java.TextBlock))
                                            {
                                                return leftText + right.Text.Trim('"');
                                            }
                                            return leftText + "*";
                                        }
                                        if (right.IsValid() && right.IsAny(TreeSitterSyntax.Java.StringLiteral, TreeSitterSyntax.Java.TextBlock))
                                        {
                                            return right.Text.Trim('"');
                                        }
                                    }
                                    if (valNode.Is(TreeSitterSyntax.Java.MethodInvocation))
                                    {
                                        var argList = valNode.FindChildOfType(TreeSitterSyntax.Java.ArgumentList);
                                        var str = argList?.Children.FirstOrDefault(c => c.IsAny(TreeSitterSyntax.Java.StringLiteral, TreeSitterSyntax.Java.TextBlock));
                                        if (str.IsValid()) return str.Text.Trim('"');
                                    }
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
}
