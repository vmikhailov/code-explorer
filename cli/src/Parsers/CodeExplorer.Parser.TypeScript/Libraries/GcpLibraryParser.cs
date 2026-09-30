using System.Text.RegularExpressions;
using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public class GcpLibraryParser : ISemanticExtension
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

        // A. Direct pubsub.topic(topicName)
        if (funcText.EndsWith(".topic", StringComparison.Ordinal))
        {
            if (args.Count > 0)
            {
                if (!AstValueResolver.TryResolveTopicOrQueue(args[0], scopeSymbolId, out var topic) || string.IsNullOrEmpty(topic))
                {
                    topic = AstHelper.ResolveTopicOrQueue(args[0], scopeSymbolId);
                }
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
                        if (!AstValueResolver.TryResolveTopicOrQueue(innerArgs[0], scopeSymbolId, out topic) || string.IsNullOrEmpty(topic))
                        {
                            topic = AstHelper.ResolveTopicOrQueue(innerArgs[0], scopeSymbolId);
                        }
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
                        if (!AstValueResolver.TryResolveTopicOrQueue(tp, scopeSymbolId, out topic) || string.IsNullOrEmpty(topic))
                        {
                            topic = AstHelper.ResolveTopicOrQueue(tp, scopeSymbolId);
                        }
                    }
                }
                else
                {
                    if (!AstValueResolver.TryResolveTopicOrQueue(args[0], scopeSymbolId, out topic) || string.IsNullOrEmpty(topic))
                    {
                        topic = AstHelper.ResolveTopicOrQueue(args[0], scopeSymbolId);
                    }
                }
            }

            AddPublishReference(references, scopeSymbolId, topic);
        }

        // --- 2. SUBSCRIBERS ---

        // A. Subscription setup: subscribeToMessages, initSubscription, listenSubscription, subscription
        else if (funcText.EndsWith(".subscribeToMessages", StringComparison.Ordinal) ||
                 funcText.EndsWith(".initSubscription", StringComparison.Ordinal) ||
                 funcText.EndsWith(".listenSubscription", StringComparison.Ordinal) ||
                 funcText.EndsWith(".subscription", StringComparison.Ordinal) ||
                 funcText.EndsWith(".subscribe", StringComparison.Ordinal))
        {
            if (args.Count > 0)
            {
                if (!AstValueResolver.TryResolveTopicOrQueue(args[0], scopeSymbolId, out var sub) || string.IsNullOrEmpty(sub))
                {
                    sub = AstHelper.ResolveTopicOrQueue(args[0], scopeSymbolId);
                }
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
        return WorkspaceConventions.IsValidTopicOrQueueName(topic);
    }

    private static string? ResolveTopicVariableInScope(Node node, string varName)
    {
        var cleanName = varName.StartsWith("this.", StringComparison.OrdinalIgnoreCase) ? varName[5..] : varName;

        if (ConstantRegistry.TryResolve(null, cleanName, out var resolved) && IsValidTopicName(resolved))
        {
            return resolved;
        }

        var declNode = AstValueResolver.FindVariableDeclarationInScope(node, cleanName) ??
                       AstValueResolver.FindClassFieldInitializer(node, cleanName);

        if (declNode.IsValid())
        {
            if (declNode.Is(TreeSitterSyntax.TypeScript.CallExpression))
            {
                var args = AstHelper.GetCallArguments(declNode);
                if (args.Count > 0 && AstValueResolver.TryResolveTopicOrQueue(args[0], null, out var tName))
                {
                    if (IsValidTopicName(tName)) return tName;
                }
            }

            if (AstValueResolver.TryResolveTopicOrQueue(declNode, null, out var tRes) && IsValidTopicName(tRes))
            {
                return tRes;
            }
        }

        var varValDirect = AstHelper.FindVariableInitializerInAst(node, cleanName);
        if (!string.IsNullOrEmpty(varValDirect) && IsValidTopicName(varValDirect))
        {
            return varValDirect;
        }

        return null;
    }
}
