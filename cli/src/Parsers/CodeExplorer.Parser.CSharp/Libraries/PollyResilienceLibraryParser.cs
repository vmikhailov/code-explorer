using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.CSharp.Libraries;

public class PollyResilienceLibraryParser : ISemanticExtension
{
    public string Name => "Polly & .NET Resilience";
    public string Id => "polly";
    public SemanticExtensionType Type => SemanticExtensionType.Library;
    public IReadOnlyList<string> SupportedPatterns =>
    [
        "Polly",
        "Polly.*",
        "Microsoft.Extensions.Http.Resilience*",
        "Microsoft.Extensions.Resilience*"
    ];
    public bool IsImplemented => true;
    public LibraryRole LibraryRole => LibraryRole.General;

    private static readonly HashSet<string> ResilienceMethods = new(StringComparer.Ordinal)
    {
        "WaitAndRetryAsync", "WaitAndRetry", "RetryAsync", "Retry",
        "CircuitBreakerAsync", "CircuitBreaker", "AddRetry", "AddCircuitBreaker",
        "AddTimeout", "AddStandardResilienceHandler", "AddResilienceHandler"
    };

    public string? MapNodeType(Node node, ParsingContext ctx) => null;
    public string? ExtractIdentifier(Node node, ParsingContext ctx) => null;

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (!node.Is(TreeSitterSyntax.CSharp.InvocationExpression)) return;

        var func = node.GetFunctionNode();
        if (!func.IsValid() || !func.Is(TreeSitterSyntax.CSharp.MemberAccessExpression)) return;

        var nameNode = func.GetField(TreeSitterSyntax.Fields.Name) ?? (func.Children.Count > 0 ? func.Children[^1] : default);
        if (!nameNode.IsValid()) return;

        var idChild = nameNode.Is(TreeSitterSyntax.CSharp.GenericName)
            ? nameNode.FindChildOfType(TreeSitterSyntax.Common.Identifier)
            : default;
        var methodName = idChild.IsValid() ? idChild.Text : nameNode.Text;

        if (ResilienceMethods.Contains(methodName))
        {
            references.Add(new Reference(scopeSymbolId, "polly:" + methodName, OntologyConstants.Relationships.Calls));
        }
    }
}
