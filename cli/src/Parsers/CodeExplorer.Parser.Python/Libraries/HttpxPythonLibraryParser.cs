using System.Collections.Frozen;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.Python.Libraries;

public class HttpxPythonLibraryParser : DeclarativeSemanticExtension
{
    private static readonly HashSet<string> HttpMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "get", "post", "put", "delete", "patch", "head", "options", "request", "stream"
    };

    private static readonly string[] NonHttpReceiverKeywords =
    [
        "header", "cookie", "param", "config", "setting", "dict", "data", "kwarg", "arg", "mapping", "limit", "environ"
    ];

    public HttpxPythonLibraryParser()
    {
        Id = "httpx";
        Name = "httpx";
        Type = SemanticExtensionType.ApiClient;
        SupportedPatterns = ["httpx*"];
        LibraryRole = LibraryRole.General;
        CustomAstRules =
        [
            new CustomAstRule
            {
                NodeTypes = new[] { TreeSitterSyntax.Python.Call }.ToFrozenSet(),
                MapNodeType = static (node, ctx) => IsHttpxCall(node) ? OntologyConstants.NodeLabels.ExternalService : null,
                ExtractIdentifier = static (node, _, _, _) => IsHttpxCall(node) ? ExtractUrlTarget(node) : null
            }
        ];
    }

    public override IEnumerable<PackageDescriptor> GetPackageDescriptors()
    {
        yield return new PackageDescriptor(Id, Name, LibraryRole, "library", SupportedPatterns);
    }

    public static bool IsHttpxCall(Node node)
    {
        if (!node.Is(TreeSitterSyntax.Python.Call)) return false;
        if (!PythonAstHelper.TryGetMemberAccess(node, out var objNode, out var methodName)) return false;
        if (string.IsNullOrEmpty(methodName) || !HttpMethods.Contains(methodName)) return false;

        var objText = objNode?.Text ?? string.Empty;
        if (string.IsNullOrWhiteSpace(objText)) return false;

        if (IsNonHttpReceiver(objText)) return false;

        var lowerObj = objText.ToLowerInvariant();
        if (lowerObj is "httpx" or "httpx._client") return true;

        if (lowerObj.StartsWith("httpx.")) return true;

        // Client / AsyncClient instances
        if (lowerObj is "client" or "c" or "cl" or "async_client" or "http_client" or "api_client" or "test_client" or "testclient" ||
            lowerObj.EndsWith(".client") || lowerObj.EndsWith("_client"))
        {
            return true;
        }

        // httpx.Client().get(...) or httpx.AsyncClient().get(...)
        if (lowerObj.Contains("client()") || lowerObj.Contains("client(") ||
            lowerObj.Contains("asyncclient()") || lowerObj.Contains("asyncclient("))
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

        // 2. Positional argument: request/stream -> pos 1, else pos 0
        if (PythonAstHelper.TryGetMemberAccess(callNode, out _, out var methodName) &&
            (string.Equals(methodName, "request", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(methodName, "stream", StringComparison.OrdinalIgnoreCase)))
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
