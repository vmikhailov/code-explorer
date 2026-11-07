using System.Collections.Frozen;

namespace CodeExplorer.Parser.Go.Libraries;

public sealed class EntGoLibraryParser : DeclarativeSemanticExtension
{
    public EntGoLibraryParser()
    {
        Id = "ent-go";
        Name = "Ent (Go ORM)";
        Type = SemanticExtensionType.RelationalDb;
        LibraryRole = LibraryRole.OrmOrDatabase;
        SupportedPatterns = ["entgo.io/ent*"];
        ServiceCallSinkRules =
        [
            new ServiceCallSinkRule
            {
                CallMethodNames = new[]
                {
                    "All", "First", "Only", "Count", "Save", "SaveX", "Exec", "ExecX"
                }.ToFrozenSet(),
                ReceiverSubstrings = new[] { ".Create(", ".Query(", ".Update(", ".Delete(" }.ToFrozenSet(),
                TargetRelation = OntologyConstants.Relationships.UsesDb,
                TargetPrefix = "ent:",
                UseMethodNameAsTarget = true,
                LowercaseMethodNameTarget = true
            }
        ];
    }
}
