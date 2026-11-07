namespace CodeExplorer.Parser.Kotlin.Libraries;

public sealed class SpringCloudStreamKotlinLibraryParser : DeclarativeSemanticExtension
{
    public SpringCloudStreamKotlinLibraryParser()
    {
        Id = "spring-cloud-stream-kotlin";
        Name = "Spring Cloud Stream & Kafka (Kotlin)";
        Type = SemanticExtensionType.Queue;
        LibraryRole = LibraryRole.MessageBroker;
        SupportedPatterns =
        [
            "org.springframework.cloud.stream.*",
            "org.springframework.kafka.*",
            "org.springframework.amqp.*"
        ];
    }
}
