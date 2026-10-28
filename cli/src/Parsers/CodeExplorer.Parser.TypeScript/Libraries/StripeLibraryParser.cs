using System.Text.RegularExpressions;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public class StripeLibraryParser : ISemanticExtension
{
    public string Type => OntologyConstants.LibraryTypes.Cloud;
    public string Name => "Stripe";
    public string Id => "stripe";
    public IReadOnlyList<string> SupportedPatterns => ["stripe", "@stripe/*"];
    public bool IsImplemented => true;
    public LibraryRole LibraryRole => LibraryRole.CloudSdk;

    private static readonly HashSet<string> StripeActions = new(StringComparer.OrdinalIgnoreCase)
    {
        "create", "retrieve", "update", "list", "del", "cancel", "capture", "confirm"
    };

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (IsStripeCall(node))
        {
            return OntologyConstants.NodeLabels.ExternalService;
        }
        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx) => ExtractIdentifier(node, ctx, null);

    public string? ExtractIdentifier(Node node, ParsingContext ctx, string? projectName)
    {
        if (IsStripeCall(node))
        {
            var path = ExtractStripePath(node);
            if (!string.IsNullOrEmpty(path))
            {
                return $"https://api.stripe.com/v1/{path}";
            }
            return "https://api.stripe.com/v1";
        }
        return null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (IsStripeCall(node))
        {
            references.Add(new Reference(scopeSymbolId, "api.stripe.com", OntologyConstants.Relationships.CallsEndpoint));
        }
    }

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
