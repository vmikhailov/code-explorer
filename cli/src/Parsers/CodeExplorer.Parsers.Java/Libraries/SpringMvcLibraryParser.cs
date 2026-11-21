using System.Collections.Frozen;

namespace CodeExplorer.Parsers.Java.Libraries;

public class SpringMvcLibraryParser : DeclarativeSemanticExtension
{
    public SpringMvcLibraryParser()
    {
        Id = "spring-web";
        Name = "Spring Web / MVC";
        Type = SemanticExtensionType.Framework;
        LibraryRole = LibraryRole.WebService;
        SupportedPatterns =
        [
            "org.springframework.web",
            "org.springframework.web.reactive.function.server*",
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
        InboundRouteCallRules =
        [
            new InboundRouteCallRule
            {
                MethodToHttpVerb = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["GET"] = "GET",
                    ["POST"] = "POST",
                    ["PUT"] = "PUT",
                    ["DELETE"] = "DELETE",
                    ["PATCH"] = "PATCH",
                    ["HEAD"] = "HEAD",
                    ["OPTIONS"] = "OPTIONS"
                }.ToFrozenDictionary(StringComparer.Ordinal),
                EmitEntryPointNode = true,
                RequireLeadingSlashOnPath = true,
                EmitHandlerTriggers = true
            },
            new InboundRouteCallRule
            {
                MethodToHttpVerb = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["GET"] = "GET",
                    ["POST"] = "POST",
                    ["PUT"] = "PUT",
                    ["DELETE"] = "DELETE",
                    ["PATCH"] = "PATCH",
                    ["HEAD"] = "HEAD",
                    ["OPTIONS"] = "OPTIONS"
                }.ToFrozenDictionary(StringComparer.Ordinal),
                ReceiverSubstrings = new[] { "RequestPredicates" }.ToFrozenSet(StringComparer.Ordinal),
                EmitEntryPointNode = true,
                RequireLeadingSlashOnPath = false
            }
        ];
    }
}

