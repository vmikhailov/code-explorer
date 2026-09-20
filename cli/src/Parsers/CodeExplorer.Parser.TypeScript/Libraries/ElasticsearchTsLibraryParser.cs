using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public class ElasticsearchTsLibraryParser : ILibraryParser
{
    public string Type => "db:search";
    public string Name => "Elasticsearch";
    public string Id => "elasticsearch";
    public IReadOnlyList<string> SupportedPatterns => ["@elastic/elasticsearch"];
    public bool IsImplemented => true;

    private static readonly HashSet<string> EsMethods = new(StringComparer.Ordinal)
    {
        "search", "index", "get", "update", "delete", "bulk", "count", "mget", "msearch"
    };

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (IsEsCall(node))
        {
            return OntologyConstants.NodeLabels.Query;
        }
        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx)
    {
        if (IsEsCall(node))
        {
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
        return null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        // Search queries represent Query nodes
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
