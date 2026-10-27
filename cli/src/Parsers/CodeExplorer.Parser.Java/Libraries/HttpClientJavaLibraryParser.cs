using System.Collections.Frozen;
using CodeExplorer.Core.Parser;

namespace CodeExplorer.Parser.Java.Libraries;

public class HttpClientJavaLibraryParser : DeclarativeSemanticExtension
{
    public HttpClientJavaLibraryParser()
    {
        Id = "java-http";
        Name = "Java HTTP Clients (HttpClient / RestTemplate / WebClient / OkHttp / Feign)";
        Type = "api:client";
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
            "feign*"
        ];
        OutboundHttpRules =
        [
            new OutboundHttpRule
            {
                CallMethodNames = new[]
                {
                    "send", "sendAsync", "execute", "newCall",
                    "getForObject", "getForEntity", "postForObject", "postForEntity", "put", "delete", "exchange",
                    "get", "post", "retrieve", "exchangeToMono", "exchangeToFlux"
                }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                ClientAnnotationSuffixes = new[] { "FeignClient" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase)
            }
        ];
    }
}

