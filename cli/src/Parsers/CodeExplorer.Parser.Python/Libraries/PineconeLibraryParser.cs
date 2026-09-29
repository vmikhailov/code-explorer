using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.Python.Libraries;

public class PineconeLibraryParser : ISemanticExtension
{
    public string Type => "db:vector";
    public string Name => "Pinecone";
    public string Id => "pinecone";
    public IReadOnlyList<string> SupportedPatterns => ["pinecone", "pinecone.*", "pinecone-client", "pinecone_client"];
    public bool IsImplemented => true;

    private static readonly HashSet<string> IndexMgmtMethods = new(StringComparer.Ordinal)
    {
        "Index", "create_index", "delete_index", "describe_index"
    };

    private static readonly HashSet<string> QueryMethods = new(StringComparer.Ordinal)
    {
        "query", "upsert", "fetch", "delete", "update", "describe_index_stats"
    };

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (IsPineconeCall(node, out _, out _, out var isIndexDef))
        {
            return isIndexDef ? OntologyConstants.NodeLabels.DataSet : OntologyConstants.NodeLabels.Query;
        }
        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx)
    {
        if (IsPineconeCall(node, out var method, out var indexName, out var isIndexDef))
        {
            if (isIndexDef && !string.IsNullOrEmpty(indexName))
            {
                return $"Pinecone: {indexName}";
            }
            return !string.IsNullOrEmpty(indexName) ? $"Pinecone: {indexName}.{method}" : $"Pinecone: {method}";
        }
        return null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (IsPineconeCall(node, out _, out var indexName, out var isIndexDef))
        {
            if (!string.IsNullOrEmpty(indexName))
            {
                var rel = isIndexDef ? OntologyConstants.Relationships.PersistedIn : OntologyConstants.Relationships.UsesDb;
                references.Add(new Reference(scopeSymbolId, indexName, rel));
            }
            references.Add(new Reference(scopeSymbolId, "pinecone", OntologyConstants.Relationships.UsesDb));
        }
    }

    private static bool IsPineconeCall(Node node, out string? methodName, out string? indexName, out bool isIndexDef)
    {
        methodName = null;
        indexName = null;
        isIndexDef = false;

        if (PythonAstHelper.TryGetMemberAccess(node, out var objNode, out methodName))
        {
            if (methodName != null && IndexMgmtMethods.Contains(methodName))
            {
                isIndexDef = true;
                indexName = PythonAstHelper.ResolveCallArgument(node, 0, "name");
                return true;
            }

            if (methodName != null && QueryMethods.Contains(methodName))
            {
                var callerText = objNode?.Text;
                if (callerText is not null and not "pc" and not "client" and not "self")
                {
                    indexName = callerText;
                }
                return true;
            }
        }

        return false;
    }
}
