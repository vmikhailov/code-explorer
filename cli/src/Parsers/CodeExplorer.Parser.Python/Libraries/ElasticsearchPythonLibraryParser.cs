using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.Python.Libraries;

public class ElasticsearchPythonLibraryParser : ISemanticExtension
{
    public string Type => OntologyConstants.LibraryTypes.AnalyticsDb;
    public string Name => "Elasticsearch";
    public string Id => "elasticsearch";
    public IReadOnlyList<string> SupportedPatterns => ["elasticsearch", "elasticsearch.*"];
    public bool IsImplemented => true;

    private static readonly HashSet<string> EsMethods = new(StringComparer.Ordinal)
    {
        "search", "index", "update", "delete", "count", "bulk", "get", "create"
    };

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (IsEsCall(node, out _, out _))
        {
            return OntologyConstants.NodeLabels.Query;
        }
        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx)
    {
        if (IsEsCall(node, out var method, out var indexName))
        {
            return !string.IsNullOrEmpty(indexName) ? $"Elasticsearch: {method} ({indexName})" : $"Elasticsearch: {method}";
        }
        return null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (IsEsCall(node, out _, out var indexName))
        {
            if (!string.IsNullOrEmpty(indexName))
            {
                references.Add(new Reference(scopeSymbolId, indexName, OntologyConstants.Relationships.UsesDb));
            }
            references.Add(new Reference(scopeSymbolId, "elasticsearch", OntologyConstants.Relationships.UsesDb));
        }
    }

    private static bool IsEsCall(Node node, out string? methodName, out string? indexName)
    {
        methodName = null;
        indexName = null;

        if (PythonAstHelper.TryGetMemberAccess(node, out _, out methodName))
        {
            if (methodName != null && EsMethods.Contains(methodName))
            {
                indexName = PythonAstHelper.ResolveCallArgument(node, 0, "index");
                return true;
            }
        }

        return false;
    }
}
