using System.Text.RegularExpressions;
using CodeExplorer.Common;
using CodeExplorer.Core.Parser;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.Rust.Libraries;

/// <summary>
/// Semantic extension for the AWS SDK for Rust (aws-sdk-sqs, aws-sdk-sns, aws-sdk-s3, aws-sdk-kms, aws-sdk-secretsmanager).
/// Detects SQS message consumption (receive_message), SQS message production (send_message),
/// and SNS topic publishing (publish).
/// Emits Topic nodes and PUBLISHES_TO / SUBSCRIBES_TO relationships.
/// </summary>
public class AwsSdkRustLibraryParser : ISemanticExtension
{
    public string Id => "aws-sdk-rust";
    public string Name => "AWS SDK for Rust";
    public string Type => "cloud";
    public bool IsImplemented => true;
    public LibraryRole LibraryRole => LibraryRole.MessageBroker;
    public IReadOnlyList<string> SupportedPatterns => ["aws-sdk-*", "aws_sdk_*", "aws-config*"];

    public IEnumerable<PackageDescriptor> GetPackageDescriptors()
    {
        yield return new PackageDescriptor("aws-sdk-sqs", "AWS SDK SQS", LibraryRole.MessageBroker, "queue", ["aws-sdk-sqs*", "aws_sdk_sqs*"]);
        yield return new PackageDescriptor("aws-sdk-sns", "AWS SDK SNS", LibraryRole.MessageBroker, "queue", ["aws-sdk-sns*", "aws_sdk_sns*"]);
        yield return new PackageDescriptor("aws-sdk-s3", "AWS SDK S3", LibraryRole.CloudSdk, "cloud", ["aws-sdk-s3*", "aws_sdk_s3*"]);
        yield return new PackageDescriptor("aws-sdk-kms", "AWS SDK KMS", LibraryRole.CloudSdk, "cloud", ["aws-sdk-kms*", "aws_sdk_kms*"]);
        yield return new PackageDescriptor("aws-sdk-secretsmanager", "AWS SDK Secrets Manager", LibraryRole.CloudSdk, "cloud", ["aws-sdk-secretsmanager*", "aws_sdk_secretsmanager*"]);
        yield return new PackageDescriptor(Id, Name, LibraryRole, Type, SupportedPatterns);
    }

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (IsAwsSdkCall(node, out _, out _, out _))
        {
            return OntologyConstants.NodeLabels.Topic;
        }
        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx) => ExtractIdentifier(node, ctx, null, null);
    public string? ExtractIdentifier(Node node, ParsingContext ctx, string? projectName) => ExtractIdentifier(node, ctx, projectName, null);

    public string? ExtractIdentifier(Node node, ParsingContext ctx, string? projectName, string? relativePath)
    {
        if (IsAwsSdkCall(node, out var topic, out _, out _))
        {
            return topic;
        }
        return null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (IsAwsSdkCall(node, out var topic, out var brokerType, out var isPublish) && !string.IsNullOrEmpty(topic))
        {
            var relKind = isPublish
                ? OntologyConstants.Relationships.PublishesTo
                : OntologyConstants.Relationships.SubscribesTo;

            references.Add(new Reference(scopeSymbolId, $"{brokerType}:{topic}", relKind));
        }
    }

    public void EnrichSymbol(Node node, SyntacticSymbol symbol, ParsingContext ctx)
    {
        if (IsAwsSdkCall(node, out _, out var brokerType, out var isPublish))
        {
            symbol.Properties["broker_type"] = brokerType;
            symbol.Properties[OntologyConstants.PropertyNames.MessagingDirection] = isPublish
                ? OntologyConstants.EvidenceKinds.Publish
                : OntologyConstants.EvidenceKinds.Subscribe;
        }
    }

    /// <summary>
    /// Detects an AWS SDK fluent operation chain such as <c>client.receive_message().queue_url(url).send()</c>.
    /// SDK operations are zero-argument builder methods invoked on a client receiver, which distinguishes them from
    /// free helper functions (<c>send_message(arn, msg)</c>) and domain methods (<c>self.publish(event)</c>).
    /// Returns false when the queue/topic name cannot be resolved statically, so no phantom topic is created.
    /// </summary>
    public static bool IsAwsSdkCall(Node node, out string topic, out string brokerType, out bool isPublish)
    {
        topic = string.Empty;
        brokerType = string.Empty;
        isPublish = false;

        if (node.Type != TreeSitterSyntax.Rust.CallExpression)
        {
            return false;
        }

        // Avoid matching sub-calls in the same fluent chain (let outer call handle it)
        if (IsReceiverOfParentCall(node))
        {
            return false;
        }

        bool hasReceiveMessage = false;
        bool hasSendMessage = false;
        bool hasPublish = false;
        Node? queueOrTopicArg = null;

        var current = node;
        while (current.IsValid())
        {
            var unwrapped = RustAstHelper.UnwrapTryAndAwait(current);
            if (unwrapped.Type != TreeSitterSyntax.Rust.CallExpression)
            {
                break;
            }

            if (!RustAstHelper.GetMethodCallInfo(unwrapped, out var receiver, out var methodName, out var args))
            {
                break;
            }

            var isSdkOperation = receiver.IsValid() && args.Count == 0;
            switch (methodName)
            {
                case "receive_message" when isSdkOperation:
                    hasReceiveMessage = true;
                    break;
                case "send_message" or "send_message_batch" when isSdkOperation:
                    hasSendMessage = true;
                    break;
                case "publish" or "publish_batch" when isSdkOperation:
                    hasPublish = true;
                    break;
                case "queue_url" or "topic_arn" or "target_arn" when args.Count > 0:
                    // Walking outer → inner, so the innermost (first in source order) setter wins.
                    queueOrTopicArg = args[0];
                    break;
            }

            if (receiver.IsValid() && receiver.Id != current.Id)
            {
                current = receiver;
            }
            else
            {
                break;
            }
        }

        if (!hasReceiveMessage && !hasSendMessage && !hasPublish)
        {
            return false;
        }

        topic = queueOrTopicArg.IsValid() ? ResolveQueueOrTopicName(queueOrTopicArg, 0) : string.Empty;
        if (topic.Length == 0)
        {
            return false;
        }

        brokerType = hasPublish ? "sns" : "sqs";
        isPublish = !hasReceiveMessage;
        return true;
    }

    private static bool IsReceiverOfParentCall(Node node)
    {
        var parent = node.Parent;
        while (parent.IsValid())
        {
            if (parent.Type is TreeSitterSyntax.Rust.TryExpression or TreeSitterSyntax.Rust.AwaitExpression)
            {
                parent = parent.Parent;
                continue;
            }
            if (parent.Type == TreeSitterSyntax.Rust.FieldExpression)
            {
                var grandParent = parent.Parent;
                if (grandParent.IsValid() && grandParent.Type == TreeSitterSyntax.Rust.CallExpression)
                {
                    return true;
                }
            }
            break;
        }
        return false;
    }

    private const int MaxResolutionDepth = 3;

    private static readonly Regex ConversionCallRegex = new(
        @"\.(clone|to_string|to_owned|as_str|as_ref|as_deref|into|unwrap|borrow|deref)\(\s*\)",
        RegexOptions.Compiled);

    private static readonly Regex EmptyCallParensRegex = new(@"\(\s*\)", RegexOptions.Compiled);
    private static readonly Regex RustIdentifierRegex = new(@"^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);
    private static readonly Regex ValidTopicNameRegex = new(@"^[a-z0-9][a-z0-9.\-]*$", RegexOptions.Compiled);

    private static readonly HashSet<string> GenericTopicNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "queue", "topic", "url", "arn", "self", "q", "t", "name", "target", "sqs", "sns"
    };

    /// <summary>
    /// Resolves a queue/topic argument to a stable name. Bare local identifiers are followed through preceding
    /// <c>let</c> bindings; field paths ending in a generic field (e.g. <c>self.publish.topic_arn</c>) are resolved
    /// through struct field initializers in the same file when they agree on a single value.
    /// </summary>
    private static string ResolveQueueOrTopicName(Node arg, int depth)
    {
        var literal = RustAstHelper.ExtractStringLiteral(arg);
        var direct = CleanTopicOrQueueName(literal != null ? $"\"{literal}\"" : arg.Text);
        if (direct.Length > 0 || depth >= MaxResolutionDepth)
        {
            return direct;
        }

        var path = NormalizeExpressionPath(arg.Text);
        if (path.Length == 0)
        {
            return string.Empty;
        }

        var segments = path.Split('.');
        if (segments.Length == 1)
        {
            if (!RustIdentifierRegex.IsMatch(segments[0]))
            {
                return string.Empty;
            }

            var bound = FindLetBindingValue(arg, segments[0]);
            return bound.IsValid() ? ResolveQueueOrTopicName(bound, depth + 1) : string.Empty;
        }

        var fieldName = segments[^1];
        if (!RustIdentifierRegex.IsMatch(fieldName))
        {
            return string.Empty;
        }

        var candidates = FindFieldInitializerValues(arg, fieldName)
            .Select(v => ResolveQueueOrTopicName(v, depth + 1))
            .Where(n => n.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return candidates.Count == 1 ? candidates[0] : string.Empty;
    }

    private static string NormalizeExpressionPath(string text)
    {
        var clean = text.Trim().TrimStart('&', '*').Trim();
        if (clean.StartsWith("mut ", StringComparison.Ordinal))
        {
            clean = clean[4..].Trim();
        }

        clean = ConversionCallRegex.Replace(clean, string.Empty);
        clean = EmptyCallParensRegex.Replace(clean, string.Empty);
        return clean;
    }

    private static Node? FindLetBindingValue(Node usage, string identifier)
    {
        var ancestor = usage.Parent;
        while (ancestor.IsValid())
        {
            if (ancestor.Type == TreeSitterSyntax.Rust.Block)
            {
                Node? match = null;
                foreach (var letDecl in ancestor.FindChildrenOfType(TreeSitterSyntax.Rust.LetDeclaration))
                {
                    if (!EndsBefore(letDecl, usage))
                    {
                        continue;
                    }

                    var pattern = letDecl.GetField(TreeSitterSyntax.Fields.Pattern);
                    var value = letDecl.GetField(TreeSitterSyntax.Fields.Value);
                    if (pattern.IsValid() && value.IsValid() && pattern.Text == identifier)
                    {
                        match = value;
                    }
                }

                if (match != null)
                {
                    return match;
                }
            }

            if (ancestor.Type is TreeSitterSyntax.Rust.FunctionItem or TreeSitterSyntax.Rust.ClosureExpression)
            {
                break;
            }

            ancestor = ancestor.Parent;
        }

        return null;
    }

    private static IEnumerable<Node> FindFieldInitializerValues(Node usage, string fieldName)
    {
        var root = usage;
        while (root.Parent.IsValid())
        {
            root = root.Parent;
        }

        foreach (var initializer in root.FindDescendantsOfType(TreeSitterSyntax.Rust.FieldInitializer))
        {
            var field = initializer.GetField(TreeSitterSyntax.Fields.Field);
            var value = initializer.GetField(TreeSitterSyntax.Fields.Value);
            if (field.IsValid() && value.IsValid() && field.Text == fieldName)
            {
                yield return value;
            }
        }
    }

    private static bool EndsBefore(Node candidate, Node usage) =>
        candidate.EndPosition.Row < usage.StartPosition.Row ||
        (candidate.EndPosition.Row == usage.StartPosition.Row && candidate.EndPosition.Column <= usage.StartPosition.Column);

    /// <summary>
    /// Normalizes a queue URL / topic ARN expression into a kebab-case resource name.
    /// Returns an empty string when the expression carries no specific name (e.g. <c>queue_url</c>, <c>self</c>).
    /// </summary>
    public static string CleanTopicOrQueueName(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        var clean = raw.Trim().TrimStart('&', '*').Trim();
        var isLiteral = false;

        if (clean.StartsWith('"') && clean.EndsWith('"') && clean.Length >= 2)
        {
            clean = clean[1..^1];
            isLiteral = true;
        }

        // Handle URL: https://sqs.../queue_name
        if (clean.Contains('/'))
        {
            var lastSlash = clean.LastIndexOf('/');
            if (lastSlash >= 0 && lastSlash + 1 < clean.Length)
            {
                clean = clean[(lastSlash + 1)..];
                isLiteral = true;
            }
        }

        // Handle ARN: arn:aws:sns:region:account:topic-name
        if (clean.StartWithIC("arn:aws:"))
        {
            var lastColon = clean.LastIndexOf(':');
            if (lastColon >= 0 && lastColon + 1 < clean.Length)
            {
                clean = clean[(lastColon + 1)..];
                isLiteral = true;
            }
        }

        if (!isLiteral)
        {
            // Strip value conversions and getter call parens: self.config().topic_arn().clone() -> self.config.topic_arn
            clean = NormalizeExpressionPath(clean);

            // Strip field access prefixes: config.requests_queue_url -> requests_queue_url
            if (clean.Contains('.'))
            {
                clean = clean.Split('.').Last();
            }

            // Broker-qualified field names describe the same resource: sns_request_topic_arn == request_topic_arn
            foreach (var brokerPrefix in (string[])["sns_", "sqs_"])
            {
                if (clean.StartsWith(brokerPrefix, StringComparison.OrdinalIgnoreCase) && clean.Length > brokerPrefix.Length)
                {
                    clean = clean[brokerPrefix.Length..];
                    break;
                }
            }
        }

        foreach (var suffix in (string[])["_url", "_arn", "-url", "-arn"])
        {
            if (clean.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                clean = clean[..^suffix.Length];
                break;
            }
        }

        clean = clean.Replace('_', '-').ToLowerInvariant().Trim('-');

        if (GenericTopicNames.Contains(clean) || !ValidTopicNameRegex.IsMatch(clean))
        {
            return string.Empty;
        }

        return clean;
    }
}
