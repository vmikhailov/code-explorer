using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public class BullMqLibraryParser : ILibraryParser
{
    public string Type => "cloud";
    public string Name => "BullMQ";
    public string Id => "bullmq";
    public IReadOnlyList<string> SupportedPatterns => ["bullmq", "bull"];
    public bool IsImplemented => true;

    public string? MapNodeType(Node node, ParsingContext ctx) => null;
    public string? ExtractIdentifier(Node node, ParsingContext ctx) => null;

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        // 1. Check for `new Worker('queue-name', ...)`
        if (node.Is(TreeSitterSyntax.TypeScript.NewExpression))
        {
            var ctorNode = node.GetField(TreeSitterSyntax.Fields.Constructor) ??
                           node.Children.FirstOrDefault(c => c.Is(TreeSitterSyntax.TypeScript.Identifier));
            if (ctorNode.IsValid() && string.Equals(ctorNode.Text, "Worker", StringComparison.OrdinalIgnoreCase))
            {
                var queueName = AstHelper.ExtractFirstStringArgument(node);
                if (!string.IsNullOrEmpty(queueName))
                {
                    references.Add(new Reference(scopeSymbolId, "bullmq:" + queueName, OntologyConstants.Relationships.SubscribesTo));
                }
            }
        }
        // 2. Check for `queue.add('job-name', ...)`
        else if (node.Is(TreeSitterSyntax.TypeScript.CallExpression))
        {
            if (AstHelper.TryGetMemberAccess(node, out var objNode, out var propName))
            {
                if (string.Equals(propName, "add", StringComparison.OrdinalIgnoreCase))
                {
                    var target = objNode.IsValid() ? objNode.Text : "queue";
                    var jobName = AstHelper.ExtractFirstStringArgument(node);
                    var identifier = !string.IsNullOrEmpty(jobName) ? $"{target}:{jobName}" : target;

                    references.Add(new Reference(scopeSymbolId, "bullmq:" + identifier, OntologyConstants.Relationships.PublishesTo));
                }
            }
        }
    }
}
