using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.Kotlin.Libraries;

public class SpringCloudStreamKotlinLibraryParser : ISemanticExtension
{
    public string Name => "Spring Cloud Stream & Kafka (Kotlin)";
    public string Id => "spring-cloud-stream-kotlin";
    public SemanticExtensionType Type => SemanticExtensionType.Queue;
    public IReadOnlyList<string> SupportedPatterns =>
    [
        "org.springframework.cloud.stream.*",
        "org.springframework.kafka.*",
        "org.springframework.amqp.*"
    ];
    public bool IsImplemented => true;
    public LibraryRole LibraryRole => LibraryRole.MessageBroker;

    public string? MapNodeType(Node node, ParsingContext ctx) => null;
    public string? ExtractIdentifier(Node node, ParsingContext ctx) => null;
    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx) { }
}
