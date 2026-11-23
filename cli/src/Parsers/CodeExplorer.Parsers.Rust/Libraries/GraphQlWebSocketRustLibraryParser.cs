using System.Collections.Frozen;

namespace CodeExplorer.Parsers.Rust.Libraries;

public class GraphQlWebSocketRustLibraryParser : DeclarativeSemanticExtension
{
    private const string ExtensionId = "graphql-websocket-rust";
    private const string ExtensionName = "async-graphql, Juniper, Cynic, Tungstenite & Socketioxide";

    public GraphQlWebSocketRustLibraryParser()
    {
        Id = ExtensionId;
        Name = ExtensionName;
        Type = SemanticExtensionType.Framework;
        LibraryRole = LibraryRole.WebService;
        SupportedPatterns =
        [
            "async-graphql*",
            "async_graphql*",
            "juniper*",
            "cynic*",
            "graphql_client*",
            "graphql-client*",
            "tungstenite*",
            "tokio-tungstenite*",
            "tokio_tungstenite*",
            "socketioxide*"
        ];
        AnnotationEndpointRules =
        [
            new AnnotationEndpointRule
            {
                AnnotationToHttpMethod = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Object"] = "QUERY",
                    ["Subscription"] = "SUBSCRIPTION",
                    ["graphql_object"] = "QUERY",
                    ["graphql_subscription"] = "SUBSCRIPTION"
                }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase)
            }
        ];
        MessagingBrokerRules =
        [
            new MessagingBrokerRule
            {
                Protocol = "ws",
                PublishMethodNames = new[] { "emit", "send" }.ToFrozenSet(StringComparer.Ordinal),
                PublishReceiverSubstrings = new[] { "socket", "ws", "io", "broadcaster" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                SubscribeMethodNames = new[] { "on" }.ToFrozenSet(StringComparer.Ordinal),
                SubscribeReceiverSubstrings = new[] { "socket", "io", "ns" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                FallbackTopicName = "ws-event"
            }
        ];
    }
}
