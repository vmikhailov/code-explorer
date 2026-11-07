using System.Collections.Frozen;

namespace CodeExplorer.Parser.Go.Libraries;

public class MachineryRiverGoLibraryParser : DeclarativeSemanticExtension
{
    public MachineryRiverGoLibraryParser()
    {
        Id = "machinery-river-go";
        Name = "Machinery & River Queue";
        Type = SemanticExtensionType.Queue;
        LibraryRole = LibraryRole.WorkerService;
        SupportedPatterns =
        [
            "github.com/RichardKnop/machinery*",
            "github.com/riverqueue/river*"
        ];
        MessagingBrokerRules =
        [
            new MessagingBrokerRule
            {
                Protocol = "machinery",
                PublishMethodNames = new[] { "SendTask", "SendTaskWithContext", "Insert", "InsertTx" }.ToFrozenSet(StringComparer.Ordinal),
                PublishReceiverSubstrings = new[] { "server", "client", "river" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                SubscribeMethodNames = new[] { "RegisterTask", "RegisterTasks", "AddWorker" }.ToFrozenSet(StringComparer.Ordinal),
                SubscribeReceiverSubstrings = new[] { "server", "workers", "river" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                FallbackTopicName = "machinery-task"
            }
        ];
    }
}
