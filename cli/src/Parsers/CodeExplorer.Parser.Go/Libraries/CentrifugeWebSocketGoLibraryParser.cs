using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.Go.Libraries;

public class CentrifugeWebSocketGoLibraryParser : ISemanticExtension
{
    private const string WsChannelPrefix = "ws:";

    public string Name => "Centrifuge & Gorilla WebSocket";
    public string Id => "centrifuge-ws-go";
    public SemanticExtensionType Type => SemanticExtensionType.Framework;
    public IReadOnlyList<string> SupportedPatterns =>
    [
        "github.com/centrifugal/centrifuge*",
        "github.com/gorilla/websocket*",
        "nhooyr.io/websocket*",
        "github.com/coder/websocket*"
    ];
    public bool IsImplemented => true;
    public LibraryRole LibraryRole => LibraryRole.WebService;

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        return IsWebSocketUpgradeCall(node) ? OntologyConstants.NodeLabels.EntryPoint : null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx)
    {
        return IsWebSocketUpgradeCall(node) ? "WS:/ws" : null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (!node.Is(TreeSitterSyntax.Go.CallExpression)) return;

        var func = node.GetFunctionNode();
        if (!func.IsValid()) return;

        var funcText = func.Text;
        var args = GoAstHelper.GetCallArguments(node);

        if (funcText.EndsWith(".Publish", StringComparison.Ordinal) && args.Count > 0)
        {
            var channel = GoAstHelper.ResolveStringOrVariable(args[0]);
            if (!string.IsNullOrEmpty(channel))
            {
                references.Add(new Reference(scopeSymbolId, WsChannelPrefix + channel, OntologyConstants.Relationships.PublishesTo));
            }
        }
        else if (funcText.EndsWith(".OnPublish", StringComparison.Ordinal) ||
                 funcText.EndsWith(".OnSubscribe", StringComparison.Ordinal))
        {
            references.Add(new Reference(scopeSymbolId, WsChannelPrefix + "centrifuge", OntologyConstants.Relationships.SubscribesTo));
        }
    }

    private static bool IsWebSocketUpgradeCall(Node node)
    {
        if (!node.Is(TreeSitterSyntax.Go.CallExpression)) return false;
        var func = node.GetFunctionNode();
        if (!func.IsValid()) return false;

        var text = func.Text;
        return text.EndsWith(".Upgrade", StringComparison.Ordinal) ||
               text.EndsWith("websocket.Accept", StringComparison.Ordinal);
    }
}
