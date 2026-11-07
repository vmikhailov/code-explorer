using System.Collections.Frozen;

namespace CodeExplorer.Parser.CSharp.Libraries;

public class NpgsqlLibraryParser : DeclarativeSemanticExtension
{
    private static readonly FrozenSet<string> SqlMethods = new[]
    {
        "NpgsqlCommand",
        "ExecuteReader", "ExecuteReaderAsync",
        "ExecuteNonQuery", "ExecuteNonQueryAsync",
        "ExecuteScalar", "ExecuteScalarAsync"
    }.ToFrozenSet(StringComparer.Ordinal);

    public NpgsqlLibraryParser()
    {
        Id = "postgres";
        Name = "PostgreSQL";
        Type = SemanticExtensionType.RelationalDb;
        LibraryRole = LibraryRole.OrmOrDatabase;
        SupportedPatterns = ["Npgsql", "Npgsql.*"];
        SqlSinkRules =
        [
            new SqlSinkRule
            {
                CallMethodNames = SqlMethods,
                RequireExtractableSqlOnCall = false,
                AppendMethodNameWhenNoSql = true,
                FallbackQueryPrefix = "PostgreSQL"
            }
        ];
    }
}
