using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.Go.Libraries;

public class TemporalGoLibraryParser : ISemanticExtension
{
    private const string TemporalPrefix = "temporal:";

    public string Name => "Temporal (Go SDK)";
    public string Id => "temporal-go";
    public SemanticExtensionType Type => SemanticExtensionType.Queue;
    public IReadOnlyList<string> SupportedPatterns => ["go.temporal.io/sdk*"];
    public bool IsImplemented => true;
    public LibraryRole LibraryRole => LibraryRole.WorkerService;

    public string? MapNodeType(Node node, ParsingContext ctx) => null;
    public string? ExtractIdentifier(Node node, ParsingContext ctx) => null;

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (!node.Is(TreeSitterSyntax.Go.CallExpression)) return;

        var func = node.GetFunctionNode();
        if (!func.IsValid()) return;

        var funcText = func.Text;
        var args = GoAstHelper.GetCallArguments(node);

        if ((funcText.EndsWith("workflow.ExecuteActivity", StringComparison.Ordinal) ||
             funcText.EndsWith("workflow.ExecuteChildWorkflow", StringComparison.Ordinal) ||
             funcText.EndsWith(".ExecuteWorkflow", StringComparison.Ordinal)) &&
            args.Count >= 2)
        {
            var target = ExtractTargetIdentifier(args[1].Text);
            if (!string.IsNullOrEmpty(target))
            {
                references.Add(new Reference(scopeSymbolId, TemporalPrefix + target, OntologyConstants.Relationships.PublishesTo));
            }
        }
        else if ((funcText.EndsWith(".RegisterWorkflow", StringComparison.Ordinal) ||
                  funcText.EndsWith(".RegisterActivity", StringComparison.Ordinal)) &&
                 args.Count >= 1)
        {
            var target = ExtractTargetIdentifier(args[0].Text);
            if (!string.IsNullOrEmpty(target))
            {
                references.Add(new Reference(scopeSymbolId, TemporalPrefix + target, OntologyConstants.Relationships.SubscribesTo));
            }
        }
    }

    private static string ExtractTargetIdentifier(string raw)
    {
        var trimmed = raw.Trim('"', '\'', '`', ' ');
        var dot = trimmed.LastIndexOf('.');
        return dot >= 0 && dot < trimmed.Length - 1 ? trimmed[(dot + 1)..] : trimmed;
    }
}
