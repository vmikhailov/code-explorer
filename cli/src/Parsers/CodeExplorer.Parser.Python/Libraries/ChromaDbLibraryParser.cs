using System.Collections.Frozen;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.Python.Libraries;

public class ChromaDbLibraryParser : DeclarativeSemanticExtension
{
    private static readonly HashSet<string> CollectionMgmtMethods = new(StringComparer.Ordinal)
    {
        "create_collection", "get_collection", "get_or_create_collection", "delete_collection"
    };

    private static readonly HashSet<string> QueryMethods = new(StringComparer.Ordinal)
    {
        "query", "add", "get", "update", "upsert", "delete", "count", "peek"
    };

    public ChromaDbLibraryParser()
    {
        Type = SemanticExtensionType.VectorDb;
        Name = "Chroma";
        Id = "chroma";
        SupportedPatterns = ["chromadb", "chromadb.*"];
        LibraryRole = LibraryRole.OrmOrDatabase;
        CustomAstRules =
        [
            new CustomAstRule
            {
                NodeTypes = new[] { TreeSitterSyntax.Python.Call }.ToFrozenSet(),
                MapNodeType = static (node, ctx) =>
                {
                    if (!IsChromaCall(node, out _, out _, out var isMgmt)) return null;
                    return isMgmt ? OntologyConstants.NodeLabels.DataSet : OntologyConstants.NodeLabels.Query;
                },
                ExtractIdentifier = static (node, _, _, _) =>
                {
                    if (!IsChromaCall(node, out var method, out var collection, out var isMgmt)) return null;
                    if (isMgmt && !string.IsNullOrEmpty(collection))
                    {
                        return $"Chroma: {collection}";
                    }
                    return !string.IsNullOrEmpty(collection) ? $"Chroma: {collection}.{method}" : $"Chroma: {method}";
                },
                CollectReferences = static (node, scopeSymbolId, references, ctx) =>
                {
                    if (IsChromaCall(node, out _, out var collection, out var isMgmt))
                    {
                        if (!string.IsNullOrEmpty(collection))
                        {
                            var rel = isMgmt ? OntologyConstants.Relationships.PersistedIn : OntologyConstants.Relationships.UsesDb;
                            references.Add(new Reference(scopeSymbolId, collection, rel));
                        }
                        references.Add(new Reference(scopeSymbolId, ChromaDbId, OntologyConstants.Relationships.UsesDb));
                    }
                }
            }
        ];
    }

    private const string ChromaDbId = "chromadb";

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
