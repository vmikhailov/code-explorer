using CodeExplorer.Common;
using CodeExplorer.Core.Parser;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.CSharp.Libraries;

public class StripeLibraryParser : ISemanticExtension
{
    public string Type => "cloud";
    public string Name => "Stripe";
    public string Id => "stripe";
    public IReadOnlyList<string> SupportedPatterns => ["Stripe", "Stripe.*", "Stripe.net"];
    public bool IsImplemented => true;
    public LibraryRole LibraryRole => LibraryRole.CloudSdk;

    public string? MapNodeType(Node node, ParsingContext ctx) => null;
    public string? ExtractIdentifier(Node node, ParsingContext ctx) => null;
    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx) { }
}
