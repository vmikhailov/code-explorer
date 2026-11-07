using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.Java.Libraries;

public class Resilience4jLibraryParser : ISemanticExtension
{
    public string Name => "Resilience4j";
    public string Id => "resilience4j";
    public SemanticExtensionType Type => SemanticExtensionType.Library;
    public IReadOnlyList<string> SupportedPatterns => ["io.github.resilience4j.*"];
    public bool IsImplemented => true;
    public LibraryRole LibraryRole => LibraryRole.General;

    private static readonly HashSet<string> ResilienceAnnotations = new(StringComparer.OrdinalIgnoreCase)
    {
        "CircuitBreaker", "Retry", "RateLimiter", "Bulkhead", "TimeLimiter"
    };

    private const string AnnotationNodeType = "annotation";
    private const string MarkerAnnotationNodeType = "marker_annotation";

    public string? MapNodeType(Node node, ParsingContext ctx) => null;
    public string? ExtractIdentifier(Node node, ParsingContext ctx) => null;

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (!node.IsAny(AnnotationNodeType, MarkerAnnotationNodeType)) return;

        var nameNode = node.GetField(TreeSitterSyntax.Fields.Name) ?? node.Children.FirstOrDefault();
        if (!nameNode.IsValid()) return;

        var shortName = nameNode.Text.TrimStart('@');
        if (ResilienceAnnotations.Contains(shortName))
        {
            references.Add(new Reference(scopeSymbolId, "resilience4j:" + shortName, OntologyConstants.Relationships.Calls));
        }
    }
}
