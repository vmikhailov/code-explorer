using System.Collections.Frozen;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.Go.Libraries;

public class RabbitMqGoLibraryParser : DeclarativeSemanticExtension
{
    public RabbitMqGoLibraryParser()
    {
        Type = SemanticExtensionType.Queue;
        Name = "RabbitMQ";
        Id = "rabbitmq";
        SupportedPatterns =
        [
            "github.com/rabbitmq/amqp091-go*",
            "github.com/streadway/amqp*",
            "*rabbit*",
            "*Rabbit*"
        ];
        LibraryRole = LibraryRole.MessageBroker;
        CustomAstRules =
        [
            new CustomAstRule
            {
                NodeTypes = new[] { TreeSitterSyntax.Go.AssignmentStatement, TreeSitterSyntax.Go.CallExpression }.ToFrozenSet(),
                CollectReferences = CollectRabbitMqReferences
            }
        ];
    }

    private static void CollectRabbitMqReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        // 0. Dispatcher index assignment: routes["v1.task.create"] = r.createTask()
        if (GoAstHelper.TryGetIndexAssignment(node, out var operandText, out var indexKey))
        {
            if (operandText.Contains("route", StringComparison.OrdinalIgnoreCase) ||
                operandText.Contains("handler", StringComparison.OrdinalIgnoreCase) ||
                operandText.Contains("sub", StringComparison.OrdinalIgnoreCase))
            {
                references.Add(new Reference(scopeSymbolId, "rabbitmq:" + indexKey, OntologyConstants.Relationships.SubscribesTo));
                return;
            }
        }

        if (!node.Is(TreeSitterSyntax.Go.CallExpression)) return;

        var func = node.GetFunctionNode();
        if (!func.IsValid()) return;

        var funcText = func.Text;
        var args = GoAstHelper.GetCallArguments(node);

        // 1. Channel Publish: ch.Publish(exchange, routingKey, ...) or ch.PublishWithContext(ctx, exchange, routingKey, ...)
        if (funcText.EndsWith(".Publish", StringComparison.Ordinal) ||
            funcText.EndsWith(".PublishWithContext", StringComparison.Ordinal))
        {
            if (args.Count >= 2)
            {
                // In messaging-go: manager.Publish(ctx, topic, message, ...) -> args[1] is topic
                // In raw AMQP: ch.Publish(exchange, routingKey, ...) -> args[0] is exchange, args[1] is routingKey
                // In raw AMQP WithContext: ch.PublishWithContext(ctx, exchange, routingKey, ...) -> args[2] is routingKey
                string? target = null;
                if (funcText.EndsWith(".PublishWithContext", StringComparison.Ordinal) && args.Count >= 3)
                {
                    target = GoAstHelper.ResolveStringOrVariable(args[2]);
                    if (string.IsNullOrEmpty(target)) target = GoAstHelper.ResolveStringOrVariable(args[1]);
                }
                else
                {
                    target = GoAstHelper.ResolveStringOrVariable(args[1]);
                    if (string.IsNullOrEmpty(target) && args.Count > 0)
                    {
                        target = GoAstHelper.ResolveStringOrVariable(args[0]);
                    }
                }

                if (!string.IsNullOrEmpty(target))
                {
                    references.Add(new Reference(scopeSymbolId, "rabbitmq:" + target, OntologyConstants.Relationships.PublishesTo));
                }
            }
        }
        // 2. Channel Consume: ch.Consume(queue, ...) or ch.ConsumeWithContext(ctx, queue, ...)
        else if (funcText.EndsWith(".Consume", StringComparison.Ordinal) ||
                 funcText.EndsWith(".ConsumeWithContext", StringComparison.Ordinal))
        {
            var queueArgIndex = funcText.EndsWith(".ConsumeWithContext", StringComparison.Ordinal) ? 1 : 0;
            if (args.Count > queueArgIndex)
            {
                var queue = GoAstHelper.ResolveStringOrVariable(args[queueArgIndex]);
                if (!string.IsNullOrEmpty(queue))
                {
                    references.Add(new Reference(scopeSymbolId, "rabbitmq:" + queue, OntologyConstants.Relationships.SubscribesTo));
                }
            }
        }
        // 3. QueueDeclare: ch.QueueDeclare(name, ...)
        else if (funcText.EndsWith(".QueueDeclare", StringComparison.Ordinal) ||
                 funcText.EndsWith(".QueueDeclarePassive", StringComparison.Ordinal))
        {
            if (args.Count > 0)
            {
                var queue = GoAstHelper.ResolveStringOrVariable(args[0]);
                if (!string.IsNullOrEmpty(queue))
                {
                    references.Add(new Reference(scopeSymbolId, "rabbitmq:" + queue, OntologyConstants.Relationships.SubscribesTo));
                }
            }
        }
        // 4. ExchangeDeclare: ch.ExchangeDeclare(name, type, ...)
        else if (funcText.EndsWith(".ExchangeDeclare", StringComparison.Ordinal) ||
                 funcText.EndsWith(".ExchangeDeclarePassive", StringComparison.Ordinal))
        {
            if (args.Count > 0)
            {
                var exchange = GoAstHelper.ResolveStringOrVariable(args[0]);
                if (!string.IsNullOrEmpty(exchange))
                {
                    references.Add(new Reference(scopeSymbolId, "rabbitmq:" + exchange, OntologyConstants.Relationships.SubscribesTo));
                }
            }
        }
        // 5. QueueBind: ch.QueueBind(queue, key, exchange, ...)
        else if (funcText.EndsWith(".QueueBind", StringComparison.Ordinal))
        {
            if (args.Count >= 3)
            {
                var exchange = GoAstHelper.ResolveStringOrVariable(args[2]);
                var queue = GoAstHelper.ResolveStringOrVariable(args[0]);
                var target = !string.IsNullOrEmpty(exchange) ? exchange : queue;
                if (!string.IsNullOrEmpty(target))
                {
                    references.Add(new Reference(scopeSymbolId, "rabbitmq:" + target, OntologyConstants.Relationships.SubscribesTo));
                }
            }
        }
        // 6. RemoteCall client: client.RemoteCall(handler, request, response)
        else if (funcText.EndsWith(".RemoteCall", StringComparison.Ordinal))
        {
            if (args.Count > 0)
            {
                var handler = GoAstHelper.ResolveStringOrVariable(args[0]);
                if (!string.IsNullOrEmpty(handler))
                {
                    references.Add(new Reference(scopeSymbolId, "rabbitmq:" + handler, OntologyConstants.Relationships.PublishesTo));
                }
            }
        }
        // 7. messaging manager: Subscribe(ctx, to, onMessage, ...)
        else if (funcText.EndsWith(".Subscribe", StringComparison.Ordinal))
        {
            if (args.Count >= 2)
            {
                var to = GoAstHelper.ResolveStringOrVariable(args[1]);
                if (!string.IsNullOrEmpty(to))
                {
                    references.Add(new Reference(scopeSymbolId, "rabbitmq:" + to, OntologyConstants.Relationships.SubscribesTo));
                }
            }
        }
        // 8. Helper function: consumeRabbit(ctx, channel, queue, handler)
        else if (funcText.Contains("consumeRabbit", StringComparison.OrdinalIgnoreCase))
        {
            if (args.Count >= 3)
            {
                var queue = GoAstHelper.ResolveStringOrVariable(args[2]);
                if (!string.IsNullOrEmpty(queue))
                {
                    references.Add(new Reference(scopeSymbolId, "rabbitmq:" + queue, OntologyConstants.Relationships.SubscribesTo));
                }
            }
        }
    }
}
