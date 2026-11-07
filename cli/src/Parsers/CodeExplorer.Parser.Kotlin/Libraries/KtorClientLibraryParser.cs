using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.Kotlin.Libraries;

public class KtorClientLibraryParser : ISemanticExtension
{
    public string Name => "Ktor Client";
    public string Id => "ktor-client";
    public SemanticExtensionType Type => SemanticExtensionType.ApiClient;
    public IReadOnlyList<string> SupportedPatterns => ["io.ktor.client.*", "io.ktor:ktor-client*"];
    public bool IsImplemented => true;
    public LibraryRole LibraryRole => LibraryRole.General;

    public string? MapNodeType(Node node, ParsingContext ctx) => null;
    public string? ExtractIdentifier(Node node, ParsingContext ctx) => null;
    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx) { }
}
