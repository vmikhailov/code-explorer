using System.Collections.Frozen;

namespace CodeExplorer.Parser.Java.Libraries;

public class SpringMvcLibraryParser : DeclarativeSemanticExtension
{
    public SpringMvcLibraryParser()
    {
        Id = "spring-web";
        Name = "Spring Web / MVC";
        Type = "framework:web";
        LibraryRole = LibraryRole.WebService;
        SupportedPatterns =
        [
            "org.springframework.web",
            "org.springframework.boot.autoconfigure",
            "org.springframework.stereotype.Controller",
            "org.springframework.web.bind.annotation"
        ];
        AnnotationEndpointRules =
        [
            new AnnotationEndpointRule
            {
                AnnotationToHttpMethod = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["GetMapping"] = "GET",
                    ["PostMapping"] = "POST",
                    ["PutMapping"] = "PUT",
                    ["DeleteMapping"] = "DELETE",
                    ["PatchMapping"] = "PATCH",
                    ["RequestMapping"] = "ALL"
                }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase)
            }
        ];
    }
}

