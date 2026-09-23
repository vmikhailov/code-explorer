using System.Text.RegularExpressions;
using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public class RabbitMqLibraryParser : ILibraryParser
{
    public string Type => OntologyConstants.LibraryTypes.Queue;
    public string Name => "RabbitMQ";
    public string Id => "rabbitmq";
    public IReadOnlyList<string> SupportedPatterns =>
    [
        "amqplib",
        "amqp-connection-manager",
        "@atsorganization/ats-lib-messaging",
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
                        target = AstHelper.ResolveStringOrTemplate(args[1]);
                        if (string.IsNullOrEmpty(target))
                        {
                            target = AstHelper.ResolveStringOrTemplate(args[0]);
                        }
                    }
                    else if (args.Count == 1)
                    {
                        target = AstHelper.ResolveStringOrTemplate(args[0]);
                    }

                    if (!string.IsNullOrEmpty(target))
                    {
                        references.Add(new Reference(scopeSymbolId, "rabbitmq:" + target, OntologyConstants.Relationships.PublishesTo));
                    }
                }
                else if (funcText.EndsWith(".sendToQueue", StringComparison.Ordinal) ||
                         funcText.EndsWith(".sendToExchange", StringComparison.Ordinal))
                {
                    if (args.Count > 0)
                    {
                        var queueArg = args[0];
                        var topicName = AstHelper.ResolveStringOrTemplate(queueArg);
                        if (!string.IsNullOrEmpty(topicName))
                        {
                            references.Add(new Reference(scopeSymbolId, "rabbitmq:" + topicName, OntologyConstants.Relationships.PublishesTo));
                        }
                    }
                }
                // 2. Channel Consume
                else if (funcText.EndsWith(".consume", StringComparison.Ordinal))
                {
                    if (args.Count > 0)
                    {
                        var queueArg = args[0];
                        var topicName = AstHelper.ResolveStringOrTemplate(queueArg);
                        if (!string.IsNullOrEmpty(topicName))
                        {
                            references.Add(new Reference(scopeSymbolId, "rabbitmq:" + topicName, OntologyConstants.Relationships.SubscribesTo));
                        }
                    }
                }
                // 3. assertQueue or createQueue (e.g. rabbit.createQueue(PA_PARTNER_QUEUE))
                else if (funcText.EndsWith(".assertQueue", StringComparison.Ordinal) ||
                         funcText.EndsWith(".createQueue", StringComparison.Ordinal))
                {
                    if (args.Count > 0)
                    {
                        var queueArg = args[0];
                        var topicName = AstHelper.ResolveStringOrTemplate(queueArg);
                        if (!string.IsNullOrEmpty(topicName))
                        {
                            references.Add(new Reference(scopeSymbolId, "rabbitmq:" + topicName, OntologyConstants.Relationships.SubscribesTo));
                        }
                    }
                }
                // 4. messaging.subscribe(to, handler, ...)
                else if (funcText.EndsWith(".subscribe", StringComparison.Ordinal))
                {
                    if (args.Count > 0)
                    {
                        var to = AstHelper.ResolveStringOrTemplate(args[0]);
                        if (!string.IsNullOrEmpty(to))
                        {
                            references.Add(new Reference(scopeSymbolId, "rabbitmq:" + to, OntologyConstants.Relationships.SubscribesTo));
                        }
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
                        if (!string.IsNullOrEmpty(queueName))
                        {
                            references.Add(new Reference(scopeSymbolId, "rabbitmq:" + queueName, OntologyConstants.Relationships.PublishesTo));
                        }
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
                        if (!string.IsNullOrEmpty(queueName))
                        {
                            references.Add(new Reference(scopeSymbolId, "rabbitmq:" + queueName, OntologyConstants.Relationships.SubscribesTo));
                        }
                    }
                }
            }
        }
    }

    private static string? ResolveQueueVariableInScope(Node node, string varName)
    {
        var curr = node.Parent;
        while (curr.IsValid())
        {
            if (curr.IsAny(TreeSitterSyntax.TypeScript.StatementBlock, TreeSitterSyntax.TypeScript.Program))
            {
                foreach (var child in curr.Children)
                {
                    // Check assignments: paPartnerQueue = await rabbit.createQueue(PA_PARTNER_QUEUE)
                    if (child.Text.Contains(varName) && child.Text.Contains("createQueue"))
                    {
                        var match = Regex.Match(child.Text, @"createQueue\s*\(\s*([^,\)]+)");
                        if (match.Success)
                        {
                            var arg = match.Groups[1].Value.Trim().Trim('\'', '"', '`');
                            if (!string.IsNullOrEmpty(arg)) return arg;
                        }
                    }

                    // Check declarations: const paPartnerQueue = ...
                    if (child.IsAny(TreeSitterSyntax.TypeScript.LexicalDeclaration, TreeSitterSyntax.TypeScript.VariableDeclaration))
                    {
                        foreach (var decl in child.Children.Where(c => c.Is(TreeSitterSyntax.TypeScript.VariableDeclarator)))
                        {
                            var nameNode = decl.GetField(TreeSitterSyntax.Fields.Name);
                            if (nameNode.IsValid() && nameNode.Text == varName)
                            {
                                var valNode = decl.GetField(TreeSitterSyntax.Fields.Value);
                                if (valNode.IsValid() && valNode.Text.Contains("createQueue"))
                                {
                                    var match = Regex.Match(valNode.Text, @"createQueue\s*\(\s*([^,\)]+)");
                                    if (match.Success)
                                    {
                                        var arg = match.Groups[1].Value.Trim().Trim('\'', '"', '`');
                                        if (!string.IsNullOrEmpty(arg)) return arg;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            curr = curr.Parent;
        }

        // Fallback: If variable ends with Queue (e.g. userDataQueue -> user_data)
        if (varName.EndsWith("Queue", StringComparison.OrdinalIgnoreCase) && varName.Length > 5)
        {
            return varName;
        }

        return null;
    }
}
