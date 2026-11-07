using System.Collections.Frozen;

namespace CodeExplorer.Parser.Python.Libraries;

public class VectorDbPythonLibraryParser : DeclarativeSemanticExtension
{
    public VectorDbPythonLibraryParser()
    {
        Id = "vector-db-python";
        Name = "Qdrant, Milvus & Weaviate Python Clients";
        Type = SemanticExtensionType.VectorDb;
        LibraryRole = LibraryRole.OrmOrDatabase;
        SupportedPatterns = ["qdrant_client*", "pymilvus*", "weaviate*"];
        DataStoreAccessRules =
        [
            new DataStoreAccessRule
            {
                CallMethodNames = new[] { "search", "upsert", "query_points", "create_collection", "Collection" }.ToFrozenSet(StringComparer.Ordinal)
            }
        ];
    }
}
