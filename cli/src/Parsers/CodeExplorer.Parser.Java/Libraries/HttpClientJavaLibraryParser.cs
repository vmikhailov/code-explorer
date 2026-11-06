using System.Collections.Frozen;

namespace CodeExplorer.Parser.Java.Libraries;

public class HttpClientJavaLibraryParser : DeclarativeSemanticExtension
{
    public HttpClientJavaLibraryParser()
    {
        Id = "java-http";
        Name = "Java HTTP Clients (HttpClient / RestTemplate / WebClient / OkHttp / Feign)";
        Type = SemanticExtensionType.ApiClient;
        LibraryRole = LibraryRole.General;
        SupportedPatterns =
        [
            "java.net.http*",
            "org.springframework.web.client*",
            "org.springframework.web.reactive.function.client*",
            "okhttp3*",
            "org.apache.http*",
            "org.apache.hc.client5*",
            "org.springframework.cloud.openfeign*",
            "org.springframework.cloud.spring-cloud-starter-openfeign*",
            "org.springframework.cloud.spring-cloud-starter-feign*",
            "*openfeign*",
            "*feign*",
            "org.eclipse.microprofile.rest.client*",
            "io.quarkus.rest.client*"
        ];
        OutboundHttpRules =
        [
            new OutboundHttpRule
            {
                CallMethodNames = new[]
                {
                    "send", "sendAsync", "execute", "newCall",
                    "getForObject", "getForEntity", "postForObject", "postForEntity", "put", "delete", "exchange",
                    "get", "post", "retrieve", "exchangeToMono", "exchangeToFlux",
                    "uri", "url"
                }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                ClientAnnotationSuffixes = new[] { "FeignClient", "RegisterRestClient", "RestClient" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase)
            }
        ];
    }
}

