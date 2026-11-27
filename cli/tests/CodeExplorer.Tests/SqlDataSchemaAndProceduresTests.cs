using System.Text.Json;
using CodeExplorer.Core.Diagrams;
using CodeExplorer.Core.Parser.Sql;
using CodeExplorer.Core.Parser.Sql.Ast;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
public class SqlDataSchemaAndProceduresTests
{
    private static string GetPgxSamplePath()
    {
        var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "cli", "tests", "SampleProjects", "go", "pgx-resty-service");
            if (Directory.Exists(candidate)) return candidate;
            var direct = Path.Combine(dir.FullName, "SampleProjects", "go", "pgx-resty-service");
            if (Directory.Exists(direct)) return direct;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("Could not locate SampleProjects/go/pgx-resty-service.");
    }

    [Test]
    public async Task SampleProjects_SqlMigrations_ExtractsSchemaColumnsFksViewsProceduresFunctionsTriggersAndErDiagram()
    {
        var samplePath = GetPgxSamplePath();
        var tempDb = Path.Combine(Path.GetTempPath(), $"ce_sql_schema_{Guid.NewGuid():N}.db");

        try
        {
            await using var db = new SqliteGraphClient(tempDb);
            var indexer = new WorkspaceIndexer(db);
            await indexer.IndexAsync(samplePath, clear: true);

            // 1. Tables & Columns (including ALTER TABLE ADD COLUMN)
            var tableRows = await db.QueryAsync<(string Name, string? SubKind, string? Columns)>(
                "MATCH (t:Table) RETURN t.name AS name, t.sub_kind AS sub_kind, t.columns AS columns");
            var byName = tableRows.ToDictionary(r => r.Name, StringComparer.OrdinalIgnoreCase);

            byName.Should().ContainKey("customers");
            byName.Should().ContainKey("orders");
            byName.Should().ContainKey("order_items");
            byName.Should().ContainKey("audit_log");
            byName.Should().ContainKey("active_customer_orders");
            byName.Should().ContainKey("mv_daily_revenue");

            byName["orders"].SubKind.Should().Be("table");
            byName["active_customer_orders"].SubKind.Should().Be("view");
            byName["mv_daily_revenue"].SubKind.Should().Be("materialized_view");

            // Verify columns JSON on orders includes PK, FK, and ALTER TABLE column shipped_at
            byName["orders"].Columns.Should().NotBeNullOrWhiteSpace();
            using (var doc = JsonDocument.Parse(byName["orders"].Columns!))
            {
                var cols = doc.RootElement.EnumerateArray().ToList();
                cols.Should().Contain(c => c.GetProperty("name").GetString() == "id" && c.GetProperty("pk").GetBoolean());
                cols.Should().Contain(c => c.GetProperty("name").GetString() == "customer_id" && c.GetProperty("fk").GetString() == "customers.id");
                cols.Should().Contain(c => c.GetProperty("name").GetString() == "shipped_at");
            }

            // 2. Foreign key REFERENCES edges
            var fkRows = await db.QueryAsync<(string Child, string Parent)>(
                "MATCH (c:Table)-[:REFERENCES]->(p:Table) RETURN c.name AS child, p.name AS parent");
            fkRows.Should().Contain(r => r.Child.Equals("orders", StringComparison.OrdinalIgnoreCase) && r.Parent.Equals("customers", StringComparison.OrdinalIgnoreCase));
            fkRows.Should().Contain(r => r.Child.Equals("order_items", StringComparison.OrdinalIgnoreCase) && r.Parent.Equals("orders", StringComparison.OrdinalIgnoreCase));

            // 3. View READS_FROM lineage
            var viewReads = await db.QueryAsync<(string View, string Src)>(
                "MATCH (v:Table)-[:READS_FROM]->(s:Table) RETURN v.name AS view, s.name AS src");
            viewReads.Should().Contain(r => r.View.Equals("active_customer_orders", StringComparison.OrdinalIgnoreCase) && r.Src.Equals("orders", StringComparison.OrdinalIgnoreCase));
            viewReads.Should().Contain(r => r.View.Equals("active_customer_orders", StringComparison.OrdinalIgnoreCase) && r.Src.Equals("customers", StringComparison.OrdinalIgnoreCase));
            viewReads.Should().Contain(r => r.View.Equals("mv_daily_revenue", StringComparison.OrdinalIgnoreCase) && r.Src.Equals("order_items", StringComparison.OrdinalIgnoreCase));

            // 4. Procedures, Functions & Triggers
            var procRows = await db.QueryAsync<(string Name, string? SubKind, string? TrigTable, string? TrigEvent)>(
                "MATCH (p:Procedure) RETURN p.name AS name, p.sub_kind AS sub_kind, p.trigger_table AS trigger_table, p.trigger_event AS trigger_event");
            var procsByName = procRows.ToDictionary(r => r.Name, StringComparer.OrdinalIgnoreCase);

            procsByName.Should().ContainKey("sp_complete_order");
            procsByName["sp_complete_order"].SubKind.Should().Be("procedure");
            procsByName.Should().ContainKey("fn_record_audit");
            procsByName["fn_record_audit"].SubKind.Should().Be("function");
            procsByName.Should().ContainKey("trg_orders_after_insert");
            procsByName["trg_orders_after_insert"].SubKind.Should().Be("trigger");
            procsByName["trg_orders_after_insert"].TrigTable.Should().Be("orders");
            procsByName["trg_orders_after_insert"].TrigEvent.Should().Contain("AFTER").And.Contain("INSERT");

            // Procedure READS_FROM / WRITES_TO / CALLS
            var procReads = await db.QueryAsync<(string Proc, string Table)>(
                "MATCH (p:Procedure)-[:READS_FROM]->(t:Table) RETURN p.name AS proc, t.name AS tbl");
            procReads.Should().Contain(r => r.Proc.Equals("sp_complete_order", StringComparison.OrdinalIgnoreCase) && r.Table.Equals("orders", StringComparison.OrdinalIgnoreCase));
            procReads.Should().Contain(r => r.Proc.Equals("sp_complete_order", StringComparison.OrdinalIgnoreCase) && r.Table.Equals("order_items", StringComparison.OrdinalIgnoreCase));

            var procWrites = await db.QueryAsync<(string Proc, string Table)>(
                "MATCH (p:Procedure)-[:WRITES_TO]->(t:Table) RETURN p.name AS proc, t.name AS tbl");
            procWrites.Should().Contain(r => r.Proc.Equals("sp_complete_order", StringComparison.OrdinalIgnoreCase) && r.Table.Equals("orders", StringComparison.OrdinalIgnoreCase));
            procWrites.Should().Contain(r => r.Proc.Equals("fn_record_audit", StringComparison.OrdinalIgnoreCase) && r.Table.Equals("audit_log", StringComparison.OrdinalIgnoreCase));

            var procCalls = await db.QueryAsync<(string Caller, string Callee)>(
                "MATCH (p:Procedure)-[:CALLS]->(c:Procedure) RETURN p.name AS caller, c.name AS callee");
            procCalls.Should().Contain(r => r.Caller.Equals("sp_complete_order", StringComparison.OrdinalIgnoreCase) && r.Callee.Equals("fn_record_audit", StringComparison.OrdinalIgnoreCase));
            procCalls.Should().Contain(r => r.Caller.Equals("trg_orders_after_insert", StringComparison.OrdinalIgnoreCase) && r.Callee.Equals("fn_record_audit", StringComparison.OrdinalIgnoreCase));

            // Cross-file Table -[:TRIGGERS]-> Trigger
            var triggerEdges = await db.QueryAsync<(string Table, string Trigger)>(
                "MATCH (t:Table)-[:TRIGGERS]->(p:Procedure) RETURN t.name AS tbl, p.name AS trig");
            triggerEdges.Should().Contain(r => r.Table.Equals("orders", StringComparison.OrdinalIgnoreCase) && r.Trigger.Equals("trg_orders_after_insert", StringComparison.OrdinalIgnoreCase));

            // 5. Mermaid ER Diagram export (both direct and via ExportAsync --type er)
            var erDiagram = await DiagramExporter.ExportAsync(db, format: "mermaid", type: "er");
            erDiagram.Should().StartWith("erDiagram");
            erDiagram.Should().Contain("customers ||--o{ orders : references");
            erDiagram.Should().Contain("orders ||--o{ order_items : references");
            erDiagram.Should().Contain("id PK");
            erDiagram.Should().Contain("customer_id FK");

            // 6. inspect_data_lineage surfaces schema, FKs, views, procedures, and triggers
            var lineageYaml = await McpLineageFormatter.InspectDataLineageYamlAsync(db, "orders", samplePath, CancellationToken.None);
            lineageYaml.Should().Contain("subKind: table");
            lineageYaml.Should().Contain("customers");
            lineageYaml.Should().Contain("order_items");
            lineageYaml.Should().Contain("active_customer_orders");
            lineageYaml.Should().Contain("sp_complete_order");
            lineageYaml.Should().Contain("trg_orders_after_insert");
        }
        finally
        {
            if (File.Exists(tempDb)) File.Delete(tempDb);
        }
    }

    [Test]
    public async Task OrmSchemaEnricher_EfCoreEntitiesWithoutSqlFiles_ExtractsColumnsAndForeignKeyReferences()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"ce_orm_er_{Guid.NewGuid():N}");
        var tempDb = Path.Combine(Path.GetTempPath(), $"ce_orm_er_{Guid.NewGuid():N}.db");
        Directory.CreateDirectory(tempDir);

        try
        {
            await File.WriteAllTextAsync(Path.Combine(tempDir, "Shop.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>");
            await File.WriteAllTextAsync(Path.Combine(tempDir, "Models.cs"), """
                using System.ComponentModel.DataAnnotations;
                using System.ComponentModel.DataAnnotations.Schema;

                namespace Shop;

                [Table("customers")]
                public class Customer
                {
                    [Key]
                    public int Id { get; set; }
                    [Required]
                    public string Email { get; set; } = "";
                }

                [Table("orders")]
                public class Order
                {
                    [Key]
                    public int Id { get; set; }
                    public int CustomerId { get; set; }
                    public Customer? Customer { get; set; }
                    public decimal Total { get; set; }
                }
                """);

            await using var db = new SqliteGraphClient(tempDb);
            var indexer = new WorkspaceIndexer(db);
            await indexer.IndexAsync(tempDir, clear: true);

            var fkRows = await db.QueryAsync<(string Child, string Parent)>(
                "MATCH (c:Table)-[:REFERENCES]->(p:Table) RETURN c.name AS child, p.name AS parent");
            fkRows.Should().Contain(r =>
                r.Child.Equals("orders", StringComparison.OrdinalIgnoreCase) &&
                r.Parent.Equals("customers", StringComparison.OrdinalIgnoreCase));

            var erDiagram = await DiagramExporter.ExportAsync(db, format: "mermaid", type: "er");
            erDiagram.Should().StartWith("erDiagram");
            erDiagram.Should().Contain("customers ||--o{ orders : references");
            erDiagram.Should().Contain("Id PK");
            erDiagram.Should().Contain("CustomerId FK");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
            if (File.Exists(tempDb)) File.Delete(tempDb);
        }
    }

    [Test]
    public void TSqlDialect_ParsesTriggerWithInlineBodyAndAlterTableForeignKey()
    {
        const string sql = """
            CREATE TABLE dbo.Invoices (
                Id INT PRIMARY KEY,
                CustomerId INT NOT NULL
            );

            ALTER TABLE dbo.Invoices
                ADD CONSTRAINT FK_Invoices_Customers FOREIGN KEY (CustomerId) REFERENCES dbo.Customers(Id);

            CREATE TRIGGER dbo.trg_Invoices_Audit
            ON dbo.Invoices
            AFTER INSERT, UPDATE
            AS
            BEGIN
                INSERT INTO dbo.InvoiceAudit (InvoiceId)
                SELECT Id FROM dbo.Invoices;
            END
            """;

        var parser = SqlDialectParserFactory.GetParser(SqlDialect.TSql);
        var script = parser.Parse(sql);

        var alter = script.Statements.OfType<SqlAlterTableStatementAst>().Single();
        alter.TargetTable.TableName.Should().Be("Invoices");
        alter.ForeignKeys.Should().ContainSingle(fk =>
            fk.FromColumn == "CustomerId" &&
            fk.TargetTable.TableName == "Customers" &&
            fk.ToColumn == "Id");

        var trigger = script.Statements.OfType<SqlCreateProcedureStatementAst>().Single();
        trigger.RoutineKind.Should().Be(SqlRoutineKind.Trigger);
        trigger.Procedure.ProcedureName.Should().Be("trg_Invoices_Audit");
        trigger.TriggerTable!.TableName.Should().Be("Invoices");
        trigger.TriggerEvent.Should().Contain("AFTER").And.Contain("INSERT").And.Contain("UPDATE");
        trigger.BodyStatements.OfType<SqlInsertStatementAst>().Should().ContainSingle(i => i.TargetTable.TableName == "InvoiceAudit");
    }
}
