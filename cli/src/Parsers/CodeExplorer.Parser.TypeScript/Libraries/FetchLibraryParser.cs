using System.Collections.Frozen;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public class FetchLibraryParser : DeclarativeSemanticExtension
{
    public FetchLibraryParser()
    {
        Name = "Fetch";
        Id = "fetch";
        Type = SemanticExtensionType.ApiClient;
        SupportedPatterns = ["node-fetch", "got", "superagent", "cross-fetch", "isomorphic-fetch"];
        IsBuiltIn = true;
        CustomAstRules =
        [
            new CustomAstRule
            {
                NodeTypes = new[] { TreeSitterSyntax.TypeScript.CallExpression }.ToFrozenSet(),
                MapNodeType = static (node, ctx) => IsFetchCallNode(node) ? OntologyConstants.NodeLabels.ExternalService : null,
                ExtractIdentifier = static (node, ctx, projectName, rel) =>
                {
                    if (IsFetchCallNode(node))
                    {
                        var args = AstHelper.GetCallArguments(node);
                        var firstArg = args.Count > 0 ? args[0] : _callFirstStringArgSelector.Select(node);
                        if (firstArg.IsValid())
                        {
                            var projectContext = projectName ?? ctx?.WorkspaceId;
                            var resolved = AstHelper.ResolveStringOrTemplate(firstArg, projectContext);
                            if (!string.IsNullOrWhiteSpace(resolved) && resolved != "unknown-service")
                            {
                                return resolved;
                            }
                        }
                        var fallback = AstHelper.ExtractEnclosingServiceFallback(node, rel);
                        if (!string.IsNullOrWhiteSpace(fallback))
                        {
                            return fallback;
                        }
                        return "http:unknown-service";
                    }
                    return null;
                }
            }
        ];
    }

    private const string PropFetch = "fetch";
    private const string PropCallApi = "callApi";

    private static bool IsFetchCallNode(Node node)
    {
        if (!node.IsValid() || !node.Is(TreeSitterSyntax.TypeScript.CallExpression)) return false;
        var func = node.GetFunctionNode();
        if (!func.IsValid()) return false;

        if (func.Is(TreeSitterSyntax.Common.Identifier))
        {
            var span = func.AsSpan();
            return span.Equals("fetch", StringComparison.OrdinalIgnoreCase) ||
                   span.Equals("nodeFetch", StringComparison.OrdinalIgnoreCase) ||
                   span.Equals("got", StringComparison.OrdinalIgnoreCase) ||
                   span.Equals("superagent", StringComparison.OrdinalIgnoreCase) ||
                   span.Equals("restRequest", StringComparison.OrdinalIgnoreCase) ||
                   span.Equals("apiClient", StringComparison.OrdinalIgnoreCase);
        }

        if (func.Is(TreeSitterSyntax.TypeScript.MemberExpression))
        {
            var prop = func.GetChildForField(TreeSitterSyntax.Fields.Property);
            if (!prop.IsValid()) return false;
            var propSpan = prop.AsSpan();

            if (propSpan.Equals(PropCallApi, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (propSpan.Equals(PropFetch, StringComparison.OrdinalIgnoreCase))
            {
                var obj = func.GetChildForField(TreeSitterSyntax.Fields.Object);
                if (!obj.IsValid()) return false;
                var objSpan = obj.AsSpan();
                return objSpan.EndsWith("client", StringComparison.OrdinalIgnoreCase) ||
                       objSpan.EndsWith("fetch", StringComparison.OrdinalIgnoreCase);
            }

            if (propSpan.Equals("get", StringComparison.OrdinalIgnoreCase) ||
                propSpan.Equals("post", StringComparison.OrdinalIgnoreCase) ||
                propSpan.Equals("put", StringComparison.OrdinalIgnoreCase) ||
                propSpan.Equals("delete", StringComparison.OrdinalIgnoreCase) ||
                propSpan.Equals("request", StringComparison.OrdinalIgnoreCase) ||
                propSpan.Equals("patch", StringComparison.OrdinalIgnoreCase) ||
                propSpan.Equals("head", StringComparison.OrdinalIgnoreCase))
            {
                var obj = func.GetChildForField(TreeSitterSyntax.Fields.Object);
                if (!obj.IsValid()) return false;
                var objSpan = obj.AsSpan();
                return objSpan.Equals("got", StringComparison.OrdinalIgnoreCase) ||
                       objSpan.Equals("superagent", StringComparison.OrdinalIgnoreCase) ||
                       objSpan.Equals("request", StringComparison.OrdinalIgnoreCase) ||
                       objSpan.Equals("http", StringComparison.OrdinalIgnoreCase) ||
                       objSpan.Equals("https", StringComparison.OrdinalIgnoreCase) ||
                       objSpan.Equals("api", StringComparison.OrdinalIgnoreCase) ||
                       objSpan.Equals("apiClient", StringComparison.OrdinalIgnoreCase) ||
                       objSpan.Equals("this.apiClient", StringComparison.OrdinalIgnoreCase) ||
                       objSpan.Equals("client", StringComparison.OrdinalIgnoreCase);
            }
        }

        return false;
    }

    private static readonly NodeSelector _fetchCallSelector = NodeSelector.New()
        .HasType(TreeSitterSyntax.TypeScript.CallExpression)
        .FunctionNode
        .Where(NodeSelector.Or(
            NodeSelector.New().HasType(TreeSitterSyntax.Common.Identifier).Text("fetch|nodeFetch|got|superagent|restRequest|apiClient"),
            NodeSelector.New()
                .HasType(TreeSitterSyntax.TypeScript.MemberExpression)
                .HasChild(TreeSitterSyntax.Fields.Object, NodeSelector.New().Text("got|superagent|request|http|https|api|apiClient|this\\.apiClient|client"))
                .HasChild(TreeSitterSyntax.Fields.Property, NodeSelector.New().Text("get|post|put|delete|request|patch|head|callApi")),
            NodeSelector.New()
                .HasType(TreeSitterSyntax.TypeScript.MemberExpression)
                .HasChild(TreeSitterSyntax.Fields.Object, NodeSelector.New().Text(".*(?:Client|client|Fetch|fetch)"))
                .HasChild(TreeSitterSyntax.Fields.Property, NodeSelector.New().Text(PropFetch)),
            NodeSelector.New()
                .HasType(TreeSitterSyntax.TypeScript.MemberExpression)
                .HasChild(TreeSitterSyntax.Fields.Property, NodeSelector.New().Text(PropCallApi))
        ));

    private static readonly NodeSelector _callFirstStringArgSelector = NodeSelector.New()
        .GetChildForField(TreeSitterSyntax.Fields.Arguments)
        .FirstChild;

    public override IReadOnlyDictionary<string, NodeSelector> Selectors => new Dictionary<string, NodeSelector>
    {
        { OntologyConstants.NodeLabels.ExternalService, _fetchCallSelector }
    };
}
