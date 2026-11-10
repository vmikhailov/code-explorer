using CodeExplorer.Core.Parser.Sql;
using CodeExplorer.Core.Parser.Sql.Ast;
using CodeExplorer.Core.Parser.Sql.Tokens;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
public class SqlDialectParserTests
{
    private static List<string> GetAllTableNames(SqlScriptAst script)
    {
        var tables = new List<string>();

        void Collect(SqlStatementAst stmt)
        {
            foreach (var cte in stmt.Ctes)
            {
                if (cte.InnerStatement != null)
                {
                    Collect(cte.InnerStatement);
                }
            }

            switch (stmt)
            {
                case SqlSelectStatementAst sel:
                    tables.AddRange(sel.Tables.Select(t => t.CanonicalName));
                    tables.AddRange(sel.Joins.Select(t => t.CanonicalName));
                    break;
                case SqlInsertStatementAst ins:
                    if (!string.IsNullOrEmpty(ins.TargetTable.TableName))
                        tables.Add(ins.TargetTable.CanonicalName);
                    if (ins.SourceQuery != null)
                    {
                        Collect(ins.SourceQuery);
                    }
                    break;
                case SqlUpdateStatementAst upd:
                    if (!string.IsNullOrEmpty(upd.TargetTable.TableName))
                        tables.Add(upd.TargetTable.CanonicalName);
                    tables.AddRange(upd.FromTables.Select(t => t.CanonicalName));
                    break;
                case SqlDeleteStatementAst del:
                    if (!string.IsNullOrEmpty(del.TargetTable.TableName))
                        tables.Add(del.TargetTable.CanonicalName);
                    tables.AddRange(del.UsingTables.Select(t => t.CanonicalName));
                    break;
                case SqlMergeStatementAst mrg:
                    if (!string.IsNullOrEmpty(mrg.TargetTable.TableName))
                        tables.Add(mrg.TargetTable.CanonicalName);
                    if (mrg.UsingTable != null && !string.IsNullOrEmpty(mrg.UsingTable.TableName))
                        tables.Add(mrg.UsingTable.CanonicalName);
                    break;
                case SqlCreateTableStatementAst crt:
                    if (!string.IsNullOrEmpty(crt.Table.TableName))
                        tables.Add(crt.Table.CanonicalName);
                    if (crt.AsSelectQuery != null)
                    {
                        Collect(crt.AsSelectQuery);
                    }
                    break;
            }
        }

        foreach (var stmt in script.Statements)
        {
            Collect(stmt);
        }
        return tables;
    }

    [Test]
    public void Ansi_SelectWithJoins_ExtractsTablesCorrectly()
    {
        var sql = """
            SELECT u.id, o.total
            FROM users u
            INNER JOIN orders o ON u.id = o.user_id
            LEFT JOIN shipping_addresses a ON u.id = a.user_id
            WHERE u.status = 'ACTIVE';
            """;

        var parser = SqlDialectParserFactory.GetParser(SqlDialect.Ansi);
        var script = parser.Parse(sql);
        var tables = GetAllTableNames(script);

        tables.Should().Contain("users");
        tables.Should().Contain("orders");
        tables.Should().Contain("shipping_addresses");
        tables.Should().NotContain("ACTIVE");
        tables.Should().NotContain("u");
        tables.Should().NotContain("o");
    }

    [Test]
    public void CteNames_AreIsolated_AndNeverExtractedAsTables()
    {
        var sql = """
            WITH RECURSIVE del_rec(id) AS (
                SELECT id FROM deleted_items
                UNION ALL
                SELECT c.id FROM items c JOIN del_rec d ON c.parent_id = d.id
            ),
            summary_cte AS (
                SELECT * FROM metrics_log
            )
            SELECT * FROM del_rec
            JOIN summary_cte ON del_rec.id = summary_cte.id;
            """;

        var parser = SqlDialectParserFactory.GetParser(SqlDialect.Ansi);
        var script = parser.Parse(sql);
        var tables = GetAllTableNames(script);

        tables.Should().NotContain("del_rec", "CTE name must not become a database table");
        tables.Should().NotContain("summary_cte", "CTE name must not become a database table");
        tables.Should().Contain("deleted_items");
        tables.Should().Contain("items");
        tables.Should().Contain("metrics_log");
    }

    [Test]
    public void StringLiterals_ContainingKeywords_NeverExtractPhantomTables()
    {
        var sql = """
            INSERT INTO audit_logs (action, query)
            VALUES ('DELETE', 'SELECT * FROM phantom_table WHERE status = ''ACTIVE''');
            """;

        var parser = SqlDialectParserFactory.GetParser(SqlDialect.Ansi);
        var script = parser.Parse(sql);
        var tables = GetAllTableNames(script);

        tables.Should().ContainSingle().Which.Should().Be("audit_logs");
        tables.Should().NotContain("phantom_table");
        tables.Should().NotContain("ACTIVE");
    }

    [Test]
    public void PostgreSql_DollarQuotes_Typecasts_FromOnly_And_Tvfs()
    {
        var sql = """
            SELECT id::text, unnest(tags)
            FROM ONLY accounts
            JOIN unnest(items) AS u(item_id) ON accounts.id = u.item_id
            RETURNING id;
            """;

        var parser = SqlDialectParserFactory.GetParser(SqlDialect.PostgreSql);
        var script = parser.Parse(sql);
        var tables = GetAllTableNames(script);

        tables.Should().ContainSingle().Which.Should().Be("accounts");
        tables.Should().NotContain("ONLY");
        tables.Should().NotContain("unnest");
    }

    [Test]
    public void TSql_BracketQuotes_Hints_And_TempTables_Ignored()
    {
        var sql = """
            SELECT c.id, s.value
            FROM [dbo].[customers] c WITH (NOLOCK)
            CROSS APPLY STRING_SPLIT(c.tags, ',') s
            JOIN #temp_discounts d ON c.id = d.customer_id;
            """;

        var parser = SqlDialectParserFactory.GetParser(SqlDialect.TSql);
        var script = parser.Parse(sql);
        var tables = GetAllTableNames(script);

        tables.Should().Contain("dbo.customers");
        tables.Should().NotContain("STRING_SPLIT");
        tables.Should().NotContain("#temp_discounts");
    }

    [Test]
    public void MySql_Backticks_And_JsonTable()
    {
        var sql = """
            SELECT *
            FROM `ecommerce`.`order_items` oi
            STRAIGHT_JOIN JSON_TABLE(oi.attributes, '$[*]' COLUMNS (id INT PATH '$.id')) jt;
            """;

        var parser = SqlDialectParserFactory.GetParser(SqlDialect.MySql);
        var script = parser.Parse(sql);
        var tables = GetAllTableNames(script);

        tables.Should().ContainSingle().Which.Should().Be("ecommerce.order_items");
        tables.Should().NotContain("JSON_TABLE");
    }

    [Test]
    public void BigQuery_Wildcards_And_Unnest()
    {
        var sql = """
            SELECT visit_id
            FROM `my-bq-project.analytics.sessions_*`
            CROSS JOIN UNNEST(hits) AS hit
            WHERE _TABLE_SUFFIX BETWEEN '20260101' AND '20260131';
            """;

        var parser = SqlDialectParserFactory.GetParser(SqlDialect.BigQuery);
        var script = parser.Parse(sql);
        var tables = GetAllTableNames(script);

        tables.Should().ContainSingle().Which.Should().Be("my-bq-project.analytics.sessions_*");
        tables.Should().NotContain("UNNEST");
    }

    [Test]
    public void PlSql_Dual_And_XmlTable_Ignored()
    {
        var sql = """
            SELECT 1 FROM DUAL;
            SELECT * FROM XMLTABLE('/root' PASSING xml_col);
            """;

        var parser = SqlDialectParserFactory.GetParser(SqlDialect.PlSql);
        var script = parser.Parse(sql);
        var tables = GetAllTableNames(script);

        tables.Should().BeEmpty("DUAL is Oracle pseudo-table and XMLTABLE is a function");
    }

    [Test]
    public void ClickHouse_Final_Sample_And_NumbersFunction()
    {
        var sql = """
            SELECT event_time
            FROM user_events FINAL
            SAMPLE 0.1
            JOIN numbers(100) n ON user_events.id = n.number;
            """;

        var parser = SqlDialectParserFactory.GetParser(SqlDialect.ClickHouse);
        var script = parser.Parse(sql);
        var tables = GetAllTableNames(script);

        tables.Should().ContainSingle().Which.Should().Be("user_events");
        tables.Should().NotContain("FINAL");
        tables.Should().NotContain("SAMPLE");
        tables.Should().NotContain("numbers");
    }

    [Test]
    public void Templated_Identifiers_And_BlockScript()
    {
        var sql = """
            BEGIN
                DROP TABLE IF EXISTS ${tracker.main_query_result};

                CREATE TABLE IF NOT EXISTS ${tracker.main_query_result}(
                    id INT
                );

                CREATE TABLE IF NOT EXISTS {DB_NAME}.bundle_placements (
                    timestamp DateTime
                );

                CREATE TABLE IF NOT EXISTS {{BIG_QUERY_DATASET}}.clicktype_rates_history (
                    tb_id INT64
                );

                CREATE TABLE IF NOT EXIST legacy_table (
                    col1 STRING
                );
            END
            """;
        var parser = SqlDialectParserFactory.GetParser(SqlDialect.Ansi);
        var script = parser.Parse(sql);
        var tables = GetAllTableNames(script);

        tables.Should().Contain("tracker.main_query_result");
        tables.Should().Contain("DB_NAME.bundle_placements");
        tables.Should().Contain("BIG_QUERY_DATASET.clicktype_rates_history");
        tables.Should().Contain("legacy_table");
        tables.Should().NotContain("IF");
        tables.Should().NotContain("NOT");
        tables.Should().NotContain("EXISTS");
        tables.Should().NotContain("EXIST");
    }

    [Test]
    public void Soql_CustomObjects_And_ChildSubSelect()
    {
        var sql = """
            SELECT Id, Name, (SELECT LastName FROM Contacts)
            FROM Account__c
            WHERE IsActive__c = TRUE;
            """;

        var parser = SqlDialectParserFactory.GetParser(SqlDialect.Soql);
        var script = parser.Parse(sql);
        var tables = GetAllTableNames(script);

        tables.Should().Contain("Account__c");
        tables.Should().NotContain("TRUE");
    }
}
