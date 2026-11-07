using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.Python.Libraries;

public class CeleryLibraryParser : ISemanticExtension
{
    private const string CeleryPrefix = "celery:";
    private const string SharedTaskName = "shared_task";
    private const string TaskMethod = "task";
    private const string DelayMethod = "delay";
    private const string ApplyAsyncMethod = "apply_async";
    private const string SendTaskMethod = "send_task";

    public string Name => "Celery";
    public string Id => "celery";
    public SemanticExtensionType Type => SemanticExtensionType.Queue;
    public IReadOnlyList<string> SupportedPatterns => ["celery", "celery.*"];
    public bool IsImplemented => true;
    public LibraryRole LibraryRole => LibraryRole.WorkerService;

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        return IsCeleryTaskDecorator(node, out _) ? OntologyConstants.NodeLabels.EntryPoint : null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx)
    {
        return IsCeleryTaskDecorator(node, out var taskName) ? $"Task:{taskName}" : null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (IsCeleryTaskDecorator(node, out var subscribedTask))
        {
            references.Add(new Reference(scopeSymbolId, CeleryPrefix + subscribedTask, OntologyConstants.Relationships.SubscribesTo));
            var fnName = GetDecoratedFunctionName(node);
            if (!string.IsNullOrEmpty(fnName))
            {
                references.Add(new Reference(scopeSymbolId, fnName, OntologyConstants.Relationships.Triggers));
            }
            return;
        }

        if (!node.Is(TreeSitterSyntax.Python.Call)) return;

        if (PythonAstHelper.TryGetMemberAccess(node, out var objNode, out var methodName) && !string.IsNullOrEmpty(methodName))
        {
            if (methodName is DelayMethod or ApplyAsyncMethod && objNode.IsValid())
            {
                var taskTarget = ExtractLeafIdentifier(objNode.Text);
                if (!string.IsNullOrEmpty(taskTarget))
                {
                    references.Add(new Reference(scopeSymbolId, CeleryPrefix + taskTarget, OntologyConstants.Relationships.PublishesTo));
                }
            }
            else if (methodName == SendTaskMethod)
            {
                var taskName = PythonAstHelper.ResolveCallArgument(node, 0, "name");
                if (!string.IsNullOrEmpty(taskName))
                {
                    references.Add(new Reference(scopeSymbolId, CeleryPrefix + NormalizeTaskName(taskName), OntologyConstants.Relationships.PublishesTo));
                }
            }
        }
    }

    private static bool IsCeleryTaskDecorator(Node node, out string taskName)
    {
        taskName = string.Empty;
        if (!node.Is(TreeSitterSyntax.Python.Decorator)) return false;

        var call = node.FindChildOfType(TreeSitterSyntax.Python.Call);
        var target = call.IsValid()
            ? (call.GetChildForField(TreeSitterSyntax.Fields.Function) ?? (call.Children.Count > 0 ? call.Children[0] : null))
            : node.Children.FirstOrDefault(c => c.Type != "@");

        if (!target.IsValid()) return false;

        var rawText = target.Text.TrimStart('@').Trim();
        var isTaskDecorator = rawText == SharedTaskName ||
                              rawText.EndsWith("." + SharedTaskName, StringComparison.Ordinal) ||
                              rawText.EndsWith("." + TaskMethod, StringComparison.Ordinal);

        if (!isTaskDecorator) return false;

        if (call.IsValid())
        {
            var explicitName = PythonAstHelper.ResolveCallArgument(call, -1, "name") ??
                               PythonAstHelper.ResolveCallArgument(call, -1, "queue");
            if (!string.IsNullOrEmpty(explicitName))
            {
                taskName = NormalizeTaskName(explicitName);
                return true;
            }
        }

        var fnName = GetDecoratedFunctionName(node);
        taskName = !string.IsNullOrEmpty(fnName) ? fnName : "task";
        return true;
    }

    private static string? GetDecoratedFunctionName(Node decoratorNode)
    {
        var parent = decoratorNode.Parent;
        if (!parent.IsValid()) return null;

        var fn = parent.FindChildOfType(TreeSitterSyntax.Python.FunctionDefinition);
        if (!fn.IsValid()) return null;

        var nameNode = fn.GetChildForField(TreeSitterSyntax.Fields.Name);
        return nameNode.IsValid() ? nameNode.Text : null;
    }

    private static string NormalizeTaskName(string name)
    {
        var lastDot = name.LastIndexOf('.');
        return lastDot >= 0 && lastDot < name.Length - 1 ? name[(lastDot + 1)..] : name;
    }

    private static string ExtractLeafIdentifier(string expr)
    {
        var trimmed = expr.Trim();
        var lastDot = trimmed.LastIndexOf('.');
        return lastDot >= 0 && lastDot < trimmed.Length - 1 ? trimmed[(lastDot + 1)..] : trimmed;
    }
}
