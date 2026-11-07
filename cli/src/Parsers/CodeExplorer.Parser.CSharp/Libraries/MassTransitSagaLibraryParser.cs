using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.CSharp.Libraries;

public class MassTransitSagaLibraryParser : ISemanticExtension
{
    private const string SagaBaseClass = "MassTransitStateMachine";
    private const string MassTransitPrefix = "masstransit:";

    public string Name => "MassTransit Sagas";
    public string Id => "masstransit-saga";
    public SemanticExtensionType Type => SemanticExtensionType.Queue;
    public IReadOnlyList<string> SupportedPatterns => ["MassTransit", "MassTransit.StateMachine*"];
    public bool IsImplemented => true;
    public LibraryRole LibraryRole => LibraryRole.MessageBroker;

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        return IsSagaStateMachineClass(node, out _) ? OntologyConstants.NodeLabels.EntryPoint : null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx)
    {
        return IsSagaStateMachineClass(node, out var stateType) ? $"Saga:{stateType}" : null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (IsSagaStateMachineClass(node, out var stateType))
        {
            references.Add(new Reference(scopeSymbolId, stateType, OntologyConstants.Relationships.UsesType));
            var declList = node.FindChildOfType(TreeSitterSyntax.CSharp.DeclarationList);
            var propNodes = declList.IsValid()
                ? declList.FindChildrenOfType(TreeSitterSyntax.CSharp.PropertyDeclaration)
                : node.FindChildrenOfType(TreeSitterSyntax.CSharp.PropertyDeclaration);
            foreach (var prop in propNodes)
            {
                var typeNode = prop.GetField(TreeSitterSyntax.Fields.Type);
                if (typeNode.IsValid() && typeNode.Text.StartsWith("Event<", StringComparison.Ordinal) && typeNode.Text.EndsWith('>'))
                {
                    var eventType = typeNode.Text["Event<".Length..^1].Trim();
                    if (!string.IsNullOrEmpty(eventType))
                    {
                        references.Add(new Reference(scopeSymbolId, MassTransitPrefix + eventType, OntologyConstants.Relationships.SubscribesTo));
                        references.Add(new Reference(scopeSymbolId, eventType, OntologyConstants.Relationships.UsesType));
                    }
                }
            }
        }
    }

    private static bool IsSagaStateMachineClass(Node node, out string stateType)
    {
        stateType = string.Empty;
        if (!node.Is(TreeSitterSyntax.CSharp.ClassDeclaration)) return false;

        var baseList = node.FindChildOfType(TreeSitterSyntax.CSharp.BaseList);
        if (!baseList.IsValid()) return false;

        foreach (var child in baseList.Children)
        {
            if (child.Is(TreeSitterSyntax.CSharp.GenericName))
            {
                var idNode = child.FindChildOfType(TreeSitterSyntax.Common.Identifier);
                if (idNode.IsValid() && idNode.Text == SagaBaseClass)
                {
                    var typeArgs = child.FindChildOfType(TreeSitterSyntax.CSharp.TypeArgumentList);
                    var firstType = typeArgs.IsValid()
                        ? typeArgs.Children.FirstOrDefault(c => c.IsAny(TreeSitterSyntax.CSharp.TypeIdentifier, TreeSitterSyntax.Common.Identifier))
                        : default;
                    stateType = firstType.IsValid() ? firstType.Text : SagaBaseClass;
                    return true;
                }
            }
        }

        return false;
    }
}
