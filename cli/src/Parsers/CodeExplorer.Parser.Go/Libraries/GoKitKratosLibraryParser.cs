using System.Collections.Frozen;

namespace CodeExplorer.Parser.Go.Libraries;

public class GoKitKratosLibraryParser : DeclarativeSemanticExtension
{
    public GoKitKratosLibraryParser()
    {
        Id = "gokit-kratos";
        Name = "Go-Kit & Go-Kratos";
        Type = SemanticExtensionType.Framework;
        LibraryRole = LibraryRole.WebService;
        SupportedPatterns =
        [
            "github.com/go-kit/kit*",
            "github.com/go-kratos/kratos*"
        ];
        InboundRouteCallRules =
        [
            new InboundRouteCallRule
            {
                MethodToHttpVerb = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["NewServer"] = "ALL"
                }.ToFrozenDictionary(StringComparer.Ordinal),
                ReceiverSubstrings = new[] { "httptransport", "grpctransport", "http", "grpc" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                StaticRoute = "/gokit/transport"
            }
        ];
    }
}
