using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.Rust.Libraries;

/// <summary>
/// Semantic extension for the Reqwest HTTP client in Rust.
/// Detects outbound HTTP requests such as reqwest::get(url) or client.get(url).send().await.
/// Emits ExternalService nodes and CALLS relationships.
/// </summary>
public class ReqwestLibraryParser : DeclarativeSemanticExtension
{
    private static readonly HashSet<string> HttpMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "get", "post", "put", "delete", "patch", "head"
    };

    public ReqwestLibraryParser()
    {
        Id = "reqwest";
        Name = "Reqwest";
        Type = SemanticExtensionType.HttpClient;
        LibraryRole = LibraryRole.General;
        SupportedPatterns = ["reqwest*"];
        CustomAstRules =
        [
            new CustomAstRule
            {
                NodeTypes = System.Collections.Frozen.FrozenSet.ToFrozenSet([TreeSitterSyntax.Rust.CallExpression]),
                MapNodeType = (node, ctx) => IsReqwestCall(node, out _) ? OntologyConstants.NodeLabels.ExternalService : null,
                ExtractIdentifier = (node, _, _, _) => IsReqwestCall(node, out var target) ? target : null,
                CollectReferences = CollectReferencesCore
            }
        ];
    }

    private static void CollectReferencesCore(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (IsReqwestCall(node, out var target) && !string.IsNullOrEmpty(target))
        {
            references.Add(new Reference(scopeSymbolId, target, OntologyConstants.Relationships.Calls));
        }
    }

    public static bool IsReqwestCall(Node node, out string target)
    {
        target = string.Empty;

        if (!node.Is(TreeSitterSyntax.Rust.CallExpression))
        {
            return false;
        }

        if (!RustAstHelper.GetMethodCallInfo(node, out var receiver, out var methodName, out var args))
        {
            return false;
        }

        if (string.IsNullOrEmpty(methodName)) return false;

        // 1. Standalone reqwest::get(url) or reqwest::post(url)
        if (methodName.StartWithIC("reqwest::"))
        {
            var shortMethod = methodName["reqwest::".Length..];
            if (HttpMethods.Contains(shortMethod) && args.Count > 0)
            {
                var url = RustAstHelper.ExtractStringLiteral(args[0]);
                if (!string.IsNullOrEmpty(url))
                {
                    target = RustAstHelper.ExtractHostOrUrl(url);
                    return true;
                }
            }
        }

        // 2. Chained method call on client: client.get(url), client.post(url)
        if (HttpMethods.Contains(methodName) && args.Count > 0 && receiver != null)
        {
            var recText = receiver.Text.ToLowerInvariant();
            if (recText.Contains("client") || recText.Contains("reqwest") || recText.Contains("http"))
            {
                var url = RustAstHelper.ExtractStringLiteral(args[0]);
                if (!string.IsNullOrEmpty(url))
                {
                    target = RustAstHelper.ExtractHostOrUrl(url);
                    return true;
                }
            }
        }

        return false;
    }
}
