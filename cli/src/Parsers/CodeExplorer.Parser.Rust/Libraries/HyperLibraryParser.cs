using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.Rust.Libraries;

/// <summary>
/// Semantic extension for the Hyper HTTP client in Rust.
/// Detects low-level request builder invocations like hyper::Request::builder().uri(url).
/// </summary>
public class HyperLibraryParser : DeclarativeSemanticExtension
{
    public HyperLibraryParser()
    {
        Id = "hyper";
        Name = "Hyper";
        Type = SemanticExtensionType.HttpClient;
        LibraryRole = LibraryRole.General;
        SupportedPatterns = ["hyper*"];
        CustomAstRules =
        [
            new CustomAstRule
            {
                NodeTypes = System.Collections.Frozen.FrozenSet.ToFrozenSet([TreeSitterSyntax.Rust.CallExpression]),
                MapNodeType = (node, ctx) => IsHyperCall(node, out _) ? OntologyConstants.NodeLabels.ExternalService : null,
                ExtractIdentifier = (node, _, _, _) => IsHyperCall(node, out var target) ? target : null,
                CollectReferences = CollectReferencesCore
            }
        ];
    }

    private static void CollectReferencesCore(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (IsHyperCall(node, out var target) && !string.IsNullOrEmpty(target))
        {
            references.Add(new Reference(scopeSymbolId, target, OntologyConstants.Relationships.Calls));
        }
    }

    public static bool IsHyperCall(Node node, out string target)
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

        if (methodName == "uri" && args.Count > 0 && receiver != null)
        {
            var recText = receiver.Text;
            if (recText.Contains("Request") || recText.Contains("builder") || recText.Contains("hyper"))
            {
                var uri = RustAstHelper.ExtractStringLiteral(args[0]);
                if (!string.IsNullOrEmpty(uri))
                {
                    target = RustAstHelper.ExtractHostOrUrl(uri);
                    return true;
                }
            }
        }

        return false;
    }
}
