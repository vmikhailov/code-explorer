using System.Collections.Frozen;

namespace CodeExplorer.Parser.Java.Libraries;

public class OpenFeignRetrofitLibraryParser : DeclarativeSemanticExtension
{
    public OpenFeignRetrofitLibraryParser()
    {
        Id = "openfeign-retrofit";
        Name = "OpenFeign & Retrofit";
        Type = SemanticExtensionType.ApiClient;
        LibraryRole = LibraryRole.General;
        SupportedPatterns =
        [
            "org.springframework.cloud.openfeign.*",
            "feign.*",
            "retrofit2.*",
            "org.springframework.web.service.annotation.*"
        ];
        OutboundHttpRules =
        [
            new OutboundHttpRule
            {
                ClientAnnotationSuffixes = new[]
                {
                    "FeignClient", "HttpExchange", "GetExchange", "PostExchange", "PutExchange", "DeleteExchange", "PatchExchange",
                    "GET", "POST", "PUT", "DELETE", "PATCH", "HTTP", "RequestLine"
                }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                CallMethodNames = new[] { "execute", "enqueue" }.ToFrozenSet(StringComparer.Ordinal),
                FallbackTarget = "http:feign-retrofit-client"
            }
        ];
    }
}
