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
                MapNodeType = static (node, ctx) => _fetchCallSelector.Matches(node) ? OntologyConstants.NodeLabels.ExternalService : null,
                ExtractIdentifier = static (node, ctx, projectName, rel) =>
                {
                    if (_fetchCallSelector.Matches(node))
                    {
                        var firstArg = _callFirstStringArgSelector.Select(node);
                        if (firstArg.IsValid())
                        {
                            var projectContext = projectName ?? ctx?.WorkspaceId;
                            var resolved = AstHelper.ResolveStringOrTemplate(firstArg, projectContext);
                            if (!string.IsNullOrWhiteSpace(resolved))
                            {
                                return resolved;
                            }
                        }
                        return "http:unknown-service";
                    }
                    return null;
                }
            }
        ];
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
                .HasChild(TreeSitterSyntax.Fields.Property, NodeSelector.New().Text("callApi"))
        ));

    private static readonly NodeSelector _callFirstStringArgSelector = NodeSelector.New()
        .GetChildForField(TreeSitterSyntax.Fields.Arguments)
        .FirstChild;

    public override IReadOnlyDictionary<string, NodeSelector> Selectors => new Dictionary<string, NodeSelector>
    {
        { OntologyConstants.NodeLabels.ExternalService, _fetchCallSelector }
    };
}
