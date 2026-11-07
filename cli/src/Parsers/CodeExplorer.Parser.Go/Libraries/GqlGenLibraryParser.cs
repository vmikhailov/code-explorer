using System.Collections.Frozen;

namespace CodeExplorer.Parser.Go.Libraries;

public class GqlGenLibraryParser : DeclarativeSemanticExtension
{
    public GqlGenLibraryParser()
    {
        Id = "gqlgen";
        Name = "gqlgen";
        Type = SemanticExtensionType.Framework;
        LibraryRole = LibraryRole.WebService;
        SupportedPatterns = ["github.com/99designs/gqlgen*"];
        InboundRouteCallRules =
        [
            new InboundRouteCallRule
            {
                MethodToHttpVerb = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["NewDefaultServer"] = "",
                    ["New"] = ""
                }.ToFrozenDictionary(StringComparer.Ordinal),
                ReceiverSubstrings = new[] { "handler" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                StaticRoute = "GRAPHQL:/graphql",
                EmitEntryPointNode = true
            },
            new InboundRouteCallRule
            {
                MethodToHttpVerb = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["Handler"] = ""
                }.ToFrozenDictionary(StringComparer.Ordinal),
                ReceiverSubstrings = new[] { "playground" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                StaticRoute = "GRAPHQL:/graphql",
                EmitEntryPointNode = true
            }
        ];
    }
}
