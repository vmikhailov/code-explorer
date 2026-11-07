using System.Collections.Frozen;

namespace CodeExplorer.Parser.Go.Libraries;

public sealed class AwsSdkGoLibraryParser : DeclarativeSemanticExtension
{
    private const string AwsS3Target = "aws:s3";
    private const string AwsSqsTarget = "aws:sqs";
    private const string AwsDynamoDbTarget = "aws:dynamodb";

    public AwsSdkGoLibraryParser()
    {
        Id = "aws-sdk-go";
        Name = "AWS SDK for Go v2";
        Type = SemanticExtensionType.Cloud;
        LibraryRole = LibraryRole.CloudSdk;
        SupportedPatterns =
        [
            "github.com/aws/aws-sdk-go-v2*",
            "github.com/aws/aws-sdk-go*"
        ];
        ServiceCallSinkRules =
        [
            new ServiceCallSinkRule
            {
                CallMethodNames = new[] { "PutObject", "GetObject" }.ToFrozenSet(),
                TargetRelation = OntologyConstants.Relationships.DependsOn,
                StaticTargetName = AwsS3Target
            },
            new ServiceCallSinkRule
            {
                CallMethodNames = new[] { "SendMessage" }.ToFrozenSet(),
                TargetRelation = OntologyConstants.Relationships.PublishesTo,
                StaticTargetName = AwsSqsTarget
            },
            new ServiceCallSinkRule
            {
                CallMethodNames = new[] { "ReceiveMessage" }.ToFrozenSet(),
                TargetRelation = OntologyConstants.Relationships.SubscribesTo,
                StaticTargetName = AwsSqsTarget
            }
        ];
        DataStoreAccessRules =
        [
            new DataStoreAccessRule
            {
                CallMethodNames = new[] { "PutItem", "GetItem" }.ToFrozenSet(),
                StaticTargetName = AwsDynamoDbTarget
            }
        ];
    }
}
