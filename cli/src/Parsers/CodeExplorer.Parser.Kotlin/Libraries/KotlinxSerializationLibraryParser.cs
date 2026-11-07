using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.Kotlin.Libraries;

public class KotlinxSerializationLibraryParser : ISemanticExtension
{
    public string Name => "kotlinx.serialization";
    public string Id => "kotlinx-serialization";
    public SemanticExtensionType Type => SemanticExtensionType.Library;
    public IReadOnlyList<string> SupportedPatterns =>
    [
        "kotlinx.serialization.*",
        "org.jetbrains.kotlinx:kotlinx-serialization*"
    ];
    public bool IsImplemented => true;
    public LibraryRole LibraryRole => LibraryRole.General;

    public string? MapNodeType(Node node, ParsingContext ctx) => null;
    public string? ExtractIdentifier(Node node, ParsingContext ctx) => null;
    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx) { }
}
