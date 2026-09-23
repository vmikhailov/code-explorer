using System.Text.RegularExpressions;
using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public class GcpLibraryParser : ILibraryParser
{
    public string Type => OntologyConstants.LibraryTypes.Queue;
    public string Name => "GCP";
    public string Id => "gcp";
    public IReadOnlyList<string> SupportedPatterns =>
    [
        "@google-cloud",
        "@google-cloud/*",
        "firebase",
        "firebase-admin",
        "@atsorganization/internal-commons-library*",
        "@atsorganization/ats-lib-messaging",
        "*pubsub*",
        "*pub-sub*"
    ];
    public bool IsImplemented => true;

    public string? MapNodeType(Node node, ParsingContext ctx) => null;
    public string? ExtractIdentifier(Node node, ParsingContext ctx) => null;

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (!node.Is(TreeSitterSyntax.TypeScript.CallExpression)) return;

        var funcNode = node.GetFunctionNode();
        if (!funcNode.IsValid()) return;

        var funcText = funcNode.Text;
        var args = AstHelper.GetCallArguments(node);

        // --- 1. PUBLISHERS ---

        // A. Direct pubsub.topic(topicName) or getPubSubTopic(topicName)
        if (funcText.EndsWith(".topic", StringComparison.Ordinal) ||
            funcText == "getPubSubTopic" ||
            funcText.EndsWith(".getPubSubTopic", StringComparison.Ordinal) ||
            funcText == "createNetworkTopic" ||
            funcText.EndsWith(".createNetworkTopic", StringComparison.Ordinal))
        {
            if (args.Count > 0)
            {
                var topic = AstHelper.ResolveStringOrTemplate(args[0]);
                if (!string.IsNullOrEmpty(topic))
                {
                    references.Add(new Reference(scopeSymbolId, "gcp:" + topic, OntologyConstants.Relationships.PublishesTo));
                }
            }
        }
        // B. Chained publish: pubsub.topic(topicName).publishMessage(...) or pubsub.topic(topicName).publish(...)
        else if ((funcText.EndsWith(".publishMessage", StringComparison.Ordinal) ||
                  funcText.EndsWith(".publish", StringComparison.Ordinal)) &&
                 funcNode.Is(TreeSitterSyntax.TypeScript.MemberExpression))
        {
            var obj = funcNode.GetField(TreeSitterSyntax.Fields.Object);
            string? topic = null;

            // Check if object is a call like pubsub.topic(topicName)
            if (obj.IsValid() && obj.Is(TreeSitterSyntax.TypeScript.CallExpression))
            {
                var innerFunc = obj.GetFunctionNode();
                if (innerFunc.IsValid() && innerFunc.Text.EndsWith(".topic", StringComparison.Ordinal))
                {
                    var innerArgs = AstHelper.GetCallArguments(obj);
                    if (innerArgs.Count > 0)
                    {
                        topic = AstHelper.ResolveStringOrTemplate(innerArgs[0]);
                    }
                }
            }

            // If not found from receiver call, check if arguments contain topic (e.g. { topicName: ... } or topic argument)
            if (string.IsNullOrEmpty(topic) && args.Count > 0)
            {
                topic = AstHelper.ResolveStringOrTemplate(args[0]);
                // If args[0] was data and args[1] was topic (e.g. publish(data, topic))
                if (string.IsNullOrEmpty(topic) && args.Count > 1)
                {
                    topic = AstHelper.ResolveStringOrTemplate(args[1]);
                }
            }

            // Fallback: check receiver variable name (e.g. this.topic, this.topicJournalEvents)
            if (string.IsNullOrEmpty(topic) && obj.IsValid())
            {
                topic = ResolveTopicVariableInScope(obj, obj.Text);
            }

            if (!string.IsNullOrEmpty(topic))
            {
                references.Add(new Reference(scopeSymbolId, "gcp:" + topic, OntologyConstants.Relationships.PublishesTo));
            }
        }
        // C. Specific publishing helper methods
        else if (funcText.EndsWith(".publishMessageJournalEvents", StringComparison.Ordinal) ||
                 funcText == "publishMessageJournalEvents")
        {
            references.Add(new Reference(scopeSymbolId, "gcp:EVENT_JOURNAL_TOPIC", OntologyConstants.Relationships.PublishesTo));
        }
        else if (funcText.EndsWith(".publishMessageToJournal", StringComparison.Ordinal) ||
                 funcText.EndsWith(".publishJournalMessage", StringComparison.Ordinal) ||
                 funcText == "sendEventToJournal" ||
                 funcText.EndsWith(".sendEventToJournal", StringComparison.Ordinal))
        {
            string? topic = null;
            if (args.Count > 1)
            {
                topic = AstHelper.ResolveStringOrTemplate(args[1]);
            }
            if (string.IsNullOrEmpty(topic))
            {
                topic = "EVENT_JOURNAL_TOPIC";
            }
            references.Add(new Reference(scopeSymbolId, "gcp:" + topic, OntologyConstants.Relationships.PublishesTo));
        }
        else if (funcText.EndsWith(".sendMessageToTopicWithAttributes", StringComparison.Ordinal) ||
                 funcText == "sendMessageToTopicWithAttributes")
        {
            if (args.Count > 0)
            {
                var topic = AstHelper.ResolveStringOrTemplate(args[0]);
                if (!string.IsNullOrEmpty(topic))
                {
                    references.Add(new Reference(scopeSymbolId, "gcp:" + topic, OntologyConstants.Relationships.PublishesTo));
                }
            }
        }
        else if (funcText.EndsWith(".sendMessageToNetwork", StringComparison.Ordinal) ||
                 funcText == "sendMessageToNetwork")
        {
            if (args.Count > 1)
            {
                var topic = AstHelper.ResolveStringOrTemplate(args[1]);
                if (!string.IsNullOrEmpty(topic))
                {
                    references.Add(new Reference(scopeSymbolId, "gcp:" + topic, OntologyConstants.Relationships.PublishesTo));
                }
            }
        }
        else if (funcText.EndsWith(".publishToTopic", StringComparison.Ordinal) ||
                 funcText == "publishToTopic" ||
                 funcText == "sendMessageToTopic")
        {
            if (args.Count > 0)
            {
                var topic = AstHelper.ResolveStringOrTemplate(args[0]);
                if (!string.IsNullOrEmpty(topic))
                {
                    references.Add(new Reference(scopeSymbolId, "gcp:" + topic, OntologyConstants.Relationships.PublishesTo));
                }
            }
        }

        // --- 2. SUBSCRIBERS ---

        // A. Subscription setup: subscribeToMessages, initSubscription, listenSubscription
        else if (funcText.EndsWith(".subscribeToMessages", StringComparison.Ordinal) ||
                 funcText.EndsWith(".initSubscription", StringComparison.Ordinal) ||
                 funcText.EndsWith(".listenSubscription", StringComparison.Ordinal) ||
                 funcText.EndsWith(".subscription", StringComparison.Ordinal))
        {
            if (args.Count > 0)
            {
                var sub = AstHelper.ResolveStringOrTemplate(args[0]);
                if (!string.IsNullOrEmpty(sub))
                {
                    references.Add(new Reference(scopeSymbolId, "gcp:" + sub, OntologyConstants.Relationships.SubscribesTo));
                }
            }
        }
        // B. initPubSub(topicName, subscriptionName)
        else if (funcText.EndsWith(".initPubSub", StringComparison.Ordinal))
        {
            if (args.Count > 0)
            {
                var topic = AstHelper.ResolveStringOrTemplate(args[0]);
                if (!string.IsNullOrEmpty(topic))
                {
                    references.Add(new Reference(scopeSymbolId, "gcp:" + topic, OntologyConstants.Relationships.SubscribesTo));
                }
            }
            if (args.Count > 1)
            {
                var sub = AstHelper.ResolveStringOrTemplate(args[1]);
                if (!string.IsNullOrEmpty(sub))
                {
                    references.Add(new Reference(scopeSymbolId, "gcp:" + sub, OntologyConstants.Relationships.SubscribesTo));
                }
            }
        }
        // C. Named subscribers in ATS (e.g. subscribeToPostbackPartnerMessages, subscribeToImpressionMessages)
        else if (funcText.EndsWith(".subscribeToPostbackPartnerMessages", StringComparison.Ordinal))
        {
            references.Add(new Reference(scopeSymbolId, "gcp:POSTBACK_PARTNER_SUB_NAME", OntologyConstants.Relationships.SubscribesTo));
        }
        else if (funcText.EndsWith(".subscribeToImpressionMessages", StringComparison.Ordinal))
        {
            references.Add(new Reference(scopeSymbolId, "gcp:IMPRESSION_SUB_NAME", OntologyConstants.Relationships.SubscribesTo));
        }
    }

    private static string? ResolveTopicVariableInScope(Node node, string varName)
    {
        var cleanName = varName.StartsWith("this.", StringComparison.OrdinalIgnoreCase) ? varName[5..] : varName;

        var curr = node.Parent;
        while (curr.IsValid())
        {
            if (curr.IsAny(TreeSitterSyntax.TypeScript.StatementBlock, TreeSitterSyntax.TypeScript.Program, "class_body"))
            {
                foreach (var child in curr.Children)
                {
                    if (child.Text.Contains(cleanName) && child.Text.Contains(".topic("))
                    {
                        var match = Regex.Match(child.Text, @"\.topic\s*\(\s*([^,\)]+)");
                        if (match.Success)
                        {
                            var arg = match.Groups[1].Value.Trim().Trim('\'', '"', '`');
                            if (!string.IsNullOrEmpty(arg)) return arg;
                        }
                    }
                }
            }
            curr = curr.Parent;
        }

        if (cleanName.EndsWith("Topic", StringComparison.OrdinalIgnoreCase) ||
            cleanName.EndsWith("TopicName", StringComparison.OrdinalIgnoreCase) ||
            cleanName.EndsWith("topicJournalEvents", StringComparison.OrdinalIgnoreCase))
        {
            if (cleanName.Contains("Journal", StringComparison.OrdinalIgnoreCase))
            {
                return "EVENT_JOURNAL_TOPIC";
            }
            return cleanName;
        }

        return null;
    }
}
