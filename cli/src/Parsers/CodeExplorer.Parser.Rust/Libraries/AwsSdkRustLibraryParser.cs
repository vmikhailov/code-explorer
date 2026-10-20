using System.Text.RegularExpressions;
using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

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
        if (IsAwsSdkCall(node, out _, out var brokerType, out _))
        {
            symbol.Properties["broker_type"] = brokerType;
        }
    }

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
        string? extractedQueueOrTopic = null;

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

            if (!string.IsNullOrEmpty(methodName))
            {
                if (methodName.Equals("receive_message", StringComparison.OrdinalIgnoreCase))
                {
                    hasReceiveMessage = true;
                }
                else if (methodName.Equals("send_message", StringComparison.OrdinalIgnoreCase) ||
                         methodName.Equals("send_message_batch", StringComparison.OrdinalIgnoreCase))
                {
                    hasSendMessage = true;
                }
                else if (methodName.Equals("publish", StringComparison.OrdinalIgnoreCase) ||
                         methodName.Equals("publish_batch", StringComparison.OrdinalIgnoreCase))
                {
                    hasPublish = true;
                }
                else if (methodName.Equals("queue_url", StringComparison.OrdinalIgnoreCase) && args.Count > 0)
                {
                    extractedQueueOrTopic ??= RustAstHelper.ExtractStringLiteral(args[0]) ?? args[0].Text;
                }
                else if ((methodName.Equals("topic_arn", StringComparison.OrdinalIgnoreCase) ||
                          methodName.Equals("target_arn", StringComparison.OrdinalIgnoreCase)) && args.Count > 0)
                {
                    extractedQueueOrTopic ??= RustAstHelper.ExtractStringLiteral(args[0]) ?? args[0].Text;
                }
            }

            if (receiver != null && receiver.IsValid() && receiver.Id != current.Id)
            {
                current = receiver;
            }
            else
            {
                break;
            }
        }

        if (hasReceiveMessage)
        {
            brokerType = "sqs";
            isPublish = false;
            topic = CleanTopicOrQueueName(extractedQueueOrTopic, "sqs");
            return true;
        }

        if (hasSendMessage)
        {
            brokerType = "sqs";
            isPublish = true;
            topic = CleanTopicOrQueueName(extractedQueueOrTopic, "sqs");
            return true;
        }

        if (hasPublish)
        {
            brokerType = "sns";
            isPublish = true;
            topic = CleanTopicOrQueueName(extractedQueueOrTopic, "sns");
            return true;
        }

        return false;
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

    public static string CleanTopicOrQueueName(string? raw, string defaultKind)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return defaultKind == "sns" ? "results-topic" : "sqs-queue";
        }

        var clean = raw.Trim().TrimStart('&', '*').Trim();

        // Handle string literals
        if (clean.StartsWith('"') && clean.EndsWith('"') && clean.Length >= 2)
        {
            clean = clean[1..^1];
        }

        // Handle URL: https://sqs.../queue_name
        if (clean.Contains('/'))
        {
            var lastSlash = clean.LastIndexOf('/');
            if (lastSlash >= 0 && lastSlash + 1 < clean.Length)
            {
                clean = clean[(lastSlash + 1)..];
            }
        }

        // Handle ARN: arn:aws:sns:region:account:topic-name
        if (clean.StartsWith("arn:aws:", StringComparison.OrdinalIgnoreCase))
        {
            var lastColon = clean.LastIndexOf(':');
            if (lastColon >= 0 && lastColon + 1 < clean.Length)
            {
                clean = clean[(lastColon + 1)..];
            }
        }

        // Strip method calls (e.g. .clone(), .to_string(), .as_str()) before splitting on field access dots
        if (clean.Contains('('))
        {
            clean = Regex.Replace(clean, @"\.[a-zA-Z0-9_]+\([^)]*\)", "");
        }

        // Strip field access prefixes: config.requests_queue_url -> requests_queue_url, self.publish.topic_arn -> topic_arn
        if (clean.Contains('.'))
        {
            clean = clean.Split('.').Last();
        }

        // Strip suffixes
        if (clean.EndsWith("_url", StringComparison.OrdinalIgnoreCase))
        {
            clean = clean[..^"_url".Length];
        }
        else if (clean.EndsWith("_arn", StringComparison.OrdinalIgnoreCase))
        {
            clean = clean[..^"_arn".Length];
        }
        else if (clean.EndsWith("-url", StringComparison.OrdinalIgnoreCase))
        {
            clean = clean[..^"-url".Length];
        }
        else if (clean.EndsWith("-arn", StringComparison.OrdinalIgnoreCase))
        {
            clean = clean[..^"-arn".Length];
        }

        clean = clean.Replace("(", "").Replace(")", "");
        clean = clean.Replace('_', '-').ToLowerInvariant().Trim('-');

        if (string.IsNullOrWhiteSpace(clean) || clean.Equals("queue", StringComparison.OrdinalIgnoreCase))
        {
            return "sqs-queue";
        }

        if (clean.Equals("topic", StringComparison.OrdinalIgnoreCase))
        {
            return "results-topic";
        }

        return clean;
    }
}
