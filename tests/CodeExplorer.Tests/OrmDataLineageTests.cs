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
}
