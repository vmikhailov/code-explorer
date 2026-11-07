using System.Collections.Frozen;

namespace CodeExplorer.Parser.CSharp.Libraries;

public class SystemDataSqlClientLibraryParser : DeclarativeSemanticExtension
{
    private static readonly FrozenSet<string> SqlMethods = new[]
    {
        "SqlCommand",
        "ExecuteReader", "ExecuteReaderAsync",
        "ExecuteNonQuery", "ExecuteNonQueryAsync",
        "ExecuteScalar", "ExecuteScalarAsync"
    }.ToFrozenSet(StringComparer.Ordinal);

    public SystemDataSqlClientLibraryParser()
    {
        Id = "mssql-legacy";
        Name = "SQL Server (Legacy)";
        Type = SemanticExtensionType.RelationalDb;
        LibraryRole = LibraryRole.OrmOrDatabase;
        SupportedPatterns = ["System.Data.SqlClient", "System.Data.SqlClient.*"];
        SqlSinkRules =
        [
            new SqlSinkRule
            {
                CallMethodNames = SqlMethods,
                RequireExtractableSqlOnCall = false,
                AppendMethodNameWhenNoSql = true,
                FallbackQueryPrefix = "SQL Server"
            }
        ];
    }
}
