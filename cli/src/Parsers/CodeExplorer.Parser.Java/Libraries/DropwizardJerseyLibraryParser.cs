using System.Collections.Frozen;

namespace CodeExplorer.Parser.Java.Libraries;

public class DropwizardJerseyLibraryParser : DeclarativeSemanticExtension
{
    public DropwizardJerseyLibraryParser()
    {
        Id = "dropwizard-jersey";
        Name = "Dropwizard & Jersey JAX-RS";
        Type = SemanticExtensionType.Framework;
        LibraryRole = LibraryRole.WebService;
        SupportedPatterns =
        [
            "io.dropwizard.*",
            "org.glassfish.jersey.*",
            "javax.ws.rs.*",
            "jakarta.ws.rs.*"
        ];
        AnnotationEndpointRules =
        [
            new AnnotationEndpointRule
            {
                AnnotationToHttpMethod = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["GET"] = "GET",
                    ["POST"] = "POST",
                    ["PUT"] = "PUT",
                    ["DELETE"] = "DELETE",
                    ["PATCH"] = "PATCH",
                    ["Path"] = "ALL"
                }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase)
            }
        ];
    }
}
