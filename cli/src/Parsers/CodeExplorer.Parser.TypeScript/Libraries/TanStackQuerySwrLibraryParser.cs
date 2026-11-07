using System.Collections.Frozen;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public class TanStackQuerySwrLibraryParser : DeclarativeSemanticExtension
{
    public TanStackQuerySwrLibraryParser()
    {
        Id = "tanstack-query-swr";
        Name = "TanStack Query & SWR";
        Type = SemanticExtensionType.ApiClient;
        LibraryRole = LibraryRole.General;
        SupportedPatterns =
        [
            "@tanstack/react-query*",
            "@tanstack/vue-query*",
            "swr",
            "swr/*"
        ];
        ServiceCallSinkRules =
        [
            new ServiceCallSinkRule
            {
                CallMethodNames = new[] { "useSWR", "useSWRMutation", "useQuery", "useMutation" }.ToFrozenSet(StringComparer.Ordinal),
                TargetRelation = OntologyConstants.Relationships.Calls,
                RequireUrlOrPathArgument = true
            }
        ];
    }
}
