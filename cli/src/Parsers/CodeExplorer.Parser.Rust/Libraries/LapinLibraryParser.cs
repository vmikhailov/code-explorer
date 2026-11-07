using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.Rust.Libraries;

/// <summary>
/// Semantic extension for the Lapin (RabbitMQ) AMQP client in Rust.
/// Detects basic_publish and basic_consume invocations.
/// Emits Topic nodes and PUBLISHES_TO / SUBSCRIBES_TO relationships.
/// </summary>
public class LapinLibraryParser : DeclarativeSemanticExtension
{
    public LapinLibraryParser()
    {
        Id = "lapin";
        Name = "Lapin (RabbitMQ)";
        Type = SemanticExtensionType.Queue;
        LibraryRole = LibraryRole.MessageBroker;
        SupportedPatterns = ["lapin*"];
        CustomAstRules =
        [
            new CustomAstRule
            {
                NodeTypes = System.Collections.Frozen.FrozenSet.ToFrozenSet([TreeSitterSyntax.Rust.CallExpression]),
                MapNodeType = (node, ctx) => IsLapinCall(node, out _, out _) ? OntologyConstants.NodeLabels.Topic : null,
                ExtractIdentifier = (node, _, _, _) => IsLapinCall(node, out var topic, out _) ? topic : null,
                CollectReferences = CollectReferencesCore,
                EnrichSymbol = EnrichSymbolCore
            }
        ];
    }

    private static void CollectReferencesCore(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (IsLapinCall(node, out var topic, out var isPublish) && !string.IsNullOrEmpty(topic))
        {
            var relKind = isPublish
                ? OntologyConstants.Relationships.PublishesTo
                : OntologyConstants.Relationships.SubscribesTo;

            references.Add(new Reference(scopeSymbolId, "rabbitmq:" + topic, relKind));
            references.Add(new Reference(scopeSymbolId, topic, relKind));
        }
    }

    private static void EnrichSymbolCore(Node node, SyntacticSymbol symbol, ParsingContext ctx)
    {
        if (IsLapinCall(node, out _, out _))
        {
            symbol.Properties["broker_type"] = "rabbitmq";
        }
    }

    public static bool IsLapinCall(Node node, out string topic, out bool isPublish)
    {
        topic = string.Empty;
        isPublish = false;

        if (!node.Is(TreeSitterSyntax.Rust.CallExpression))
        {
            return false;
        }

        if (!RustAstHelper.GetMethodCallInfo(node, out _, out var methodName, out var args))
        {
            return false;
        }

        if (string.IsNullOrEmpty(methodName)) return false;

        // 1. basic_publish(exchange, routing_key, ...)
        if (methodName.EndsWith("basic_publish", StringComparison.OrdinalIgnoreCase))
        {
            if (args.Count >= 2)
            {
                var routingKey = RustAstHelper.ExtractStringLiteral(args[1]);
                if (string.IsNullOrEmpty(routingKey))
                {
                    routingKey = RustAstHelper.ExtractStringLiteral(args[0]);
                }
                if (!string.IsNullOrEmpty(routingKey))
                {
                    topic = routingKey;
                    isPublish = true;
                    return true;
                }
            }
        }

        // 2. basic_consume(queue_name, ...)
        if (methodName.EndsWith("basic_consume", StringComparison.OrdinalIgnoreCase))
        {
            if (args.Count >= 1)
            {
                var queue = RustAstHelper.ExtractStringLiteral(args[0]);
                if (!string.IsNullOrEmpty(queue))
                {
                    topic = queue;
                    isPublish = false;
                    return true;
                }
            }
        }

        return false;
    }
}
