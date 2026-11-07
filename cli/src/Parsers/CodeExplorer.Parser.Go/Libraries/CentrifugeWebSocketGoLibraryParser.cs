using System.Collections.Frozen;

namespace CodeExplorer.Parser.Go.Libraries;

public class CentrifugeWebSocketGoLibraryParser : DeclarativeSemanticExtension
{
    private const string WsChannelPrefix = "ws:";

    public CentrifugeWebSocketGoLibraryParser()
    {
        Id = "centrifuge-ws-go";
        Name = "Centrifuge & Gorilla WebSocket";
        Type = SemanticExtensionType.Framework;
        LibraryRole = LibraryRole.WebService;
        SupportedPatterns =
        [
            "github.com/centrifugal/centrifuge*",
            "github.com/gorilla/websocket*",
            "nhooyr.io/websocket*",
            "github.com/coder/websocket*"
        ];
        InboundRouteCallRules =
        [
            new InboundRouteCallRule
            {
                MethodToHttpVerb = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["Upgrade"] = "",
                    ["Accept"] = ""
                }.ToFrozenDictionary(StringComparer.Ordinal),
                StaticRoute = "WS:/ws",
                EmitEntryPointNode = true
            }
        ];
        ServiceCallSinkRules =
        [
            new ServiceCallSinkRule
            {
                CallMethodNames = new[] { "Publish" }.ToFrozenSet(StringComparer.Ordinal),
                TargetPrefix = WsChannelPrefix,
                TargetRelation = OntologyConstants.Relationships.PublishesTo,
                AllowIdentifierArgument = true
            },
            new ServiceCallSinkRule
            {
                CallMethodNames = new[] { "OnPublish", "OnSubscribe" }.ToFrozenSet(StringComparer.Ordinal),
                StaticTargetName = WsChannelPrefix + "centrifuge",
                TargetRelation = OntologyConstants.Relationships.SubscribesTo
            }
        ];
    }
}
