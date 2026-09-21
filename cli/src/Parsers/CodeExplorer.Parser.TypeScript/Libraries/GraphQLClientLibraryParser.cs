using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public class GraphQLClientLibraryParser : ILibraryParser
{
    public string Type => "api";

    public string Name => "GraphQL Client";

    public string Id => "graphql-client";

    public IReadOnlyList<string> SupportedPatterns =>
    [
        "@apollo/client",
        "graphql-request",
        "urql",
        "@urql/core"
    ];

    public bool IsImplemented => true;

    private static bool IsGraphQLTarget(Node node)
    {
        if (!node.IsValid()) return false;

        // 1. new ApolloClient({ uri: '...' }) or new HttpLink({ uri: '...' }) or createClient({ url: '...' })
        if (node.Is(TreeSitterSyntax.TypeScript.NewExpression) || node.Is(TreeSitterSyntax.TypeScript.CallExpression))
        {
            var func = node.GetField("constructor") ?? node.GetFunctionNode();
            if (!func.IsValid() || func.Text == "new")
            {
                func = node.Children.FirstOrDefault(c => c.IsAny(TreeSitterSyntax.TypeScript.Identifier, TreeSitterSyntax.TypeScript.MemberExpression, TreeSitterSyntax.Common.Identifier, "type_identifier"));
            }

            if (func.IsValid())
            {
                var name = func.Text;
                if (name is "ApolloClient" or "HttpLink" or "createClient" ||
                    name.EndsWith(".ApolloClient", StringComparison.Ordinal) ||
                    name.EndsWith(".HttpLink", StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        // 2. Direct call: request(url, query)
        if (node.Is(TreeSitterSyntax.TypeScript.CallExpression))
        {
            var func = node.GetFunctionNode();
            if (func.IsValid())
            {
                var name = func.Text.ToLowerInvariant();
                if (name is "request" or "graphqlclient.request" || name.EndsWith(".request"))
                {
                    var args = AstHelper.GetCallArguments(node);
                    if (args.Count >= 2)
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (IsGraphQLTarget(node)) return OntologyConstants.NodeLabels.ExternalService;
        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx)
    {
        if (IsGraphQLTarget(node))
        {
            var args = AstHelper.GetCallArguments(node);
            if (args.Count > 0 && args[0].IsValid())
            {
                // If argument 0 is options object { uri: '...', url: '...' }
                if (args[0].Is(TreeSitterSyntax.TypeScript.Object))
                {
                    if (AstHelper.TryGetObjectProperty(args[0], "uri", out var uriNode) && uriNode.IsValid())
                    {
                        var resolved = AstHelper.ResolveStringOrTemplate(uriNode);
                        if (!string.IsNullOrWhiteSpace(resolved)) return FormatTarget(resolved);
                    }
                    if (AstHelper.TryGetObjectProperty(args[0], "url", out var urlNode) && urlNode.IsValid())
                    {
                        var resolved = AstHelper.ResolveStringOrTemplate(urlNode);
                        if (!string.IsNullOrWhiteSpace(resolved)) return FormatTarget(resolved);
                    }
                }

                // If argument 0 is URL string: request('http://localhost:8060/graphql', ...)
                var directResolved = AstHelper.ResolveStringOrTemplate(args[0]);
                if (!string.IsNullOrWhiteSpace(directResolved))
                {
                    return FormatTarget(directResolved);
                }
            }

            return "graphql";
        }
        return null;
    }

    private static string FormatTarget(string raw)
    {
        if (Uri.TryCreate(raw, UriKind.Absolute, out var uri) && !string.IsNullOrEmpty(uri.Host))
        {
            var path = uri.AbsolutePath.TrimStart('/');
            return string.IsNullOrEmpty(path) ? uri.Host : $"{uri.Host}/{path}";
        }
        return raw.StartsWith('/') ? raw : $"/{raw}";
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx) { }
}
