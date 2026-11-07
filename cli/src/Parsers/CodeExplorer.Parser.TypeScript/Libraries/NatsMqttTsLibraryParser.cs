using System.Collections.Frozen;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public class NatsMqttTsLibraryParser : DeclarativeSemanticExtension
{
    public NatsMqttTsLibraryParser()
    {
        Id = "nats-mqtt-ts";
        Name = "NATS.js & MQTT.js";
        Type = SemanticExtensionType.Queue;
        LibraryRole = LibraryRole.MessageBroker;
        SupportedPatterns = ["nats", "nats/*", "mqtt", "mqtt/*", "async-mqtt"];
        MessagingBrokerRules =
        [
            new MessagingBrokerRule
            {
                Protocol = "nats",
                PublishMethodNames = new[] { "publish", "publishAsync" }.ToFrozenSet(StringComparer.Ordinal),
                PublishReceiverSubstrings = new[] { "nc", "nats", "js", "jetstream", "mqtt", "client" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                SubscribeMethodNames = new[] { "subscribe", "subscribeAsync" }.ToFrozenSet(StringComparer.Ordinal),
                SubscribeReceiverSubstrings = new[] { "nc", "nats", "js", "jetstream", "mqtt", "client" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                FallbackTopicName = "nats-subject"
            }
        ];
    }
}
