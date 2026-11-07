using System.Collections.Frozen;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public class MetaFrameworkRoutesLibraryParser : DeclarativeSemanticExtension
{
    public MetaFrameworkRoutesLibraryParser()
    {
        Id = "meta-framework-routes";
        Name = "SvelteKit, Astro & Nitro/H3 Server Routes";
        Type = SemanticExtensionType.Framework;
        LibraryRole = LibraryRole.WebService;
        SupportedPatterns =
        [
            "@sveltejs/kit*",
            "astro*",
            "h3",
            "h3/*",
            "nitropack*"
        ];
        InboundRouteCallRules =
        [
            new InboundRouteCallRule
            {
                MethodToHttpVerb = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["defineEventHandler"] = "ALL",
                    ["eventHandler"] = "ALL"
                }.ToFrozenDictionary(StringComparer.Ordinal),
                StaticRoute = "/api/nitro-handler"
            }
        ];
    }
}
