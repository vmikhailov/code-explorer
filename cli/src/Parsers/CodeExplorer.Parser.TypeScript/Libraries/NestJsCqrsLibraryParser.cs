using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public class NestJsCqrsLibraryParser : ILibraryParser
{
    public string Name => "NestJS CQRS";
    public string Id => "nestjs-cqrs";
    public string Type => OntologyConstants.LibraryTypes.Queue;
    public IReadOnlyList<string> SupportedPatterns => ["@nestjs/cqrs"];
    public bool IsImplemented => true;

    private static readonly HashSet<string> HandlerDecorators =
    [
        "CommandHandler",
        "EventsHandler",
        "QueryHandler"
    ];

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (IsCqrsHandlerClass(node))
        {
            return OntologyConstants.NodeLabels.EntryPoint;
        }
        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx)
    {
        if (IsCqrsHandlerClass(node))
        {
            var (handlerType, msgType) = ExtractHandlerDecoratorInfo(node);
            return !string.IsNullOrEmpty(msgType) ? $"{handlerType}:{msgType}" : "CqrsHandler";
        }
        return null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        // 1. Ingress: @CommandHandler(CreateOrderCommand), @EventsHandler(OrderCreatedEvent)
        if (IsCqrsHandlerClass(node))
        {
            var (_, msgType) = ExtractHandlerDecoratorInfo(node);
            if (!string.IsNullOrEmpty(msgType))
            {
                references.Add(new Reference(scopeSymbolId, "cqrs:" + msgType, OntologyConstants.Relationships.SubscribesTo));
            }
        }

        // 2. Egress: commandBus.execute(new CreateOrderCommand(...)) or eventBus.publish(new OrderCreatedEvent(...))
        if (node.Is(TreeSitterSyntax.TypeScript.CallExpression))
        {
            if (AstHelper.TryGetMemberAccess(node, out _, out var memberName))
            {
                if (memberName is "execute" or "publish")
                {
                    var args = node.FindChildOfType(TreeSitterSyntax.TypeScript.Arguments);
                    if (args.IsValid())
                    {
                        var newExpr = args.FindChildOfType(TreeSitterSyntax.TypeScript.NewExpression);
                        if (newExpr.IsValid())
                        {
                            var constr = newExpr.FindChildOfType(TreeSitterSyntax.Common.Identifier);
                            if (constr.IsValid())
                            {
                                references.Add(new Reference(scopeSymbolId, "cqrs:" + constr.Text, OntologyConstants.Relationships.PublishesTo));
                            }
                        }
                    }
                }
            }
        }
    }

    private static bool IsCqrsHandlerClass(Node node)
    {
        if (!node.IsAny(TreeSitterSyntax.TypeScript.ClassDeclaration, TreeSitterSyntax.TypeScript.ClassExpression))
            return false;

        var decorators = node.FindChildrenOfType(TreeSitterSyntax.TypeScript.Decorator);
        foreach (var dec in decorators)
        {
            var text = dec.Text;
            foreach (var h in HandlerDecorators)
            {
                if (text.Contains(h)) return true;
            }
        }
        return false;
    }

    private static (string HandlerType, string? MessageType) ExtractHandlerDecoratorInfo(Node classNode)
    {
        var decorators = classNode.FindChildrenOfType(TreeSitterSyntax.TypeScript.Decorator);
        foreach (var dec in decorators)
        {
            foreach (var h in HandlerDecorators)
            {
                if (dec.Text.Contains(h))
                {
                    var call = dec.FindChildOfType(TreeSitterSyntax.TypeScript.CallExpression);
                    if (call.IsValid())
                    {
                        var args = call.FindChildOfType(TreeSitterSyntax.TypeScript.Arguments);
                        if (args.IsValid())
                        {
                            var firstIdent = args.FindChildOfType(TreeSitterSyntax.Common.Identifier);
                            if (firstIdent.IsValid())
                            {
                                return (h, firstIdent.Text);
                            }
                        }
                    }
                    return (h, null);
                }
            }
        }
        return ("Handler", null);
    }
}
