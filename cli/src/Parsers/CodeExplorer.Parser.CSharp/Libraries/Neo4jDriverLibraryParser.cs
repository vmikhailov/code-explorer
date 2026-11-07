using System.Collections.Frozen;

namespace CodeExplorer.Parser.CSharp.Libraries;

public sealed class Neo4jDriverLibraryParser : DeclarativeSemanticExtension
{
    private static readonly FrozenSet<string> Neo4jMethods = new[]
    {
        "Run", "RunAsync", "ExecuteReadAsync", "ExecuteWriteAsync"
    }.ToFrozenSet();

    public Neo4jDriverLibraryParser()
    {
        Id = "neo4j";
        Name = "Neo4j";
        Type = SemanticExtensionType.GraphDb;
        LibraryRole = LibraryRole.OrmOrDatabase;
        SupportedPatterns = ["Neo4j.Driver", "Neo4j.Driver.*", "Neo4j.Driver.Simple"];
        SqlSinkRules =
        [
            new SqlSinkRule
            {
                CallMethodNames = Neo4jMethods,
                RequireExtractableSqlOnCall = false,
                AppendMethodNameWhenNoSql = true,
                FallbackQueryPrefix = "Neo4j"
            }
        ];
        DataStoreAccessRules =
        [
            new DataStoreAccessRule
            {
                CallMethodNames = Neo4jMethods,
                StaticTargetName = "neo4j"
            }
        ];
    }
}
