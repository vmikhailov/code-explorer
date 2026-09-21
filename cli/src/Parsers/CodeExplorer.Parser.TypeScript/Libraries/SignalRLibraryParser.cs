using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public class SignalRLibraryParser : ILibraryParser
{
    public string Type => "api";

    public string Name => "SignalR Client";

    public string Id => "signalr-client";

    public IReadOnlyList<string> SupportedPatterns =>
    [
        "@microsoft/signalr",
        "@aspnet/signalr"
    ];

    public bool IsImplemented => true;

    private static bool IsSignalRCall(Node node)
    {
        if (!node.IsValid() || !node.Is(TreeSitterSyntax.TypeScript.CallExpression))
            return false;

        var func = node.GetFunctionNode();
        if (!func.IsValid() || !func.Is(TreeSitterSyntax.TypeScript.MemberExpression))
            return false;

        var prop = func.GetField(TreeSitterSyntax.Fields.Property);
        if (!prop.IsValid()) return false;

        return string.Equals(prop.Text, "withUrl", StringComparison.OrdinalIgnoreCase);
    }

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (IsSignalRCall(node)) return OntologyConstants.NodeLabels.ExternalService;
        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx)
    {
        if (IsSignalRCall(node))
        {
            var args = AstHelper.GetCallArguments(node);
            if (args.Count > 0 && args[0].IsValid())
            {
                var resolved = AstHelper.ResolveStringOrTemplate(args[0]);
                if (!string.IsNullOrWhiteSpace(resolved))
                {
                    if (Uri.TryCreate(resolved, UriKind.Absolute, out var uri) && !string.IsNullOrEmpty(uri.Host))
                    {
                        var path = uri.AbsolutePath.TrimStart('/');
                        return string.IsNullOrEmpty(path) ? $"ws:{uri.Host}" : $"ws:{uri.Host}/{path}";
                    }
                    var cleanPath = resolved.StartsWith('/') ? resolved : $"/{resolved}";
                    return $"ws:*{cleanPath}";
                }
            }
            return "ws:*";
        }
        return null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx) { }
}
