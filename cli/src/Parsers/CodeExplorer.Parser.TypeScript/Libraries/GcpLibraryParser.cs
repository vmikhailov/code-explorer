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
                AddPublishReference(references, scopeSymbolId, topic);
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

            // Check receiver variable name (e.g. this.topic, this.ruleTreeTopic, customTopic)
            if (string.IsNullOrEmpty(topic) && obj.IsValid())
            {
                topic = ResolveTopicVariableInScope(obj, obj.Text);
            }

            // If not found from receiver call or variable, check if arguments contain { topicName: ... } or topic argument
            if (string.IsNullOrEmpty(topic) && args.Count > 0)
            {
                if (args[0].Is(TreeSitterSyntax.TypeScript.Object) || args[0].Type == "object")
                {
                    if (AstHelper.TryGetObjectProperty(args[0], "topicName", out var tp) ||
                        AstHelper.TryGetObjectProperty(args[0], "topic", out tp))
                    {
                        topic = AstHelper.ResolveStringOrTemplate(tp);
                    }
                }
                else if (args.Count > 1)
                {
                    topic = AstHelper.ResolveStringOrTemplate(args[1]);
                }
            }

            AddPublishReference(references, scopeSymbolId, topic);
        }
        // C. Specific publishing helper methods
        else if (funcText.EndsWith(".sendMessageToTopicWithAttributes", StringComparison.Ordinal) ||
                 funcText == "sendMessageToTopicWithAttributes")
        {
            if (args.Count > 0)
            {
                var topic = AstHelper.ResolveStringOrTemplate(args[0]);
                AddPublishReference(references, scopeSymbolId, topic);
            }
        }
        else if (funcText.EndsWith(".sendMessageToNetwork", StringComparison.Ordinal) ||
                 funcText == "sendMessageToNetwork")
        {
            if (args.Count > 1)
            {
                var topic = AstHelper.ResolveStringOrTemplate(args[1]);
                AddPublishReference(references, scopeSymbolId, topic);
            }
        }
        else if (funcText.EndsWith(".publishToTopic", StringComparison.Ordinal) ||
                 funcText == "publishToTopic" ||
                 funcText == "sendMessageToTopic")
        {
            if (args.Count > 0)
            {
                var topic = AstHelper.ResolveStringOrTemplate(args[0]);
                AddPublishReference(references, scopeSymbolId, topic);
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
                AddSubscribeReference(references, scopeSymbolId, sub);
            }
        }
        // B. initPubSub(topicName, subscriptionName)
        else if (funcText.EndsWith(".initPubSub", StringComparison.Ordinal))
        {
            if (args.Count > 0)
            {
                var topic = AstHelper.ResolveStringOrTemplate(args[0]);
                AddSubscribeReference(references, scopeSymbolId, topic);
            }
            if (args.Count > 1)
            {
                var sub = AstHelper.ResolveStringOrTemplate(args[1]);
                AddSubscribeReference(references, scopeSymbolId, sub);
            }
        }
    }

    private static void AddPublishReference(List<Reference> references, string scopeSymbolId, string? topic)
    {
        if (IsValidTopicName(topic))
        {
            references.Add(new Reference(scopeSymbolId, "gcp:" + topic!.Trim(), OntologyConstants.Relationships.PublishesTo));
        }
    }

    private static void AddSubscribeReference(List<Reference> references, string scopeSymbolId, string? topic)
    {
        if (IsValidTopicName(topic))
        {
            references.Add(new Reference(scopeSymbolId, "gcp:" + topic!.Trim(), OntologyConstants.Relationships.SubscribesTo));
        }
    }

    private static bool IsValidTopicName(string? topic)
    {
        if (string.IsNullOrWhiteSpace(topic)) return false;
        var t = topic.Trim();
        if (t.StartsWith(':') ||
            t.Equals("Topic", StringComparison.OrdinalIgnoreCase) ||
            t.Equals("string", StringComparison.OrdinalIgnoreCase) ||
            t.Equals("undefined", StringComparison.OrdinalIgnoreCase) ||
            t.Equals("null", StringComparison.OrdinalIgnoreCase) ||
            t.Equals("void", StringComparison.OrdinalIgnoreCase) ||
            t.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            t.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
            t.Length <= 3)
        {
            return false;
        }
        return true;
    }

    private static string? ResolveTopicVariableInScope(Node node, string varName)
    {
        var cleanName = varName.StartsWith("this.", StringComparison.OrdinalIgnoreCase) ? varName[5..] : varName;

        if (ConstantRegistry.TryResolve(null, cleanName, out var resolved) && IsValidTopicName(resolved))
        {
            return resolved;
        }

        var curr = node.Parent;
        while (curr.IsValid())
        {
            if (curr.IsAny(TreeSitterSyntax.TypeScript.StatementBlock, TreeSitterSyntax.TypeScript.Program, "class_body"))
            {
                foreach (var child in curr.Children)
                {
                    if (child.Text.Contains(cleanName) && child.Text.Contains(".topic("))
                    {
                        var match = Regex.Match(child.Text, $@"(?:this\.)?{Regex.Escape(cleanName)}\s*=\s*[^;]*?\.topic\s*\(\s*([^,\)]+)");
                        if (!match.Success)
                        {
                            match = Regex.Match(child.Text, @"\.topic\s*\(\s*([^,\)]+)");
                        }
                        if (match.Success)
                        {
                            var arg = match.Groups[1].Value.Trim().Trim('\'', '"', '`');
                            var cleanArg = arg.StartsWith("this.", StringComparison.OrdinalIgnoreCase) ? arg[5..] : arg;
                            if (ConstantRegistry.TryResolve(null, cleanArg, out var resArg) && IsValidTopicName(resArg))
                            {
                                return resArg;
                            }
                            var varVal = AstHelper.FindVariableInitializerInAst(node, cleanArg);
                            if (!string.IsNullOrEmpty(varVal) && IsValidTopicName(varVal))
                            {
                                return varVal;
                            }
                            if (IsValidTopicName(arg)) return arg;
                        }
                    }
                }
            }
            curr = curr.Parent;
        }

        var varValDirect = AstHelper.FindVariableInitializerInAst(node, cleanName);
        if (!string.IsNullOrEmpty(varValDirect) && IsValidTopicName(varValDirect))
        {
            return varValDirect;
        }

        if (cleanName.EndsWith("Topic", StringComparison.OrdinalIgnoreCase) ||
            cleanName.EndsWith("TopicName", StringComparison.OrdinalIgnoreCase))
        {
            if (IsValidTopicName(cleanName)) return cleanName;
        }

        return null;
    }
}
