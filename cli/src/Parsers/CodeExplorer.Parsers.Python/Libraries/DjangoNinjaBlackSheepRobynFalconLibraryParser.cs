using System.Collections.Frozen;

namespace CodeExplorer.Parsers.Python.Libraries;

public class DjangoNinjaBlackSheepRobynFalconLibraryParser : DeclarativeSemanticExtension
{
    private const string ExtensionId = "django-ninja-blacksheep-robyn-falcon";
    private const string ExtensionName = "Django-Ninja, BlackSheep, Robyn, Quart, Falcon, Sanic & Tornado";
    private const string MethodGet = "GET";
    private const string MethodPost = "POST";
    private const string MethodPut = "PUT";
    private const string MethodDelete = "DELETE";
    private const string MethodPatch = "PATCH";

    public DjangoNinjaBlackSheepRobynFalconLibraryParser()
    {
        Id = ExtensionId;
        Name = ExtensionName;
        Type = SemanticExtensionType.Framework;
        LibraryRole = LibraryRole.WebService;
        SupportedPatterns =
        [
            "django-ninja*",
            "ninja*",
            "blacksheep*",
            "robyn*",
            "quart*",
            "falcon*",
            "sanic*",
            "tornado*",
            "bottle*"
        ];
        AnnotationEndpointRules =
        [
            new AnnotationEndpointRule
            {
                AnnotationToHttpMethod = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["get"] = MethodGet,
                    ["post"] = MethodPost,
                    ["put"] = MethodPut,
                    ["delete"] = MethodDelete,
                    ["patch"] = MethodPatch,
                    ["route"] = MethodGet
                }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase)
            }
        ];
        InboundRouteCallRules =
        [
            new InboundRouteCallRule
            {
                EmitEntryPointNode = true,
                MethodToHttpVerb = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["add_route"] = MethodGet,
                    ["add_get"] = MethodGet,
                    ["add_post"] = MethodPost,
                    ["add_put"] = MethodPut,
                    ["add_delete"] = MethodDelete
                }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase),
                ReceiverSubstrings = new[] { "app", "api", "router" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                PathArgumentIndex = 0
            }
        ];
    }
}
