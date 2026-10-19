using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.Rust.Libraries;

/// <summary>
/// Semantic extension for the rdkafka (Apache Kafka) client in Rust.
/// Detects message publishing via FutureRecord::to("topic") and consumer subscriptions via consumer.subscribe(&amp;["topic"]).
/// Emits Topic nodes and PUBLISHES_TO / SUBSCRIBES_TO relationships.
/// </summary>
public class RdKafkaRustLibraryParser : ISemanticExtension
{
    public string Id => "rdkafka";
    public string Name => "rdkafka (Kafka)";
    public string Type => "queue";
    public bool IsImplemented => true;
    public LibraryRole LibraryRole => LibraryRole.MessageBroker;
    public IReadOnlyList<string> SupportedPatterns => ["rdkafka*"];

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (IsKafkaCall(node, out _, out _))
        {
            return OntologyConstants.NodeLabels.Topic;
        }
        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx) => ExtractIdentifier(node, ctx, null, null);
    public string? ExtractIdentifier(Node node, ParsingContext ctx, string? projectName) => ExtractIdentifier(node, ctx, projectName, null);

    public string? ExtractIdentifier(Node node, ParsingContext ctx, string? projectName, string? relativePath)
    {
        if (IsKafkaCall(node, out var topic, out _))
        {
            return topic;
        }
        return null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (IsKafkaCall(node, out var topic, out var isProducer) && !string.IsNullOrEmpty(topic))
        {
            var relKind = isProducer
                ? OntologyConstants.Relationships.PublishesTo
                : OntologyConstants.Relationships.SubscribesTo;

            references.Add(new Reference(scopeSymbolId, "kafka:" + topic, relKind));
            references.Add(new Reference(scopeSymbolId, topic, relKind));
        }
    }

    public void EnrichSymbol(Node node, SyntacticSymbol symbol, ParsingContext ctx)
    {
        if (IsKafkaCall(node, out _, out _))
        {
            symbol.Properties["broker_type"] = "kafka";
        }
    }

    public static bool IsKafkaCall(Node node, out string topic, out bool isProducer)
    {
        topic = string.Empty;
        isProducer = false;

        if (node.Type != TreeSitterSyntax.Rust.CallExpression)
        {
            return false;
        }

        if (!RustAstHelper.GetMethodCallInfo(node, out _, out var methodName, out var args))
        {
            return false;
        }

        if (string.IsNullOrEmpty(methodName)) return false;

        // 1. Producer: FutureRecord::to("topic") or BaseRecord::to("topic") or .to("topic")
        if (methodName.EndsWith("::to", StringComparison.OrdinalIgnoreCase) ||
            methodName.Equals("to", StringComparison.OrdinalIgnoreCase))
        {
            if (args.Count > 0)
            {
                var candidate = RustAstHelper.ExtractStringLiteral(args[0]);
                if (!string.IsNullOrEmpty(candidate))
                {
                    topic = candidate;
                    isProducer = true;
                    return true;
                }
            }
        }

        // 2. Consumer: consumer.subscribe(&["topic-1", "topic-2"])
        if (methodName.Equals("subscribe", StringComparison.OrdinalIgnoreCase) && args.Count > 0)
        {
            var text = args[0].Text;
            var match = System.Text.RegularExpressions.Regex.Match(text, @"""([^""]+)""");
            if (match.Success)
            {
                topic = match.Groups[1].Value;
                isProducer = false;
                return true;
            }
        }

        return false;
    }
}
