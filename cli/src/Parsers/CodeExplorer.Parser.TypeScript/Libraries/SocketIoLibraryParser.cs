using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public class SocketIoLibraryParser : ISemanticExtension
{
    public string Name => "Socket.io";
    public string Id => "socketio";
    public string Type => "api";
    public IReadOnlyList<string> SupportedPatterns => ["socket.io", "socket.io-client"];
    public bool IsImplemented => true;

    private static readonly NodeSelector _socketOnSelector = NodeSelector.New()
        .HasType(TreeSitterSyntax.TypeScript.CallExpression)
        .FunctionNode
        .Where(NodeSelector.Or(
            NodeSelector.New().HasType(TreeSitterSyntax.Common.Identifier).Text("on"),
            NodeSelector.New()
                .HasType(TreeSitterSyntax.TypeScript.MemberExpression)
                .HasChild(TreeSitterSyntax.Fields.Property, NodeSelector.New().Text("on"))
        ));

    private static readonly NodeSelector _socketEmitSelector = NodeSelector.New()
        .HasType(TreeSitterSyntax.TypeScript.CallExpression)
        .FunctionNode
        .Where(NodeSelector.Or(
            NodeSelector.New().HasType(TreeSitterSyntax.Common.Identifier).Text("emit"),
            NodeSelector.New()
                .HasType(TreeSitterSyntax.TypeScript.MemberExpression)
                .HasChild(TreeSitterSyntax.Fields.Property, NodeSelector.New().Text("emit"))
        ));

    public IReadOnlyDictionary<string, NodeSelector> Selectors => new Dictionary<string, NodeSelector>
    {
        { OntologyConstants.NodeLabels.EntryPoint, _socketOnSelector },
        { OntologyConstants.NodeLabels.ExternalService, _socketEmitSelector }
    };

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (_socketOnSelector.Matches(node)) return OntologyConstants.NodeLabels.EntryPoint;
        if (_socketEmitSelector.Matches(node))
        {
            if (IsServerContext(node)) return null;
            return OntologyConstants.NodeLabels.ExternalService;
        }
        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx)
    {
        var isOn = _socketOnSelector.Matches(node);
        var isEmit = _socketEmitSelector.Matches(node);

        if (isEmit && IsServerContext(node)) return null;

        if (isOn || isEmit)
        {
            var argList = node.GetChildForField("arguments");
            if (argList != null && argList.Children.Count > 1)
            {
                var firstArg = argList.Children[1];
                var eventName = AstHelper.ResolveStringOrTemplate(firstArg);
                if (!string.IsNullOrEmpty(eventName))
                {
                    return $"ws:{eventName}";
                }
            }
        }
        return null;
    }

    private static bool IsServerContext(Node node)
    {
        var curr = node.Parent;
        var steps = 0;
        while (curr.IsValid() && ++steps <= 40)
        {
            if (curr.Is(TreeSitterSyntax.TypeScript.CallExpression))
            {
                var func = curr.GetFunctionNode();
                if (func.IsValid() && (func.Text.EndsWith(".on") || func.Text == "on"))
                {
                    var args = AstHelper.GetCallArguments(curr);
                    if (args.Count > 0)
                    {
                        var first = AstHelper.ResolveStringOrTemplate(args[0]);
                        if (string.Equals(first, "connection", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(first, "connect", StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }
                }
            }

            if (curr.Is(TreeSitterSyntax.TypeScript.Decorator) || curr.Text.Contains("@SubscribeMessage") || curr.Text.Contains("@WebSocketGateway"))
            {
                return true;
            }

            curr = curr.Parent;
        }

        return false;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
    }
}
