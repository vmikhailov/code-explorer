using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.Kotlin.Libraries;

public class GraphQlKotlinLibraryParser : ISemanticExtension
{
    public string Name => "graphql-kotlin & DGS";
    public string Id => "graphql-kotlin";
    public SemanticExtensionType Type => SemanticExtensionType.Framework;
    public IReadOnlyList<string> SupportedPatterns =>
    [
        "com.expediagroup.graphql.*",
        "com.expediagroup:graphql-kotlin*",
        "com.netflix.graphql.dgs.*"
    ];
    public bool IsImplemented => true;
    public LibraryRole LibraryRole => LibraryRole.WebService;

    public string? MapNodeType(Node node, ParsingContext ctx) => null;
    public string? ExtractIdentifier(Node node, ParsingContext ctx) => null;
    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx) { }
}
