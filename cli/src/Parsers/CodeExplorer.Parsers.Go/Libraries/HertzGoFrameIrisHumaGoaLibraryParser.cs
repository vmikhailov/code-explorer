using System.Collections.Frozen;

namespace CodeExplorer.Parsers.Go.Libraries;

public class HertzGoFrameIrisHumaGoaLibraryParser : DeclarativeSemanticExtension
{
    private const string ExtensionId = "hertz-goframe-iris-huma-goa";
    private const string ExtensionName = "CloudWeGo Hertz/Kitex, GoFrame, Iris, Huma, Goa, Ogen & Oapi-Codegen";
    private const string MethodGet = "GET";
    private const string MethodPost = "POST";
    private const string MethodPut = "PUT";
    private const string MethodDelete = "DELETE";
    private const string MethodPatch = "PATCH";

    public HertzGoFrameIrisHumaGoaLibraryParser()
    {
        Id = ExtensionId;
        Name = ExtensionName;
        Type = SemanticExtensionType.Framework;
        LibraryRole = LibraryRole.WebService;
        SupportedPatterns =
        [
            "github.com/cloudwego/hertz*",
            "github.com/cloudwego/kitex*",
            "github.com/gogf/gf*",
            "github.com/kataras/iris*",
            "github.com/danielgtaylor/huma*",
            "goa.design/goa*",
            "github.com/ogen-go/ogen*",
            "github.com/oapi-codegen/oapi-codegen*",
            "github.com/deepmap/oapi-codegen*",
            "github.com/smallnest/rpcx*"
        ];
        InboundRouteCallRules =
        [
            new InboundRouteCallRule
            {
                EmitEntryPointNode = true,
                MethodToHttpVerb = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["GET"] = MethodGet,
                    ["POST"] = MethodPost,
                    ["PUT"] = MethodPut,
                    ["DELETE"] = MethodDelete,
                    ["PATCH"] = MethodPatch,
                    ["RegisterGet"] = MethodGet,
                    ["RegisterPost"] = MethodPost
                }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase),
                ReceiverSubstrings = new[]
                {
                    "h", "hertz", "app", "router", "group", "api", "s", "server"
                }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                PathArgumentIndex = 0
            }
        ];
    }
}
