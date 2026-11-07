using System.Collections.Frozen;

namespace CodeExplorer.Parser.CSharp.Libraries;

public sealed class AwsSdkDotNetLibraryParser : DeclarativeSemanticExtension
{
    private const string AwsS3Target = "aws:s3";
    private const string AwsSqsTarget = "aws:sqs";
    private const string AwsSnsTarget = "aws:sns";
    private const string AwsDynamoDbTarget = "aws:dynamodb";

    public AwsSdkDotNetLibraryParser()
    {
        Id = "aws-sdk-dotnet";
        Name = "AWS SDK for .NET";
        Type = SemanticExtensionType.Cloud;
        LibraryRole = LibraryRole.CloudSdk;
        SupportedPatterns =
        [
            "AWSSDK.*",
            "Amazon.S3*",
            "Amazon.SQS*",
            "Amazon.SimpleNotificationService*",
            "Amazon.DynamoDBv2*"
        ];
        ServiceCallSinkRules =
        [
            new ServiceCallSinkRule
            {
                CallMethodNames = new[]
                {
                    "PutObjectAsync", "GetObjectAsync", "DeleteObjectAsync", "ListObjectsV2Async"
                }.ToFrozenSet(),
                TargetRelation = OntologyConstants.Relationships.DependsOn,
                StaticTargetName = AwsS3Target
            },
            new ServiceCallSinkRule
            {
                CallMethodNames = new[]
                {
                    "SendMessageAsync", "SendMessageBatchAsync"
                }.ToFrozenSet(),
                TargetRelation = OntologyConstants.Relationships.PublishesTo,
                StaticTargetName = AwsSqsTarget
            },
            new ServiceCallSinkRule
            {
                CallMethodNames = new[] { "ReceiveMessageAsync" }.ToFrozenSet(),
                TargetRelation = OntologyConstants.Relationships.SubscribesTo,
                StaticTargetName = AwsSqsTarget
            },
            new ServiceCallSinkRule
            {
                CallMethodNames = new[] { "PublishAsync", "PublishBatchAsync" }.ToFrozenSet(),
                ReceiverSubstrings = new[] { "sns" }.ToFrozenSet(),
                TargetRelation = OntologyConstants.Relationships.PublishesTo,
                StaticTargetName = AwsSnsTarget
            }
        ];
        DataStoreAccessRules =
        [
            new DataStoreAccessRule
            {
                CallMethodNames = new[]
                {
                    "PutItemAsync", "GetItemAsync", "UpdateItemAsync",
                    "DeleteItemAsync", "QueryAsync", "ScanAsync"
                }.ToFrozenSet(),
                StaticTargetName = AwsDynamoDbTarget
            }
        ];
    }
}
