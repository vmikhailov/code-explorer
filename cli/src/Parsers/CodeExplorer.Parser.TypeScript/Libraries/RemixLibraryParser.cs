using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public class RemixLibraryParser : ISemanticExtension
{
    public string Name => "Remix / React Router";
    public string Id => "remix";
    public SemanticExtensionType Type => SemanticExtensionType.Framework;
    public IReadOnlyList<string> SupportedPatterns => ["@remix-run/*", "react-router", "react-router-dom", "@react-router/*"];
    public bool IsImplemented => true;
    public LibraryRole LibraryRole => LibraryRole.WebService;

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        return TryExtractRemixRouteHandler(node, out _, out _) ? OntologyConstants.NodeLabels.EntryPoint : null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx)
    {
        return TryExtractRemixRouteHandler(node, out var method, out var fnName) ? $"{method}:/{fnName}" : null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (TryExtractRemixRouteHandler(node, out _, out var fnName))
        {
            references.Add(new Reference(scopeSymbolId, fnName, OntologyConstants.Relationships.Triggers));
        }
    }

    private static bool TryExtractRemixRouteHandler(Node node, out string httpMethod, out string fnName)
    {
        httpMethod = "GET";
        fnName = string.Empty;

        if (node.Is(TreeSitterSyntax.TypeScript.FunctionDeclaration))
        {
            var nameNode = node.GetField(TreeSitterSyntax.Fields.Name);
            if (!nameNode.IsValid()) return false;
            var name = nameNode.Text;
            if (name is "loader" or "clientLoader")
            {
                httpMethod = "GET";
                fnName = name;
                return true;
            }
            if (name is "action" or "clientAction")
            {
                httpMethod = "POST";
                fnName = name;
                return true;
            }
        }

        return false;
    }
}
