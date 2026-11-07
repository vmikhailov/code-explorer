using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public class GraphQlYogaApolloLibraryParser : ISemanticExtension
{
    public string Name => "GraphQL Yoga & Apollo Server";
    public string Id => "graphql-yoga-apollo";
    public SemanticExtensionType Type => SemanticExtensionType.Framework;
    public IReadOnlyList<string> SupportedPatterns => ["graphql-yoga", "@apollo/server*", "apollo-server*", "type-graphql"];
    public bool IsImplemented => true;
    public LibraryRole LibraryRole => LibraryRole.WebService;

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        return TryExtractGraphQlServer(node, out _) ? OntologyConstants.NodeLabels.EntryPoint : null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx)
    {
        return TryExtractGraphQlServer(node, out var endpoint) ? $"GRAPHQL:{endpoint}" : null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
    }

    private static bool TryExtractGraphQlServer(Node node, out string endpoint)
    {
        endpoint = "/graphql";
        if (node.Is(TreeSitterSyntax.TypeScript.CallExpression))
        {
            var func = node.GetFunctionNode();
            if (func.IsValid() && func.Text is "createYoga" or "createSchema")
            {
                return true;
            }
        }
        else if (node.Is(TreeSitterSyntax.TypeScript.NewExpression))
        {
            var ctor = node.GetField(TreeSitterSyntax.Fields.Constructor) ??
                       node.Children.FirstOrDefault(c => c.Is(TreeSitterSyntax.TypeScript.Identifier));
            if (ctor.IsValid() && ctor.Text == "ApolloServer")
            {
                return true;
            }
        }

        return false;
    }
}
