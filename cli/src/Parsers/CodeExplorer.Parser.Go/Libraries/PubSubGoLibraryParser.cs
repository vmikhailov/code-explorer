using System.Text.RegularExpressions;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.Go.Libraries;

public class PubSubGoLibraryParser : ISemanticExtension
{
    public SemanticExtensionType Type => SemanticExtensionType.Queue;
    public string Name => "PubSub";
    public string Id => "pubsub";
    public IReadOnlyList<string> SupportedPatterns =>
    [
        "cloud.google.com/go/pubsub",
        "cloud.google.com/go/pubsub/*",
        "*pubsub*",
        "*pub-sub*",
        "*dapr*",
        "github.com/dapr/go-sdk*"
    ];
    public bool IsImplemented => true;
    public bool IsBuiltIn => true;
    public LibraryRole LibraryRole => LibraryRole.MessageBroker;

    public string? MapNodeType(Node node, ParsingContext ctx) => null;
    public string? ExtractIdentifier(Node node, ParsingContext ctx) => null;

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        // 1. Composite literals with topic fields (Topic, TopicID, TopicName, SubID, WorkerDef)
        if (node.Type is TreeSitterSyntax.Go.CompositeLiteral or "literal_value")
        {
            if (TryExtractTopicFromCompositeLiteral(node, out var topic, out var isPubLiteral, out var isSubLiteral))
            {
                if (isPubLiteral)
                {
                    AddPublish(references, scopeSymbolId, topic);
                }
                else
                {
                    AddSubscribe(references, scopeSymbolId, topic);
                }
            }
        }

        // 2. Struct tags: detect event types in structs (e.g. event_type:"TYPE_CPV1")
        if (node.Is(TreeSitterSyntax.Go.FieldDeclaration))
        {
            var tagNode = node.GetChildForField(TreeSitterSyntax.Fields.Tag);
            if (tagNode.IsValid())
            {
                var evMatch = Regex.Match(tagNode.Text, @"event_type:""([^""]+)""");
                if (evMatch.Success)
                {
                    var evType = evMatch.Groups[1].Value;
                    AddSubscribe(references, scopeSymbolId, evType);
                }
            }
        }

        // 3. Const declarations: detect constants representing topics (e.g. TopicImpression = "IMPRESSION_TOPIC")
        if (node.Is(TreeSitterSyntax.Go.ConstSpec))
        {
            var nameNode = node.GetChildForField(TreeSitterSyntax.Fields.Name);
            var valNode = node.GetChildForField(TreeSitterSyntax.Fields.Value);
            var name = nameNode.IsValid() ? nameNode.Text : null;
            var val = valNode.IsValid() ? valNode.Text.Trim('"', '`') : null;
            if (!string.IsNullOrEmpty(val) && (val.EndsWith("_TOPIC", StringComparison.OrdinalIgnoreCase) || val.EndsWith("-topic", StringComparison.OrdinalIgnoreCase)))
            {
                AddSubscribe(references, scopeSymbolId, val);
            }
            else if (!string.IsNullOrEmpty(name) && (name.EndsWith("Topic", StringComparison.OrdinalIgnoreCase) || name.StartWithIC("Topic")))
            {
                AddSubscribe(references, scopeSymbolId, name);
            }
        }

        // 4. Configuration getEnv / os.Getenv calls with topic keys or default values
        if (node.Is(TreeSitterSyntax.Go.CallExpression))
        {
            var callFunc = node.GetFunctionNode();
            if (callFunc.IsValid())
            {
                var fnText = callFunc.Text;
                if (fnText.Equals("getEnv", StringComparison.OrdinalIgnoreCase) ||
                    fnText.EndsWith(".getEnv", StringComparison.OrdinalIgnoreCase) ||
                    fnText.Equals("os.Getenv", StringComparison.OrdinalIgnoreCase))
                {
                    var callArgs = GoAstHelper.GetCallArguments(node);
                    if (callArgs.Count >= 2)
                    {
                        var defaultVal = GoAstHelper.ResolveStringOrVariable(callArgs[1]);
                        var keyVal = GoAstHelper.ResolveStringOrVariable(callArgs[0]);
                        if (!string.IsNullOrEmpty(defaultVal) && (defaultVal.EndsWith("-topic", StringComparison.OrdinalIgnoreCase) || defaultVal.EndsWith("_topic", StringComparison.OrdinalIgnoreCase)))
                        {
                            AddSubscribe(references, scopeSymbolId, defaultVal);
                        }
                        else if (!string.IsNullOrEmpty(keyVal) && (keyVal.Contains("TOPIC") || keyVal.Contains("topic")))
                        {
                            if (!string.IsNullOrEmpty(defaultVal))
                            {
                                AddSubscribe(references, scopeSymbolId, defaultVal);
                            }
                            else
                            {
                                AddSubscribe(references, scopeSymbolId, keyVal);
                            }
                        }
                    }
                    else if (callArgs.Count == 1)
                    {
                        var keyVal = GoAstHelper.ResolveStringOrVariable(callArgs[0]);
                        if (!string.IsNullOrEmpty(keyVal) && (keyVal.Contains("TOPIC") || keyVal.Contains("topic")))
                        {
                            AddSubscribe(references, scopeSymbolId, keyVal);
                        }
                    }
                }
            }
        }

        // 5. Call expressions
        if (!node.Is(TreeSitterSyntax.Go.CallExpression)) return;

        var func = node.GetFunctionNode();
        if (!func.IsValid()) return;

        var funcText = func.Text;
        var args = GoAstHelper.GetCallArguments(node);

        // Client Subscription: client.Subscription(subName)
        if (funcText.EndsWith(".Subscription", StringComparison.Ordinal))
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
        // Topic Publish: topic.Publish(ctx, msg) or client.Topic(name).Publish(ctx, msg) or manager.Publish(ctx, topic, msg, ...)
        else if (funcText.EndsWith(".Publish", StringComparison.Ordinal) ||
                 funcText.EndsWith(".PublishAsync", StringComparison.Ordinal) ||
                 funcText.EndsWith(".PublishEvent", StringComparison.Ordinal) ||
                 funcText.EndsWith(".PublishEvents", StringComparison.Ordinal) ||
                 funcText.EndsWith(".PublishEventAlpha1", StringComparison.Ordinal) ||
                 funcText.EndsWith(".BulkPublishEvent", StringComparison.Ordinal) ||
                 funcText.EndsWith(".BulkPublishEventAlpha1", StringComparison.Ordinal))
        {
            string? topic = null;

            foreach (var arg in args)
            {
                if (TryExtractTopicFromCompositeLiteral(arg, out var reqTopic, out _, out _))
                {
                    topic = reqTopic;
                    break;
                }
            }

            if (string.IsNullOrEmpty(topic) && func.Is(TreeSitterSyntax.Go.SelectorExpression))
            {
                var operand = func.GetChildForField(TreeSitterSyntax.Fields.Operand);
                if (operand.IsValid())
                {
                    topic = ResolveTopicFromOperand(operand);
                }
            }

            if (string.IsNullOrEmpty(topic) && args.Count > 0)
            {
                if (args.Count >= 3)
                {
                    topic = GoAstHelper.ResolveStringOrVariable(args[2]);
                }
                if (string.IsNullOrEmpty(topic) && args.Count >= 2)
                {
                    topic = GoAstHelper.ResolveStringOrVariable(args[1]);
                }
                if (string.IsNullOrEmpty(topic))
                {
                    topic = GoAstHelper.ResolveStringOrVariable(args[0]);
                }
            }

            AddPublish(references, scopeSymbolId, topic);
        }
        // Subscription Receive / Subscribe: sub.Receive(ctx, handler) or manager.Subscribe(ctx, to, handler, ...)
        else if (funcText.EndsWith(".Receive", StringComparison.Ordinal) ||
                 funcText.EndsWith(".Subscribe", StringComparison.Ordinal) ||
                 funcText.EndsWith(".SubscribeTopic", StringComparison.Ordinal) ||
                 funcText.EndsWith(".SubscribeTopics", StringComparison.Ordinal) ||
                 funcText.EndsWith(".SubscribeTopicEvents", StringComparison.Ordinal) ||
                 funcText.EndsWith(".SubscribeTopicEventsAlpha1", StringComparison.Ordinal))
        {
            string? topic = null;
            foreach (var arg in args)
            {
                if (TryExtractTopicFromCompositeLiteral(arg, out var reqTopic, out _, out _))
                {
                    topic = reqTopic;
                    break;
                }
            }

            if (string.IsNullOrEmpty(topic) && args.Count >= 2)
            {
                topic = GoAstHelper.ResolveStringOrVariable(args[1]);
            }
            if (string.IsNullOrEmpty(topic) && args.Count >= 1)
            {
                topic = GoAstHelper.ResolveStringOrVariable(args[0]);
            }

            if (!string.IsNullOrEmpty(topic))
            {
                AddSubscribe(references, scopeSymbolId, topic);
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

    private static string? ResolveTopicFromOperand(Node operand)
    {
        // 1. Direct call: client.Topic("events_topic").Publish(...)
        if (operand.Is(TreeSitterSyntax.Go.CallExpression))
        {
            var opFunc = operand.GetFunctionNode();
            if (opFunc.IsValid() && opFunc.Text.EndsWith(".Topic", StringComparison.Ordinal))
            {
                var opArgs = GoAstHelper.GetCallArguments(operand);
                if (opArgs.Count > 0)
                {
                    return GoAstHelper.ResolveStringOrVariable(opArgs[0]);
                }
            }
            return null;
        }

        // 2. Identifier: topic.Publish(...)
        if (operand.IsAny(TreeSitterSyntax.Go.Identifier, TreeSitterSyntax.Go.VariableName))
        {
            var varName = operand.Text;

            // Find declaration in scope: topic := client.Topic("events_topic")
            var declRhs = GoAstHelper.FindVariableInitializerNodeInScope(operand, varName);
            if (declRhs.IsValid())
            {
                if (declRhs.Is(TreeSitterSyntax.Go.CallExpression))
                {
                    var opFunc = declRhs.GetFunctionNode();
                    if (opFunc.IsValid() && opFunc.Text.EndsWith(".Topic", StringComparison.Ordinal))
                    {
                        var opArgs = GoAstHelper.GetCallArguments(declRhs);
                        if (opArgs.Count > 0)
                        {
                            return GoAstHelper.ResolveStringOrVariable(opArgs[0]);
                        }
                    }
                }

                var resolved = GoAstHelper.ResolveStringOrVariable(declRhs);
                if (!string.IsNullOrEmpty(resolved) && !WorkspaceConventions.IsPlaceholderName(resolved))
                {
                    return resolved;
                }
            }

            if (!WorkspaceConventions.IsPlaceholderName(varName) &&
                (varName.EndsWith("Topic", StringComparison.OrdinalIgnoreCase) ||
                 Regex.IsMatch(varName, @"^[A-Z0-9_]{3,}$")))
            {
                return varName;
            }
        }

        // 3. Selector expression: p.topic.Publish(...)
        if (operand.Is(TreeSitterSyntax.Go.SelectorExpression))
        {
            var field = operand.GetChildForField(TreeSitterSyntax.Fields.Field);
            if (field.IsValid())
            {
                var fieldText = field.Text;
                if (!WorkspaceConventions.IsPlaceholderName(fieldText) &&
                    fieldText.EndsWith("Topic", StringComparison.OrdinalIgnoreCase))
                {
                    return fieldText;
                }
            }
        }

        return null;
    }

    private static void AddPublish(List<Reference> references, string scopeSymbolId, string? target)
    {
        if (string.IsNullOrWhiteSpace(target)) return;
        var clean = target.Trim().Trim('"', '`');
        if (clean.StartWithIC("gcp:"))
        {
            clean = clean["gcp:".Length..];
        }
        if (WorkspaceConventions.IsPlaceholderName(clean)) return;

        var normalized = WorkspaceConventions.NormalizeTopicName(clean);
        if (!string.IsNullOrEmpty(normalized))
        {
            references.Add(new Reference(scopeSymbolId, "gcp:" + normalized, OntologyConstants.Relationships.PublishesTo));
        }

        // Also preserve raw identifier if it was already lowercase/snake_case naming (e.g. events_topic, events_sub)
        if (WorkspaceConventions.IsValidTopicOrQueueName(clean) &&
            !clean.Equals(normalized, StringComparison.OrdinalIgnoreCase) &&
            clean.Any(char.IsLower) &&
            (clean.Contains('_') || clean.Contains('-') || clean.All(char.IsLower)))
        {
            references.Add(new Reference(scopeSymbolId, "gcp:" + clean, OntologyConstants.Relationships.PublishesTo));
        }
    }

    private static void AddSubscribe(List<Reference> references, string scopeSymbolId, string? target)
    {
        if (string.IsNullOrWhiteSpace(target)) return;
        var clean = target.Trim().Trim('"', '`');
        if (clean.StartWithIC("gcp:"))
        {
            clean = clean["gcp:".Length..];
        }
        if (WorkspaceConventions.IsPlaceholderName(clean)) return;

        var normalized = WorkspaceConventions.NormalizeTopicName(clean);
        if (!string.IsNullOrEmpty(normalized))
        {
            references.Add(new Reference(scopeSymbolId, "gcp:" + normalized, OntologyConstants.Relationships.SubscribesTo));
        }

        // Also preserve raw identifier if it was already lowercase/snake_case naming (e.g. events_topic, events_sub)
        if (WorkspaceConventions.IsValidTopicOrQueueName(clean) &&
            !clean.Equals(normalized, StringComparison.OrdinalIgnoreCase) &&
            clean.Any(char.IsLower) &&
            (clean.Contains('_') || clean.Contains('-') || clean.All(char.IsLower)))
        {
            references.Add(new Reference(scopeSymbolId, "gcp:" + clean, OntologyConstants.Relationships.SubscribesTo));
        }
    }

    private const string LiteralValueNode = "literal_value";
    private const string UnaryExpressionNode = "unary_expression";
    private const string FieldTopic = "Topic";
    private const string FieldTopicId = "TopicID";
    private const string FieldTopicName = "TopicName";
    private const string FieldSubId = "SubID";

    private static bool TryExtractTopicFromCompositeLiteral(
        Node node,
        out string? topic,
        out bool isPublish,
        out bool isSubscribe)
    {
        topic = null;
        isPublish = false;
        isSubscribe = false;

        var targetNode = node;
        if (targetNode.Is(UnaryExpressionNode) && targetNode.Children.Count >= 2)
        {
            targetNode = targetNode.Children[1];
        }

        if (targetNode.Type is not (TreeSitterSyntax.Go.CompositeLiteral or "literal_value"))
        {
            return false;
        }

        var text = targetNode.Text;
        if (!text.Contains("Topic") && !text.Contains("SubID") && !text.Contains("topic"))
        {
            return false;
        }

        // 1. WorkerDef special case
        if (text.Contains("WorkerDef"))
        {
            var topicMatches = Regex.Matches(text, @"TopicID\s*:\s*([^\s,;\}]+)");
            foreach (Match m in topicMatches)
            {
                var raw = m.Groups[1].Value.Trim('"', '`');
                if (!string.IsNullOrEmpty(raw))
                {
                    topic = CleanTopicTarget(raw);
                    isSubscribe = true;
                    return true;
                }
            }

            var subMatches = Regex.Matches(text, @"SubID\s*:\s*([^\s,;\}]+)");
            foreach (Match m in subMatches)
            {
                var raw = m.Groups[1].Value.Trim('"', '`');
                if (!string.IsNullOrEmpty(raw))
                {
                    topic = CleanTopicTarget(raw);
                    isSubscribe = true;
                    return true;
                }
            }
        }

        // 2. Generic composite literal with Topic / TopicID / TopicName
        var keyedElements = GetKeyedElements(targetNode);
        if (keyedElements.TryGetValue(FieldTopic, out var tNode) ||
            keyedElements.TryGetValue(FieldTopicId, out tNode) ||
            keyedElements.TryGetValue(FieldTopicName, out tNode) ||
            keyedElements.TryGetValue(FieldSubId, out tNode))
        {
            var rawTopic = GoAstHelper.ResolveStringOrVariable(tNode);
            if (string.IsNullOrEmpty(rawTopic))
            {
                var tText = tNode.Text.Trim('"', '`');
                if (ConstantRegistry.TryResolve(null, tText, out var resolvedConst) && !string.IsNullOrEmpty(resolvedConst))
                {
                    rawTopic = resolvedConst;
                }
                else
                {
                    rawTopic = tText;
                }
            }

            if (!string.IsNullOrEmpty(rawTopic) && !WorkspaceConventions.IsPlaceholderName(rawTopic))
            {
                topic = CleanTopicTarget(rawTopic);

                var isPub = text.Contains("Publish", StringComparison.OrdinalIgnoreCase);
                var isSub = text.Contains("Subscribe", StringComparison.OrdinalIgnoreCase) ||
                            text.Contains("Subscription", StringComparison.OrdinalIgnoreCase);

                if (isPub && !isSub)
                {
                    isPublish = true;
                }
                else
                {
                    isSubscribe = true;
                }

                return true;
            }
        }

        return false;
    }

    private static string CleanTopicTarget(string target)
    {
        var clean = target.Trim('"', '`');
        if (clean.Contains('.'))
        {
            clean = clean[(clean.LastIndexOf('.') + 1)..];
        }
        return clean;
    }

    private static Dictionary<string, Node> GetKeyedElements(Node node)
    {
        var result = new Dictionary<string, Node>(StringComparer.OrdinalIgnoreCase);

        var container = node;
        if (node.Type is TreeSitterSyntax.Go.CompositeLiteral)
        {
            var body = node.GetChildForField(TreeSitterSyntax.Fields.Body);
            if (body.IsValid()) container = body;
            else
            {
                var litVal = node.FindChildOfType(LiteralValueNode);
                if (litVal.IsValid()) container = litVal;
            }
        }

        foreach (var rawChild in container.Children)
        {
            var elem = rawChild;
            if (elem.Type == "literal_element" && elem.Children.Count > 0)
            {
                elem = elem.Children[0];
            }

            if (elem.Type is "keyed_element" && elem.Children.Count >= 2)
            {
                var keyChild = elem.Children[0];
                if (keyChild.Type == "literal_element" && keyChild.Children.Count > 0)
                {
                    keyChild = keyChild.Children[0];
                }

                var valChild = elem.Children[^1];
                if (valChild.Type == "literal_element" && valChild.Children.Count > 0)
                {
                    valChild = valChild.Children[0];
                }

                var keyName = keyChild.Text.Trim();
                if (!string.IsNullOrEmpty(keyName) && valChild.IsValid())
                {
                    result[keyName] = valChild;
                }
            }
        }

        return result;
    }
}
