using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.Kotlin.Libraries;

public class KtormLibraryParser : ISemanticExtension
{
    public string Name => "Ktorm";
    public string Id => "ktorm";
    public SemanticExtensionType Type => SemanticExtensionType.RelationalDb;
    public IReadOnlyList<string> SupportedPatterns =>
    [
        "org.ktorm.*",
        "org.ktorm:ktorm*"
    ];
    public bool IsImplemented => true;
    public LibraryRole LibraryRole => LibraryRole.OrmOrDatabase;

    public string? MapNodeType(Node node, ParsingContext ctx) => null;
    public string? ExtractIdentifier(Node node, ParsingContext ctx) => null;
    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx) { }
}
