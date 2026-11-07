using System.Collections.Frozen;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public class BullLegacyLibraryParser : DeclarativeSemanticExtension
{
    private const string BullPrefix = "bull:";
    private const string ProcessDecoratorName = "Process";
    private const string ProcessMethodName = "process";

    public BullLegacyLibraryParser()
    {
        Name = "Bull (Legacy Queue)";
        Id = "bull-legacy";
        Type = SemanticExtensionType.Queue;
        SupportedPatterns = ["bull", "@nestjs/bull"];
        LibraryRole = LibraryRole.WorkerService;
        CustomAstRules =
        [
            new CustomAstRule
            {
                NodeTypes = new[] { TreeSitterSyntax.TypeScript.Decorator, TreeSitterSyntax.TypeScript.CallExpression }.ToFrozenSet(),
                MapNodeType = static (node, ctx) => TryExtractProcessTarget(node, out _) ? OntologyConstants.NodeLabels.EntryPoint : null,
                ExtractIdentifier = static (node, _, _, _) => TryExtractProcessTarget(node, out var jobName) ? $"BullProcess:{jobName}" : null,
                CollectReferences = static (node, scopeSymbolId, references, ctx) =>
                {
                    if (TryExtractProcessTarget(node, out var jobName))
                    {
                        references.Add(new Reference(scopeSymbolId, BullPrefix + jobName, OntologyConstants.Relationships.SubscribesTo));
                    }
                }
            }
        ];
    }

    private static bool TryExtractProcessTarget(Node node, out string jobName)
    {
        jobName = string.Empty;
        if (node.Is(TreeSitterSyntax.TypeScript.Decorator))
        {
            var call = node.FindChildOfType(TreeSitterSyntax.TypeScript.CallExpression);
            if (!call.IsValid()) return false;
            var func = call.GetFunctionNode();
            if (!func.IsValid() || func.Text != ProcessDecoratorName) return false;

            var arg = AstHelper.ExtractFirstStringArgument(call);
            jobName = !string.IsNullOrEmpty(arg) ? arg : "default";
            return true;
        }

        if (node.Is(TreeSitterSyntax.TypeScript.CallExpression) &&
            AstHelper.TryGetMemberAccess(node, out var objNode, out var propName) &&
            propName == ProcessMethodName &&
            objNode.IsValid() &&
            objNode.Text.Contains("queue", StringComparison.OrdinalIgnoreCase))
        {
            var arg = AstHelper.ExtractFirstStringArgument(node);
            jobName = !string.IsNullOrEmpty(arg) ? arg : objNode.Text;
            return true;
        }

        return false;
    }
}
