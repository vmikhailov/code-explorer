using System.Collections.Frozen;

namespace CodeExplorer.Parser.Go.Libraries;

public class OtelPrometheusGoLibraryParser : DeclarativeSemanticExtension
{
    public OtelPrometheusGoLibraryParser()
    {
        Id = "otel-prometheus-go";
        Name = "OpenTelemetry Go & Prometheus Client";
        Type = SemanticExtensionType.Library;
        LibraryRole = LibraryRole.General;
        SupportedPatterns =
        [
            "go.opentelemetry.io/otel*",
            "github.com/prometheus/client_golang*"
        ];
        InboundRouteCallRules =
        [
            new InboundRouteCallRule
            {
                MethodToHttpVerb = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["Handler"] = "GET",
                    ["HandlerFor"] = "GET"
                }.ToFrozenDictionary(StringComparer.Ordinal),
                ReceiverSubstrings = new[] { "promhttp" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                StaticRoute = "/metrics"
            }
        ];
    }
}
