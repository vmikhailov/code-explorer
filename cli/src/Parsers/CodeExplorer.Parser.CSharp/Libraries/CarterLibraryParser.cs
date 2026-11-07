using System.Collections.Frozen;

namespace CodeExplorer.Parser.CSharp.Libraries;

public class CarterLibraryParser : DeclarativeSemanticExtension
{
    public CarterLibraryParser()
    {
        Id = "carter";
        Name = "Carter";
        Type = SemanticExtensionType.Framework;
        LibraryRole = LibraryRole.WebService;
        SupportedPatterns = ["Carter", "Carter.*"];
        InboundRouteCallRules =
        [
            new InboundRouteCallRule
            {
                MethodToHttpVerb = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["MapGet"] = "GET",
                    ["MapPost"] = "POST",
                    ["MapPut"] = "PUT",
                    ["MapDelete"] = "DELETE",
                    ["MapPatch"] = "PATCH"
                }.ToFrozenDictionary(StringComparer.Ordinal)
            }
        ];
    }
}
