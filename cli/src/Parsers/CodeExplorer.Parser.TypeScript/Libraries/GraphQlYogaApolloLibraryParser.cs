using System.Collections.Frozen;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public class GraphQlYogaApolloLibraryParser : DeclarativeSemanticExtension
{
    public GraphQlYogaApolloLibraryParser()
    {
        Id = "graphql-yoga-apollo";
        Name = "GraphQL Yoga & Apollo Server";
        Type = SemanticExtensionType.Framework;
        LibraryRole = LibraryRole.WebService;
        SupportedPatterns = ["graphql-yoga", "@apollo/server*", "apollo-server*", "type-graphql"];
        InboundRouteCallRules =
        [
            new InboundRouteCallRule
            {
                MethodToHttpVerb = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["createYoga"] = "",
                    ["createSchema"] = "",
                    ["ApolloServer"] = ""
                }.ToFrozenDictionary(StringComparer.Ordinal),
                StaticRoute = "GRAPHQL:/graphql",
                EmitEntryPointNode = true
            }
        ];
    }
}
