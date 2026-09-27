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

                    AddPublishReference(references, scopeSymbolId, target);
                }
                else if (funcText.EndsWith(".sendToQueue", StringComparison.Ordinal) ||
                         funcText.EndsWith(".sendToExchange", StringComparison.Ordinal))
                {
                    if (args.Count > 0)
                    {
                        var queueArg = args[0];
                        var topicName = AstHelper.ResolveStringOrTemplate(queueArg);
                        AddPublishReference(references, scopeSymbolId, topicName);
                    }
                }
                // 2. Channel Consume
                else if (funcText.EndsWith(".consume", StringComparison.Ordinal))
                {
                    if (args.Count > 0)
                    {
                        var queueArg = args[0];
                        var topicName = AstHelper.ResolveStringOrTemplate(queueArg);
                        AddSubscribeReference(references, scopeSymbolId, topicName);
                    }
                }
                // 3. assertQueue or createQueue (e.g. rabbit.createQueue(PA_PARTNER_QUEUE))
                else if (funcText.EndsWith(".assertQueue", StringComparison.Ordinal) ||
                         funcText.EndsWith(".createQueue", StringComparison.Ordinal))
                {
                    var obj = funcNode.GetField(TreeSitterSyntax.Fields.Object);
                    var objText = obj.IsValid() ? obj.Text.ToLowerInvariant() : "";
                    if (objText.Contains("cache") || objText.Contains("redis"))
                    {
                        return; // Пропускаем Redis очередь
                    }
                    if (args.Count > 0)
                    {
                        var queueArg = args[0];
                        var topicName = AstHelper.ResolveStringOrTemplate(queueArg);
                        if (!string.Equals(topicName, "QUEUE_NAME", StringComparison.OrdinalIgnoreCase))
                        {
                            AddSubscribeReference(references, scopeSymbolId, topicName);
                        }
                    }
                }
                // 4. messaging.subscribe(to, handler, ...)
                else if (funcText.EndsWith(".subscribe", StringComparison.Ordinal))
                {
                    if (args.Count > 0)
                    {
                        var to = AstHelper.ResolveStringOrTemplate(args[0]);
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
        if (string.IsNullOrWhiteSpace(name)) return false;
        var t = name.Trim();
        if (t.StartsWith(':') ||
            t.Equals("string", StringComparison.OrdinalIgnoreCase) ||
            t.Equals("undefined", StringComparison.OrdinalIgnoreCase) ||
            t.Equals("null", StringComparison.OrdinalIgnoreCase) ||
            t.Equals("void", StringComparison.OrdinalIgnoreCase) ||
            t.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            t.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
            t.Length <= 2)
        {
            return false;
        }
        return true;
    }

    private static string? ResolveQueueVariableInScope(Node node, string varName)
    {
        if (ConstantRegistry.TryResolve(null, varName, out var cr) && IsValidQueueName(cr))
        {
            return cr;
        }

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
                            if (IsValidQueueName(arg)) return arg;
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
                                        if (IsValidQueueName(arg)) return arg;
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
            if (IsValidQueueName(varName)) return varName;
        }

        return null;
    }
}
