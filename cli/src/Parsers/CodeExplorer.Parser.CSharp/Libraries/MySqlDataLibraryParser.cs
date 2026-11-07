using System.Collections.Frozen;

namespace CodeExplorer.Parser.CSharp.Libraries;

public class MySqlDataLibraryParser : DeclarativeSemanticExtension
{
    private static readonly FrozenSet<string> SqlMethods = new[]
    {
        "MySqlCommand",
        "ExecuteReader", "ExecuteReaderAsync",
        "ExecuteNonQuery", "ExecuteNonQueryAsync",
        "ExecuteScalar", "ExecuteScalarAsync"
    }.ToFrozenSet(StringComparer.Ordinal);

    public MySqlDataLibraryParser()
    {
        Id = "mysql";
        Name = "MySQL";
        Type = SemanticExtensionType.RelationalDb;
        LibraryRole = LibraryRole.OrmOrDatabase;
        SupportedPatterns = ["MySql.Data", "MySql.Data.*", "MySqlConnector", "MySqlConnector.*"];
        SqlSinkRules =
        [
            new SqlSinkRule
            {
                CallMethodNames = SqlMethods,
                RequireExtractableSqlOnCall = false,
                AppendMethodNameWhenNoSql = true,
                FallbackQueryPrefix = "MySQL"
            }
        ];
    }
}
