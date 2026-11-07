using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.CSharp.Libraries;

public class AwsSdkDotNetLibraryParser : ISemanticExtension
{
    private const string AwsS3Target = "aws:s3";
    private const string AwsSqsTarget = "aws:sqs";
    private const string AwsSnsTarget = "aws:sns";
    private const string AwsDynamoDbTarget = "aws:dynamodb";

    public string Name => "AWS SDK for .NET";
    public string Id => "aws-sdk-dotnet";
    public SemanticExtensionType Type => SemanticExtensionType.Cloud;
    public IReadOnlyList<string> SupportedPatterns =>
    [
        "AWSSDK.*",
        "Amazon.S3*",
        "Amazon.SQS*",
        "Amazon.SimpleNotificationService*",
        "Amazon.DynamoDBv2*"
    ];
    public bool IsImplemented => true;
    public LibraryRole LibraryRole => LibraryRole.CloudSdk;

    private static readonly HashSet<string> S3Methods = new(StringComparer.Ordinal)
    {
        "PutObjectAsync", "GetObjectAsync", "DeleteObjectAsync", "ListObjectsV2Async"
    };

    private static readonly HashSet<string> SqsPublishMethods = new(StringComparer.Ordinal)
    {
        "SendMessageAsync", "SendMessageBatchAsync"
    };

    private static readonly HashSet<string> SqsSubscribeMethods = new(StringComparer.Ordinal)
    {
        "ReceiveMessageAsync"
    };

    private static readonly HashSet<string> SnsMethods = new(StringComparer.Ordinal)
    {
        "PublishAsync", "PublishBatchAsync"
    };

    private static readonly HashSet<string> DynamoMethods = new(StringComparer.Ordinal)
    {
        "PutItemAsync", "GetItemAsync", "UpdateItemAsync", "DeleteItemAsync", "QueryAsync", "ScanAsync"
    };

    public string? MapNodeType(Node node, ParsingContext ctx) => null;
    public string? ExtractIdentifier(Node node, ParsingContext ctx) => null;

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (!node.Is(TreeSitterSyntax.CSharp.InvocationExpression)) return;

        var func = node.GetFunctionNode();
        if (!func.IsValid() || !func.Is(TreeSitterSyntax.CSharp.MemberAccessExpression)) return;

        var exprNode = func.GetField(TreeSitterSyntax.Fields.Expression) ?? (func.Children.Count > 0 ? func.Children[0] : default);
        var nameNode = func.GetField(TreeSitterSyntax.Fields.Name) ?? (func.Children.Count > 0 ? func.Children[^1] : default);
        if (!nameNode.IsValid()) return;

        var method = nameNode.Text;
        var receiver = exprNode.IsValid() ? exprNode.Text : string.Empty;

        if (S3Methods.Contains(method))
        {
            references.Add(new Reference(scopeSymbolId, AwsS3Target, OntologyConstants.Relationships.DependsOn));
        }
        else if (SqsPublishMethods.Contains(method))
        {
            references.Add(new Reference(scopeSymbolId, AwsSqsTarget, OntologyConstants.Relationships.PublishesTo));
        }
        else if (SqsSubscribeMethods.Contains(method))
        {
            references.Add(new Reference(scopeSymbolId, AwsSqsTarget, OntologyConstants.Relationships.SubscribesTo));
        }
        else if (SnsMethods.Contains(method) && receiver.Contains("sns", StringComparison.OrdinalIgnoreCase))
        {
            references.Add(new Reference(scopeSymbolId, AwsSnsTarget, OntologyConstants.Relationships.PublishesTo));
        }
        else if (DynamoMethods.Contains(method))
        {
            references.Add(new Reference(scopeSymbolId, AwsDynamoDbTarget, OntologyConstants.Relationships.UsesDb));
        }
    }
}
