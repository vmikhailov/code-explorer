using System.Text.RegularExpressions;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;

namespace CodeExplorer.Parsers.Kotlin.Libraries;

public sealed class SpringCloudStreamKotlinLibraryParser : DeclarativeSemanticExtension
{
    private const string MarkerSpring = "org.springframework";
    private const string KafkaPrefix = "kafka:";
    private const string RabbitPrefix = "rabbitmq:";
    private const string JmsPrefix = "jms:";

    private static readonly Regex ListenerTopicRegex = new(
        @"@(?:KafkaListener|RabbitListener|JmsListener|SqsListener)\s*\([^)]*?(?:topics|queues|destination|value)\s*=\s*(?:\[|arrayOf\s*\()?\s*[""'](?<topic>[^""']+)[""']",
        RegexOptions.Compiled);

    private static readonly Regex TemplateSendRegex = new(
        @"\b(?<tmpl>[a-zA-Z0-9_]*(?:kafkaTemplate|rabbitTemplate|jmsTemplate|streamBridge))\s*\.\s*(?:send|convertAndSend)\s*\(\s*[""'](?<topic>[^""']+)[""']",
        RegexOptions.Compiled);

    public SpringCloudStreamKotlinLibraryParser()
    {
        Id = "spring-cloud-stream-kotlin";
        Name = "Spring Cloud Stream, Kafka, RabbitMQ & JMS (Kotlin)";
        Type = SemanticExtensionType.Queue;
        LibraryRole = LibraryRole.MessageBroker;
        SupportedPatterns =
        [
            "org.springframework.cloud.stream.*",
            "org.springframework.kafka.*",
            "org.springframework.amqp.*",
            "org.springframework.jms.*"
        ];
    }

    public static void AnalyzeSource(string content, FileNode fileNode)
    {
        if (!content.Contains(MarkerSpring, StringComparison.Ordinal))
        {
            return;
        }

        foreach (Match match in ListenerTopicRegex.Matches(content))
        {
            var topic = match.Groups[1].Value.Trim();
            if (!string.IsNullOrEmpty(topic))
            {
                var prefix = content.Contains("RabbitListener", StringComparison.Ordinal)
                    ? RabbitPrefix
                    : content.Contains("JmsListener", StringComparison.Ordinal)
                        ? JmsPrefix
                        : KafkaPrefix;
                fileNode.References.Add(new Reference(fileNode.Id, prefix + topic, OntologyConstants.Relationships.SubscribesTo));
            }
        }

        foreach (Match match in TemplateSendRegex.Matches(content))
        {
            var tmpl = match.Groups[1].Value;
            var topic = match.Groups[2].Value.Trim();
            if (!string.IsNullOrEmpty(topic))
            {
                var prefix = tmpl.Contains("rabbit", StringComparison.OrdinalIgnoreCase)
                    ? RabbitPrefix
                    : tmpl.Contains("jms", StringComparison.OrdinalIgnoreCase)
                        ? JmsPrefix
                        : KafkaPrefix;
                fileNode.References.Add(new Reference(fileNode.Id, prefix + topic, OntologyConstants.Relationships.PublishesTo));
            }
        }
    }
}
