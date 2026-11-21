using System.Collections.Frozen;

namespace CodeExplorer.Parsers.Java.Libraries;

/// <summary>
/// Semantic extension for SOAP, JAX-WS (jakarta.jws / javax.jws / jakarta.xml.ws / javax.xml.ws),
/// Spring-WS (@Endpoint / @PayloadRoot / WebServiceTemplate), and Apache CXF.
/// </summary>
public class SoapJaxWsLibraryParser : DeclarativeSemanticExtension
{
    public SoapJaxWsLibraryParser()
    {
        Id = "soap-jaxws";
        Name = "SOAP / JAX-WS / Spring-WS / Apache CXF";
        Type = SemanticExtensionType.Framework;
        LibraryRole = LibraryRole.WebService;
        SupportedPatterns =
        [
            "jakarta.jws.*",
            "javax.jws.*",
            "jakarta.xml.ws.*",
            "javax.xml.ws.*",
            "org.springframework.ws.*",
            "org.springframework.boot.spring-boot-starter-web-services*",
            "org.apache.cxf.*",
            "*spring-ws*",
            "*cxf-rt-frontend-jaxws*",
            "*jaxws*"
        ];
        AnnotationEndpointRules =
        [
            new AnnotationEndpointRule
            {
                AnnotationToHttpMethod = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["WebMethod"] = "SOAP",
                    ["PayloadRoot"] = "SOAP",
                    ["SoapAction"] = "SOAP"
                }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase),
                PathArgumentKeys = new[] { "operationName", "localPart", "action", "value", "name" }.ToFrozenSet(StringComparer.Ordinal),
                RoutePrefixAnnotationNames = new[] { "WebService", "Endpoint" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase)
            }
        ];
        OutboundHttpRules =
        [
            new OutboundHttpRule
            {
                CallMethodNames = new[]
                {
                    "marshalSendAndReceive",
                    "sendSourceAndReceiveToResult",
                    "sendAndReceive",
                    "sendSourceAndReceive"
                }.ToFrozenSet(StringComparer.Ordinal),
                ClientAnnotationSuffixes = new[] { "WebServiceRef", "WebServiceClient" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                FallbackTarget = "soap:unknown-service"
            }
        ];
    }
}
