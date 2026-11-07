using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public class TemporalTypeScriptLibraryParser : ISemanticExtension
{
    private const string TemporalPrefix = "temporal:";

    public string Name => "Temporal (TypeScript SDK)";
    public string Id => "temporal-ts";
    public SemanticExtensionType Type => SemanticExtensionType.Queue;
    public IReadOnlyList<string> SupportedPatterns =>
    [
        "@temporalio/workflow*",
        "@temporalio/client*",
        "@temporalio/worker*",
        "@temporalio/activity*"
    ];
    public bool IsImplemented => true;
    public LibraryRole LibraryRole => LibraryRole.WorkerService;

    public string? MapNodeType(Node node, ParsingContext ctx) => null;
    public string? ExtractIdentifier(Node node, ParsingContext ctx) => null;

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (!node.Is(TreeSitterSyntax.TypeScript.CallExpression)) return;

        var func = node.GetFunctionNode();
        if (!func.IsValid()) return;

        var funcText = func.Text;
        if (funcText is "proxyActivities" or "proxyLocalActivities")
        {
            references.Add(new Reference(scopeSymbolId, TemporalPrefix + "activities", OntologyConstants.Relationships.PublishesTo));
            return;
        }

        if (funcText is "executeChild" or "startChild")
        {
            var args = AstHelper.GetCallArguments(node);
            var childTarget = args.Count > 0 ? args[0].Text.Trim('"', '\'', '`') : "child-workflow";
            references.Add(new Reference(scopeSymbolId, TemporalPrefix + childTarget, OntologyConstants.Relationships.PublishesTo));
            return;
        }

        if (AstHelper.TryGetMemberAccess(node, out var objNode, out var propName) &&
            propName is "start" or "execute" or "signalWithStart" &&
            objNode.IsValid() &&
            objNode.Text.Contains("workflow", StringComparison.OrdinalIgnoreCase))
        {
            var args = AstHelper.GetCallArguments(node);
            var wfTarget = args.Count > 0 ? args[0].Text.Trim('"', '\'', '`') : "workflow";
            references.Add(new Reference(scopeSymbolId, TemporalPrefix + wfTarget, OntologyConstants.Relationships.PublishesTo));
        }
    }
}
