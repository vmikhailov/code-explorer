using System.Collections.Frozen;

namespace CodeExplorer.Parser.Go.Libraries;

public class GorillaMuxHttpRouterLibraryParser : DeclarativeSemanticExtension
{
    public GorillaMuxHttpRouterLibraryParser()
    {
        Id = "gorilla-mux-httprouter";
        Name = "Gorilla Mux & HttpRouter";
        Type = SemanticExtensionType.Framework;
        LibraryRole = LibraryRole.WebService;
        SupportedPatterns =
        [
            "github.com/gorilla/mux*",
            "github.com/julienschmidt/httprouter*"
        ];
        InboundRouteCallRules =
        [
            new InboundRouteCallRule
            {
                MethodToHttpVerb = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["HandleFunc"] = "ALL",
                    ["Handle"] = "ALL",
                    ["GET"] = "GET",
                    ["POST"] = "POST",
                    ["PUT"] = "PUT",
                    ["DELETE"] = "DELETE",
                    ["PATCH"] = "PATCH"
                }.ToFrozenDictionary(StringComparer.Ordinal)
            }
        ];
    }
}
