using System.Collections.Frozen;

namespace CodeExplorer.Parser.CSharp.Libraries;

public class FastEndpointsLibraryParser : DeclarativeSemanticExtension
{
    public FastEndpointsLibraryParser()
    {
        Id = "fastendpoints";
        Name = "FastEndpoints";
        Type = SemanticExtensionType.Framework;
        LibraryRole = LibraryRole.WebService;
        SupportedPatterns = ["FastEndpoints", "FastEndpoints.*"];
        InboundRouteCallRules =
        [
            new InboundRouteCallRule
            {
                MethodToHttpVerb = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["Get"] = "GET",
                    ["Post"] = "POST",
                    ["Put"] = "PUT",
                    ["Delete"] = "DELETE",
                    ["Patch"] = "PATCH"
                }.ToFrozenDictionary(StringComparer.Ordinal)
            }
        ];
    }
}
