using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.Python.Libraries;

public class RequestsPythonLibraryParser : ISemanticExtension
{
    public string Id => "requests";
    public string Name => "requests";
    public string Type => "api:client";
    public IReadOnlyList<string> SupportedPatterns => ["requests*"];
    public bool IsImplemented => true;
    public LibraryRole LibraryRole => LibraryRole.General;

    private static readonly HashSet<string> HttpMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "get", "post", "put", "delete", "patch", "head", "options", "request"
    };

    private static readonly string[] NonHttpReceiverKeywords =
    [
        "header", "cookie", "param", "config", "setting", "dict", "data", "kwarg", "arg", "mapping", "limit", "environ"
    ];

    public IEnumerable<PackageDescriptor> GetPackageDescriptors()
    {
        yield return new PackageDescriptor(Id, Name, LibraryRole, "library", SupportedPatterns);
    }

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (IsRequestsCall(node))
        {
            return OntologyConstants.NodeLabels.ExternalService;
        }
        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx) => ExtractIdentifier(node, ctx, null, null);
    public string? ExtractIdentifier(Node node, ParsingContext ctx, string? projectName) => ExtractIdentifier(node, ctx, projectName, null);

    public string? ExtractIdentifier(Node node, ParsingContext ctx, string? projectName, string? relativePath)
    {
        if (IsRequestsCall(node))
        {
            return ExtractUrlTarget(node);
        }
        return null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx) { }

    public static bool IsRequestsCall(Node node)
    {
        if (!node.Is(TreeSitterSyntax.Python.Call)) return false;
        if (!PythonAstHelper.TryGetMemberAccess(node, out var objNode, out var methodName)) return false;
        if (string.IsNullOrEmpty(methodName) || !HttpMethods.Contains(methodName)) return false;

        var objText = objNode?.Text ?? string.Empty;
        if (string.IsNullOrWhiteSpace(objText)) return false;

        if (IsNonHttpReceiver(objText)) return false;

        var lowerObj = objText.ToLowerInvariant();
        if (lowerObj is "requests" or "requests.api" or "requests.sessions") return true;

        if (lowerObj.StartsWith("requests.")) return true;

        // Session object instances
        if (lowerObj is "session" or "s" or "sess" or "req_session" or "req" or "requests_session" or "client" or "http" ||
            lowerObj.EndsWith(".session") || lowerObj.EndsWith("_session") || lowerObj.EndsWith("_client"))
        {
            return true;
        }

        // requests.Session().get(...)
        if (lowerObj.Contains("session()") || lowerObj.Contains("session("))
        {
            return true;
        }

        return false;
    }

    public static string ExtractUrlTarget(Node callNode)
    {
        // 1. Keyword argument url=...
        var kwUrl = PythonAstHelper.ResolveCallArgument(callNode, -1, "url");
        if (!string.IsNullOrEmpty(kwUrl))
        {
            return CleanUrl(kwUrl);
        }

        // 2. Positional argument: requests.request("METHOD", url) -> pos 1, else pos 0
        if (PythonAstHelper.TryGetMemberAccess(callNode, out _, out var methodName) &&
            string.Equals(methodName, "request", StringComparison.OrdinalIgnoreCase))
        {
            var target = PythonAstHelper.ResolveCallArgument(callNode, 1);
            if (!string.IsNullOrEmpty(target)) return CleanUrl(target);
        }
        else
        {
            var target = PythonAstHelper.ResolveCallArgument(callNode, 0);
            if (!string.IsNullOrEmpty(target)) return CleanUrl(target);
        }

        // 3. Fallback scan all arguments
        var args = PythonAstHelper.GetCallArguments(callNode);
        foreach (var arg in args)
        {
            var resolved = PythonAstHelper.ResolveStringOrVariable(arg);
            if (!string.IsNullOrEmpty(resolved) && (resolved.Contains('/') || resolved.StartWithIC("http")))
            {
                return CleanUrl(resolved);
            }
        }

        return "http:unknown-service";
    }

    private static bool IsNonHttpReceiver(string objText)
    {
        var lower = objText.ToLowerInvariant();
        return NonHttpReceiverKeywords.Any(k => lower.Contains(k));
    }

    private static string CleanUrl(string url)
    {
        return url.Trim('\'', '"');
    }
}
