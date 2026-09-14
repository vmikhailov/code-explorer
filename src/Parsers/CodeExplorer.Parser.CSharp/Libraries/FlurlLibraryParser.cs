using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.CSharp.Libraries;

public class FlurlLibraryParser : ILibraryParser
{
    public string Type => "api";

    public string Name => "Flurl";

    public string Id => "flurl";

    public IReadOnlyList<string> SupportedPatterns => ["Flurl", "Flurl.Http"];

    public bool IsImplemented => true;

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (IsFlurlCall(node))
        {
            return OntologyConstants.NodeLabels.ExternalService;
        }
        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx)
    {
        if (IsFlurlCall(node))
        {
            var rootUrl = ExtractFlurlRootUrl(node);
            if (!string.IsNullOrEmpty(rootUrl))
            {
                if (Uri.TryCreate(rootUrl, UriKind.Absolute, out var uri))
                {
                    return uri.Host;
                }
                return rootUrl;
            }
            return "Flurl Call";
        }
        return null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        // Flurl calls represent external services, no custom inner references are required
    }

    private static bool IsFlurlCall(Node node)
    {
        if (!node.Is(TreeSitterSyntax.CSharp.InvocationExpression)) return false;

        var func = node.GetFunctionNode();
        if (func.Is(TreeSitterSyntax.CSharp.MemberAccessExpression))
        {
            var methodName = func.GetChildFieldText(TreeSitterSyntax.Fields.Name);
            if (!string.IsNullOrEmpty(methodName))
            {
                return methodName is "GetAsync" or "PostAsync" or "PutAsync" or "DeleteAsync" or "PatchAsync"
                                   or "GetJsonAsync" or "PostJsonAsync" or "PutJsonAsync" or "DeleteJsonAsync" or "PatchJsonAsync"
                                   or "GetStringAsync" or "GetStreamAsync" or "GetXmlAsync" or "PostUrlEncodedAsync";
            }
        }
        return false;
    }

    private static string? ExtractFlurlRootUrl(Node node)
    {
        var current = node;
        while (current.IsValid())
        {
            if (current.Is(TreeSitterSyntax.CSharp.InvocationExpression))
            {
                var func = current.GetFunctionNode();
                if (func.Is(TreeSitterSyntax.CSharp.MemberAccessExpression))
                {
                    current = func.GetField(TreeSitterSyntax.Fields.Expression);
                    continue;
                }
            }
            if (current.Is(TreeSitterSyntax.CSharp.MemberAccessExpression))
            {
                current = current.GetField(TreeSitterSyntax.Fields.Expression);
                continue;
            }
            break;
        }

        if (current.IsValid())
        {
            if (current.IsAny(TreeSitterSyntax.CSharp.StringLiteral, TreeSitterSyntax.CSharp.VerbatimStringLiteral))
            {
                return current.Text.Trim('"');
            }
            return current.Text;
        }
        return null;
    }
}
