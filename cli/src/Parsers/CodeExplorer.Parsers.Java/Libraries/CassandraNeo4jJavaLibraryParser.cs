using System.Collections.Frozen;

namespace CodeExplorer.Parsers.Java.Libraries;

public class CassandraNeo4jJavaLibraryParser : DeclarativeSemanticExtension
{
    private const string ExtensionId = "cassandra-neo4j-java";
    private const string ExtensionName = "Spring Data Cassandra, DataStax & Neo4j";
    private const string TableAnnotation = "Table";
    private const string NodeAnnotation = "Node";
    private const string NodeEntityAnnotation = "NodeEntity";
    private const string ValueKey = "value";
    private const string PrimaryLabelKey = "primaryLabel";

    public CassandraNeo4jJavaLibraryParser()
    {
        Id = ExtensionId;
        Name = ExtensionName;
        Type = SemanticExtensionType.DocumentDb;
        PackageDescriptorTypeOverride = SemanticExtensionType.Library;
        LibraryRole = LibraryRole.OrmOrDatabase;
        SupportedPatterns =
        [
            "org.springframework.data.cassandra*",
            "com.datastax.oss*",
            "org.springframework.data.neo4j*",
            "org.neo4j.driver*",
            "org.neo4j.ogm*"
        ];
        OrmTableRules =
        [
            new OrmTableRule
            {
                TableAnnotationNames = new[] { TableAnnotation, NodeAnnotation, NodeEntityAnnotation }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                NameArgumentKeys = new[] { ValueKey, PrimaryLabelKey }.ToFrozenSet()
            }
        ];
        DataStoreAccessRules =
        [
            new DataStoreAccessRule
            {
                CallMethodNames = new[]
                {
                    "execute", "executeAsync", "run", "readTransaction", "writeTransaction", "executeRead", "executeWrite"
                }.ToFrozenSet(StringComparer.Ordinal),
                ReceiverSubstrings = new[] { "cassandra", "cql", "neo4j", "session", "driver" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                StaticTargetName = "nosql-store"
            }
        ];
    }
}
