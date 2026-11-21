using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parsers.TypeScript.Libraries;

public class NestJsCqrsLibraryParser : DeclarativeSemanticExtension
{
    public override string Name => "NestJS CQRS";
    public override string Id => "nestjs-cqrs";
    public override SemanticExtensionType Type => SemanticExtensionType.Queue;
    public override IReadOnlyList<string> SupportedPatterns => ["@nestjs/cqrs"];

    private static readonly HashSet<string> HandlerDecorators =
    [
        "CommandHandler",
        "EventsHandler",
        "QueryHandler"
    ];

    private static readonly IReadOnlyList<CustomAstRule> Rules =
    [
        new CustomAstRule
        {
            NodeTypes =
            [
                TreeSitterSyntax.TypeScript.ClassDeclaration,
                TreeSitterSyntax.TypeScript.ClassExpression,
                TreeSitterSyntax.TypeScript.CallExpression
            ],
            CollectReferences = CollectReferencesCore
        }
    ];

    public override IReadOnlyList<CustomAstRule> CustomAstRules => Rules;

    private static void CollectReferencesCore(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        // 1. Ingress: @CommandHandler(CreateOrderCommand), @EventsHandler(OrderCreatedEvent)
        if (IsCqrsHandlerClass(node))
        {
            var (_, msgType) = ExtractHandlerDecoratorInfo(node);
            if (!string.IsNullOrEmpty(msgType))
            {
                references.Add(new Reference(scopeSymbolId, "cqrs:" + msgType, OntologyConstants.Relationships.SubscribesTo));
                references.Add(new Reference(scopeSymbolId, msgType, OntologyConstants.Relationships.UsesType));
            }
        }

        // 2. Egress: eventBus.publish(new OrderCreatedEvent(...)) / publishAll(...)
        if (node.Is(TreeSitterSyntax.TypeScript.CallExpression))
        {
            if (AstHelper.TryGetMemberAccess(node, out _, out var memberName))
            {
                if (memberName is "publish" or "publishAll")
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

    private static IEnumerable<Node> GetClassDecorators(Node node)
    {
        foreach (var d in node.FindChildrenOfType(TreeSitterSyntax.TypeScript.Decorator))
            yield return d;
        if (node.Parent.IsValid() && node.Parent.Is(TreeSitterSyntax.TypeScript.ExportStatement))
        {
            foreach (var d in node.Parent.FindChildrenOfType(TreeSitterSyntax.TypeScript.Decorator))
                yield return d;
        }
    }

    private static bool IsCqrsHandlerClass(Node node)
    {
        if (!node.IsAny(TreeSitterSyntax.TypeScript.ClassDeclaration, TreeSitterSyntax.TypeScript.ClassExpression))
            return false;

        foreach (var dec in GetClassDecorators(node))
        {
            var text = dec.Text;
            foreach (var h in HandlerDecorators)
            {
                if (text.Contains(h, StringComparison.Ordinal)) return true;
            }
        }
        return false;
    }

    private static (string HandlerType, string? MessageType) ExtractHandlerDecoratorInfo(Node classNode)
    {
        foreach (var dec in GetClassDecorators(classNode))
        {
            foreach (var h in HandlerDecorators)
            {
                if (dec.Text.Contains(h, StringComparison.Ordinal))
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
