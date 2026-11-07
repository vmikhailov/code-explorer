using System.Collections.Frozen;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.Go.Libraries;

public class ConnectTwirpGoLibraryParser : DeclarativeSemanticExtension
{
    private const string RpcRoutePrefix = "GRPC:/";
    private const string RpcCallPrefix = "grpc://";
    private const string NewPrefix = "New";
    private const string HandlerSuffix = "Handler";
    private const string ServerSuffix = "Server";
    private const string ClientSuffix = "Client";
    private const char DotChar = '.';

    public ConnectTwirpGoLibraryParser()
    {
        Name = "ConnectRPC & Twirp Go";
        Id = "connect-twirp-go";
        Type = SemanticExtensionType.Framework;
        SupportedPatterns =
        [
            "connectrpc.com/connect*",
            "github.com/bufbuild/connect-go*",
            "github.com/twitchtv/twirp*"
        ];
        LibraryRole = LibraryRole.WebService;
        CustomAstRules =
        [
            new CustomAstRule
            {
                NodeTypes = new[] { TreeSitterSyntax.Go.CallExpression }.ToFrozenSet(),
                CollectReferences = CollectConnectTwirpReferences
            }
        ];
    }

    private static void CollectConnectTwirpReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (!node.Is(TreeSitterSyntax.Go.CallExpression)) return;

        var func = node.GetFunctionNode();
        if (!func.IsValid()) return;

        var funcText = func.Text;
        var dotIdx = funcText.LastIndexOf(DotChar);
        var method = dotIdx >= 0 ? funcText[(dotIdx + 1)..] : funcText;
        if (!method.StartsWith(NewPrefix, StringComparison.Ordinal)) return;

        if (method.EndsWith(HandlerSuffix, StringComparison.Ordinal) ||
            method.EndsWith(ServerSuffix, StringComparison.Ordinal))
        {
            var svcName = method[3..];
            references.Add(new Reference(scopeSymbolId, RpcRoutePrefix + svcName, OntologyConstants.Relationships.Exposes));
        }
        else if (method.EndsWith(ClientSuffix, StringComparison.Ordinal))
        {
            var svcName = method[3..];
            references.Add(new Reference(scopeSymbolId, RpcCallPrefix + svcName, OntologyConstants.Relationships.Calls));
        }
    }
}
