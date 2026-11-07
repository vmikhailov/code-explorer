using System.Collections.Frozen;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public class HonoLibraryParser : DeclarativeSemanticExtension
{
    public HonoLibraryParser()
    {
        Id = "hono";
        Name = "Hono";
        Type = SemanticExtensionType.Framework;
        LibraryRole = LibraryRole.WebService;
        SupportedPatterns = ["hono", "hono/*", "@hono/*"];
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
                    ["all"] = "ALL",
                    ["on"] = "ON"
                }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase),
                ExcludedReceivers = new[] { "axios", "http", "https", "fetch" }.ToFrozenSet(StringComparer.Ordinal),
                EmitEntryPointNode = true,
                RequireLeadingSlashOnPath = true,
                EmitHandlerTriggers = true
            }
        ];
    }
}
