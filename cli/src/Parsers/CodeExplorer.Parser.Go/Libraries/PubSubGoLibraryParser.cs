using System.Text.RegularExpressions;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.Go.Libraries;

public class PubSubGoLibraryParser : ISemanticExtension
{
    public string Type => OntologyConstants.LibraryTypes.Queue;
    public string Name => "PubSub";
    public string Id => "pubsub";
    public IReadOnlyList<string> SupportedPatterns =>
    [
        "cloud.google.com/go/pubsub",
        "cloud.google.com/go/pubsub/*",
        "*pubsub*",
        "*pub-sub*"
    ];
    public bool IsImplemented => true;
    public LibraryRole LibraryRole => LibraryRole.MessageBroker;

    public string? MapNodeType(Node node, ParsingContext ctx) => null;
    public string? ExtractIdentifier(Node node, ParsingContext ctx) => null;

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        // 1. WorkerDef composite literals (used in streaming ingest pipelines)
        if (node.Type is TreeSitterSyntax.Go.CompositeLiteral)
        {
            if (node.Text.Contains("WorkerDef"))
            {
                var topicMatches = Regex.Matches(node.Text, @"TopicID\s*:\s*([^\s,;\}]+)");
                foreach (Match m in topicMatches)
                {
                    var raw = m.Groups[1].Value.Trim('"', '`');
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

                if (topicMatches.Count == 0)
                {
                    var subMatches = Regex.Matches(node.Text, @"SubID\s*:\s*([^\s,;\}]+)");
                    foreach (Match m in subMatches)
                    {
                        var raw = m.Groups[1].Value.Trim('"', '`');
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
                 funcText.EndsWith(".PublishAsync", StringComparison.Ordinal))
        {
            string? topic = null;

            if (func.Is(TreeSitterSyntax.Go.SelectorExpression))
            {
                var operand = func.GetChildForField(TreeSitterSyntax.Fields.Operand);
                if (operand.IsValid())
                {
                    topic = ResolveTopicFromOperand(operand);
                }
            }

            if (string.IsNullOrEmpty(topic) && args.Count > 0)
            {
                if (args.Count >= 2)
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
}
