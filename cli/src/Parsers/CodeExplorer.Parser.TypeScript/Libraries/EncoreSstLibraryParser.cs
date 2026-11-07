using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public class EncoreSstLibraryParser : ISemanticExtension
{
    private static readonly char[] QuoteChars = ['"', '\'', '`'];
    private const string EncoreTopicPrefix = "encore:";

    public string Name => "Encore.ts, SST & Nitric";
    public string Id => "encore-sst";
    public SemanticExtensionType Type => SemanticExtensionType.Framework;
    public IReadOnlyList<string> SupportedPatterns =>
    [
        "encore.dev",
        "encore.dev/*",
        "sst",
        "sst/*",
        "@nitric/sdk*"
    ];
    public bool IsImplemented => true;
    public LibraryRole LibraryRole => LibraryRole.WebService;

    public string? MapNodeType(Node node, ParsingContext ctx) => null;
    public string? ExtractIdentifier(Node node, ParsingContext ctx) => null;

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (node.Is(TreeSitterSyntax.TypeScript.NewExpression))
        {
            var ctor = node.GetField(TreeSitterSyntax.Fields.Constructor);
            if (!ctor.IsValid()) return;

            var ctorName = ctor.Text;
            var firstArg = ExtractFirstStringLiteral(node);
            if (string.IsNullOrEmpty(firstArg)) return;

            if (ctorName is "Topic" or "pubsub.Topic")
            {
                references.Add(new Reference(scopeSymbolId, EncoreTopicPrefix + firstArg, OntologyConstants.Relationships.PublishesTo));
            }
            else if (ctorName is "Subscription" or "pubsub.Subscription")
            {
                references.Add(new Reference(scopeSymbolId, EncoreTopicPrefix + firstArg, OntologyConstants.Relationships.SubscribesTo));
            }
            else if (ctorName is "SQLDatabase" or "Bucket")
            {
                references.Add(new Reference(scopeSymbolId, firstArg, OntologyConstants.Relationships.UsesDb));
            }
            return;
        }

        if (!node.Is(TreeSitterSyntax.TypeScript.CallExpression)) return;

        var func = node.GetFunctionNode();
        if (!func.IsValid() || func.Text != "api") return;

        var args = node.GetField(TreeSitterSyntax.Fields.Arguments);
        if (!args.IsValid()) return;

        string? path = null;
        var method = "POST";

        foreach (var child in args.Descendants())
        {
            if (child.Is(TreeSitterSyntax.TypeScript.Pair))
            {
                var k = child.GetField(TreeSitterSyntax.Fields.Key);
                var v = child.GetField(TreeSitterSyntax.Fields.Value);
                if (k.IsValid() && v.IsValid())
                {
                    var keyText = k.Text.Trim(QuoteChars);
                    var valText = v.Text.Trim(QuoteChars);
                    if (keyText == "path") path = valText;
                    else if (keyText == "method" && !string.IsNullOrEmpty(valText)) method = valText.ToUpperInvariant();
                }
            }
        }

        if (!string.IsNullOrEmpty(path))
        {
            references.Add(new Reference(scopeSymbolId, $"{method}:{path}", OntologyConstants.Relationships.Exposes));
        }
    }

    internal static string? ExtractFirstStringLiteral(Node node)
    {
        var args = node.GetField(TreeSitterSyntax.Fields.Arguments);
        if (!args.IsValid()) return null;

        foreach (var child in args.Descendants())
        {
            if (child.Is(TreeSitterSyntax.TypeScript.String))
            {
                var val = child.Text.Trim(QuoteChars);
                if (!string.IsNullOrEmpty(val)) return val;
            }
        }
        return null;
    }
}
