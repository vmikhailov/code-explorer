using System.Collections.Frozen;

namespace CodeExplorer.Parser.CSharp.Libraries;

public class AzureSdkDotNetLibraryParser : DeclarativeSemanticExtension
{
    public AzureSdkDotNetLibraryParser()
    {
        Id = "azure-sdk-dotnet";
        Name = "Azure SDK for .NET (ServiceBus, CosmosDB, Blobs)";
        Type = SemanticExtensionType.Cloud;
        LibraryRole = LibraryRole.CloudSdk;
        SupportedPatterns =
        [
            "Azure.Messaging.ServiceBus*",
            "Azure.Messaging.EventHubs*",
            "Azure.Storage.Blobs*",
            "Microsoft.Azure.Cosmos*"
        ];
        MessagingBrokerRules =
        [
            new MessagingBrokerRule
            {
                Protocol = "servicebus",
                PublishMethodNames = new[] { "CreateSender", "SendMessageAsync", "SendMessagesAsync" }.ToFrozenSet(StringComparer.Ordinal),
                SubscribeMethodNames = new[] { "CreateProcessor", "CreateReceiver" }.ToFrozenSet(StringComparer.Ordinal),
                StaticPublishTopic = "azure-servicebus"
            }
        ];
        DataStoreAccessRules =
        [
            new DataStoreAccessRule
            {
                CallMethodNames = new[] { "GetContainer" }.ToFrozenSet(StringComparer.Ordinal),
                TableArgumentIndex = 1
            }
        ];
        ServiceCallSinkRules =
        [
            new ServiceCallSinkRule
            {
                CallMethodNames = new[] { "GetBlobClient", "GetBlobContainerClient", "UploadAsync" }.ToFrozenSet(StringComparer.Ordinal),
                TargetRelation = OntologyConstants.Relationships.DependsOn,
                StaticTargetName = "azure:blob-storage"
            }
        ];
    }
}
