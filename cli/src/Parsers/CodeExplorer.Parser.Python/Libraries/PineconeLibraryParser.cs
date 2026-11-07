using System.Collections.Frozen;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.Python.Libraries;

public class PineconeLibraryParser : DeclarativeSemanticExtension
{
    private static readonly HashSet<string> IndexMgmtMethods = new(StringComparer.Ordinal)
    {
        "Index", "create_index", "delete_index", "describe_index"
    };

    private static readonly HashSet<string> QueryMethods = new(StringComparer.Ordinal)
    {
        "query", "upsert", "fetch", "delete", "update", "describe_index_stats"
    };

    public PineconeLibraryParser()
    {
        Type = SemanticExtensionType.VectorDb;
        Name = "Pinecone";
        Id = "pinecone";
        SupportedPatterns = ["pinecone", "pinecone.*", "pinecone-client", "pinecone_client"];
        LibraryRole = LibraryRole.OrmOrDatabase;
        CustomAstRules =
        [
            new CustomAstRule
            {
                NodeTypes = new[] { TreeSitterSyntax.Python.Call }.ToFrozenSet(),
                MapNodeType = static (node, ctx) =>
                {
                    if (!IsPineconeCall(node, out _, out _, out var isIndexDef)) return null;
                    return isIndexDef ? OntologyConstants.NodeLabels.DataSet : OntologyConstants.NodeLabels.Query;
                },
                ExtractIdentifier = static (node, _, _, _) =>
                {
                    if (!IsPineconeCall(node, out var method, out var indexName, out var isIndexDef)) return null;
                    if (isIndexDef && !string.IsNullOrEmpty(indexName))
                    {
                        return $"Pinecone: {indexName}";
                    }
                    return !string.IsNullOrEmpty(indexName) ? $"Pinecone: {indexName}.{method}" : $"Pinecone: {method}";
                },
                CollectReferences = static (node, scopeSymbolId, references, ctx) =>
                {
                    if (IsPineconeCall(node, out _, out var indexName, out var isIndexDef))
                    {
                        if (!string.IsNullOrEmpty(indexName))
                        {
                            var rel = isIndexDef ? OntologyConstants.Relationships.PersistedIn : OntologyConstants.Relationships.UsesDb;
                            references.Add(new Reference(scopeSymbolId, indexName, rel));
                        }
                        references.Add(new Reference(scopeSymbolId, PineconeDbId, OntologyConstants.Relationships.UsesDb));
                    }
                }
            }
        ];
    }

    private const string PineconeDbId = "pinecone";

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
