using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.Python.Libraries;

public class Boto3LibraryParser : ISemanticExtension
{
    private const string ServiceNameArg = "service_name";
    private const string AwsS3Target = "aws:s3";
    private const string AwsSqsTarget = "aws:sqs";
    private const string AwsDynamoDbTarget = "aws:dynamodb";

    public string Name => "Boto3 (AWS SDK for Python)";
    public string Id => "boto3";
    public SemanticExtensionType Type => SemanticExtensionType.Cloud;
    public IReadOnlyList<string> SupportedPatterns => ["boto3", "boto3.*", "aioboto3", "aioboto3.*", "botocore", "botocore.*"];
    public bool IsImplemented => true;
    public LibraryRole LibraryRole => LibraryRole.CloudSdk;

    private static readonly HashSet<string> S3Methods = new(StringComparer.Ordinal)
    {
        "put_object", "get_object", "delete_object", "upload_file", "download_file"
    };

    private static readonly HashSet<string> SqsPublishMethods = new(StringComparer.Ordinal)
    {
        "send_message", "send_message_batch"
    };

    private static readonly HashSet<string> SqsSubscribeMethods = new(StringComparer.Ordinal)
    {
        "receive_message"
    };

    private static readonly HashSet<string> DynamoMethods = new(StringComparer.Ordinal)
    {
        "put_item", "get_item", "update_item", "delete_item", "query", "scan"
    };

    public string? MapNodeType(Node node, ParsingContext ctx) => null;
    public string? ExtractIdentifier(Node node, ParsingContext ctx) => null;

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (!node.Is(TreeSitterSyntax.Python.Call)) return;

        if (!PythonAstHelper.TryGetMemberAccess(node, out var objNode, out var methodName) ||
            string.IsNullOrEmpty(methodName))
        {
            return;
        }

        if (methodName is "client" or "resource" && objNode.IsValid() && objNode.Text.Contains("boto", StringComparison.OrdinalIgnoreCase))
        {
            var svc = PythonAstHelper.ResolveCallArgument(node, 0, ServiceNameArg);
            if (svc == "s3")
            {
                references.Add(new Reference(scopeSymbolId, AwsS3Target, OntologyConstants.Relationships.DependsOn));
            }
            else if (svc == "dynamodb")
            {
                references.Add(new Reference(scopeSymbolId, AwsDynamoDbTarget, OntologyConstants.Relationships.UsesDb));
            }
            return;
        }

        if (S3Methods.Contains(methodName))
        {
            references.Add(new Reference(scopeSymbolId, AwsS3Target, OntologyConstants.Relationships.DependsOn));
        }
        else if (SqsPublishMethods.Contains(methodName))
        {
            references.Add(new Reference(scopeSymbolId, AwsSqsTarget, OntologyConstants.Relationships.PublishesTo));
        }
        else if (SqsSubscribeMethods.Contains(methodName))
        {
            references.Add(new Reference(scopeSymbolId, AwsSqsTarget, OntologyConstants.Relationships.SubscribesTo));
        }
        else if (DynamoMethods.Contains(methodName) &&
                 objNode.IsValid() &&
                 (objNode.Text.Contains("dynamo", StringComparison.OrdinalIgnoreCase) ||
                  objNode.Text.Contains("table", StringComparison.OrdinalIgnoreCase)))
        {
            references.Add(new Reference(scopeSymbolId, AwsDynamoDbTarget, OntologyConstants.Relationships.UsesDb));
        }
    }
}
