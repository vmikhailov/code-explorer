using System.Collections.Frozen;

namespace CodeExplorer.Parsers.Rust.Libraries;

public class PoemSalvoWarpLocoRustLibraryParser : DeclarativeSemanticExtension
{
    private const string ExtensionId = "poem-salvo-warp-loco";
    private const string ExtensionName = "Poem, Salvo, Warp, Loco & Dropshot";
    private const string MethodGet = "GET";
    private const string MethodPost = "POST";
    private const string MethodPut = "PUT";
    private const string MethodDelete = "DELETE";
    private const string MethodPatch = "PATCH";

    public PoemSalvoWarpLocoRustLibraryParser()
    {
        Id = ExtensionId;
        Name = ExtensionName;
        Type = SemanticExtensionType.Framework;
        LibraryRole = LibraryRole.WebService;
        SupportedPatterns =
        [
            "poem*",
            "salvo*",
            "warp*",
            "loco-rs*",
            "loco_rs*",
            "pavex*",
            "tide*",
            "gotham*",
            "cot*",
            "dropshot*"
        ];
        AnnotationEndpointRules =
        [
            new AnnotationEndpointRule
            {
                AnnotationToHttpMethod = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["handler"] = MethodGet,
                    ["endpoint"] = MethodGet,
                    ["api_operation"] = MethodGet
                }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase),
                PathArgumentKeys = new[] { "path", "uri", "value" }.ToFrozenSet(StringComparer.Ordinal)
            }
        ];
        InboundRouteCallRules =
        [
            new InboundRouteCallRule
            {
                EmitEntryPointNode = true,
                MethodToHttpVerb = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["at"] = MethodGet,
                    ["get"] = MethodGet,
                    ["post"] = MethodPost,
                    ["put"] = MethodPut,
                    ["delete"] = MethodDelete,
                    ["patch"] = MethodPatch,
                    ["path"] = MethodGet
                }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase),
                ReceiverSubstrings = new[]
                {
                    "Route", "Router", "warp", "app", "routes", "api"
                }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                PathArgumentIndex = 0
            }
        ];
    }
}
