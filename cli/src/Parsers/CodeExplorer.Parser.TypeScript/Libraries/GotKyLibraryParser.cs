using System.Collections.Frozen;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public class GotKyLibraryParser : DeclarativeSemanticExtension
{
    private static readonly HashSet<string> ClientNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "got", "ky", "superagent", "needle"
    };

    private static readonly HashSet<string> HttpMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "get", "post", "put", "delete", "patch", "head"
    };

    public GotKyLibraryParser()
    {
        Type = SemanticExtensionType.ApiClient;
        Name = "Got / Ky HTTP Clients";
        Id = "got-ky";
        SupportedPatterns = ["got", "ky", "ky-universal", "superagent", "needle"];
        CustomAstRules =
        [
            new CustomAstRule
            {
                NodeTypes = new[] { TreeSitterSyntax.TypeScript.CallExpression }.ToFrozenSet(),
                MapNodeType = static (node, ctx) => IsHttpClientCall(node) ? OntologyConstants.NodeLabels.ExternalService : null,
                ExtractIdentifier = static (node, _, _, _) =>
                {
                    if (!IsHttpClientCall(node)) return null;
                    var rawArg = AstHelper.ExtractFirstStringArgument(node);
                    if (!string.IsNullOrEmpty(rawArg))
                    {
                        return Uri.TryCreate(rawArg, UriKind.Absolute, out var uri) ? uri.Host : rawArg;
                    }
                    return "http-client";
                },
                CollectReferences = static (node, scopeSymbolId, references, ctx) =>
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
            }
        ];
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
