using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public class AzureSdkLibraryParser : ISemanticExtension
{
    public string Type => OntologyConstants.LibraryTypes.Cloud;
    public string Name => "Azure";
    public string Id => "azure";
    public IReadOnlyList<string> SupportedPatterns => ["@azure/*", "@azure/service-bus", "@azure/storage-blob", "@azure/cosmos"];
    public bool IsImplemented => true;
    public LibraryRole LibraryRole => LibraryRole.CloudSdk;

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (IsCosmosContainerCall(node))
        {
            return OntologyConstants.NodeLabels.Query;
        }
        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx)
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

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
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
