using System.Collections.Frozen;

namespace CodeExplorer.Parsers.CSharp.Libraries;

public class AkkaProtoActorBrighterLibraryParser : DeclarativeSemanticExtension
{
    private const string ExtensionId = "akka-protoactor-brighter";
    private const string ExtensionName = "Akka.NET, Proto.Actor, Brighter, Coravel, TickerQ & RabbitMQ/NATS .NET";
    private const string ProtocolActorBus = "dotnet-actor-bus";
    private const string FallbackTopic = "actor-command";

    public AkkaProtoActorBrighterLibraryParser()
    {
        Id = ExtensionId;
        Name = ExtensionName;
        Type = SemanticExtensionType.Queue;
        LibraryRole = LibraryRole.MessageBroker;
        SupportedPatterns =
        [
            "Akka*",
            "Proto.Actor*",
            "Paramore.Brighter*",
            "Paramore.Darker*",
            "Coravel*",
            "TickerQ*",
            "RabbitMQ.Client*",
            "NATS.Client*",
            "NATS.Net*",
            "Mediator*"
        ];
        MessagingBrokerRules =
        [
            new MessagingBrokerRule
            {
                Protocol = ProtocolActorBus,
                ConsumerInterfaceSubstrings = new[]
                {
                    "IHandleRequests", "IHandleRequestsAsync", "ReceiveActor", "IActor", "IInvocable"
                }.ToFrozenSet(StringComparer.Ordinal),
                PublishMethodNames = new[]
                {
                    "Tell", "Ask", "Forward", "Send", "SendAsync", "Post", "PostAsync",
                    "DepositPost", "DepositPostAsync", "BasicPublish", "BasicPublishAsync",
                    "PublishAsync", "QueueInvocable", "QueueBroadcast"
                }.ToFrozenSet(StringComparer.Ordinal),
                PublishReceiverSubstrings = new[]
                {
                    "actor", "context", "system", "commandProcessor", "queryProcessor",
                    "channel", "nats", "queue", "mediator", "sender"
                }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                FallbackTopicName = FallbackTopic
            }
        ];
    }
}
