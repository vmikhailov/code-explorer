using System.Collections.Frozen;
using System.Text.RegularExpressions;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public class StripeLibraryParser : DeclarativeSemanticExtension
{
    private static readonly HashSet<string> StripeActions = new(StringComparer.OrdinalIgnoreCase)
    {
        "create", "retrieve", "update", "list", "del", "cancel", "capture", "confirm"
    };

    public StripeLibraryParser()
    {
        Type = SemanticExtensionType.Cloud;
        Name = "Stripe";
        Id = "stripe";
        SupportedPatterns = ["stripe", "@stripe/*"];
        LibraryRole = LibraryRole.CloudSdk;
        CustomAstRules =
        [
            new CustomAstRule
            {
                NodeTypes = new[] { TreeSitterSyntax.TypeScript.CallExpression }.ToFrozenSet(),
                MapNodeType = static (node, ctx) => IsStripeCall(node) ? OntologyConstants.NodeLabels.ExternalService : null,
                ExtractIdentifier = static (node, _, _, _) =>
                {
                    if (!IsStripeCall(node)) return null;
                    var path = ExtractStripePath(node);
                    return !string.IsNullOrEmpty(path) ? $"https://api.stripe.com/v1/{path}" : "https://api.stripe.com/v1";
                },
                CollectReferences = static (node, scopeSymbolId, references, ctx) =>
                {
                    if (IsStripeCall(node))
                    {
                        references.Add(new Reference(scopeSymbolId, StripeApiEndpoint, OntologyConstants.Relationships.CallsEndpoint));
                    }
                }
            }
        ];
    }

    private const string StripeApiEndpoint = "api.stripe.com";

    private static bool IsStripeCall(Node node)
    {
        if (!node.Is(TreeSitterSyntax.TypeScript.CallExpression)) return false;

        return ExtractStripePath(node) != null;
    }

    private static string? ExtractStripePath(Node node)
    {
        if (!AstHelper.TryGetMemberAccess(node, out var objNode, out var actionName)) return null;
        if (actionName == null || !StripeActions.Contains(actionName)) return null;
        if (!objNode.IsValid()) return null;

        var fullReceiver = objNode.Text;
        if (string.IsNullOrEmpty(fullReceiver)) return null;

        var parts = fullReceiver.Split('.');
        if (parts.Length < 2) return null;

        var root = parts[0].ToLowerInvariant();
        if (root.StartsWith("this.")) root = root[5..];
        if (!root.Contains("stripe")) return null;

        var resourceSegments = parts.Skip(1).Select(CamelToSnake).ToArray();
        return string.Join("/", resourceSegments);
    }

    private static string CamelToSnake(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        return Regex.Replace(input, @"(?<!^)(?=[A-Z])", "_").ToLowerInvariant();
    }
}
