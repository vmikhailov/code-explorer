using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public class AzureSdkLibraryParser : DeclarativeSemanticExtension
{
    public override SemanticExtensionType Type => SemanticExtensionType.Cloud;
    public override string Name => "Azure";
    public override string Id => "azure";
    public override IReadOnlyList<string> SupportedPatterns => ["@azure/*", "@azure/service-bus", "@azure/storage-blob", "@azure/cosmos"];
    public override LibraryRole LibraryRole => LibraryRole.CloudSdk;

    private static readonly IReadOnlyList<CustomAstRule> Rules =
    [
        new CustomAstRule
        {
            NodeTypes = [TreeSitterSyntax.TypeScript.CallExpression],
            MapNodeType = static (node, ctx) => IsCosmosContainerCall(node) ? OntologyConstants.NodeLabels.Query : null,
            ExtractIdentifier = static (node, ctx, _, _) => ExtractIdentifierCore(node, ctx),
            CollectReferences = CollectReferencesCore
        }
    ];

    public override IReadOnlyList<CustomAstRule> CustomAstRules => Rules;

    private static string? ExtractIdentifierCore(Node node, ParsingContext ctx)
    {
        if (IsCosmosContainerCall(node))
        {
            var container = AstHelper.ExtractFirstStringArgument(node);
            if (!string.IsNullOrEmpty(container))
            {
                return $"CosmosDB: {container}";
            }
            return "CosmosDB Query";
        }
        return null;
    }

    private static void CollectReferencesCore(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (!node.Is(TreeSitterSyntax.TypeScript.CallExpression)) return;

        if (AstHelper.TryGetMemberAccess(node, out _, out var propName) && propName != null)
        {
            // 1. Service Bus sender/receiver
            if (string.Equals(propName, "createSender", StringComparison.OrdinalIgnoreCase))
            {
                var queue = AstHelper.ExtractFirstStringArgument(node);
                if (!string.IsNullOrEmpty(queue) && WorkspaceConventions.IsValidTopicOrQueueName(queue))
                {
                    references.Add(new Reference(scopeSymbolId, queue, OntologyConstants.Relationships.PublishesTo));
                }
            }
            else if (string.Equals(propName, "createReceiver", StringComparison.OrdinalIgnoreCase))
            {
                var queue = AstHelper.ExtractFirstStringArgument(node);
                if (!string.IsNullOrEmpty(queue) && WorkspaceConventions.IsValidTopicOrQueueName(queue))
                {
                    references.Add(new Reference(scopeSymbolId, queue, OntologyConstants.Relationships.SubscribesTo));
                }
            }
            // 2. Blob Storage container
            else if (string.Equals(propName, "getContainerClient", StringComparison.OrdinalIgnoreCase))
            {
                var container = AstHelper.ExtractFirstStringArgument(node);
                if (!string.IsNullOrEmpty(container))
                {
                    references.Add(new Reference(scopeSymbolId, container, OntologyConstants.Relationships.DependsOn));
                }
            }
            // 3. Cosmos DB container
            else if (string.Equals(propName, "container", StringComparison.OrdinalIgnoreCase))
            {
                var container = AstHelper.ExtractFirstStringArgument(node);
                if (!string.IsNullOrEmpty(container))
                {
                    references.Add(new Reference(scopeSymbolId, container, OntologyConstants.Relationships.DependsOn));
                    references.Add(new Reference(scopeSymbolId, container, OntologyConstants.Relationships.UsesDb));
                }
            }
        }
    }

    private static bool IsCosmosContainerCall(Node node)
    {
        if (!node.Is(TreeSitterSyntax.TypeScript.CallExpression)) return false;

        if (AstHelper.TryGetMemberAccess(node, out var objNode, out var propName))
        {
            if (string.Equals(propName, "container", StringComparison.OrdinalIgnoreCase))
            {
                var container = AstHelper.ExtractFirstStringArgument(node);
                if (!string.IsNullOrEmpty(container))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
