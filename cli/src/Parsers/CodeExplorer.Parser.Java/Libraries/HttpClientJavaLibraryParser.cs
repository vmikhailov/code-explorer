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
            var str = node.FindChildOfType(TreeSitterSyntax.Java.StringLiteral);
            if (str.IsValid()) return $"Feign: {str.Text.Trim('"')}";
            foreach (var pair in node.Children)
            {
                if (pair.Is(TreeSitterSyntax.Java.ElementValuePair))
                {
                    var key = pair.FindChildOfType(TreeSitterSyntax.Java.Identifier);
                    if (key.IsValid() && key.Text is "name" or "value" or "url")
                    {
                        var val = pair.FindChildOfType(TreeSitterSyntax.Java.StringLiteral);
                        if (val.IsValid()) return $"Feign: {val.Text.Trim('"')}";
                    }
                }
            }
        }

        if (node.Is(TreeSitterSyntax.Java.MethodInvocation))
        {
            var argList = node.FindChildOfType(TreeSitterSyntax.Java.ArgumentList);
            if (argList.IsValid())
            {
                var strArg = argList.Children.FirstOrDefault(c => c.IsAny(TreeSitterSyntax.Java.StringLiteral, TreeSitterSyntax.Java.TextBlock));
                if (strArg.IsValid())
                {
                    return $"HTTP: {strArg.Text.Trim('"')}";
                }
            }
        }

        return "HTTP Request";
    }
}
