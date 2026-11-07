using System.Collections.Frozen;

namespace CodeExplorer.Parser.Go.Libraries;

public class GoZeroLibraryParser : DeclarativeSemanticExtension
{
    private const string ZrpcClientTarget = "zrpc:client";

    public GoZeroLibraryParser()
    {
        Id = "go-zero";
        Name = "go-zero";
        Type = SemanticExtensionType.Framework;
        LibraryRole = LibraryRole.WebService;
        SupportedPatterns = ["github.com/zeromicro/go-zero*"];
        InboundRouteCallRules =
        [
            new InboundRouteCallRule
            {
                MethodToHttpVerb = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["AddRoutes"] = "",
                    ["AddRoute"] = ""
                }.ToFrozenDictionary(StringComparer.Ordinal),
                StaticRoute = "REST:/go-zero/routes",
                EmitEntryPointNode = true
            },
            new InboundRouteCallRule
            {
                MethodToHttpVerb = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["MustNewServer"] = ""
                }.ToFrozenDictionary(StringComparer.Ordinal),
                ReceiverSubstrings = new[] { "zrpc" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                StaticRoute = "gRPC:/zrpc",
                EmitEntryPointNode = true
            }
        ];
        ServiceCallSinkRules =
        [
            new ServiceCallSinkRule
            {
                CallMethodNames = new[] { "MustNewClient", "NewClient" }.ToFrozenSet(StringComparer.Ordinal),
                ReceiverSubstrings = new[] { "zrpc" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                StaticTargetName = ZrpcClientTarget,
                TargetRelation = OntologyConstants.Relationships.Calls
            }
        ];
    }
}
