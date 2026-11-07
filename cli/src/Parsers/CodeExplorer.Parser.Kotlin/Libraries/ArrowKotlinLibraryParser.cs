using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.Kotlin.Libraries;

public class ArrowKotlinLibraryParser : ISemanticExtension
{
    public string Name => "Arrow-kt";
    public string Id => "arrow-kt";
    public SemanticExtensionType Type => SemanticExtensionType.Library;
    public IReadOnlyList<string> SupportedPatterns =>
    [
        "arrow.core.*",
        "arrow.fx.*",
        "io.arrow-kt:arrow*"
    ];
    public bool IsImplemented => true;
    public LibraryRole LibraryRole => LibraryRole.General;

    public string? MapNodeType(Node node, ParsingContext ctx) => null;
    public string? ExtractIdentifier(Node node, ParsingContext ctx) => null;
    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx) { }
}
