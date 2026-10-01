using System.Text.RegularExpressions;
using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public class RabbitMqLibraryParser : ISemanticExtension
{
    public string Type => OntologyConstants.LibraryTypes.Queue;
    public string Name => "RabbitMQ";
    public string Id => "rabbitmq";
    public IReadOnlyList<string> SupportedPatterns =>
    [
        "amqplib",
        "amqp-connection-manager",
        "*rabbit*",
        "*Rabbit*"
    ];
    public bool IsImplemented => true;

    public string? MapNodeType(Node node, ParsingContext ctx) => null;
    public string? ExtractIdentifier(Node node, ParsingContext ctx) => null;

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (node.Is(TreeSitterSyntax.TypeScript.CallExpression))
        {
            var funcNode = node.GetFunctionNode();
            if (funcNode.IsValid())
            {
                var funcText = funcNode.Text;
                var args = AstHelper.GetCallArguments(node);

                // 1. Channel Publish or sendToQueue or sendToExchange
                if (funcText.EndsWith(".publish", StringComparison.Ordinal))
                {
                    // channel.publish(exchange, routingKey, content) -> args: exchange, routingKey, ...
                    // OR messaging.publish(message, topic, ...) -> args: message, topic
                    string? target = null;
                    if (args.Count >= 2)
                    {
                        if (!AstValueResolver.TryResolveTopicOrQueue(args[1], scopeSymbolId, out target) || string.IsNullOrEmpty(target))
                        {
                            if (!AstValueResolver.TryResolveTopicOrQueue(args[0], scopeSymbolId, out target) || string.IsNullOrEmpty(target))
                            {
                                target = AstHelper.ResolveTopicOrQueue(args[1], scopeSymbolId) ?? AstHelper.ResolveTopicOrQueue(args[0], scopeSymbolId);
                            }
                        }
                    }
                    else if (args.Count == 1)
                    {
                        if (!AstValueResolver.TryResolveTopicOrQueue(args[0], scopeSymbolId, out target) || string.IsNullOrEmpty(target))
                        {
                            target = AstHelper.ResolveTopicOrQueue(args[0], scopeSymbolId);
                        }
                    }

                    AddPublishReference(references, scopeSymbolId, target);
                }
                else if (funcText.EndsWith(".sendToQueue", StringComparison.Ordinal) ||
                         funcText.EndsWith(".sendToExchange", StringComparison.Ordinal))
                {
                    if (args.Count > 0)
                    {
                        var queueArg = args[0];
                        if (!AstValueResolver.TryResolveTopicOrQueue(queueArg, scopeSymbolId, out var topicName) || string.IsNullOrEmpty(topicName))
                        {
                            topicName = AstHelper.ResolveTopicOrQueue(queueArg, scopeSymbolId);
                        }
                        AddPublishReference(references, scopeSymbolId, topicName);
                    }
                }
                // 2. Channel Consume
                else if (funcText.EndsWith(".consume", StringComparison.Ordinal))
                {
                    if (args.Count > 0)
                    {
                        var queueArg = args[0];
                        if (!AstValueResolver.TryResolveTopicOrQueue(queueArg, scopeSymbolId, out var topicName) || string.IsNullOrEmpty(topicName))
                        {
                            topicName = AstHelper.ResolveTopicOrQueue(queueArg, scopeSymbolId);
                        }
                        AddSubscribeReference(references, scopeSymbolId, topicName);
                    }
                }
                // 3. assertQueue or createQueue (e.g. rabbit.createQueue(QUEUE))
                else if (funcText.EndsWith(".assertQueue", StringComparison.Ordinal) ||
                         funcText.EndsWith(".createQueue", StringComparison.Ordinal))
                {
                    var obj = funcNode.GetField(TreeSitterSyntax.Fields.Object);
                    var objText = obj.IsValid() ? obj.Text.ToLowerInvariant() : "";

                    // .createQueue is only treated as queue creation if object is a rabbit/amqp/channel/bus client
                    if (funcText.EndsWith(".createQueue", StringComparison.Ordinal) &&
                        !objText.Contains("rabbit") && !objText.Contains("amqp") && !objText.Contains("channel") && !objText.Contains("bus"))
                    {
                        return;
                    }

                    if (args.Count > 0)
                    {
                        var queueArg = args[0];
                        if (!AstValueResolver.TryResolveTopicOrQueue(queueArg, scopeSymbolId, out var topicName) || string.IsNullOrEmpty(topicName))
                        {
                            topicName = AstHelper.ResolveTopicOrQueue(queueArg, scopeSymbolId);
                        }
                        AddSubscribeReference(references, scopeSymbolId, topicName);

                        var assignedVar = FindAssignedVariableName(node);
                        if (!string.IsNullOrEmpty(assignedVar) && !string.IsNullOrEmpty(topicName))
                        {
                            ConstantRegistry.Register(null, assignedVar, topicName);
                        }
                    }
                }
                // 4. messaging.subscribe(to, handler, ...)
                else if (funcText.EndsWith(".subscribe", StringComparison.Ordinal))
                {
                    if (args.Count > 0)
                    {
                        var to = AstHelper.ResolveTopicOrQueue(args[0], scopeSymbolId);
                        AddSubscribeReference(references, scopeSymbolId, to);
                    }
                }
                // 5. rabbitQueue.send(msg) or paPartnerQueue.send(msg)
                else if (funcText.EndsWith(".send", StringComparison.Ordinal) &&
                         funcNode.Is(TreeSitterSyntax.TypeScript.MemberExpression))
                {
                    var obj = funcNode.GetField(TreeSitterSyntax.Fields.Object);
                    if (obj.IsValid())
                    {
                        var objText = obj.Text;
                        var queueName = ResolveQueueVariableInScope(obj, objText);
                        AddPublishReference(references, scopeSymbolId, queueName);
                    }
                }
                // 6. rabbitQueue.listen(...) or paPartnerQueue.listen(...)
                else if (funcText.EndsWith(".listen", StringComparison.Ordinal) &&
                         funcNode.Is(TreeSitterSyntax.TypeScript.MemberExpression))
                {
                    var obj = funcNode.GetField(TreeSitterSyntax.Fields.Object);
                    if (obj.IsValid())
                    {
                        var objText = obj.Text;
                        var queueName = ResolveQueueVariableInScope(obj, objText);
                        AddSubscribeReference(references, scopeSymbolId, queueName);
                    }
                }
            }
        }
    }

    private static void AddPublishReference(List<Reference> references, string scopeSymbolId, string? target)
    {
        if (IsValidQueueName(target))
        {
            references.Add(new Reference(scopeSymbolId, "rabbitmq:" + target!.Trim(), OntologyConstants.Relationships.PublishesTo));
        }
    }

    private static void AddSubscribeReference(List<Reference> references, string scopeSymbolId, string? target)
    {
        if (IsValidQueueName(target))
        {
            references.Add(new Reference(scopeSymbolId, "rabbitmq:" + target!.Trim(), OntologyConstants.Relationships.SubscribesTo));
        }
    }

    private static bool IsValidQueueName(string? name)
    {
        return WorkspaceConventions.IsValidTopicOrQueueName(name);
    }

    private static string? ResolveQueueVariableInScope(Node node, string varName)
    {
        if (Regex.IsMatch(varName, @"^[A-Z0-9_]{3,}$") &&
            ConstantRegistry.TryResolve(null, varName, out var cr) && IsValidQueueName(cr))
        {
            return cr;
        }

        // Trace up to root to find any assignment to varName: varName = await rabbit.createQueue(...)
        var root = node;
        while (root.Parent.IsValid())
        {
            root = root.Parent;
        }

        foreach (var assign in root.FindDescendantsOfType("assignment_expression"))
        {
            var left = assign.GetChildForField(TreeSitterSyntax.Fields.Left) ?? assign.Children.FirstOrDefault();
            if (left.IsValid() && left.Text == varName)
            {
                var right = assign.GetChildForField(TreeSitterSyntax.Fields.Right) ?? assign.Children.LastOrDefault();
                if (right.IsValid())
                {
                    while (right.IsValid() && (right.Type is TreeSitterSyntax.Common.AwaitExpression or TreeSitterSyntax.Common.ParenthesizedExpression))
                    {
                        right = right.Children.FirstOrDefault(c => c.IsValid() && c.Type is not "await" and not "(" and not ")");
                    }

                    if (right.IsValid() && right.Type == TreeSitterSyntax.TypeScript.CallExpression)
                    {
                        var args = AstHelper.GetCallArguments(right);
                        if (args.Count > 0 && (AstValueResolver.TryResolveTopicOrQueue(args[0], null, out var qName) ||
                                               !string.IsNullOrEmpty(qName = AstHelper.ResolveTopicOrQueue(args[0], null))))
                        {
                            if (IsValidQueueName(qName))
                            {
                                ConstantRegistry.Register(null, varName, qName);
                                return qName;
                            }
                        }
                    }
                }
            }
        }

        var declNode = AstValueResolver.FindVariableDeclarationInScope(node, varName);
        if (declNode.IsValid())
        {
            // If the initializer is a call (e.g. rabbit.createQueue(arg) or channel.assertQueue(arg))
            if (declNode.Is(TreeSitterSyntax.TypeScript.CallExpression))
            {
                var args = AstHelper.GetCallArguments(declNode);
                if (args.Count > 0 && AstValueResolver.TryResolveTopicOrQueue(args[0], null, out var qName))
                {
                    if (IsValidQueueName(qName)) return qName;
                }
            }

            if (AstValueResolver.TryResolveTopicOrQueue(declNode, null, out var resolved) && IsValidQueueName(resolved))
            {
                return resolved;
            }
        }

        return null;
    }

    private static string? FindAssignedVariableName(Node node)
    {
        var curr = node.Parent;
        while (curr.IsValid() && (curr.Type is TreeSitterSyntax.Common.AwaitExpression or TreeSitterSyntax.Common.ParenthesizedExpression))
        {
            curr = curr.Parent;
        }

        if (curr.IsValid())
        {
            if (curr.Type is TreeSitterSyntax.TypeScript.AssignmentExpression)
            {
                var left = curr.GetChildForField(TreeSitterSyntax.Fields.Left) ?? curr.Children.FirstOrDefault();
                if (left.IsValid()) return left.Text;
            }
            else if (curr.Type is TreeSitterSyntax.TypeScript.VariableDeclarator)
            {
                var name = curr.GetChildForField(TreeSitterSyntax.Fields.Name) ?? curr.Children.FirstOrDefault(c => c.Type == TreeSitterSyntax.Common.Identifier);
                if (name.IsValid()) return name.Text;
            }
        }
        return null;
    }
}
