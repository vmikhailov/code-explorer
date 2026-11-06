using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.Go.Libraries;

public class NatsGoLibraryParser : ISemanticExtension
{
    public SemanticExtensionType Type => SemanticExtensionType.Queue;
    public string Name => "NATS";
    public string Id => "nats";
    public IReadOnlyList<string> SupportedPatterns =>
    [
        "github.com/nats-io/nats.go*",
        "*nats-io*",
        "*nats_rpc*"
    ];
    public bool IsImplemented => true;
    public LibraryRole LibraryRole => LibraryRole.MessageBroker;

    public string? MapNodeType(Node node, ParsingContext ctx) => null;
    public string? ExtractIdentifier(Node node, ParsingContext ctx) => null;

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        // 0. Dispatcher index assignment: routes["v1.task.create"] = r.createTask()
        if (GoAstHelper.TryGetIndexAssignment(node, out var operandText, out var indexKey))
        {
            if (operandText.Contains("route", StringComparison.OrdinalIgnoreCase) ||
                operandText.Contains("handler", StringComparison.OrdinalIgnoreCase) ||
                operandText.Contains("sub", StringComparison.OrdinalIgnoreCase))
            {
                references.Add(new Reference(scopeSymbolId, "nats:" + indexKey, OntologyConstants.Relationships.SubscribesTo));
                return;
            }
        }

        if (!node.Is(TreeSitterSyntax.Go.CallExpression)) return;

        var func = node.GetFunctionNode();
        if (!func.IsValid()) return;

        var funcText = func.Text;
        var args = GoAstHelper.GetCallArguments(node);

        // 1. Publish / PublishMsg: nc.Publish(subject, data)
        if (funcText.EndsWith(".Publish", StringComparison.Ordinal) ||
            funcText.EndsWith(".PublishMsg", StringComparison.Ordinal))
        {
            if (args.Count > 0)
            {
                var subject = GoAstHelper.ResolveStringOrVariable(args[0]);
                if (!string.IsNullOrEmpty(subject))
                {
                    references.Add(new Reference(scopeSymbolId, "nats:" + subject, OntologyConstants.Relationships.PublishesTo));
                }
            }
        }
        // 2. Request / RequestWithContext / RemoteCall: nc.Request(subject, data, timeout)
        else if (funcText.EndsWith(".Request", StringComparison.Ordinal) ||
                 funcText.EndsWith(".RequestWithContext", StringComparison.Ordinal) ||
                 funcText.EndsWith(".RemoteCall", StringComparison.Ordinal))
        {
            if (args.Count > 0)
            {
                var subject = GoAstHelper.ResolveStringOrVariable(args[0]);
                if (!string.IsNullOrEmpty(subject))
                {
                    references.Add(new Reference(scopeSymbolId, "nats:" + subject, OntologyConstants.Relationships.PublishesTo));
                }
            }
        }
        // 3. Subscribe / QueueSubscribe: nc.Subscribe(subj, cb) or nc.QueueSubscribe(subj, queue, cb)
        else if (funcText.EndsWith(".Subscribe", StringComparison.Ordinal) ||
                 funcText.EndsWith(".QueueSubscribe", StringComparison.Ordinal) ||
                 funcText.EndsWith(".SubscribeSync", StringComparison.Ordinal))
        {
            if (args.Count > 0)
            {
                var subject = GoAstHelper.ResolveStringOrVariable(args[0]);
                if (!string.IsNullOrEmpty(subject))
                {
                    references.Add(new Reference(scopeSymbolId, "nats:" + subject, OntologyConstants.Relationships.SubscribesTo));
                }
            }
        }
    }
}
