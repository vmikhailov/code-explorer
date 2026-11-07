using System.Collections.Frozen;

namespace CodeExplorer.Parser.Kotlin.Libraries;

public class MicronautKotlinLibraryParser : DeclarativeSemanticExtension
{
    public MicronautKotlinLibraryParser()
    {
        Id = "micronaut-kotlin";
        Name = "Micronaut Kotlin";
        Type = SemanticExtensionType.Framework;
        LibraryRole = LibraryRole.WebService;
        SupportedPatterns = ["io.micronaut.*", "io.micronaut:micronaut*"];
        AnnotationEndpointRules =
        [
            new AnnotationEndpointRule
            {
                AnnotationToHttpMethod = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Get"] = "GET",
                    ["Post"] = "POST",
                    ["Put"] = "PUT",
                    ["Delete"] = "DELETE",
                    ["Patch"] = "PATCH",
                    ["Controller"] = "ALL"
                }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase),
                RoutePrefixAnnotationNames = new[] { "Controller" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase)
            }
        ];
    }
}
