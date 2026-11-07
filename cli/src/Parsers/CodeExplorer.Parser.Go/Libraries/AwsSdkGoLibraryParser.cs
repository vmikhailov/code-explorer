using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.Go.Libraries;

public class AwsSdkGoLibraryParser : ISemanticExtension
{
    private const string AwsS3Target = "aws:s3";
    private const string AwsSqsTarget = "aws:sqs";
    private const string AwsDynamoDbTarget = "aws:dynamodb";

    public string Name => "AWS SDK for Go v2";
    public string Id => "aws-sdk-go";
    public SemanticExtensionType Type => SemanticExtensionType.Cloud;
    public IReadOnlyList<string> SupportedPatterns =>
    [
        "github.com/aws/aws-sdk-go-v2*",
        "github.com/aws/aws-sdk-go*"
    ];
    public bool IsImplemented => true;
    public LibraryRole LibraryRole => LibraryRole.CloudSdk;

    public string? MapNodeType(Node node, ParsingContext ctx) => null;
    public string? ExtractIdentifier(Node node, ParsingContext ctx) => null;

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (!node.Is(TreeSitterSyntax.Go.CallExpression)) return;

        var func = node.GetFunctionNode();
        if (!func.IsValid()) return;

        var funcText = func.Text;
        if (funcText.EndsWith(".PutObject", StringComparison.Ordinal) ||
            funcText.EndsWith(".GetObject", StringComparison.Ordinal))
        {
            references.Add(new Reference(scopeSymbolId, AwsS3Target, OntologyConstants.Relationships.DependsOn));
        }
        else if (funcText.EndsWith(".SendMessage", StringComparison.Ordinal))
        {
            references.Add(new Reference(scopeSymbolId, AwsSqsTarget, OntologyConstants.Relationships.PublishesTo));
        }
        else if (funcText.EndsWith(".ReceiveMessage", StringComparison.Ordinal))
        {
            references.Add(new Reference(scopeSymbolId, AwsSqsTarget, OntologyConstants.Relationships.SubscribesTo));
        }
        else if (funcText.EndsWith(".PutItem", StringComparison.Ordinal) ||
                 funcText.EndsWith(".GetItem", StringComparison.Ordinal))
        {
            references.Add(new Reference(scopeSymbolId, AwsDynamoDbTarget, OntologyConstants.Relationships.UsesDb));
        }
    }
}
