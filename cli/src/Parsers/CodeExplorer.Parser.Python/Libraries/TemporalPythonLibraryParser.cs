using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.Python.Libraries;

public class TemporalPythonLibraryParser : ISemanticExtension
{
    private const string TemporalPrefix = "temporal:";

    public string Name => "Temporal (Python SDK)";
    public string Id => "temporal-python";
    public SemanticExtensionType Type => SemanticExtensionType.Queue;
    public IReadOnlyList<string> SupportedPatterns => ["temporalio", "temporalio.*"];
    public bool IsImplemented => true;
    public LibraryRole LibraryRole => LibraryRole.WorkerService;

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        return TryExtractTemporalDecorator(node, out _) ? OntologyConstants.NodeLabels.EntryPoint : null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx)
    {
        return TryExtractTemporalDecorator(node, out var target) ? $"Temporal:{target}" : null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (TryExtractTemporalDecorator(node, out var defTarget))
        {
            references.Add(new Reference(scopeSymbolId, TemporalPrefix + defTarget, OntologyConstants.Relationships.SubscribesTo));
            return;
        }

        if (!node.Is(TreeSitterSyntax.Python.Call)) return;

        if (PythonAstHelper.TryGetMemberAccess(node, out _, out var methodName) &&
            methodName is "execute_activity" or "start_activity" or "execute_workflow" or "start_workflow" or "execute_child_workflow")
        {
            var arg0 = PythonAstHelper.ResolveCallArgument(node, 0);
            if (!string.IsNullOrEmpty(arg0))
            {
                var leaf = arg0.Contains('.') ? arg0[(arg0.LastIndexOf('.') + 1)..] : arg0;
                references.Add(new Reference(scopeSymbolId, TemporalPrefix + leaf, OntologyConstants.Relationships.PublishesTo));
            }
        }
    }

    private static bool TryExtractTemporalDecorator(Node node, out string targetName)
    {
        targetName = string.Empty;
        if (!node.Is(TreeSitterSyntax.Python.Decorator)) return false;

        var raw = node.Text.TrimStart('@').Trim();
        var isActivity = raw.StartsWith("activity.defn", StringComparison.Ordinal);
        var isWorkflowRun = raw.StartsWith("workflow.run", StringComparison.Ordinal);
        if (!isActivity && !isWorkflowRun) return false;

        var parent = node.Parent;
        if (!parent.IsValid()) return false;

        if (isWorkflowRun)
        {
            var cls = FindParentClass(parent);
            var clsName = cls.IsValid() ? cls.GetChildForField(TreeSitterSyntax.Fields.Name) : default;
            if (clsName.IsValid() && !string.IsNullOrEmpty(clsName.Text))
            {
                targetName = clsName.Text;
                return true;
            }
        }

        var fn = parent.FindChildOfType(TreeSitterSyntax.Python.FunctionDefinition);
        var fnName = fn.IsValid() ? fn.GetChildForField(TreeSitterSyntax.Fields.Name) : default;
        if (!fnName.IsValid() || string.IsNullOrEmpty(fnName.Text)) return false;

        targetName = fnName.Text;
        return true;
    }

    private static Node? FindParentClass(Node start)
    {
        var curr = start;
        while (curr.IsValid())
        {
            if (curr.Is(TreeSitterSyntax.Python.ClassDefinition)) return curr;
            curr = curr.Parent;
        }
        return null;
    }
}
