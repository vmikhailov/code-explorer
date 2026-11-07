using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public class BullLegacyLibraryParser : ISemanticExtension
{
    private const string BullPrefix = "bull:";
    private const string ProcessDecoratorName = "Process";
    private const string ProcessMethodName = "process";

    public string Name => "Bull (Legacy Queue)";
    public string Id => "bull-legacy";
    public SemanticExtensionType Type => SemanticExtensionType.Queue;
    public IReadOnlyList<string> SupportedPatterns => ["bull", "@nestjs/bull"];
    public bool IsImplemented => true;
    public LibraryRole LibraryRole => LibraryRole.WorkerService;

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        return TryExtractProcessTarget(node, out _) ? OntologyConstants.NodeLabels.EntryPoint : null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx)
    {
        return TryExtractProcessTarget(node, out var jobName) ? $"BullProcess:{jobName}" : null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (TryExtractProcessTarget(node, out var jobName))
        {
            references.Add(new Reference(scopeSymbolId, BullPrefix + jobName, OntologyConstants.Relationships.SubscribesTo));
        }
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
