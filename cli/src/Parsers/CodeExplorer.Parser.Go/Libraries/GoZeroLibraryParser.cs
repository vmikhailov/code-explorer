using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.Go.Libraries;

public class GoZeroLibraryParser : ISemanticExtension
{
    private const string ZrpcClientTarget = "zrpc:client";

    public string Name => "go-zero";
    public string Id => "go-zero";
    public SemanticExtensionType Type => SemanticExtensionType.Framework;
    public IReadOnlyList<string> SupportedPatterns => ["github.com/zeromicro/go-zero*"];
    public bool IsImplemented => true;
    public LibraryRole LibraryRole => LibraryRole.WebService;

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        return TryExtractGoZeroServerCall(node, out _) ? OntologyConstants.NodeLabels.EntryPoint : null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx)
    {
        return TryExtractGoZeroServerCall(node, out var id) ? id : null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (!node.Is(TreeSitterSyntax.Go.CallExpression)) return;

        var func = node.GetFunctionNode();
        if (!func.IsValid()) return;

        var funcText = func.Text;
        if (funcText.EndsWith("zrpc.MustNewClient", StringComparison.Ordinal) ||
            funcText.EndsWith("zrpc.NewClient", StringComparison.Ordinal))
        {
            references.Add(new Reference(scopeSymbolId, ZrpcClientTarget, OntologyConstants.Relationships.Calls));
        }
    }

    private static bool TryExtractGoZeroServerCall(Node node, out string identifier)
    {
        identifier = string.Empty;
        if (!node.Is(TreeSitterSyntax.Go.CallExpression)) return false;

        var func = node.GetFunctionNode();
        if (!func.IsValid()) return false;

        var funcText = func.Text;
        if (funcText.EndsWith(".AddRoutes", StringComparison.Ordinal) ||
            funcText.EndsWith(".AddRoute", StringComparison.Ordinal))
        {
            identifier = "REST:/go-zero/routes";
            return true;
        }

        if (funcText.EndsWith("zrpc.MustNewServer", StringComparison.Ordinal))
        {
            identifier = "gRPC:/zrpc";
            return true;
        }

        return false;
    }
}
