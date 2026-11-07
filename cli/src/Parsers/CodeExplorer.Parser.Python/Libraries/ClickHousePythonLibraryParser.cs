using System.Collections.Frozen;

namespace CodeExplorer.Parser.Python.Libraries;

public class ClickHousePythonLibraryParser : DeclarativeSemanticExtension
{
    public ClickHousePythonLibraryParser()
    {
        Id = "clickhouse-python";
        Name = "ClickHouse Connect & Driver";
        Type = SemanticExtensionType.RelationalDb;
        LibraryRole = LibraryRole.OrmOrDatabase;
        SupportedPatterns = ["clickhouse_connect*", "clickhouse_driver*", "asynch*"];
        SqlSinkRules =
        [
            new SqlSinkRule
            {
                CallMethodNames = new[] { "query", "command", "execute", "insert" }.ToFrozenSet(StringComparer.Ordinal),
                FallbackQueryPrefix = "ClickHouse SQL"
            }
        ];
    }
}
