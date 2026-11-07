using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.Python.Libraries;

public class DramatiqLibraryParser : ISemanticExtension
{
    private const string DramatiqPrefix = "dramatiq:";
    private const string ActorDecorator = "actor";
    private const string SendMethod = "send";
    private const string SendWithOptionsMethod = "send_with_options";
    private const string ActorNameKeyword = "actor_name";
    private const string QueueNameKeyword = "queue_name";

    public string Name => "Dramatiq";
    public string Id => "dramatiq";
    public SemanticExtensionType Type => SemanticExtensionType.Queue;
    public IReadOnlyList<string> SupportedPatterns => ["dramatiq", "dramatiq.*"];
    public bool IsImplemented => true;
    public LibraryRole LibraryRole => LibraryRole.WorkerService;

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        return IsDramatiqActorDecorator(node, out _) ? OntologyConstants.NodeLabels.EntryPoint : null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx)
    {
        return IsDramatiqActorDecorator(node, out var actorName) ? $"Actor:{actorName}" : null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (IsDramatiqActorDecorator(node, out var actorName))
        {
            references.Add(new Reference(scopeSymbolId, DramatiqPrefix + actorName, OntologyConstants.Relationships.SubscribesTo));
            var fnName = GetDecoratedFunctionName(node);
            if (!string.IsNullOrEmpty(fnName))
            {
                references.Add(new Reference(scopeSymbolId, fnName, OntologyConstants.Relationships.Triggers));
            }
            return;
        }

        if (!node.Is(TreeSitterSyntax.Python.Call)) return;

        if (PythonAstHelper.TryGetMemberAccess(node, out var objNode, out var methodName) &&
            methodName is SendMethod or SendWithOptionsMethod &&
            objNode.IsValid())
        {
            var target = ExtractLeafIdentifier(objNode.Text);
            if (!string.IsNullOrEmpty(target) && !IsGenericReceiver(target))
            {
                references.Add(new Reference(scopeSymbolId, DramatiqPrefix + target, OntologyConstants.Relationships.PublishesTo));
            }
        }
    }

    private static bool IsDramatiqActorDecorator(Node node, out string actorName)
    {
        actorName = string.Empty;
        if (!node.Is(TreeSitterSyntax.Python.Decorator)) return false;

        var call = node.FindChildOfType(TreeSitterSyntax.Python.Call);
        var target = call.IsValid()
            ? (call.GetChildForField(TreeSitterSyntax.Fields.Function) ?? (call.Children.Count > 0 ? call.Children[0] : null))
            : node.Children.FirstOrDefault(c => c.Type != "@");

        if (!target.IsValid()) return false;

        var rawText = target.Text.TrimStart('@').Trim();
        var isActor = rawText == ActorDecorator ||
                      rawText.EndsWith("." + ActorDecorator, StringComparison.Ordinal);

        if (!isActor) return false;

        if (call.IsValid())
        {
            var explicitName = PythonAstHelper.ResolveCallArgument(call, -1, ActorNameKeyword) ??
                               PythonAstHelper.ResolveCallArgument(call, -1, QueueNameKeyword);
            if (!string.IsNullOrEmpty(explicitName))
            {
                actorName = explicitName;
                return true;
            }
        }

        var fnName = GetDecoratedFunctionName(node);
        actorName = !string.IsNullOrEmpty(fnName) ? fnName : ActorDecorator;
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

    private static string ExtractLeafIdentifier(string expr)
    {
        var trimmed = expr.Trim();
        var lastDot = trimmed.LastIndexOf('.');
        return lastDot >= 0 && lastDot < trimmed.Length - 1 ? trimmed[(lastDot + 1)..] : trimmed;
    }

    private static bool IsGenericReceiver(string name)
    {
        return name is "self" or "cls" or "socket" or "ws" or "websocket" or "client" or "producer";
    }
}
