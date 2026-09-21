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
        var (tName1, tType1, tKey1) = CodeExplorer.Core.Protocol.GraphDataConverter.CanonicalizeDatabase("typeorm", "relational");
        var (tName2, tType2, tKey2) = CodeExplorer.Core.Protocol.GraphDataConverter.CanonicalizeDatabase("TypeORM", "relational");
        Assert.That(tName1, Is.EqualTo("TypeORM"));
        Assert.That(tName2, Is.EqualTo("TypeORM"));
        Assert.That(tKey1, Is.EqualTo("typeorm"));
        Assert.That(tKey2, Is.EqualTo("typeorm"));

        var (pName1, _, pKey1) = CodeExplorer.Core.Protocol.GraphDataConverter.CanonicalizeDatabase("postgres", null);
        var (pName2, _, pKey2) = CodeExplorer.Core.Protocol.GraphDataConverter.CanonicalizeDatabase("PostgreSQL", "relational");
        Assert.That(pName1, Is.EqualTo("PostgreSQL"));
        Assert.That(pName2, Is.EqualTo("PostgreSQL"));
        Assert.That(pKey1, Is.EqualTo("postgresql"));
        Assert.That(pKey2, Is.EqualTo("postgresql"));

        var (rName1, rType1, rKey1) = CodeExplorer.Core.Protocol.GraphDataConverter.CanonicalizeDatabase("REDIS_HOST", "cache");
        var (rName2, rType2, rKey2) = CodeExplorer.Core.Protocol.GraphDataConverter.CanonicalizeDatabase("redis", "keyvalue");
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
}
