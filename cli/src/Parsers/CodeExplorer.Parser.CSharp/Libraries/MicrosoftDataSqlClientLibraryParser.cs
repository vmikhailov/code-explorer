using System.Collections.Frozen;

namespace CodeExplorer.Parser.CSharp.Libraries;

public class MicrosoftDataSqlClientLibraryParser : DeclarativeSemanticExtension
{
    private static readonly FrozenSet<string> SqlMethods = new[]
    {
        "SqlCommand",
        "ExecuteReader", "ExecuteReaderAsync",
        "ExecuteNonQuery", "ExecuteNonQueryAsync",
        "ExecuteScalar", "ExecuteScalarAsync"
    }.ToFrozenSet(StringComparer.Ordinal);

    public MicrosoftDataSqlClientLibraryParser()
    {
        Id = "mssql";
        Name = "SQL Server";
        Type = SemanticExtensionType.RelationalDb;
        LibraryRole = LibraryRole.OrmOrDatabase;
        SupportedPatterns = ["Microsoft.Data.SqlClient", "Microsoft.Data.SqlClient.*"];
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
