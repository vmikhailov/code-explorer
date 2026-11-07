using System.Collections.Frozen;

namespace CodeExplorer.Parser.CSharp.Libraries;

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
                    "Query", "QueryAsync", "QueryFirst", "QueryFirstOrDefault",
                    "QuerySingle", "QuerySingleOrDefault", "QueryMultiple", "QueryMultipleAsync",
                    "Execute", "ExecuteAsync", "ExecuteReader", "ExecuteScalar"
                }.ToFrozenSet(),
                RequireExtractableSqlOnCall = false,
                FallbackQueryPrefix = "Dapper Query"
            }
        ];
    }
}
