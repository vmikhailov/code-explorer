using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public class AwsSdkLibraryParser : ISemanticExtension
{
    public string Type => OntologyConstants.LibraryTypes.Cloud;
    public string Name => "AWS";
    public string Id => "aws";
    public IReadOnlyList<string> SupportedPatterns => ["aws-sdk", "@aws-sdk/*"];
    public bool IsImplemented => true;
    public LibraryRole LibraryRole => LibraryRole.CloudSdk;

    private static readonly HashSet<string> DynamoDbCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "PutItemCommand", "GetItemCommand", "UpdateItemCommand", "DeleteItemCommand",
        "QueryCommand", "ScanCommand", "BatchGetItemCommand", "BatchWriteItemCommand",
        "PutCommand", "GetCommand", "UpdateCommand", "DeleteCommand"
    };

    private static readonly HashSet<string> DynamoDbMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "putItem", "getItem", "updateItem", "deleteItem"
    };

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (IsDynamoDbCall(node))
        {
            return OntologyConstants.NodeLabels.Query;
        }
        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx)
    {
        if (IsDynamoDbCall(node))
        {
            var table = ExtractDynamoTable(node);
            if (!string.IsNullOrEmpty(table))
            {
                return $"DynamoDB: {table}";
            }
            return "DynamoDB Query";
        }
        return null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        // 1. SQS queue references
        var queueUrl = ExtractQueueUrl(node, out var isReceive);
        var queue = CleanQueueUrl(queueUrl);
        if (!string.IsNullOrEmpty(queue) && WorkspaceConventions.IsValidTopicOrQueueName(queue))
        {
            var rel = isReceive ? OntologyConstants.Relationships.SubscribesTo : OntologyConstants.Relationships.PublishesTo;
            references.Add(new Reference(scopeSymbolId, queue, rel));
        }

        // 2. S3 bucket references
        var bucket = ExtractBucketName(node);
        if (!string.IsNullOrEmpty(bucket))
        {
            references.Add(new Reference(scopeSymbolId, bucket, OntologyConstants.Relationships.DependsOn));
        }

        // 3. DynamoDB table references
        var table = ExtractDynamoTable(node);
        if (!string.IsNullOrEmpty(table))
        {
            references.Add(new Reference(scopeSymbolId, table, OntologyConstants.Relationships.DependsOn));
            references.Add(new Reference(scopeSymbolId, table, OntologyConstants.Relationships.UsesDb));
        }
    }

    private static bool IsDynamoDbCall(Node node)
    {
        return !string.IsNullOrEmpty(ExtractDynamoTable(node));
    }

    private static string? ExtractDynamoTable(Node node)
    {
        if (node.Is(TreeSitterSyntax.TypeScript.NewExpression))
        {
            var ctorName = GetConstructorName(node);
            if (!string.IsNullOrEmpty(ctorName) && DynamoDbCommands.Contains(ctorName))
            {
                return ExtractStringPropertyFromArgs(node, "TableName");
            }
        }
        else if (node.Is(TreeSitterSyntax.TypeScript.CallExpression))
        {
            if (AstHelper.TryGetMemberAccess(node, out _, out var propName))
            {
                if (propName != null && DynamoDbMethods.Contains(propName))
                {
                    return ExtractStringPropertyFromArgs(node, "TableName");
                }
            }
        }

        return null;
    }

    private static string? ExtractBucketName(Node node)
    {
        if (node.Is(TreeSitterSyntax.TypeScript.NewExpression))
        {
            var ctorName = GetConstructorName(node);
            if (!string.IsNullOrEmpty(ctorName) && ctorName.Contains("ObjectCommand", StringComparison.OrdinalIgnoreCase))
            {
                return ExtractStringPropertyFromArgs(node, "Bucket");
            }
        }
        else if (node.Is(TreeSitterSyntax.TypeScript.CallExpression))
        {
            if (AstHelper.TryGetMemberAccess(node, out _, out var propName))
            {
                if (propName != null && propName.Contains("object", StringComparison.OrdinalIgnoreCase))
                {
                    return ExtractStringPropertyFromArgs(node, "Bucket");
                }
            }
        }

        return null;
    }

    private static string? ExtractQueueUrl(Node node, out bool isReceive)
    {
        isReceive = false;

        if (node.Is(TreeSitterSyntax.TypeScript.NewExpression))
        {
            var ctorName = GetConstructorName(node);
            if (!string.IsNullOrEmpty(ctorName))
            {
                if (ctorName.Contains("ReceiveMessage", StringComparison.OrdinalIgnoreCase))
                {
                    isReceive = true;
                    return ExtractStringPropertyFromArgs(node, "QueueUrl");
                }
                if (ctorName.Contains("SendMessage", StringComparison.OrdinalIgnoreCase))
                {
                    return ExtractStringPropertyFromArgs(node, "QueueUrl");
                }
            }
        }
        else if (node.Is(TreeSitterSyntax.TypeScript.CallExpression))
        {
            if (AstHelper.TryGetMemberAccess(node, out _, out var propName))
            {
                if (propName != null)
                {
                    if (propName.Contains("receiveMessage", StringComparison.OrdinalIgnoreCase))
                    {
                        isReceive = true;
                        return ExtractStringPropertyFromArgs(node, "QueueUrl");
                    }
                    if (propName.Contains("sendMessage", StringComparison.OrdinalIgnoreCase))
                    {
                        return ExtractStringPropertyFromArgs(node, "QueueUrl");
                    }
                }
            }
        }

        return null;
    }

    private static string? CleanQueueUrl(string? rawUrl)
    {
        if (string.IsNullOrEmpty(rawUrl)) return null;
        var trimmed = rawUrl.Trim('\'', '"', '`', ' ');
        if (trimmed.Contains('/'))
        {
            var slashIdx = trimmed.LastIndexOf('/');
            if (slashIdx >= 0 && slashIdx < trimmed.Length - 1)
            {
                return trimmed[(slashIdx + 1)..].Trim();
            }
        }
        return trimmed;
    }

    private static string? GetConstructorName(Node node)
    {
        var ctor = node.GetField(TreeSitterSyntax.Fields.Constructor) ??
                   node.Children.FirstOrDefault(c => c.IsValid() && c.Type != "new");
        if (ctor.IsValid())
        {
            var text = ctor.Text;
            var dotIdx = text.LastIndexOf('.');
            return dotIdx >= 0 ? text[(dotIdx + 1)..] : text;
        }
        return null;
    }

    private static string? ExtractStringPropertyFromArgs(Node node, string propertyName)
    {
        var args = AstHelper.GetCallArguments(node);
        if (args.Count == 0)
        {
            var argList = node.FindChildOfType(TreeSitterSyntax.TypeScript.Arguments);
            if (argList.IsValid())
            {
                foreach (var child in argList.Children)
                {
                    if (child.IsValid() && !string.IsNullOrEmpty(child.Type) && (char.IsLetter(child.Type[0]) || child.Type[0] == '_'))
                    {
                        args.Add(child);
                    }
                }
            }
        }

        if (args.Count > 0)
        {
            var firstArg = args[0];
            if (firstArg.Is(TreeSitterSyntax.TypeScript.Object))
            {
                if (AstHelper.TryGetObjectProperty(firstArg, propertyName, out var valNode) && valNode.IsValid())
                {
                    if (AstHelper.IsStringLiteralNode(valNode))
                    {
                        return valNode.Text.Trim('\'', '"', '`');
                    }
                }
            }
        }

        return null;
    }
}
