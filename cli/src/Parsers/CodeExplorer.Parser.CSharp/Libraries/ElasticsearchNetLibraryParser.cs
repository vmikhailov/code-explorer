using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.CSharp.Libraries;

public class ElasticsearchNetLibraryParser : ISemanticExtension
{
    public string Type => "db:search";
    public string Name => "Elasticsearch (Low Level)";
    public string Id => "elasticsearch-net";
    public IReadOnlyList<string> SupportedPatterns => ["Elasticsearch.Net", "Elasticsearch.Net.*"];
    public bool IsImplemented => true;

    private static readonly HashSet<string> EsMethods = new(StringComparer.Ordinal)
    {
        "Search", "SearchAsync", "Index", "IndexAsync", "Update", "UpdateAsync", "Delete", "DeleteAsync", "Count", "CountAsync", "Get", "GetAsync", "PerformRequest", "PerformRequestAsync"
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
        if (!node.Is(TreeSitterSyntax.CSharp.InvocationExpression)) return false;

        var func = node.GetFunctionNode();
        if (func.Is(TreeSitterSyntax.CSharp.MemberAccessExpression))
        {
            var name = func.GetChildFieldText(TreeSitterSyntax.Fields.Name);
            if (!string.IsNullOrEmpty(name) && EsMethods.Contains(name))
            {
                methodName = name;
                return true;
            }
        }
        return false;
    }
}
