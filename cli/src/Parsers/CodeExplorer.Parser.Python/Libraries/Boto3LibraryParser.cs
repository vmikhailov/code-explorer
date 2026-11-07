using System.Collections.Frozen;

namespace CodeExplorer.Parser.Python.Libraries;

public sealed class Boto3LibraryParser : DeclarativeSemanticExtension
{
    private const string AwsS3Target = "aws:s3";
    private const string AwsSqsTarget = "aws:sqs";
    private const string AwsDynamoDbTarget = "aws:dynamodb";

    public Boto3LibraryParser()
    {
        Id = "boto3";
        Name = "Boto3 (AWS SDK for Python)";
        Type = SemanticExtensionType.Cloud;
        LibraryRole = LibraryRole.CloudSdk;
        SupportedPatterns = ["boto3", "boto3.*", "aioboto3", "aioboto3.*", "botocore", "botocore.*"];
        ServiceCallSinkRules =
        [
            new ServiceCallSinkRule
            {
                CallMethodNames = new[] { "client", "resource" }.ToFrozenSet(),
                ReceiverSubstrings = new[] { "boto" }.ToFrozenSet(),
                TargetPrefix = "aws:",
                TargetRelation = OntologyConstants.Relationships.DependsOn
            },
            new ServiceCallSinkRule
            {
                CallMethodNames = new[]
                {
                    "put_object", "get_object", "delete_object", "upload_file", "download_file"
                }.ToFrozenSet(),
                TargetRelation = OntologyConstants.Relationships.DependsOn,
                StaticTargetName = AwsS3Target
            },
            new ServiceCallSinkRule
            {
                CallMethodNames = new[] { "send_message", "send_message_batch" }.ToFrozenSet(),
                TargetRelation = OntologyConstants.Relationships.PublishesTo,
                StaticTargetName = AwsSqsTarget
            },
            new ServiceCallSinkRule
            {
                CallMethodNames = new[] { "receive_message" }.ToFrozenSet(),
                TargetRelation = OntologyConstants.Relationships.SubscribesTo,
                StaticTargetName = AwsSqsTarget
            }
        ];
        DataStoreAccessRules =
        [
            new DataStoreAccessRule
            {
                CallMethodNames = new[]
                {
                    "put_item", "get_item", "update_item", "delete_item", "query", "scan"
                }.ToFrozenSet(),
                ReceiverSubstrings = new[] { "dynamo", "table" }.ToFrozenSet(),
                StaticTargetName = AwsDynamoDbTarget
            }
        ];
    }
}
