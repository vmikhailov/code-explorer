using System.Collections.Frozen;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public class GraphQlClientTsLibraryParser : DeclarativeSemanticExtension
{
    public GraphQlClientTsLibraryParser()
    {
        Id = "graphql-client-ts";
        Name = "Apollo Client, urql & Relay";
        Type = SemanticExtensionType.ApiClient;
        LibraryRole = LibraryRole.General;
        SupportedPatterns =
        [
            "@apollo/client*",
            "urql*",
            "react-relay*"
        ];
        ServiceCallSinkRules =
        [
            new ServiceCallSinkRule
            {
                CallMethodNames = new[] { "useQuery", "useMutation", "useLazyQuery", "useSubscription" }.ToFrozenSet(StringComparer.Ordinal),
                TargetRelation = OntologyConstants.Relationships.Calls,
                StaticTargetName = "graphql:client-operation"
            }
        ];
    }
}
