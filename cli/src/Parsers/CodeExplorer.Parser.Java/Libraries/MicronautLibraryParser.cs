using System.Collections.Frozen;

namespace CodeExplorer.Parser.Java.Libraries;

public class MicronautLibraryParser : DeclarativeSemanticExtension
{
    private const string ExtensionId = "micronaut";
    private const string ExtensionName = "Micronaut Framework (HTTP / Client / Data / Messaging)";
    private const string ProtocolKafka = "kafka";
    private const string ProtocolRabbitMq = "rabbitmq";
    private const string FallbackTopic = "unknown-topic";
    private const string FallbackQueue = "unknown-queue";
    private const string FallbackQuery = "Micronaut Data Query";

    public MicronautLibraryParser()
    {
        Id = ExtensionId;
        Name = ExtensionName;
        Type = SemanticExtensionType.Framework;
        LibraryRole = LibraryRole.WebService;
        IsBuiltIn = true;
        SupportedPatterns =
        [
            "io.micronaut.*",
            "io.micronaut.http.*",
            "io.micronaut.http.annotation.*",
            "io.micronaut.http.client.*",
            "io.micronaut.data.*",
            "io.micronaut.configuration.kafka.*",
            "io.micronaut.configuration.rabbitmq.*",
            "io.micronaut.kafka.*",
            "io.micronaut.rabbitmq.*",
            "io.micronaut.websocket.*"
        ];
        AnnotationEndpointRules =
        [
            new AnnotationEndpointRule
            {
                AnnotationToHttpMethod = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Get"] = "GET",
                    ["Post"] = "POST",
                    ["Put"] = "PUT",
                    ["Delete"] = "DELETE",
                    ["Patch"] = "PATCH",
                    ["Head"] = "HEAD",
                    ["Options"] = "OPTIONS",
                    ["Error"] = "ALL",
                    ["Controller"] = "ALL",
                    ["ServerWebSocket"] = "WS"
                }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase),
                RoutePrefixAnnotationNames = new[] { "Controller", "RequestMapping", "Path" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                PathArgumentKeys = new[] { "value", "path", "uri" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                WebSocketAnnotationNames = new[] { "ServerWebSocket", "WebSocket", "ServerEndpoint" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase)
            }
        ];
        OutboundHttpRules =
        [
            new OutboundHttpRule
            {
                CallMethodNames = new[]
                {
                    "toBlocking", "exchange", "retrieve", "retrieveFlux", "retrieveMono", "jsonStream"
                }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                ClientAnnotationSuffixes = new[] { "Client", "HttpClient", "ClientWebSocket" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase)
            }
        ];
        OrmTableRules =
        [
            new OrmTableRule
            {
                TableAnnotationNames = new[] { "MappedEntity", "Entity", "Table", "MongoEntity", "NodeEntity" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                NameArgumentKeys = new[] { "value", "name", "collection", "label" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                RepositoryInterfaceNames = new[]
                {
                    "GenericRepository", "CrudRepository", "PageableRepository",
                    "JpaRepository", "JdbcRepository", "CoroutineCrudRepository",
                    "RxJavaCrudRepository", "ReactiveStreamsCrudRepository"
                }.ToFrozenSet(StringComparer.OrdinalIgnoreCase)
            }
        ];
        SqlSinkRules =
        [
            new SqlSinkRule
            {
                CallMethodNames = new[]
                {
                    "find", "findAll", "findById", "save", "saveAll", "update", "delete", "deleteById", "count", "existsById"
                }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                QueryAnnotationSuffixes = new[] { "Query" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                RequireExtractableSqlOnCall = false,
                FallbackQueryPrefix = FallbackQuery
            }
        ];
        MessagingBrokerRules =
        [
            new MessagingBrokerRule
            {
                Protocol = ProtocolKafka,
                ListenerAnnotationNames = new[] { "KafkaListener", "Topic" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                ProducerAnnotationNames = new[] { "KafkaClient", "Topic" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                ChannelAnnotationNames = new[] { "Topic" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                TopicArgumentKeys = new[] { "value", "patterns", "topics", "topic" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                FallbackTopicName = FallbackTopic
            },
            new MessagingBrokerRule
            {
                Protocol = ProtocolRabbitMq,
                ListenerAnnotationNames = new[] { "RabbitListener", "Queue" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                ProducerAnnotationNames = new[] { "RabbitClient", "Binding" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                ChannelAnnotationNames = new[] { "Queue" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                TopicArgumentKeys = new[] { "value", "connection", "queue" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                FallbackTopicName = FallbackQueue
            }
        ];
    }
}
