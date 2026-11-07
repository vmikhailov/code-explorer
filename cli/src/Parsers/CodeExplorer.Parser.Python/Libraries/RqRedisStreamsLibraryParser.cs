using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.Python.Libraries;

public class RqRedisStreamsLibraryParser : ISemanticExtension
{
    private const string RqPrefix = "rq:";
    private const string RedisStreamPrefix = "redis-stream:";
    private const string FuncArgKeyword = "f";
    private const string NameKeyword = "name";
    private const string StreamsKeyword = "streams";
    private const string QueueKeyword = "queue";
    private const string DictionaryNodeType = "dictionary";
    private const string PairNodeType = "pair";

    public string Name => "RQ & Redis Streams";
    public string Id => "rq-redis-streams";
    public SemanticExtensionType Type => SemanticExtensionType.Queue;
    public IReadOnlyList<string> SupportedPatterns => ["rq", "rq.*", "django_rq", "django-rq"];
    public bool IsImplemented => true;
    public LibraryRole LibraryRole => LibraryRole.WorkerService;

    private static readonly HashSet<string> EnqueueMethods = new(StringComparer.Ordinal)
    {
        "enqueue", "enqueue_call", "enqueue_in", "enqueue_at", "enqueue_many"
    };

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        return IsRqJobDecorator(node, out _) ? OntologyConstants.NodeLabels.EntryPoint : null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx)
    {
        return IsRqJobDecorator(node, out var jobName) ? $"Job:{jobName}" : null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (IsRqJobDecorator(node, out var jobName))
        {
            references.Add(new Reference(scopeSymbolId, RqPrefix + jobName, OntologyConstants.Relationships.SubscribesTo));
            return;
        }

        if (!node.Is(TreeSitterSyntax.Python.Call)) return;

        if (!PythonAstHelper.TryGetMemberAccess(node, out _, out var methodName) || string.IsNullOrEmpty(methodName))
        {
            return;
        }

        if (EnqueueMethods.Contains(methodName))
        {
            var fnArg = PythonAstHelper.ResolveCallArgument(node, 0, FuncArgKeyword);
            if (!string.IsNullOrEmpty(fnArg))
            {
                var leaf = ExtractLeafIdentifier(fnArg);
                references.Add(new Reference(scopeSymbolId, RqPrefix + leaf, OntologyConstants.Relationships.PublishesTo));
            }
        }
        else if (methodName == "xadd")
        {
            var stream = PythonAstHelper.ResolveCallArgument(node, 0, NameKeyword);
            if (!string.IsNullOrEmpty(stream))
            {
                references.Add(new Reference(scopeSymbolId, RedisStreamPrefix + stream, OntologyConstants.Relationships.PublishesTo));
            }
        }
        else if (methodName is "xread" or "xreadgroup")
        {
            var stream = ExtractStreamKeyFromReadCall(node);
            if (!string.IsNullOrEmpty(stream))
            {
                references.Add(new Reference(scopeSymbolId, RedisStreamPrefix + stream, OntologyConstants.Relationships.SubscribesTo));
            }
        }
    }

    private static string? ExtractStreamKeyFromReadCall(Node callNode)
    {
        var streamsArg = PythonAstHelper.GetKeywordArgument(callNode, StreamsKeyword);
        if (!streamsArg.IsValid())
        {
            var args = PythonAstHelper.GetCallArguments(callNode);
            streamsArg = args.FirstOrDefault(a => a.Is(DictionaryNodeType));
        }

        if (streamsArg.IsValid() && streamsArg.Is(DictionaryNodeType))
        {
            var pair = streamsArg.FindChildOfType(PairNodeType);
            if (pair.IsValid() && pair.Children.Count > 0)
            {
                return PythonAstHelper.ResolveStringOrVariable(pair.Children[0]);
            }
        }

        return PythonAstHelper.ResolveCallArgument(callNode, 0, StreamsKeyword);
    }

    private static bool IsRqJobDecorator(Node node, out string jobName)
    {
        jobName = string.Empty;
        if (!node.Is(TreeSitterSyntax.Python.Decorator)) return false;

        var call = node.FindChildOfType(TreeSitterSyntax.Python.Call);
        if (!call.IsValid()) return false;

        var func = call.GetChildForField(TreeSitterSyntax.Fields.Function) ??
                   (call.Children.Count > 0 ? call.Children[0] : null);
        if (!func.IsValid()) return false;

        var text = func.Text.TrimStart('@').Trim();
        if (text != "job" && !text.EndsWith(".job", StringComparison.Ordinal)) return false;

        var parent = node.Parent;
        var fn = parent.IsValid() ? parent.FindChildOfType(TreeSitterSyntax.Python.FunctionDefinition) : default;
        var nameNode = fn.IsValid() ? fn.GetChildForField(TreeSitterSyntax.Fields.Name) : default;
        jobName = nameNode.IsValid() && !string.IsNullOrEmpty(nameNode.Text)
            ? nameNode.Text
            : (PythonAstHelper.ResolveCallArgument(call, 0, QueueKeyword) ?? "default");
        return true;
    }

    private static string ExtractLeafIdentifier(string expr)
    {
        var trimmed = expr.Trim().Trim('\'', '"');
        var lastDot = trimmed.LastIndexOf('.');
        return lastDot >= 0 && lastDot < trimmed.Length - 1 ? trimmed[(lastDot + 1)..] : trimmed;
    }
}
