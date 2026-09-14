using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public class GotKyLibraryParser : ILibraryParser
{
    public string Type => "api";
    public string Name => "Got / Ky HTTP Clients";
    public string Id => "got-ky";
    public IReadOnlyList<string> SupportedPatterns => ["got", "ky", "ky-universal", "superagent", "needle"];
    public bool IsImplemented => true;

    private static readonly HashSet<string> ClientNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "got", "ky", "superagent", "needle"
    };

    private static readonly HashSet<string> HttpMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "get", "post", "put", "delete", "patch", "head"
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
            var rawArg = AstHelper.ExtractFirstStringArgument(node);
            if (!string.IsNullOrEmpty(rawArg))
            {
                if (Uri.TryCreate(rawArg, UriKind.Absolute, out var uri))
                {
                    return uri.Host;
                }
                return rawArg;
            }

            return "http-client";
        }
        return null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (IsHttpClientCall(node))
        {
            var rawArg = AstHelper.ExtractFirstStringArgument(node);
            if (!string.IsNullOrEmpty(rawArg))
            {
                var target = Uri.TryCreate(rawArg, UriKind.Absolute, out var uri) ? uri.Host : rawArg;
                references.Add(new Reference(scopeSymbolId, target, OntologyConstants.Relationships.Calls));
            }
        }
    }

    private static bool IsHttpClientCall(Node node)
    {
        if (!node.Is(TreeSitterSyntax.TypeScript.CallExpression)) return false;

        var func = node.GetFunctionNode();
        if (!func.IsValid()) return false;

        if (func.Is(TreeSitterSyntax.TypeScript.Identifier))
        {
            return ClientNames.Contains(func.Text);
        }

        if (func.Is(TreeSitterSyntax.TypeScript.MemberExpression))
        {
            var objNode = func.GetField(TreeSitterSyntax.Fields.Object);
            var propNode = func.GetField(TreeSitterSyntax.Fields.Property);

            if (objNode.IsValid() && propNode.IsValid())
            {
                var objText = objNode.Text;
                var propText = propNode.Text;

                return ClientNames.Contains(objText) && HttpMethods.Contains(propText);
            }
        }

        return false;
    }
}
