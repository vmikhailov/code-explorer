using System.Collections.Frozen;

namespace CodeExplorer.Parser.Go.Libraries;

public sealed class BunGoLibraryParser : DeclarativeSemanticExtension
{
    private const string BunSqlTarget = "bun:sql";

    private static readonly FrozenSet<string> BunBuilderMethods = new[]
    {
        "NewSelect", "NewInsert", "NewUpdate", "NewDelete", "NewRaw", "NewCreateTable", "NewDropTable"
    }.ToFrozenSet();

    public BunGoLibraryParser()
    {
        Id = "bun-go";
        Name = "Bun (Go SQL-first ORM)";
        Type = SemanticExtensionType.RelationalDb;
        LibraryRole = LibraryRole.OrmOrDatabase;
        SupportedPatterns = ["github.com/uptrace/bun*"];
        SqlSinkRules =
        [
            new SqlSinkRule
            {
                CallMethodNames = BunBuilderMethods,
                RequireExtractableSqlOnCall = false,
                AppendMethodNameWhenNoSql = true,
                FallbackQueryPrefix = "Bun"
            }
        ];
        DataStoreAccessRules =
        [
            new DataStoreAccessRule
            {
                CallMethodNames = BunBuilderMethods,
                StaticTargetName = BunSqlTarget
            }
        ];
    }
}
