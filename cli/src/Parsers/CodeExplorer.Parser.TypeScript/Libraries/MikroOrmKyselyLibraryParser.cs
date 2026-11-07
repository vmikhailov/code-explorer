using System.Collections.Frozen;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public sealed class MikroOrmKyselyLibraryParser : DeclarativeSemanticExtension
{
    private const string MikroOrmTarget = "mikro-orm";

    private static readonly FrozenSet<string> KyselyTableMethods = new[]
    {
        "selectFrom", "insertInto", "updateTable", "deleteFrom"
    }.ToFrozenSet();

    private static readonly FrozenSet<string> MikroOrmMethods = new[]
    {
        "find", "findOne", "findOneOrFail", "findAndCount",
        "persistAndFlush", "nativeInsert", "nativeUpdate", "nativeDelete"
    }.ToFrozenSet();

    public MikroOrmKyselyLibraryParser()
    {
        Id = "mikro-orm-kysely";
        Name = "MikroORM & Kysely";
        Type = SemanticExtensionType.RelationalDb;
        LibraryRole = LibraryRole.OrmOrDatabase;
        SupportedPatterns = ["@mikro-orm/*", "kysely", "kysely-*"];
        SqlSinkRules =
        [
            new SqlSinkRule
            {
                CallMethodNames = KyselyTableMethods,
                RequireExtractableSqlOnCall = false,
                AppendMethodNameWhenNoSql = true,
                FallbackQueryPrefix = "Kysely"
            }
        ];
        DataStoreAccessRules =
        [
            new DataStoreAccessRule
            {
                CallMethodNames = KyselyTableMethods,
                TableArgumentIndex = 0
            },
            new DataStoreAccessRule
            {
                CallMethodNames = MikroOrmMethods,
                ReceiverSubstrings = new[] { "em", "entityManager" }.ToFrozenSet(),
                StaticTargetName = MikroOrmTarget
            }
        ];
    }
}
