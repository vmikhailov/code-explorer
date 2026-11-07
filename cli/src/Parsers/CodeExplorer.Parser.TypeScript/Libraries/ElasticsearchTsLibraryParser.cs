using System.Collections.Frozen;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public class ElasticsearchTsLibraryParser : DeclarativeSemanticExtension
{
    private static readonly HashSet<string> EsMethods = new(StringComparer.Ordinal)
    {
        "search", "index", "get", "update", "delete", "bulk", "count", "mget", "msearch"
    };

    public ElasticsearchTsLibraryParser()
    {
        Type = SemanticExtensionType.SearchDb;
        Name = "Elasticsearch";
        Id = "elasticsearch";
        SupportedPatterns = ["@elastic/elasticsearch"];
        LibraryRole = LibraryRole.OrmOrDatabase;
        CustomAstRules =
        [
            new CustomAstRule
            {
                NodeTypes = new[] { TreeSitterSyntax.TypeScript.CallExpression }.ToFrozenSet(),
                MapNodeType = static (node, ctx) => IsEsCall(node) ? OntologyConstants.NodeLabels.Query : null,
                ExtractIdentifier = static (node, _, _, _) =>
                {
                    if (!IsEsCall(node)) return null;
                    if (AstHelper.TryGetMemberAccess(node, out _, out var propName))
                    {
                        var indexName = TryExtractIndexFromArgs(node);
                        if (!string.IsNullOrEmpty(indexName))
                        {
                            return $"Elasticsearch: {propName} ({indexName})";
                        }
                        return $"Elasticsearch: {propName}";
                    }
                    return "Elasticsearch Query";
                }
            }
        ];
    }

    private static bool IsEsCall(Node node)
    {
        if (!node.Is(TreeSitterSyntax.TypeScript.CallExpression)) return false;

        if (AstHelper.TryGetMemberAccess(node, out _, out var propName))
        {
            return propName != null && EsMethods.Contains(propName);
        }

        return false;
    }

    private static string? TryExtractIndexFromArgs(Node callNode)
    {
        var args = AstHelper.GetCallArguments(callNode);
        if (args.Count == 0) return null;

        if (AstHelper.TryGetObjectProperty(args[0], "index", out var valNode))
        {
            return AstHelper.ResolveStringOrTemplate(valNode);
        }

        return null;
    }
}
