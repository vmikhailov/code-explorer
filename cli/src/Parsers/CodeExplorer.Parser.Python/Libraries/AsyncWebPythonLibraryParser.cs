using System.Collections.Frozen;

namespace CodeExplorer.Parser.Python.Libraries;

public class AsyncWebPythonLibraryParser : DeclarativeSemanticExtension
{
    public AsyncWebPythonLibraryParser()
    {
        Id = "async-web-python";
        Name = "Sanic, Tornado & Falcon";
        Type = SemanticExtensionType.Framework;
        LibraryRole = LibraryRole.WebService;
        SupportedPatterns = ["sanic*", "tornado*", "falcon*"];
        InboundRouteCallRules =
        [
            new InboundRouteCallRule
            {
                MethodToHttpVerb = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["get"] = "GET",
                    ["post"] = "POST",
                    ["put"] = "PUT",
                    ["delete"] = "DELETE",
                    ["patch"] = "PATCH",
                    ["route"] = "ALL",
                    ["add_route"] = "ALL"
                }.ToFrozenDictionary(StringComparer.Ordinal)
            }
        ];
    }
}
