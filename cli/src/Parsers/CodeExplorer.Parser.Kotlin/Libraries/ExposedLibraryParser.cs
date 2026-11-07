using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.Kotlin.Libraries;

public class ExposedLibraryParser : ISemanticExtension
{
    public string Name => "JetBrains Exposed";
    public string Id => "exposed";
    public SemanticExtensionType Type => SemanticExtensionType.RelationalDb;
    public IReadOnlyList<string> SupportedPatterns =>
    [
        "org.jetbrains.exposed.*",
        "org.jetbrains.exposed:exposed*"
    ];
    public bool IsImplemented => true;
    public LibraryRole LibraryRole => LibraryRole.OrmOrDatabase;

    public string? MapNodeType(Node node, ParsingContext ctx) => null;
    public string? ExtractIdentifier(Node node, ParsingContext ctx) => null;
    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx) { }
}
