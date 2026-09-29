using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.Python.Libraries;

public class ChromaDbLibraryParser : ISemanticExtension
{
    public string Type => "db:vector";
    public string Name => "Chroma";
    public string Id => "chroma";
    public IReadOnlyList<string> SupportedPatterns => ["chromadb", "chromadb.*"];
    public bool IsImplemented => true;

    private static readonly HashSet<string> CollectionMgmtMethods = new(StringComparer.Ordinal)
    {
        "create_collection", "get_collection", "get_or_create_collection", "delete_collection"
    };

    private static readonly HashSet<string> QueryMethods = new(StringComparer.Ordinal)
    {
        "query", "add", "get", "update", "upsert", "delete", "count", "peek"
    };

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (IsChromaCall(node, out _, out _, out var isMgmt))
        {
            return isMgmt ? OntologyConstants.NodeLabels.DataSet : OntologyConstants.NodeLabels.Query;
        }
        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx)
    {
        if (IsChromaCall(node, out var method, out var collection, out var isMgmt))
        {
            if (isMgmt && !string.IsNullOrEmpty(collection))
            {
                return $"Chroma: {collection}";
            }
            return !string.IsNullOrEmpty(collection) ? $"Chroma: {collection}.{method}" : $"Chroma: {method}";
        }
        return null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (IsChromaCall(node, out _, out var collection, out var isMgmt))
        {
            if (!string.IsNullOrEmpty(collection))
            {
                var rel = isMgmt ? OntologyConstants.Relationships.PersistedIn : OntologyConstants.Relationships.UsesDb;
                references.Add(new Reference(scopeSymbolId, collection, rel));
            }
            references.Add(new Reference(scopeSymbolId, "chromadb", OntologyConstants.Relationships.UsesDb));
        }
    }

    private static bool IsChromaCall(Node node, out string? methodName, out string? collection, out bool isMgmt)
    {
        methodName = null;
        collection = null;
        isMgmt = false;

        if (PythonAstHelper.TryGetMemberAccess(node, out var objNode, out methodName))
        {
            if (methodName != null && CollectionMgmtMethods.Contains(methodName))
            {
                isMgmt = true;
                collection = PythonAstHelper.ResolveCallArgument(node, 0, "name");
                return true;
            }

            if (methodName != null && QueryMethods.Contains(methodName))
            {
                var callerText = objNode?.Text;
                if (callerText is not null and not "client" and not "self")
                {
                    collection = callerText;
                }
                return true;
            }
        }

        return false;
    }
}
