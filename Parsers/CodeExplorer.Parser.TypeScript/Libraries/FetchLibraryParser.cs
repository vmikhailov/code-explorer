using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public class FetchLibraryParser : ILibraryParser
{
    public string Name => "Fetch";
    public string Id => "fetch";
    public string Type => "api";
    // fetch is built-in; node-fetch/got/superagent are npm packages
    public IReadOnlyList<string> SupportedPatterns => ["node-fetch", "got", "superagent", "cross-fetch", "isomorphic-fetch"];
    public bool IsImplemented => true;
    public bool IsBuiltIn => true;   // fetch is available without import in browsers/Node 18+

    private static readonly HashSet<string> DirectCallNames = ["fetch", "nodeFetch", "got", "superagent"];
    private static readonly HashSet<string> ObjectNames = ["got", "superagent", "request", "http", "https"];
    private static readonly HashSet<string> HttpMethods = ["get", "post", "put", "delete", "request", "patch", "head"];

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (IsHttpCall(node)) return OntologyConstants.NodeLabels.ExternalService;
        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx)
    {
        if (IsHttpCall(node)) return ExtractTarget(node);
        return null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx) { }

    private static bool IsHttpCall(Node node)
    {
        if (node.Type != "call_expression") return false;
        var func = node.GetChildForField("function")
                   ?? (node.Children.Count > 0 ? node.Children[0] : null);
        if (func == null || func.Id == IntPtr.Zero) return false;

        if (func.Type == "identifier") return DirectCallNames.Contains(func.Text);

        if (func.Type == "member_expression")
        {
            var obj = func.GetChildForField("object");
            var prop = func.GetChildForField("property");
            if (obj != null && prop != null && prop.Id != IntPtr.Zero)
                return ObjectNames.Contains(obj.Text) && HttpMethods.Contains(prop.Text);
        }
        return false;
    }

    private static string? ExtractTarget(Node node)
    {
        var args = node.Children.FirstOrDefault(c => c.Type == "arguments");
        if (args != null)
        {
            var firstArg = args.Children.FirstOrDefault(c => c.Type is "string" or "template_string");
            if (firstArg != null)
            {
                var url = firstArg.Text.Trim('\'', '"', '`');
                if (Uri.TryCreate(url, UriKind.Absolute, out var uri)) return uri.Host;
                return url;
            }
        }
        return "http:unknown-service";
    }
}
