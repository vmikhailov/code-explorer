using System.Collections.Frozen;

namespace CodeExplorer.Parser.Go.Libraries;

public class UberFxWireGoLibraryParser : DeclarativeSemanticExtension
{
    public UberFxWireGoLibraryParser()
    {
        Id = "uber-fx-wire-go";
        Name = "Uber Fx & Google Wire";
        Type = SemanticExtensionType.Library;
        LibraryRole = LibraryRole.General;
        SupportedPatterns =
        [
            "go.uber.org/fx*",
            "github.com/google/wire*"
        ];
        ServiceCallSinkRules =
        [
            new ServiceCallSinkRule
            {
                CallMethodNames = new[] { "Provide", "Invoke", "Build", "NewSet" }.ToFrozenSet(StringComparer.Ordinal),
                ReceiverSubstrings = new[] { "fx", "wire" }.ToFrozenSet(StringComparer.Ordinal),
                TargetRelation = OntologyConstants.Relationships.DependsOn,
                CollectAllIdentifierArguments = true
            }
        ];
    }
}
