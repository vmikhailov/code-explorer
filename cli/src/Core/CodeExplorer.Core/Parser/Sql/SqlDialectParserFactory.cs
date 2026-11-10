using CodeExplorer.Core.Parser.Sql.Dialects;

namespace CodeExplorer.Core.Parser.Sql;

public static class SqlDialectParserFactory
{
    private static readonly AnsiSqlParser AnsiInstance = new();
    private static readonly PostgreSqlParser PostgreSqlInstance = new();
    private static readonly MySqlParser MySqlInstance = new();
    private static readonly TSqlParser TSqlInstance = new();
    private static readonly PlSqlParser PlSqlInstance = new();
    private static readonly BigQueryParser BigQueryInstance = new();
    private static readonly ClickHouseParser ClickHouseInstance = new();
    private static readonly SoqlParser SoqlInstance = new();

    public static ISqlDialectParser GetParser(SqlDialect dialect) => dialect switch
    {
        SqlDialect.PostgreSql => PostgreSqlInstance,
        SqlDialect.MySql => MySqlInstance,
        SqlDialect.TSql => TSqlInstance,
        SqlDialect.PlSql => PlSqlInstance,
        SqlDialect.BigQuery => BigQueryInstance,
        SqlDialect.ClickHouse => ClickHouseInstance,
        SqlDialect.Soql => SoqlInstance,
        _ => AnsiInstance
    };
}
