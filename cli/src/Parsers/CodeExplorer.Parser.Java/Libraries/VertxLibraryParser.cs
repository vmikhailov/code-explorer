using System.Collections.Frozen;

namespace CodeExplorer.Parser.Java.Libraries;

public class VertxLibraryParser : DeclarativeSemanticExtension
{
    public VertxLibraryParser()
    {
        Id = "vertx";
        Name = "Eclipse Vert.x";
        Type = SemanticExtensionType.Framework;
        LibraryRole = LibraryRole.WebService;
        SupportedPatterns = ["io.vertx.*"];
        InboundRouteCallRules =
        [
            new InboundRouteCallRule
            {
                MethodToHttpVerb = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["get"] = "GET",
                    ["post"] = "POST",
                    ["put"] = "PUT",
                    ["delete"] = "DELETE",
                    ["patch"] = "PATCH",
                    ["route"] = "ALL"
                }.ToFrozenDictionary(StringComparer.Ordinal),
                ReceiverSubstrings = new[] { "router", "route" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase)
            }
        ];
        MessagingBrokerRules =
        [
            new MessagingBrokerRule
            {
                Protocol = "vertx",
                PublishMethodNames = new[] { "publish", "send", "request" }.ToFrozenSet(StringComparer.Ordinal),
                SubscribeMethodNames = new[] { "consumer", "localConsumer" }.ToFrozenSet(StringComparer.Ordinal)
            }
        ];
    }
}
