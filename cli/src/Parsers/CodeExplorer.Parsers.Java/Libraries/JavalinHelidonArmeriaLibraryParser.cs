using System.Collections.Frozen;

namespace CodeExplorer.Parsers.Java.Libraries;

public class JavalinHelidonArmeriaLibraryParser : DeclarativeSemanticExtension
{
    private const string ExtensionId = "javalin-helidon-armeria";
    private const string ExtensionName = "Javalin, Helidon, Armeria & SparkJava";
    private const string MethodGet = "GET";
    private const string MethodPost = "POST";
    private const string MethodPut = "PUT";
    private const string MethodDelete = "DELETE";
    private const string MethodPatch = "PATCH";

    public JavalinHelidonArmeriaLibraryParser()
    {
        Id = ExtensionId;
        Name = ExtensionName;
        Type = SemanticExtensionType.Framework;
        LibraryRole = LibraryRole.WebService;
        SupportedPatterns =
        [
            "io.javalin*",
            "io.helidon*",
            "com.linecorp.armeria*",
            "spark.Spark*",
            "ratpack*"
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
                    ["patch"] = MethodPatch
                }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase),
                ReceiverSubstrings = new[]
                {
                    "app", "javalin", "routing", "router", "server", "Spark", "ApiBuilder"
                }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                PathArgumentIndex = 0
            }
        ];
    }
}
