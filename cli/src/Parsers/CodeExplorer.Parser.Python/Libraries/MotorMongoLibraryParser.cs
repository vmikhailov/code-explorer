using System.Collections.Frozen;

namespace CodeExplorer.Parser.Python.Libraries;

public sealed class MotorMongoLibraryParser : DeclarativeSemanticExtension
{
    private const string MongoTarget = "mongodb";

    private static readonly FrozenSet<string> MongoMethods = new[]
    {
        "find", "find_one", "insert_one", "insert_many", "update_one", "update_many",
        "delete_one", "delete_many", "aggregate", "count_documents", "find_one_and_update"
    }.ToFrozenSet();

    public MotorMongoLibraryParser()
    {
        Id = "motor-mongo";
        Name = "Motor (AsyncIO MongoDB)";
        Type = SemanticExtensionType.DocumentDb;
        LibraryRole = LibraryRole.OrmOrDatabase;
        SupportedPatterns = ["motor", "motor.*", "beanie", "beanie.*"];
        SqlSinkRules =
        [
            new SqlSinkRule
            {
                CallMethodNames = MongoMethods,
                RequireExtractableSqlOnCall = false,
                AppendMethodNameWhenNoSql = true,
                FallbackQueryPrefix = "MongoDB"
            }
        ];
        DataStoreAccessRules =
        [
            new DataStoreAccessRule
            {
                CallMethodNames = MongoMethods,
                StaticTargetName = MongoTarget
            }
        ];
    }
}
