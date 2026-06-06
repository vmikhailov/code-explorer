using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public class ExpressLibraryParser : ILibraryParser
{
    public string Name => "Express";
    public string Id => "express";
    public string Type => "framework";
    public IReadOnlyList<string> SupportedPatterns => ["express", "@types/express"];
    public bool IsImplemented => true;

    private static readonly HashSet<string> HttpMethods = ["get", "post", "put", "delete", "patch"];

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (IsExpressRoute(node)) return OntologyConstants.NodeLabels.EntryPoint;
        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx)
    {
        if (IsExpressRoute(node)) return ExtractRoute(node);
        return null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx) { }

    private static bool IsExpressRoute(Node node)
    {
        if (node.Type != "call_expression") return false;
        var func = node.GetChildForField("function")
                   ?? (node.Children.Count > 0 ? node.Children[0] : null);
        if (func == null || func.Id == IntPtr.Zero) return false;

        if (func.Type != "member_expression") return false;
        var obj = func.GetChildForField("object");
        var prop = func.GetChildForField("property");
        return obj != null && prop != null && prop.Id != IntPtr.Zero
            && obj.Text is "app" or "router" or "express"
            && HttpMethods.Contains(prop.Text);
    }

    private static string? ExtractRoute(Node node)
    {
        var func = node.GetChildForField("function")
                   ?? (node.Children.Count > 0 ? node.Children[0] : null);
        if (func == null) return null;
        var prop = func.GetChildForField("property");
        if (prop == null) return null;

        var args = node.Children.FirstOrDefault(c => c.Type == "arguments");
        var routeVal = "/";
        if (args != null)
        {
            var firstArg = args.Children.FirstOrDefault(c => c.Type is "string" or "template_string");
            if (firstArg != null) routeVal = firstArg.Text.Trim('\'', '"', '`');
        }
        return $"{prop.Text.ToUpperInvariant()}:{routeVal}";
    }
}
