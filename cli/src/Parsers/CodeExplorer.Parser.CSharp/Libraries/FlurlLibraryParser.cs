using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.CSharp.Libraries;

public class FlurlLibraryParser : DeclarativeSemanticExtension
{
    public FlurlLibraryParser()
    {
        Id = "flurl";
        Name = "Flurl";
        Type = SemanticExtensionType.ApiClient;
        LibraryRole = LibraryRole.General;
        SupportedPatterns = ["Flurl", "Flurl.Http", "Flurl.*"];
        CustomAstRules =
        [
            new CustomAstRule
            {
                NodeTypes = System.Collections.Frozen.FrozenSet.ToFrozenSet([TreeSitterSyntax.CSharp.InvocationExpression]),
                MapNodeType = MapNodeTypeCore,
                ExtractIdentifier = (node, _, _, _) => ExtractIdentifierCore(node)
            }
        ];
    }

    private static string? MapNodeTypeCore(Node node, ParsingContext ctx)
    {
        if (IsFlurlCall(node))
        {
            var target = ExtractIdentifierCore(node);
            if (!string.IsNullOrEmpty(target) && target != "flurl-service")
            {
                return OntologyConstants.NodeLabels.ExternalService;
            }
        }
        return null;
    }

    private const string HttpPrefix = "http://";
    private const string HttpsPrefix = "https://";

    private static string? ExtractIdentifierCore(Node node)
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
                if (rootUrl.StartWithIC(HttpPrefix) || rootUrl.StartWithIC(HttpsPrefix))
                {
                    return rootUrl;
                }
                if (ConstantRegistry.TryResolve(null, rootUrl, out var resolved) && Uri.TryCreate(resolved, UriKind.Absolute, out var rUri))
                {
                    return rUri.Host;
                }
            }
            return null;
        }
        return null;
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
