using System.Collections.Frozen;

namespace CodeExplorer.Parsers.TypeScript.Libraries;

public class ElysiaH3NitroAdonisTsLibraryParser : DeclarativeSemanticExtension
{
    private const string ExtensionId = "elysia-h3-nitro-adonis";
    private const string ExtensionName = "Elysia, UnJS H3/Nitro, AdonisJS, FeathersJS, LoopBack, Ts.ED, TSOA & ts-rest";
    private const string MethodGet = "GET";
    private const string MethodPost = "POST";
    private const string MethodPut = "PUT";
    private const string MethodDelete = "DELETE";
    private const string MethodPatch = "PATCH";

    public ElysiaH3NitroAdonisTsLibraryParser()
    {
        Id = ExtensionId;
        Name = ExtensionName;
        Type = SemanticExtensionType.Framework;
        LibraryRole = LibraryRole.WebService;
        SupportedPatterns =
        [
            "elysia*",
            "@elysiajs/*",
            "h3*",
            "nitropack*",
            "@adonisjs/*",
            "@feathersjs/*",
            "@loopback/*",
            "@tsed/*",
            "tsoa*",
            "@ts-rest/*"
        ];
        AnnotationEndpointRules =
        [
            new AnnotationEndpointRule
            {
                AnnotationToHttpMethod = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Get"] = MethodGet,
                    ["Post"] = MethodPost,
                    ["Put"] = MethodPut,
                    ["Delete"] = MethodDelete,
                    ["Patch"] = MethodPatch
                }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase),
                RoutePrefixAnnotationNames = new[] { "Route", "Controller" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase)
            }
        ];
        InboundRouteCallRules =
        [
            new InboundRouteCallRule
            {
                EmitEntryPointNode = true,
                MethodToHttpVerb = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["get"] = MethodGet,
                    ["post"] = MethodPost,
                    ["put"] = MethodPut,
                    ["delete"] = MethodDelete,
                    ["patch"] = MethodPatch,
                    ["defineEventHandler"] = MethodGet,
                    ["eventHandler"] = MethodGet
                }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase),
                ReceiverSubstrings = new[]
                {
                    "app", "router", "Route", "elysia", "h3"
                }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                PathArgumentIndex = 0
            }
        ];
    }
}
