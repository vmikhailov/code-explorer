using System.Collections.Frozen;

namespace CodeExplorer.Parsers.Go.Libraries;

public class FranzGoNsqPulsarCronAuthGoLibraryParser : DeclarativeSemanticExtension
{
    private const string ExtensionId = "franzgo-nsq-pulsar-cron-auth-go";
    private const string ExtensionName = "Franz-Go, Confluent-Kafka-Go, NSQ, Pulsar-Go, Robfig/GoCron, Req/RetryableHTTP, Samber/Do, GoBrake/Failsafe & JWT/OIDC/Fosite";
    private const string ProtocolBroker = "go-messaging";
    private const string FallbackTopic = "go-topic";

    public FranzGoNsqPulsarCronAuthGoLibraryParser()
    {
        Id = ExtensionId;
        Name = ExtensionName;
        Type = SemanticExtensionType.Queue;
        LibraryRole = LibraryRole.MessageBroker;
        SupportedPatterns =
        [
            "github.com/twmb/franz-go*",
            "github.com/confluentinc/confluent-kafka-go*",
            "github.com/wagslane/go-rabbitmq*",
            "github.com/nsqio/go-nsq*",
            "github.com/apache/pulsar-client-go*",
            "github.com/robfig/cron*",
            "github.com/go-co-op/gocron*",
            "github.com/olahol/melody*",
            "github.com/coder/websocket*",
            "nhooyr.io/websocket*",
            "github.com/graphql-go/graphql*",
            "github.com/graph-gophers/graphql-go*",
            "github.com/imroc/req*",
            "github.com/carlmjohnson/requests*",
            "github.com/hashicorp/go-retryablehttp*",
            "github.com/sony/gobreaker*",
            "github.com/failsafe-go/failsafe-go*",
            "github.com/samber/do*",
            "go.uber.org/dig*",
            "github.com/golang-jwt/jwt*",
            "github.com/coreos/go-oidc*",
            "github.com/ory/fosite*",
            "go.uber.org/zap*",
            "github.com/rs/zerolog*",
            "github.com/sirupsen/logrus*",
            "github.com/onsi/ginkgo*",
            "github.com/DATA-DOG/go-sqlmock*",
            "golang.org/x/oauth2*",
            "cloud.google.com/go/*",
            "github.com/Azure/azure-sdk-for-go*",
            "github.com/cloudevents/sdk-go*",
            "github.com/pocketbase/dbx*",
            "github.com/gocql/gocql*",
            "github.com/cockroachdb/cockroach-go*",
            "github.com/snowflakedb/gosnowflake*",
            "github.com/ydb-platform/ydb-go-sdk*",
            "github.com/go-redsync/redsync*"
        ];
        MessagingBrokerRules =
        [
            new MessagingBrokerRule
            {
                Protocol = ProtocolBroker,
                PublishMethodNames = new[]
                {
                    "Produce", "ProduceSync", "Publish", "MultiPublish", "DeferredPublish", "Send", "SendAsync"
                }.ToFrozenSet(StringComparer.Ordinal),
                PublishReceiverSubstrings = new[]
                {
                    "client", "producer", "kgo", "nsq", "pulsar", "publisher"
                }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                SubscribeMethodNames = new[] { "AddConsumerTopics", "Subscribe", "AddHandler" }.ToFrozenSet(StringComparer.Ordinal),
                SubscribeReceiverSubstrings = new[] { "client", "consumer", "kgo", "nsq", "pulsar" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                FallbackTopicName = FallbackTopic
            }
        ];
    }
}
