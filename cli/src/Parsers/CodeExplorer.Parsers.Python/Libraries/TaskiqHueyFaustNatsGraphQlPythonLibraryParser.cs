using System.Collections.Frozen;

namespace CodeExplorer.Parsers.Python.Libraries;

public class TaskiqHueyFaustNatsGraphQlPythonLibraryParser : DeclarativeSemanticExtension
{
    private const string ExtensionId = "taskiq-huey-faust-nats-graphql-python";
    private const string ExtensionName = "Taskiq, Huey, Faust, Kombu, NATS.py, Graphene/Ariadne, SocketIO, Authlib/PyJWT/Casbin & DSPy/Instructor/LiteLLM";
    private const string ProtocolQueue = "python-queue";
    private const string FallbackTopic = "python-task";

    public TaskiqHueyFaustNatsGraphQlPythonLibraryParser()
    {
        Id = ExtensionId;
        Name = ExtensionName;
        Type = SemanticExtensionType.Queue;
        LibraryRole = LibraryRole.MessageBroker;
        SupportedPatterns =
        [
            "taskiq*",
            "huey*",
            "faust*",
            "kombu*",
            "aio-pika*",
            "aio_pika*",
            "aiokafka*",
            "confluent-kafka*",
            "confluent_kafka*",
            "nats-py*",
            "nats*",
            "graphene*",
            "ariadne*",
            "grpclib*",
            "python-socketio*",
            "socketio*",
            "websockets*",
            "supabase*",
            "dspy*",
            "instructor*",
            "litellm*",
            "pydantic-ai*",
            "pydantic_ai*",
            "langgraph*",
            "llama-index*",
            "llama_index*",
            "crewai*",
            "dishka*",
            "dependency-injector*",
            "dependency_injector*",
            "pyjwt*",
            "jwt*",
            "authlib*",
            "casbin*",
            "pycasbin*",
            "tenacity*",
            "backoff*",
            "loguru*",
            "structlog*",
            "sentry-sdk*",
            "opentelemetry*",
            "msgspec*",
            "attrs*",
            "marshmallow*",
            "urllib3*"
        ];
        MessagingBrokerRules =
        [
            new MessagingBrokerRule
            {
                Protocol = ProtocolQueue,
                ListenerAnnotationNames = new[] { "task", "periodic_task", "agent" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                FallbackTopicName = FallbackTopic,
                PublishMethodNames = new[]
                {
                    "kiq", "send", "publish", "emit"
                }.ToFrozenSet(StringComparer.Ordinal),
                PublishReceiverSubstrings = new[]
                {
                    "broker", "huey", "app", "nc", "nats", "sio", "socketio", "topic"
                }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                SubscribeMethodNames = new[] { "subscribe", "on" }.ToFrozenSet(StringComparer.Ordinal),
                SubscribeReceiverSubstrings = new[] { "nc", "nats", "sio", "socketio" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase)
            }
        ];
        ServiceCallSinkRules =
        [
            new ServiceCallSinkRule
            {
                CallMethodNames = new[] { "completion", "acompletion", "run", " run_sync" }.ToFrozenSet(StringComparer.Ordinal),
                ReceiverSubstrings = new[] { "litellm", "agent", "dspy", "crew" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                TargetRelation = OntologyConstants.Relationships.Calls,
                StaticTargetName = "llm:python-ai"
            }
        ];
    }
}
