using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.Kotlin.Libraries;

public class KoinLibraryParser : ISemanticExtension
{
    public string Name => "Koin (Kotlin DI)";
    public string Id => "koin";
    public SemanticExtensionType Type => SemanticExtensionType.Library;
    public IReadOnlyList<string> SupportedPatterns =>
    [
        "org.koin.*",
        "io.insert-koin:koin*"
    ];
    public bool IsImplemented => true;
    public LibraryRole LibraryRole => LibraryRole.General;

    public string? MapNodeType(Node node, ParsingContext ctx) => null;
    public string? ExtractIdentifier(Node node, ParsingContext ctx) => null;
    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx) { }
}
