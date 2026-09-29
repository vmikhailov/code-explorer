using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.Go.Libraries;

public class ElasticsearchGoLibraryParser : ISemanticExtension
{
    public string Type => "db:search";
    public string Name => "Elasticsearch";
    public string Id => "elasticsearch";
    public IReadOnlyList<string> SupportedPatterns => ["github.com/elastic/go-elasticsearch", "github.com/elastic/go-elasticsearch/*"];
    public bool IsImplemented => true;

    private static readonly HashSet<string> EsMethods = new(StringComparer.Ordinal)
    {
        "Search", "Index", "Update", "Delete", "Get", "Count"
    };

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (IsEsCall(node, out _))
        {
            return OntologyConstants.NodeLabels.Query;
        }
        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx)
    {
        if (IsEsCall(node, out var method))
        {
            return $"Elasticsearch: {method}";
        }
        return null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (IsEsCall(node, out _))
        {
            references.Add(new Reference(scopeSymbolId, "elasticsearch", OntologyConstants.Relationships.UsesDb));
        }
    }

    private static bool IsEsCall(Node node, out string? methodName)
    {
        methodName = null;
        if (!node.Is(TreeSitterSyntax.Go.CallExpression)) return false;

        var func = node.GetFunctionNode();
        if (!func.IsValid()) return false;

        var funcText = func.Text;
        foreach (var m in EsMethods)
        {
            if (funcText.EndsWith("." + m, StringComparison.Ordinal) || funcText == m)
            {
                methodName = m;
                return true;
            }
        }

        return false;
    }
}
