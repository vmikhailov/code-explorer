using CodeExplorer.Common;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Parser;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
public class SqlParserFalsePositiveTests
{
    private static List<string> GetExtractedTableNames(string sql)
    {
        var q = NestedSqlParser.ParseNestedSql(sql, "test_query", "test.cs");
        if (q == null) return [];

        var tables = new List<string>();
        void Collect(IOntologyNode node)
        {
            if (node is TableNode t) tables.Add(t.Name);
            foreach (var child in node.Children) Collect(child);
        }
        Collect(q);
        return tables;
    }

    [Test]
    public void IsValidTableIdentifier_RejectsTemporaryTablesAndTableValuedFunctions()
    {
        // Temporary tables (SQLite / Postgres / SQL Server)
        PostIndexAnalyzer.IsValidTableIdentifier("temp_test_ids").Should().BeFalse("temp_ prefix denotes temporary scratch tables");
        PostIndexAnalyzer.IsValidTableIdentifier("temp_frontier").Should().BeFalse("temp_ prefix denotes temporary scratch tables");
        PostIndexAnalyzer.IsValidTableIdentifier("temp_target_names").Should().BeFalse("temp_ prefix denotes temporary scratch tables");
        PostIndexAnalyzer.IsValidTableIdentifier("temp_del_subpath_nodes").Should().BeFalse("temp_ prefix denotes temporary scratch tables");
        PostIndexAnalyzer.IsValidTableIdentifier("temp_del_ws_nodes").Should().BeFalse("temp_ prefix denotes temporary scratch tables");
        PostIndexAnalyzer.IsValidTableIdentifier("tmp_cache").Should().BeFalse("tmp_ prefix denotes temporary scratch tables");
        PostIndexAnalyzer.IsValidTableIdentifier("#local_temp").Should().BeFalse("# denotes local temporary table in T-SQL");
        PostIndexAnalyzer.IsValidTableIdentifier("##global_temp").Should().BeFalse("## denotes global temporary table in T-SQL");
        PostIndexAnalyzer.IsValidTableIdentifier("@tblVar").Should().BeFalse("@ denotes table variable in T-SQL");

        // Cut-off interpolation fragments ending in underscore
        PostIndexAnalyzer.IsValidTableIdentifier("table_").Should().BeFalse("trailing underscore denotes truncated interpolation token");

        // Built-in Table-Valued Functions (TVFs)
        PostIndexAnalyzer.IsValidTableIdentifier("json_each").Should().BeFalse("json_each is a SQLite/Postgres TVF, not a table");
        PostIndexAnalyzer.IsValidTableIdentifier("json_tree").Should().BeFalse("json_tree is a SQLite TVF, not a table");
        PostIndexAnalyzer.IsValidTableIdentifier("string_split").Should().BeFalse("string_split is a T-SQL TVF, not a table");
        PostIndexAnalyzer.IsValidTableIdentifier("openjson").Should().BeFalse("openjson is a T-SQL TVF, not a table");
        PostIndexAnalyzer.IsValidTableIdentifier("generate_series").Should().BeFalse("generate_series is a TVF, not a table");

        // Legitimate persistent tables
        PostIndexAnalyzer.IsValidTableIdentifier("orders").Should().BeTrue();
        PostIndexAnalyzer.IsValidTableIdentifier("users").Should().BeTrue();
        PostIndexAnalyzer.IsValidTableIdentifier("accounts_payable").Should().BeTrue();
        PostIndexAnalyzer.IsValidTableIdentifier("nodes").Should().BeTrue();
    }

    [Test]
    public void ExtractSqlDependencies_IgnoresCommentsWithFromOrIntoKeywords()
    {
        // Comment containing "into their ID" must not extract "their" as a table
        var sql1 = """
            SELECT id, name FROM nodes
            -- Pass 2: Property matching for nodes that don't embed the relative path into their ID
            WHERE id = @id
            """;
        var tables1 = GetExtractedTableNames(sql1);
        tables1.Should().ContainSingle().Which.Should().Be("nodes");
        tables1.Should().NotContain("their");

        // Comment containing "from test methods" must not extract "test" as a table
        var sql2 = """
            SELECT id FROM methods
            -- Anchor: direct calls or uses from test methods
            WHERE is_test = 1
            """;
        var tables2 = GetExtractedTableNames(sql2);
        tables2.Should().ContainSingle().Which.Should().Be("methods");
        tables2.Should().NotContain("test");

        // Multi-line block comment containing "into their records"
        var sql3 = """
            SELECT id, price FROM orders /* insert into their records later */ WHERE id = @id
            """;
        var tables3 = GetExtractedTableNames(sql3);
        tables3.Should().ContainSingle().Which.Should().Be("orders");
        tables3.Should().NotContain("their");
    }

    [Test]
    public void ExtractSqlDependencies_HandlesCteWithColumnListWithoutExtractingCteNameAsTable()
    {
        // CTE with column specifications: del_rec(id)
        var sql = """
            WITH RECURSIVE del_rec(id) AS (
                SELECT id FROM deleted_items
                UNION ALL
                SELECT c.id FROM items c JOIN del_rec d ON c.parent_id = d.id
            )
            SELECT * FROM del_rec
            """;
        var tables = GetExtractedTableNames(sql);
        tables.Should().NotContain("del_rec", "CTE names with column lists must not be extracted as persistent database tables");
        tables.Should().Contain("deleted_items");
        tables.Should().Contain("items");
    }

    [Test]
    public void ExtractSqlDependencies_HandlesMultipleCtesWithColumnLists()
    {
        var sql = """
            WITH RECURSIVE del_rec(id) AS (
                SELECT id FROM deleted_items
            ),
            del_nodes(id, parent_id) AS (
                SELECT id, parent_id FROM graph_nodes
            )
            SELECT * FROM del_rec JOIN del_nodes ON del_rec.id = del_nodes.id
            """;
        var tables = GetExtractedTableNames(sql);
        tables.Should().NotContain("del_rec");
        tables.Should().NotContain("del_nodes");
        tables.Should().Contain("deleted_items");
        tables.Should().Contain("graph_nodes");
    }

    [Test]
    public void ExtractSqlDependencies_IgnoresTableValuedFunctionsInFromAndJoin()
    {
        // json_each TVF
        var sql1 = "SELECT value FROM json_each(@namesJson)";
        var tables1 = GetExtractedTableNames(sql1);
        tables1.Should().NotContain("json_each");

        // json_tree TVF in JOIN
        var sql2 = "SELECT c.id, jt.value FROM customers c JOIN json_tree(c.details) jt WHERE jt.type = 'text'";
        var tables2 = GetExtractedTableNames(sql2);
        tables2.Should().ContainSingle().Which.Should().Be("customers");
        tables2.Should().NotContain("json_tree");

        // string_split TVF
        var sql3 = "SELECT * FROM STRING_SPLIT(@tags, ',')";
        var tables3 = GetExtractedTableNames(sql3);
        tables3.Should().NotContain("string_split");
    }

    [Test]
    public void ExtractSqlDependencies_IgnoresCreateTempTableAndTemporaryTables()
    {
        // CREATE TEMP TABLE IF NOT EXISTS
        var sql1 = "CREATE TEMP TABLE IF NOT EXISTS temp_test_ids (id TEXT PRIMARY KEY)";
        var tables1 = GetExtractedTableNames(sql1);
        tables1.Should().BeEmpty("temporary scratch tables should not be captured in schema ontology");

        // CREATE TEMPORARY TABLE
        var sql2 = "CREATE TEMPORARY TABLE temp_del_ws_nodes AS SELECT id FROM nodes";
        var tables2 = GetExtractedTableNames(sql2);
        tables2.Should().NotContain("temp_del_ws_nodes");

        // Query referencing temp table
        var sql3 = "SELECT * FROM temp_frontier";
        var tables3 = GetExtractedTableNames(sql3);
        tables3.Should().BeEmpty();
    }

    [Test]
    public void ExtractSqlDependencies_IgnoresCutOffInterpolationFragments()
    {
        var sql = "SELECT * FROM table_";
        var tables = GetExtractedTableNames(sql);
        tables.Should().BeEmpty("identifiers ending in trailing underscore denote cut-off interpolation tokens");
    }
}

