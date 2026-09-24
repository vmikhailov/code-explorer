using System.Text.Json;
using NUnit.Framework;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Mcp;
using CodeExplorer.Core.Parser;
using CodeExplorer.Parser.CSharp;
using CodeExplorer.Parser.Java;
using CodeExplorer.Parser.TypeScript;

namespace CodeExplorer.Tests;

[TestFixture]
[Category("Integration")]
public class OrmDataLineageTests
{
    private string _tempWorkspace = null!;
    private string _dbPath = null!;
    private SqliteGraphClient _client = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _tempWorkspace = Path.Combine(Path.GetTempPath(), "codeexplorer_orm_test_" + Guid.NewGuid()).Replace('\\', '/');
        Directory.CreateDirectory(_tempWorkspace);

        // 1. C# with EF Core DbContext and [Table("customers")]
        var csharpDir = Path.Combine(_tempWorkspace, "DotNetOrmApp").Replace('\\', '/');
        Directory.CreateDirectory(csharpDir);
        await File.WriteAllTextAsync(Path.Combine(csharpDir, "DotNetOrmApp.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>");

        var csharpCode = @"
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace DotNetOrmApp;

[Table(""customers"")]
public class Customer
{
    public int Id { get; set; }
    public string Name { get; set; }
}

public class AppDbContext : DbContext
{
    public DbSet<Customer> Customers { get; set; }
    public DbSet<OrderEntity> Orders { get; set; }
}

public class OrderEntity
{
    public int Id { get; set; }
    public decimal Amount { get; set; }
}

public class Invoice
{
    public int Id { get; set; }
    public string Number { get; set; }
}

public class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<Invoice> builder)
    {
        builder.ToTable(""invoices"");
    }
}
";
        await File.WriteAllTextAsync(Path.Combine(csharpDir, "AppDbContext.cs"), csharpCode);

        // 2. Java with JPA @Entity and @Table(name = "jpa_accounts")
        var javaDir = Path.Combine(_tempWorkspace, "JavaOrmApp").Replace('\\', '/');
        Directory.CreateDirectory(javaDir);
        await File.WriteAllTextAsync(Path.Combine(javaDir, "pom.xml"), "<project></project>");

        var javaCode = @"
package com.example.orm;

import jakarta.persistence.Entity;
import jakarta.persistence.Table;
import jakarta.persistence.Id;

@Entity
@Table(name = ""jpa_accounts"")
public class Account {
    @Id
    private Long id;
    private String accountNumber;
}
";
        await File.WriteAllTextAsync(Path.Combine(javaDir, "Account.java"), javaCode);

        // 3. TypeScript with TypeORM @Entity('typeorm_products')
        var tsDir = Path.Combine(_tempWorkspace, "TsOrmApp").Replace('\\', '/');
        Directory.CreateDirectory(tsDir);
        await File.WriteAllTextAsync(Path.Combine(tsDir, "package.json"), "{}");

        var tsCode = @"
import { Entity, PrimaryGeneratedColumn, Column } from 'typeorm';

@Entity('typeorm_products')
export class Product {
    @PrimaryGeneratedColumn()
    id: number;

    @Column()
    title: string;
}
";
        await File.WriteAllTextAsync(Path.Combine(tsDir, "Product.ts"), tsCode);

        _dbPath = Path.Combine(_tempWorkspace, "orm_graph.db");
        _client = new SqliteGraphClient(_dbPath);

        WorkspaceIndexer.Register(new CSharpParser());
        WorkspaceIndexer.Register(new JavaParser());
        WorkspaceIndexer.Register(new TypeScriptParser());

        var indexer = new WorkspaceIndexer(_client);
        await indexer.IndexAsync(_tempWorkspace, _tempWorkspace, clear: true);
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        await _client.DisposeAsync();
        if (Directory.Exists(_tempWorkspace))
        {
            try { Directory.Delete(_tempWorkspace, true); } catch { }
        }
    }

    [Test]
    public async Task Test_EfCore_ExtractsTablesAndPersistedInRelationships()
    {
        // Check customers table from [Table("customers")]
        var qTable = "MATCH (t:Table) WHERE t.name = 'customers' OR t.name = 'Orders' OR t.name = 'orders' OR t.name = 'invoices' RETURN t.name AS name";
        var res = await _client.ExecuteQueryAsync(qTable);
        using var doc = JsonDocument.Parse(res);
        var tables = doc.RootElement.EnumerateArray().Select(x => x.GetProperty("name").GetString()).ToList();

        Assert.That(tables, Does.Contain("customers").Or.Contain("Orders").Or.Contain("orders"), "EF Core tables not extracted!");
        Assert.That(tables, Does.Contain("invoices"), "EF Core IEntityTypeConfiguration ToTable not extracted!");

        // Check PERSISTED_IN relationship between Customer Type and Table
        var qRel = "MATCH (c:Type)-[:PERSISTED_IN]->(t:Table) WHERE c.name = 'Customer' OR c.name = 'OrderEntity' OR c.name = 'Invoice' RETURN c.name AS className, t.name AS tableName";
        var resRel = await _client.ExecuteQueryAsync(qRel);
        using var docRel = JsonDocument.Parse(resRel);
        Assert.That(docRel.RootElement.GetArrayLength(), Is.GreaterThan(0), "No PERSISTED_IN relationship found for EF Core entity!");
    }

    [Test]
    public async Task Test_JpaHibernate_ExtractsTablesAndPersistedInRelationships()
    {
        var qRel = "MATCH (a:Type)-[:PERSISTED_IN]->(t:Table) WHERE a.name = 'Account' RETURN a.name AS className, t.name AS tableName";
        var resRel = await _client.ExecuteQueryAsync(qRel);
        using var docRel = JsonDocument.Parse(resRel);
        var array = docRel.RootElement.EnumerateArray().ToList();

        Assert.That(array.Count, Is.GreaterThan(0), "JPA Account entity not linked to Table via PERSISTED_IN!");
        var tableName = array[0].GetProperty("tableName").GetString();
        Assert.That(tableName, Is.EqualTo("jpa_accounts").IgnoreCase);
    }

    [Test]
    public async Task Test_TypeOrm_ExtractsTablesAndPersistedInRelationships()
    {
        var qRel = "MATCH (p:Type)-[:PERSISTED_IN]->(t:Table) WHERE p.name = 'Product' RETURN p.name AS className, t.name AS tableName";
        var resRel = await _client.ExecuteQueryAsync(qRel);
        using var docRel = JsonDocument.Parse(resRel);
        var array = docRel.RootElement.EnumerateArray().ToList();

        Assert.That(array.Count, Is.GreaterThan(0), "TypeORM Product entity not linked to Table via PERSISTED_IN!");
        var tableName = array[0].GetProperty("tableName").GetString();
        Assert.That(tableName, Is.EqualTo("typeorm_products").IgnoreCase);
    }

    [Test]
    public async Task Test_InspectDataLineage_BuiltInQuery_WorksForOrmEntities()
    {
        var query = Queries.Get("inspect_data_lineage");
        var res = await _client.ExecuteQueryAsync(query, new Dictionary<string, object?> { ["tableName"] = "jpa_accounts" });
        using var doc = JsonDocument.Parse(res);
        var array = doc.RootElement.EnumerateArray().ToList();

        Assert.That(array.Count, Is.GreaterThan(0), "inspect_data_lineage returned 0 rows for jpa_accounts table!");
        var tableName = array[0].GetProperty("tableName").GetString();
        Assert.That(tableName, Is.EqualTo("jpa_accounts").IgnoreCase);
    }

    [Test]
    public void Test_Database_Deduplication_And_Canonicalization()
    {
        var (tName1, tType1, tKey1) = CodeExplorer.Core.Parser.PostIndexAnalyzer.CanonicalizeDatabase("typeorm", "relational");
        var (tName2, tType2, tKey2) = CodeExplorer.Core.Parser.PostIndexAnalyzer.CanonicalizeDatabase("TypeORM", "relational");
        Assert.That(tName1, Is.EqualTo("TypeORM"));
        Assert.That(tName2, Is.EqualTo("TypeORM"));
        Assert.That(tKey1, Is.EqualTo("typeorm"));
        Assert.That(tKey2, Is.EqualTo("typeorm"));

        var (pName1, _, pKey1) = CodeExplorer.Core.Parser.PostIndexAnalyzer.CanonicalizeDatabase("postgres", null);
        var (pName2, _, pKey2) = CodeExplorer.Core.Parser.PostIndexAnalyzer.CanonicalizeDatabase("PostgreSQL", "relational");
        Assert.That(pName1, Is.EqualTo("PostgreSQL"));
        Assert.That(pName2, Is.EqualTo("PostgreSQL"));
        Assert.That(pKey1, Is.EqualTo("postgresql"));
        Assert.That(pKey2, Is.EqualTo("postgresql"));

        var (rName1, rType1, rKey1) = CodeExplorer.Core.Parser.PostIndexAnalyzer.CanonicalizeDatabase("REDIS_HOST", "cache");
        var (rName2, rType2, rKey2) = CodeExplorer.Core.Parser.PostIndexAnalyzer.CanonicalizeDatabase("redis", "keyvalue");
        Assert.That(rName1, Is.EqualTo("Redis"));
        Assert.That(rName2, Is.EqualTo("Redis"));
        Assert.That(rType1, Is.EqualTo("cache"));
        Assert.That(rType2, Is.EqualTo("cache"));
        Assert.That(rKey1, Is.EqualTo("redis"));
        Assert.That(rKey2, Is.EqualTo("redis"));
    }

    [Test]
    public async Task Test_ArchitectureGraph_Consolidates_TypeOrm_And_Cased_Databases()
    {
        var tempWorkspace = Path.Combine(Path.GetTempPath(), "ce_graph_dedup_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempWorkspace);
        try
        {
            var dbPath = Path.Combine(tempWorkspace, "graph.db").Replace('\\', '/');
            using var db = new SqliteGraphClient(dbPath);

            var nodes = new List<CodeExplorer.Core.Database.Node>
            {
                new("proj:svc_a", "Project", new Dictionary<string, object> { ["name"] = "ServiceA", ["path"] = "/src/a", ["project_type"] = "typescript" }),
                new("proj:svc_b", "Project", new Dictionary<string, object> { ["name"] = "ServiceB", ["path"] = "/src/b", ["project_type"] = "typescript" }),
                // Casing variants of TypeORM and project-scoped DBs
                new("workspace:project:svc_a:db:typeorm", "Database", new Dictionary<string, object> { ["name"] = "typeorm", ["db_type"] = "relational" }),
                new("workspace:project:svc_b:db:TypeORM", "Database", new Dictionary<string, object> { ["name"] = "TypeORM", ["db_type"] = "relational" }),
                new("workspace:database:relational:typeorm", "Database", new Dictionary<string, object> { ["name"] = "typeorm", ["db_type"] = "relational" }),
                // Casing variants of PostgreSQL
                new("workspace:database:relational:PostgreSQL", "Database", new Dictionary<string, object> { ["name"] = "PostgreSQL", ["db_type"] = "relational" }),
                new("workspace:database:relational:postgres", "Database", new Dictionary<string, object> { ["name"] = "postgres", ["db_type"] = "relational" }),
            };
            await db.UploadNodesAsync(nodes);

            var rels = new List<CodeExplorer.Core.Database.Relationship>
            {
                new("proj:svc_a", "workspace:project:svc_a:db:typeorm", "USES_DB", new Dictionary<string, object> { ["kind"] = "USES_DB" }),
                new("proj:svc_b", "workspace:project:svc_b:db:TypeORM", "USES_DB", new Dictionary<string, object> { ["kind"] = "USES_DB" }),
                new("proj:svc_a", "workspace:database:relational:postgres", "USES_DB", new Dictionary<string, object> { ["kind"] = "USES_DB" }),
                new("proj:svc_b", "workspace:database:relational:PostgreSQL", "USES_DB", new Dictionary<string, object> { ["kind"] = "USES_DB" }),
            };
            await db.UploadRelationshipsAsync(rels);

            var graph = await CodeExplorer.Core.Protocol.GraphDataConverter.GetArchitectureGraphAsync(db);

            // Exactly 1 TypeORM node
            var typeOrmNodes = graph.Nodes.Where(n => n.Name.Equals("TypeORM", StringComparison.OrdinalIgnoreCase)).ToList();
            Assert.That(typeOrmNodes, Has.Count.EqualTo(1), "All TypeORM nodes must collapse to exactly 1 node");
            Assert.That(typeOrmNodes[0].Name, Is.EqualTo("TypeORM"));
            Assert.That(typeOrmNodes[0].Id, Is.EqualTo("workspace:database:relational:typeorm"));

            // Exactly 1 PostgreSQL node
            var postgresNodes = graph.Nodes.Where(n => n.Name.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase)).ToList();
            Assert.That(postgresNodes, Has.Count.EqualTo(1), "All PostgreSQL nodes must collapse to exactly 1 node");
            Assert.That(postgresNodes[0].Name, Is.EqualTo("PostgreSQL"));
            Assert.That(postgresNodes[0].Id, Is.EqualTo("workspace:database:relational:postgresql"));

            // Edges must point to canonical IDs
            var aToTypeOrm = graph.Edges.FirstOrDefault(e => e.Source == "proj:svc_a" && e.Target == "workspace:database:relational:typeorm");
            var bToTypeOrm = graph.Edges.FirstOrDefault(e => e.Source == "proj:svc_b" && e.Target == "workspace:database:relational:typeorm");
            Assert.That(aToTypeOrm, Is.Not.Null, "Service A must connect to canonical TypeORM");
            Assert.That(bToTypeOrm, Is.Not.Null, "Service B must connect to canonical TypeORM");

            var aToPg = graph.Edges.FirstOrDefault(e => e.Source == "proj:svc_a" && e.Target == "workspace:database:relational:postgresql");
            var bToPg = graph.Edges.FirstOrDefault(e => e.Source == "proj:svc_b" && e.Target == "workspace:database:relational:postgresql");
            Assert.That(aToPg, Is.Not.Null, "Service A must connect to canonical PostgreSQL");
            Assert.That(bToPg, Is.Not.Null, "Service B must connect to canonical PostgreSQL");
        }
        finally
        {
            try { Directory.Delete(tempWorkspace, true); } catch { }
        }
    }

    [Test]
    public void Test_TypeScriptParser_GetProjectName_UnscopesPackageJson()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ts_proj_name_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var parser = new TypeScriptParser();
            File.WriteAllText(Path.Combine(tempDir, "package.json"), "{\"name\": \"@ats/sample-service\"}");
            var name = parser.GetProjectName(tempDir, ["package.json"]);
            Assert.That(name, Is.EqualTo("sample-service"));

            File.WriteAllText(Path.Combine(tempDir, "package.json"), "{\"name\": \"direct-name\"}");
            var directName = parser.GetProjectName(tempDir, ["package.json"]);
            Assert.That(directName, Is.EqualTo("direct-name"));
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Test]
    public async Task Test_TypeScriptParser_Creates_UsesDb_For_TypeOrm_Import()
    {
        var qDb = "MATCH (d:Database) WHERE d.name = 'TypeORM' RETURN d.name AS name, d.id AS id";
        var resDb = await _client.ExecuteQueryAsync(qDb);
        using var docDb = JsonDocument.Parse(resDb);
        var dbList = docDb.RootElement.EnumerateArray().ToList();
        Assert.That(dbList, Is.Not.Empty, "Database node for TypeORM should exist in graph");

        var qRel = "MATCH (p:Project)-[:USES_DB]->(d:Database) WHERE d.name = 'TypeORM' RETURN p.name AS projName, d.name AS dbName";
        var resRel = await _client.ExecuteQueryAsync(qRel);
        using var docRel = JsonDocument.Parse(resRel);
        var relList = docRel.RootElement.EnumerateArray().ToList();
        Assert.That(relList, Is.Not.Empty, "USES_DB relationship from project to TypeORM should exist");
    }


    [Test]
    public async Task Test_GraphDataConverter_Does_Not_Convert_Project_To_Database()
    {
        var tempWorkspace = Path.Combine(Path.GetTempPath(), "proj_not_db_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempWorkspace);
        try
        {
            var dbPath = Path.Combine(tempWorkspace, "graph.db").Replace('\\', '/');
            using var db = new SqliteGraphClient(dbPath);

            var nodes = new List<CodeExplorer.Core.Database.Node>
            {
                new("proj:svc_a", "Project", new Dictionary<string, object> { ["name"] = "ServiceA", ["path"] = "/src/a", ["project_type"] = "typescript", ["db_type"] = "relational" }),
                new("workspace:database:relational:typeorm", "Database", new Dictionary<string, object> { ["name"] = "TypeORM", ["db_type"] = "relational" })
            };
            await db.UploadNodesAsync(nodes);

            var rels = new List<CodeExplorer.Core.Database.Relationship>
            {
                new("proj:svc_a", "workspace:database:relational:typeorm", "USES_DB", new Dictionary<string, object> { ["kind"] = "USES_DB" })
            };
            await db.UploadRelationshipsAsync(rels);

            var graph = await CodeExplorer.Core.Protocol.GraphDataConverter.GetArchitectureGraphAsync(db);

            var projNode = graph.Nodes.FirstOrDefault(n => n.Id == "proj:svc_a");
            Assert.That(projNode, Is.Not.Null);
            Assert.That(projNode.Kind, Is.EqualTo("Project"), "Project node with db_type property must not be converted to Kind 'Database'!");

            var dbNode = graph.Nodes.FirstOrDefault(n => n.Id == "workspace:database:relational:typeorm");
            Assert.That(dbNode, Is.Not.Null);
            Assert.That(dbNode.Kind, Is.EqualTo("Database"));
        }
        finally
        {
            try { Directory.Delete(tempWorkspace, true); } catch { }
        }
    }

    [Test]
    public async Task Test_PostIndexAnalyzer_Canonicalizes_Databases_And_Links_Projects()
    {
        var tempWorkspace = Path.Combine(Path.GetTempPath(), "postindex_canonical_db_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempWorkspace);
        try
        {
            var dbPath = Path.Combine(tempWorkspace, "graph.db").Replace('\\', '/');
            using var db = new SqliteGraphClient(dbPath);

            var nodes = new List<CodeExplorer.Core.Database.Node>
            {
                new("workspace:project:order_service", "Project", new Dictionary<string, object> { ["name"] = "OrderService", ["path"] = "/src/order_service" }),
                new("workspace:project:billing_service", "Project", new Dictionary<string, object> { ["name"] = "BillingService", ["path"] = "/src/billing_service" }),
                // Project-scoped database node
                new("workspace:project:order_service:db:typeorm", "Database", new Dictionary<string, object> { ["name"] = "TypeORM", ["db_type"] = "relational" }),
                // Cased raw database node
                new("workspace:database:relational:PostgreSQL", "Database", new Dictionary<string, object> { ["name"] = "PostgreSQL", ["db_type"] = "relational" }),
                // Generic database name
                new("workspace:database:relational:orders_db", "Database", new Dictionary<string, object> { ["name"] = "orders_db", ["db_type"] = "relational" })
            };
            await db.UploadNodesAsync(nodes);

            var rels = new List<CodeExplorer.Core.Database.Relationship>
            {
                new("workspace:project:order_service", "workspace:project:order_service:db:typeorm", "USES_DB", new Dictionary<string, object> { ["kind"] = "USES_DB" }),
                new("workspace:project:billing_service", "workspace:database:relational:PostgreSQL", "USES_DB", new Dictionary<string, object> { ["kind"] = "USES_DB" }),
                new("workspace:project:billing_service", "workspace:database:relational:orders_db", "USES_DB", new Dictionary<string, object> { ["kind"] = "USES_DB" })
            };
            await db.UploadRelationshipsAsync(rels);

            var analyzer = new PostIndexAnalyzer(db);
            await analyzer.RunAsync("workspace");

            // 1. Verify canonical database nodes exist in the graph
            var typeOrmDb = await db.ExecuteQueryAsync("MATCH (d:Database) WHERE d.id = 'workspace:database:relational:typeorm' RETURN d.id AS id, d.name AS name, d.is_canonical AS is_canonical");
            using (var doc = JsonDocument.Parse(typeOrmDb))
            {
                var rows = doc.RootElement.EnumerateArray().ToList();
                Assert.That(rows, Has.Count.EqualTo(1));
                Assert.That(rows[0].GetProperty("name").GetString(), Is.EqualTo("TypeORM"));
                Assert.That(rows[0].GetProperty("is_canonical").GetString(), Is.EqualTo("true"));
            }

            var postgresDb = await db.ExecuteQueryAsync("MATCH (d:Database) WHERE d.id = 'workspace:database:relational:postgresql' RETURN d.id AS id, d.name AS name");
            using (var doc = JsonDocument.Parse(postgresDb))
            {
                var rows = doc.RootElement.EnumerateArray().ToList();
                Assert.That(rows, Has.Count.EqualTo(1));
                Assert.That(rows[0].GetProperty("name").GetString(), Is.EqualTo("PostgreSQL"));
            }

            // 2. Verify non-canonical project-scoped node was cleaned up
            var staleDb = await db.ExecuteQueryAsync("MATCH (d:Database) WHERE d.id = 'workspace:project:order_service:db:typeorm' RETURN d.id AS id");
            using (var doc = JsonDocument.Parse(staleDb))
            {
                Assert.That(doc.RootElement.EnumerateArray().Count(), Is.EqualTo(0));
            }

            // 3. Verify direct USES_DB relationships point to the canonical database nodes
            var orderUsesDb = await db.ExecuteQueryAsync("MATCH (p:Project)-[r:USES_DB]->(d:Database) WHERE p.id = 'workspace:project:order_service' RETURN d.id AS dbId, r.is_canonical AS isCanonical");
            using (var doc = JsonDocument.Parse(orderUsesDb))
            {
                var rows = doc.RootElement.EnumerateArray().ToList();
                Assert.That(rows, Has.Count.EqualTo(1));
                Assert.That(rows[0].GetProperty("dbId").GetString(), Is.EqualTo("workspace:database:relational:typeorm"));
                Assert.That(rows[0].GetProperty("isCanonical").GetString(), Is.EqualTo("true"));
            }

            var billingUsesDb = await db.ExecuteQueryAsync("MATCH (p:Project)-[r:USES_DB]->(d:Database) WHERE p.id = 'workspace:project:billing_service' RETURN d.id AS dbId ORDER BY d.id");
            using (var doc = JsonDocument.Parse(billingUsesDb))
            {
                var rows = doc.RootElement.EnumerateArray().ToList();
                Assert.That(rows, Has.Count.EqualTo(2));
                var targets = rows.Select(r => r.GetProperty("dbId").GetString()).ToList();
                Assert.That(targets, Does.Contain("workspace:database:relational:postgresql"));
                Assert.That(targets, Does.Contain("workspace:database:relational:orders_db"));
            }
        }
        finally
        {
            try { Directory.Delete(tempWorkspace, true); } catch { }
        }
    }
}
