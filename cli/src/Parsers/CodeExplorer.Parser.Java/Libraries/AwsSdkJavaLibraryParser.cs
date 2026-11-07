using System.Collections.Frozen;

namespace CodeExplorer.Parser.Java.Libraries;

public class AwsSdkJavaLibraryParser : DeclarativeSemanticExtension
{
    public AwsSdkJavaLibraryParser()
    {
        Id = "aws-sdk-java";
        Name = "AWS SDK for Java v2";
        Type = SemanticExtensionType.Cloud;
        LibraryRole = LibraryRole.CloudSdk;
        SupportedPatterns = ["software.amazon.awssdk.*", "com.amazonaws.*"];
        MessagingBrokerRules =
        [
            new MessagingBrokerRule
            {
                Protocol = "aws",
                PublishMethodNames = new[] { "sendMessage", "sendMessageBatch", "publish", "publishBatch", "putEvents" }.ToFrozenSet(StringComparer.Ordinal),
                PublishReceiverSubstrings = new[] { "sqs", "sns", "eventBridge" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                SubscribeMethodNames = new[] { "receiveMessage" }.ToFrozenSet(StringComparer.Ordinal),
                SubscribeReceiverSubstrings = new[] { "sqs" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                FallbackTopicName = "aws-messaging"
            }
        ];
    }
}
