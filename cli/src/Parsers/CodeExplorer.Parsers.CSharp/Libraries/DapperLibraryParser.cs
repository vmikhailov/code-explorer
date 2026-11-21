using System.Collections.Frozen;

namespace CodeExplorer.Parsers.CSharp.Libraries;

public sealed class DapperLibraryParser : DeclarativeSemanticExtension
{
    public DapperLibraryParser()
    {
        Id = "dapper";
        Name = "Dapper";
        Type = SemanticExtensionType.RelationalDb;
        LibraryRole = LibraryRole.OrmOrDatabase;
        SupportedPatterns = ["Dapper", "Dapper.*"];
        SqlSinkRules =
        [
            new SqlSinkRule
            {
                CallMethodNames = new[]
                {
                    "Query", "QueryAsync", "QueryFirst", "QueryFirstAsync",
                    "QueryFirstOrDefault", "QueryFirstOrDefaultAsync",
                    "QuerySingle", "QuerySingleAsync",
                    "QuerySingleOrDefault", "QuerySingleOrDefaultAsync",
                    "QueryMultiple", "QueryMultipleAsync",
                    "Execute", "ExecuteAsync",
                    "ExecuteReader", "ExecuteReaderAsync",
                    "ExecuteScalar", "ExecuteScalarAsync"
                }.ToFrozenSet(),
                RequireExtractableSqlOnCall = false,
                FallbackQueryPrefix = "Dapper Query"
            }
        ];
    }
}
