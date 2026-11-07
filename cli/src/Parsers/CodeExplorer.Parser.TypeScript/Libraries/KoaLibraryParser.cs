using System.Collections.Frozen;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public class KoaLibraryParser : DeclarativeSemanticExtension
{
    public KoaLibraryParser()
    {
        Id = "koa";
        Name = "Koa";
        Type = SemanticExtensionType.Framework;
        LibraryRole = LibraryRole.WebService;
        SupportedPatterns = ["@koa/router", "koa-router", "koa"];
        InboundRouteCallRules =
        [
            new InboundRouteCallRule
            {
                MethodToHttpVerb = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["get"] = "GET",
                    ["post"] = "POST",
                    ["put"] = "PUT",
                    ["delete"] = "DELETE",
                    ["patch"] = "PATCH",
                    ["options"] = "OPTIONS",
                    ["head"] = "HEAD",
                    ["all"] = "ALL"
                }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase),
                ReceiverSubstrings = new[] { "router", "app" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                EmitEntryPointNode = true
            }
        ];
    }
}
