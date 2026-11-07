using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.Go.Libraries;

public class AsynqLibraryParser : ISemanticExtension
{
    private const string AsynqPrefix = "asynq:";

    public string Name => "Asynq";
    public string Id => "asynq";
    public SemanticExtensionType Type => SemanticExtensionType.Queue;
    public IReadOnlyList<string> SupportedPatterns =>
    [
        "github.com/hibiken/asynq*",
        "*hibiken/asynq*"
    ];
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
        if (args.Count == 0) return;

        // Producer: asynq.NewTask("email:deliver", payload) or client.Enqueue(task)
        if (funcText.EndsWith("asynq.NewTask", StringComparison.Ordinal))
        {
            var taskType = GoAstHelper.ResolveStringOrVariable(args[0]);
            if (!string.IsNullOrEmpty(taskType))
            {
                references.Add(new Reference(scopeSymbolId, AsynqPrefix + taskType, OntologyConstants.Relationships.PublishesTo));
            }
        }
        // Consumer: mux.HandleFunc("email:deliver", handler) or mux.Handle("email:deliver", processor)
        else if ((funcText.EndsWith(".HandleFunc", StringComparison.Ordinal) || funcText.EndsWith(".Handle", StringComparison.Ordinal)) &&
                 funcText.Contains("mux", StringComparison.OrdinalIgnoreCase))
        {
            var pattern = GoAstHelper.ResolveStringOrVariable(args[0]);
            if (!string.IsNullOrEmpty(pattern) && !pattern.StartsWith('/'))
            {
                references.Add(new Reference(scopeSymbolId, AsynqPrefix + pattern, OntologyConstants.Relationships.SubscribesTo));
            }
        }
    }
}
