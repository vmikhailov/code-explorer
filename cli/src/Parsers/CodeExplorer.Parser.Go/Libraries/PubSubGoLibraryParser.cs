using System.Text.RegularExpressions;
using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.Go.Libraries;

public class PubSubGoLibraryParser : ISemanticExtension
{
    public string Type => OntologyConstants.LibraryTypes.Queue;
    public string Name => "PubSub";
    public string Id => "pubsub";
    public IReadOnlyList<string> SupportedPatterns =>
    [
        "cloud.google.com/go/pubsub",
        "cloud.google.com/go/pubsub/*"
    ];
    public bool IsImplemented => true;

    public string? MapNodeType(Node node, ParsingContext ctx) => null;
    public string? ExtractIdentifier(Node node, ParsingContext ctx) => null;

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        // 1. WorkerDef composite literals (used in streaming ingest pipelines)
        if (node.Type is "composite_literal" or TreeSitterSyntax.Go.CompositeLiteral)
        {
            if (node.Text.Contains("WorkerDef"))
            {
                var topicMatch = Regex.Match(node.Text, @"TopicID\s*:\s*([^\s,;\}]+)");
                if (topicMatch.Success)
                {
                    var raw = topicMatch.Groups[1].Value.Trim('"', '`');
                    if (!string.IsNullOrEmpty(raw))
                    {
                        var target = raw.Trim('"', '`');
                        if (target.Contains('.'))
                        {
                            target = target[(target.LastIndexOf('.') + 1)..];
                        }
                        AddSubscribe(references, scopeSymbolId, target);
                    }
                }

                var subMatch = Regex.Match(node.Text, @"SubID\s*:\s*([^\s,;\}]+)");
                if (subMatch.Success)
                {
                    var raw = subMatch.Groups[1].Value.Trim('"', '`');
                    if (!string.IsNullOrEmpty(raw))
                    {
                        var target = raw.Trim('"', '`');
                        if (target.Contains('.'))
                        {
                            target = target[(target.LastIndexOf('.') + 1)..];
                        }
                        AddSubscribe(references, scopeSymbolId, target);
                    }
                }
            }
        }

        // 2. Call expressions
        if (!node.Is(TreeSitterSyntax.Go.CallExpression)) return;

        var func = node.GetFunctionNode();
        if (!func.IsValid()) return;

        var funcText = func.Text;
        var args = GoAstHelper.GetCallArguments(node);

        // Client Topic: client.Topic(topicName)
        if (funcText.EndsWith(".Topic", StringComparison.Ordinal))
        {
            if (args.Count > 0)
            {
                var topic = GoAstHelper.ResolveStringOrVariable(args[0]);
                AddPublish(references, scopeSymbolId, topic);
            }
        }
        // Client Subscription: client.Subscription(subName)
        else if (funcText.EndsWith(".Subscription", StringComparison.Ordinal))
        {
            if (args.Count > 0)
            {
                var sub = GoAstHelper.ResolveStringOrVariable(args[0]);
                AddSubscribe(references, scopeSymbolId, sub);
            }
        }
        // Client CreateSubscription: client.CreateSubscription(ctx, subID, cfg)
        else if (funcText.EndsWith(".CreateSubscription", StringComparison.Ordinal) ||
                 funcText.EndsWith(".EnsureSubscription", StringComparison.Ordinal))
        {
            if (args.Count >= 2)
            {
                var target = GoAstHelper.ResolveStringOrVariable(args[1]);
                if (string.IsNullOrEmpty(target) && args.Count >= 3)
                {
                    target = GoAstHelper.ResolveStringOrVariable(args[2]);
                }
                AddSubscribe(references, scopeSymbolId, target);
            }
        }
        // Topic Publish: topic.Publish(ctx, msg) or manager.Publish(ctx, topic, msg, ...)
        else if (funcText.EndsWith(".Publish", StringComparison.Ordinal))
        {
            if (args.Count >= 2)
            {
                var topic = GoAstHelper.ResolveStringOrVariable(args[1]);
                AddPublish(references, scopeSymbolId, topic);
            }
        }
        // Subscription Receive / Subscribe: sub.Receive(ctx, handler) or manager.Subscribe(ctx, to, handler, ...)
        else if (funcText.EndsWith(".Receive", StringComparison.Ordinal) ||
                 funcText.EndsWith(".Subscribe", StringComparison.Ordinal))
        {
            if (funcText.EndsWith(".Subscribe", StringComparison.Ordinal) && args.Count >= 2)
            {
                var to = GoAstHelper.ResolveStringOrVariable(args[1]);
                AddSubscribe(references, scopeSymbolId, to);
            }
            else if (funcText.EndsWith(".Receive", StringComparison.Ordinal))
            {
                if (func.Is(TreeSitterSyntax.Go.SelectorExpression))
                {
                    var operand = func.GetChildForField(TreeSitterSyntax.Fields.Operand);
                    if (operand.IsValid())
                    {
                        var sub = GoAstHelper.ResolveStringOrVariable(operand);
                        AddSubscribe(references, scopeSymbolId, sub);
                    }
                }
            }
        }
    }

    private static void AddPublish(List<Reference> references, string scopeSymbolId, string? target)
    {
        if (!string.IsNullOrEmpty(target) && WorkspaceConventions.IsValidTopicOrQueueName(target))
        {
            references.Add(new Reference(scopeSymbolId, "gcp:" + target.Trim(), OntologyConstants.Relationships.PublishesTo));
        }
    }

    private static void AddSubscribe(List<Reference> references, string scopeSymbolId, string? target)
    {
        if (!string.IsNullOrEmpty(target) && WorkspaceConventions.IsValidTopicOrQueueName(target))
        {
            references.Add(new Reference(scopeSymbolId, "gcp:" + target.Trim(), OntologyConstants.Relationships.SubscribesTo));
        }
    }
}
