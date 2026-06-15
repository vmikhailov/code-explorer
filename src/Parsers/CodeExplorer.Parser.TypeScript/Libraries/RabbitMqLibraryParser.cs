using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;
using System;
using System.Collections.Generic;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public class RabbitMqLibraryParser : ILibraryParser
{
    public string Type => "cloud";
    public string Name => "RabbitMQ";
    public string Id => "rabbitmq";
    public IReadOnlyList<string> SupportedPatterns => ["amqplib", "amqp-connection-manager"];
    public bool IsImplemented => true;

    public string? MapNodeType(Node node, ParsingContext ctx) => null;
    public string? ExtractIdentifier(Node node, ParsingContext ctx) => null;

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (node.Type == "call_expression")
        {
            var funcNode = node.GetChildForField("function");
            if (funcNode != null && funcNode.Id != IntPtr.Zero)
            {
                var funcText = funcNode.Text;

                if (funcText.EndsWith(".publish", StringComparison.Ordinal))
                {
                    var argList = node.GetChildForField("arguments");
                    if (argList != null && argList.Children.Count > 3)
                    {
                        var routingKeyArg = argList.Children[2]; // argList elements: '(', exchange, routingKey, ...
                        var topicName = AstHelper.ResolveStringOrTemplate(routingKeyArg);
                        if (!string.IsNullOrEmpty(topicName))
                        {
                            references.Add(new Reference(scopeSymbolId, "rabbitmq:" + topicName, OntologyConstants.Relationships.PublishesTo));
                        }
                    }
                }
                else if (funcText.EndsWith(".sendToQueue", StringComparison.Ordinal))
                {
                    var argList = node.GetChildForField("arguments");
                    if (argList != null && argList.Children.Count > 1)
                    {
                        var queueArg = argList.Children[1];
                        var topicName = AstHelper.ResolveStringOrTemplate(queueArg);
                        if (!string.IsNullOrEmpty(topicName))
                        {
                            references.Add(new Reference(scopeSymbolId, "rabbitmq:" + topicName, OntologyConstants.Relationships.PublishesTo));
                        }
                    }
                }
                else if (funcText.EndsWith(".consume", StringComparison.Ordinal))
                {
                    var argList = node.GetChildForField("arguments");
                    if (argList != null && argList.Children.Count > 1)
                    {
                        var queueArg = argList.Children[1];
                        var topicName = AstHelper.ResolveStringOrTemplate(queueArg);
                        if (!string.IsNullOrEmpty(topicName))
                        {
                            references.Add(new Reference(scopeSymbolId, "rabbitmq:" + topicName, OntologyConstants.Relationships.SubscribesTo));
                        }
                    }
                }
            }
        }
    }
}
