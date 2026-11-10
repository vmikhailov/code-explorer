namespace CodeExplorer.Core.Parser.Sql;

public static class SqlDialectDetector
{
    public static SqlDialect DetectDialect(string sqlText, string? filePath = null)
    {
        if (string.IsNullOrWhiteSpace(sqlText)) return SqlDialect.Ansi;

        var path = filePath ?? string.Empty;

        // 1. Salesforce SOQL
        if (sqlText.Contains("__c", StringComparison.OrdinalIgnoreCase) ||
            sqlText.Contains("__r", StringComparison.OrdinalIgnoreCase) ||
            path.Contains(".soql", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("salesforce", StringComparison.OrdinalIgnoreCase))
        {
            return SqlDialect.Soql;
        }

        // 2. Google BigQuery
        if (sqlText.Contains("_TABLE_SUFFIX", StringComparison.OrdinalIgnoreCase) ||
            sqlText.Contains("BigQuery", StringComparison.OrdinalIgnoreCase) ||
            sqlText.Contains("bq.driver", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("bq-", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("bigquery", StringComparison.OrdinalIgnoreCase))
        {
            return SqlDialect.BigQuery;
        }

        // 3. ClickHouse
        if (sqlText.Contains("MergeTree", StringComparison.OrdinalIgnoreCase) ||
            sqlText.Contains("FINAL", StringComparison.OrdinalIgnoreCase) ||
            sqlText.Contains("SAMPLE", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("clickhouse", StringComparison.OrdinalIgnoreCase))
        {
            return SqlDialect.ClickHouse;
        }

        // 4. Oracle PL/SQL
        if (sqlText.Contains("FROM DUAL", StringComparison.OrdinalIgnoreCase) ||
            sqlText.Contains("XMLTABLE", StringComparison.OrdinalIgnoreCase) ||
            sqlText.Contains("CONNECT BY", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("oracle", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("plsql", StringComparison.OrdinalIgnoreCase))
        {
            return SqlDialect.PlSql;
        }

        // 5. PostgreSQL
        if (sqlText.Contains("$$", StringComparison.Ordinal) ||
            sqlText.Contains("::", StringComparison.Ordinal) ||
            sqlText.Contains("RETURNING", StringComparison.OrdinalIgnoreCase) ||
            sqlText.Contains("FROM ONLY", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("postgres", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("npgsql", StringComparison.OrdinalIgnoreCase))
        {
            return SqlDialect.PostgreSql;
        }

        // 6. Microsoft T-SQL
        if (sqlText.Contains("[dbo]", StringComparison.OrdinalIgnoreCase) ||
            sqlText.Contains("(NOLOCK)", StringComparison.OrdinalIgnoreCase) ||
            sqlText.Contains("STRING_SPLIT", StringComparison.OrdinalIgnoreCase) ||
            sqlText.Contains("OPENJSON", StringComparison.OrdinalIgnoreCase) ||
            sqlText.Contains("#temp", StringComparison.OrdinalIgnoreCase) ||
            sqlText.Contains("@tbl", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("tsql", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("mssql", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("sqlserver", StringComparison.OrdinalIgnoreCase))
        {
            return SqlDialect.TSql;
        }

        // 7. MySQL
        if (sqlText.Contains("STRAIGHT_JOIN", StringComparison.OrdinalIgnoreCase) ||
            sqlText.Contains("JSON_TABLE", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("mysql", StringComparison.OrdinalIgnoreCase))
        {
            return SqlDialect.MySql;
        }

        return SqlDialect.Ansi;
    }
}
