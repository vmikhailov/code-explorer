using System.Collections.Frozen;

namespace CodeExplorer.Parser.Java.Libraries;

public class QuarkusJaxRsLibraryParser : DeclarativeSemanticExtension
{
    public QuarkusJaxRsLibraryParser()
    {
        Id = "quarkus-jaxrs";
        Name = "Quarkus RESTEasy Reactive / JAX-RS / WebSockets";
        Type = SemanticExtensionType.Framework;
        LibraryRole = LibraryRole.WebService;
        IsBuiltIn = true;
        SupportedPatterns =
        [
            "jakarta.ws.rs*",
            "javax.ws.rs*",
            "io.quarkus.resteasy*",
            "io.quarkus.websockets*",
            "org.jboss.resteasy*"
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
                    ["HEAD"] = "HEAD",
                    ["OPTIONS"] = "OPTIONS",
                    ["Path"] = "ALL",
                    ["WebSocket"] = "WS",
                    ["ServerEndpoint"] = "WS"
                }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase)
            }
        ];
    }
}
