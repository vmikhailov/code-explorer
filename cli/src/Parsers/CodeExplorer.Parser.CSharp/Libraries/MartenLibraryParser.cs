using System.Collections.Frozen;

namespace CodeExplorer.Parser.CSharp.Libraries;

public sealed class MartenLibraryParser : DeclarativeSemanticExtension
{
    private const string MartenDbTarget = "marten:postgres";
    private const string MartenEventPrefix = "marten-events:";

    public MartenLibraryParser()
    {
        Id = "marten";
        Name = "Marten";
        Type = SemanticExtensionType.DocumentDb;
        LibraryRole = LibraryRole.OrmOrDatabase;
        SupportedPatterns = ["Marten", "Marten.*"];
        DataStoreAccessRules =
        [
            new DataStoreAccessRule
            {
                CallMethodNames = new[]
                {
                    "Store", "Insert", "Update", "Delete", "LoadAsync", "Query", "SaveChangesAsync",
                    "Append", "StartStream", "FetchStreamAsync", "AggregateStreamAsync"
                }.ToFrozenSet(),
                ReceiverSubstrings = new[] { "session", "store", "Events" }.ToFrozenSet(),
                StaticTargetName = MartenDbTarget
            }
        ];
        ServiceCallSinkRules =
        [
            new ServiceCallSinkRule
            {
                CallMethodNames = new[]
                {
                    "Append", "StartStream", "FetchStreamAsync", "AggregateStreamAsync"
                }.ToFrozenSet(),
                ReceiverSubstrings = new[] { "Events" }.ToFrozenSet(),
                TargetRelation = OntologyConstants.Relationships.PublishesTo,
                TargetPrefix = MartenEventPrefix,
                UseMethodNameAsTarget = true
            }
        ];
    }
}
