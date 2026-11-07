using System.Collections.Frozen;

namespace CodeExplorer.Parser.Python.Libraries;

public class DiContainerPythonLibraryParser : DeclarativeSemanticExtension
{
    public DiContainerPythonLibraryParser()
    {
        Id = "di-container-python";
        Name = "Dependency Injector & Dishka";
        Type = SemanticExtensionType.Library;
        LibraryRole = LibraryRole.General;
        SupportedPatterns = ["dependency_injector*", "dishka*"];
        ServiceCallSinkRules =
        [
            new ServiceCallSinkRule
            {
                CallMethodNames = new[] { "Factory", "Singleton", "Resource", "Callable" }.ToFrozenSet(StringComparer.Ordinal),
                ReceiverSubstrings = new[] { "providers" }.ToFrozenSet(StringComparer.Ordinal),
                TargetRelation = OntologyConstants.Relationships.DependsOn,
                CollectAllIdentifierArguments = true
            }
        ];
    }
}
