using System.Collections.Frozen;

namespace CodeExplorer.Parser.CSharp.Libraries;

public class OracleDataAccessLibraryParser : DeclarativeSemanticExtension
{
    private static readonly FrozenSet<string> SqlMethods = new[]
    {
        "OracleCommand",
        "ExecuteReader", "ExecuteReaderAsync",
        "ExecuteNonQuery", "ExecuteNonQueryAsync",
        "ExecuteScalar", "ExecuteScalarAsync"
    }.ToFrozenSet(StringComparer.Ordinal);

    public OracleDataAccessLibraryParser()
    {
        Id = "oracle";
        Name = "Oracle";
        Type = SemanticExtensionType.RelationalDb;
        LibraryRole = LibraryRole.OrmOrDatabase;
        SupportedPatterns = ["Oracle.ManagedDataAccess", "Oracle.ManagedDataAccess.*", "Oracle.DataAccess", "Oracle.DataAccess.*"];
        SqlSinkRules =
        [
            new SqlSinkRule
            {
                CallMethodNames = SqlMethods,
                RequireExtractableSqlOnCall = false,
                AppendMethodNameWhenNoSql = true,
                FallbackQueryPrefix = "Oracle"
            }
        ];
    }
}
