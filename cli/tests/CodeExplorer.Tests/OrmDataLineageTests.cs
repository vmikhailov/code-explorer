using System.Text.Json;
using CodeExplorer.Cypher.Common;
using CodeExplorer.Parser.CSharp;
using CodeExplorer.Parser.Java;
using CodeExplorer.Parser.TypeScript;
using NUnit.Framework;

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
        await File.WriteAllTextAsync(Path.Combine(csharpDir, "DotNetOrmApp.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"><ItemGroup><PackageReference Include=\"Npgsql.EntityFrameworkCore.PostgreSQL\" Version=\"8.0.0\" /></ItemGroup></Project>");

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
        await File.WriteAllTextAsync(Path.Combine(tsDir, "package.json"), "{\"name\": \"ts-orm-app\", \"dependencies\": {\"typeorm\": \"^0.3.0\", \"pg\": \"^8.11.0\"}}");

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

        WorkspaceIndexer.AutoRegisterParsers();

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

        tables.Any(t => t is "customers" or "Orders" or "orders").Should().BeTrue("EF Core tables not extracted!");
        tables.Should().Contain("invoices", "EF Core IEntityTypeConfiguration ToTable not extracted!");

        // Check downward IMPLEMENTED_BY relationship from Table (Layer 4) to Customer Type (Layer 3)
        var qRel =
            "MATCH (t:Table)-[:IMPLEMENTED_BY]->(c:Type) WHERE c.name = 'Customer' OR c.name = 'OrderEntity' OR c.name = 'Invoice' RETURN c.name AS className, t.name AS tableName";
        var resRel = await _client.ExecuteQueryAsync(qRel);

        using var docRel = JsonDocument.Parse(resRel);

        docRel.RootElement.GetArrayLength().Should().BeGreaterThan(0, "No IMPLEMENTED_BY relationship found from Table to EF Core entity!");
    }

    [Test]
    public async Task Test_JpaHibernate_ExtractsTablesAndPersistedInRelationships()
    {
        var qRel = "MATCH (t:Table)-[:IMPLEMENTED_BY]->(a:Type) WHERE a.name = 'Account' RETURN a.name AS className, t.name AS tableName";
        var resRel = await _client.ExecuteQueryAsync(qRel);

        using var docRel = JsonDocument.Parse(resRel);

        var array = docRel.RootElement.EnumerateArray().ToList();

        array.Count.Should().BeGreaterThan(0, "JPA Account entity not linked from Table via IMPLEMENTED_BY!");
        var tableName = array[0].GetProperty("tableName").GetString();
        tableName.Should().BeEquivalentTo("jpa_accounts");
    }

    [Test]
    public async Task Test_TypeOrm_ExtractsTablesAndPersistedInRelationships()
    {
        var qRel = "MATCH (t:Table)-[:IMPLEMENTED_BY]->(p:Type) WHERE p.name = 'Product' RETURN p.name AS className, t.name AS tableName";
        var resRel = await _client.ExecuteQueryAsync(qRel);

        using var docRel = JsonDocument.Parse(resRel);

        var array = docRel.RootElement.EnumerateArray().ToList();

        array.Count.Should().BeGreaterThan(0, "TypeORM Product entity not linked from Table via IMPLEMENTED_BY!");
        var tableName = array[0].GetProperty("tableName").GetString();
        tableName.Should().BeEquivalentTo("typeorm_products");
    }

    [Test]
    public async Task Test_InspectDataLineage_BuiltInQuery_WorksForOrmEntities()
    {
        var query = Queries.Get("inspect_data_lineage");
        var res = await _client.ExecuteQueryAsync(query, new Dictionary<string, object?> { ["tableName"] = "jpa_accounts" });

        using var doc = JsonDocument.Parse(res);

        var array = doc.RootElement.EnumerateArray().ToList();

        array.Count.Should().BeGreaterThan(0, "inspect_data_lineage returned 0 rows for jpa_accounts table!");
        var tableName = array[0].GetProperty("tableName").GetString();
        tableName.Should().BeEquivalentTo("jpa_accounts");
    }

    [Test]
    public void Test_Database_Deduplication_And_Canonicalization()
    {
        var (tName1, tType1, tKey1) = PostIndexAnalyzer.CanonicalizeDatabase("typeorm", "relational");
        var (tName2, tType2, tKey2) = PostIndexAnalyzer.CanonicalizeDatabase("TypeORM", "relational");
        tName1.Should().Be("Database");
        tName2.Should().Be("Database");
        tKey1.Should().Be("database");
        tKey2.Should().Be("database");

        var (pName1, _, pKey1) = PostIndexAnalyzer.CanonicalizeDatabase("postgres", null);
        var (pName2, _, pKey2) = PostIndexAnalyzer.CanonicalizeDatabase("PostgreSQL", "relational");
        pName1.Should().Be("PostgreSQL");
        pName2.Should().Be("PostgreSQL");
        pKey1.Should().Be("postgresql");
        pKey2.Should().Be("postgresql");

        var (rName1, rType1, rKey1) = PostIndexAnalyzer.CanonicalizeDatabase("REDIS_HOST", "cache");
        var (rName2, rType2, rKey2) = PostIndexAnalyzer.CanonicalizeDatabase("redis", "keyvalue");
        rName1.Should().Be("Redis");
        rName2.Should().Be("Redis");
        rType1.Should().Be("cache");
        rType2.Should().Be("cache");
        rKey1.Should().Be("redis");
        rKey2.Should().Be("redis");
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

            var nodes = new List<Node>
            {
                new("proj:svc_a", "Project",
                    new Dictionary<string, object>
                    {
                        ["name"] = "ServiceA", ["path"] = "/src/a", ["project_type"] = "typescript"
                    }),
                new("proj:svc_b", "Project",
                    new Dictionary<string, object>
                    {
                        ["name"] = "ServiceB", ["path"] = "/src/b", ["project_type"] = "typescript"
                    }),

                // Casing variants of TypeORM and project-scoped DBs
                new("workspace:project:svc_a:db:typeorm", "Database",
                    new Dictionary<string, object> { ["name"] = "typeorm", ["db_type"] = "relational" }),
                new("workspace:project:svc_b:db:TypeORM", "Database",
                    new Dictionary<string, object> { ["name"] = "TypeORM", ["db_type"] = "relational" }),
                new("workspace:database:relational:typeorm", "Database",
                    new Dictionary<string, object> { ["name"] = "typeorm", ["db_type"] = "relational" }),

                // Casing variants of PostgreSQL
                new("workspace:database:relational:PostgreSQL", "Database",
                    new Dictionary<string, object> { ["name"] = "PostgreSQL", ["db_type"] = "relational" }),
                new("workspace:database:relational:postgres", "Database",
                    new Dictionary<string, object> { ["name"] = "postgres", ["db_type"] = "relational" }),
            };
            await db.UploadNodesAsync(nodes);

            var rels = new List<Relationship>
            {
                new("proj:svc_a", "workspace:project:svc_a:db:typeorm", "USES_DB",
                    new Dictionary<string, object> { ["kind"] = "USES_DB" }),
                new("proj:svc_b", "workspace:project:svc_b:db:TypeORM", "USES_DB",
                    new Dictionary<string, object> { ["kind"] = "USES_DB" }),
                new("proj:svc_a", "workspace:database:relational:postgres", "USES_DB",
                    new Dictionary<string, object> { ["kind"] = "USES_DB" }),
                new("proj:svc_b", "workspace:database:relational:PostgreSQL", "USES_DB",
                    new Dictionary<string, object> { ["kind"] = "USES_DB" }),
            };
            await db.UploadRelationshipsAsync(rels);

            var graph = await new ArchitectureViewEngine(db).GetSystemContextViewAsync(includeLibraries: true);

            // TypeORM nodes collapse to canonical Database node
            var dbNodes = graph.Nodes.Where(n => n.Name.Equals("Database", StringComparison.OrdinalIgnoreCase))
                .ToList();

            dbNodes.Should().HaveCount(1, "All TypeORM nodes must collapse to canonical Database node");
            dbNodes[0].Name.Should().Be("Database");
            dbNodes[0].Id.Should().Be("ws:db:relational:database");

            // Exactly 1 PostgreSQL node
            var postgresNodes = graph.Nodes
                .Where(n => n.Name.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase)).ToList();

            postgresNodes.Should().HaveCount(1, "All PostgreSQL nodes must collapse to exactly 1 node");
            postgresNodes[0].Name.Should().Be("PostgreSQL");
            postgresNodes[0].Id.Should().Be("ws:db:relational:postgresql");

            // Edges must point to canonical IDs
            var aToDb = graph.Edges.FirstOrDefault(e =>
                e.Source == "proj:svc_a" && e.Target == "ws:db:relational:database");

            var bToDb = graph.Edges.FirstOrDefault(e =>
                e.Source == "proj:svc_b" && e.Target == "ws:db:relational:database");
            aToDb.Should().NotBeNull("Service A must connect to canonical Database");
            bToDb.Should().NotBeNull("Service B must connect to canonical Database");

            var aToPg = graph.Edges.FirstOrDefault(e =>
                e.Source == "proj:svc_a" && e.Target == "ws:db:relational:postgresql");

            var bToPg = graph.Edges.FirstOrDefault(e =>
                e.Source == "proj:svc_b" && e.Target == "ws:db:relational:postgresql");
            aToPg.Should().NotBeNull("Service A must connect to canonical PostgreSQL");
            bToPg.Should().NotBeNull("Service B must connect to canonical PostgreSQL");
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
            name.Should().Be("sample-service");

            File.WriteAllText(Path.Combine(tempDir, "package.json"), "{\"name\": \"direct-name\"}");
            var directName = parser.GetProjectName(tempDir, ["package.json"]);
            directName.Should().Be("direct-name");
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Test]
    public async Task Test_TypeScriptParser_Creates_UsesDb_For_TypeOrm_Import()
    {
        // TypeORM is an ORM, so the database node must NOT be named 'TypeORM', but 'Database'
        var qTypeOrmDb = "MATCH (d:Database) WHERE d.name = 'TypeORM' RETURN d.name AS name";
        var resTypeOrm = await _client.ExecuteQueryAsync(qTypeOrmDb);

        using var docTypeOrm = JsonDocument.Parse(resTypeOrm);

        docTypeOrm.RootElement.EnumerateArray().ToList().Should().BeEmpty("TypeORM must NOT be a Database node!");

        var qDb =
            "MATCH (d:Database) WHERE d.name = 'Database' OR d.name STARTS WITH 'PostgreSQL' RETURN d.name AS name, d.id AS id";
        var resDb = await _client.ExecuteQueryAsync(qDb);

        using var docDb = JsonDocument.Parse(resDb);

        var dbList = docDb.RootElement.EnumerateArray().ToList();
        dbList.Should().NotBeEmpty("Canonical relational Database node should exist in graph");

        var qRel =
            "MATCH (w)-[r:USES_DB]->(d:Database) WHERE d.name = 'Database' OR d.name STARTS WITH 'PostgreSQL' RETURN w.name AS projName, d.name AS dbName, r.properties AS props";
        var resRel = await _client.ExecuteQueryAsync(qRel);

        using var docRel = JsonDocument.Parse(resRel);

        var relList = docRel.RootElement.EnumerateArray().ToList();
        relList.Should().NotBeEmpty("USES_DB relationship from workload to Database should exist");
        var tsRel = relList.FirstOrDefault(r => r.GetProperty("props").GetRawText().Contains("TypeORM"));

        tsRel.ValueKind.Should().NotBe(JsonValueKind.Undefined, "USES_DB edge for TsOrmApp must retain via: TypeORM attribute");
    }


    [Test]
    public async Task Test_ArchitectureView_Does_Not_Convert_Project_To_Database()
    {
        var tempWorkspace = Path.Combine(Path.GetTempPath(), "proj_not_db_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempWorkspace);
        try
        {
            var dbPath = Path.Combine(tempWorkspace, "graph.db").Replace('\\', '/');

            using var db = new SqliteGraphClient(dbPath);

            var nodes = new List<Node>
            {
                new("proj:svc_a", "Project",
                    new Dictionary<string, object>
                    {
                        ["name"] = "ServiceA",
                        ["path"] = "/src/a",
                        ["project_type"] = "typescript",
                        ["db_type"] = "relational"
                    }),
                new("ws:db:relational:postgresql", "Database",
                    new Dictionary<string, object> { ["name"] = "PostgreSQL", ["db_type"] = "relational" })
            };
            await db.UploadNodesAsync(nodes);

            var rels = new List<Relationship>
            {
                new("proj:svc_a", "ws:db:relational:postgresql", "USES_DB",
                    new Dictionary<string, object> { ["kind"] = "USES_DB" })
            };
            await db.UploadRelationshipsAsync(rels);

            var graph = await new ArchitectureViewEngine(db).GetSystemContextViewAsync(includeLibraries: true);

            var projNode = graph.Nodes.FirstOrDefault(n => n.Id == "proj:svc_a");
            projNode.Should().NotBeNull();

            projNode.Kind.Should().Be(NodeKind.Project, "Project node with db_type property must not be converted to Kind 'Database'!");

            var dbNode = graph.Nodes.FirstOrDefault(n => n.Id == "ws:db:relational:postgresql");
            dbNode.Should().NotBeNull();
            dbNode.Kind.Should().Be(NodeKind.Database);
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

            var nodes = new List<Node>
            {
                new("workspace:project:order_service", "Project",
                    new Dictionary<string, object> { ["name"] = "OrderService", ["path"] = "/src/order_service" }),
                new("workspace:project:billing_service", "Project",
                    new Dictionary<string, object>
                    {
                        ["name"] = "BillingService", ["path"] = "/src/billing_service"
                    }),

                // Project-scoped database node
                new("workspace:project:order_service:db:orders", "Database",
                    new Dictionary<string, object> { ["name"] = "PostgreSQL.orders", ["engine"] = "PostgreSQL", ["schema"] = "orders", ["db_type"] = "relational" }),

                // Cased raw database node
                new("workspace:database:relational:PostgreSQL.orders", "Database",
                    new Dictionary<string, object> { ["name"] = "PostgreSQL.orders", ["engine"] = "PostgreSQL", ["schema"] = "orders", ["db_type"] = "relational" }),

                // Generic database name with stem
                new("workspace:database:relational:orders_db", "Database",
                    new Dictionary<string, object> { ["name"] = "PostgreSQL.orders_db", ["engine"] = "PostgreSQL", ["schema"] = "orders_db", ["db_type"] = "relational" })
            };
            await db.UploadNodesAsync(nodes);

            var rels = new List<Relationship>
            {
                new("workspace:project:order_service", "workspace:project:order_service:db:orders", "USES_DB",
                    new Dictionary<string, object> { ["kind"] = "USES_DB" }),
                new("workspace:project:billing_service", "workspace:database:relational:PostgreSQL.orders", "USES_DB",
                    new Dictionary<string, object> { ["kind"] = "USES_DB" }),
                new("workspace:project:billing_service", "workspace:database:relational:orders_db", "USES_DB",
                    new Dictionary<string, object> { ["kind"] = "USES_DB" })
            };
            await db.UploadRelationshipsAsync(rels);

            var analyzer = new PostIndexAnalyzer(db);
            await analyzer.RunAsync("workspace");

            // 1. Verify canonical database nodes exist in the graph
            var canonicalDb = await db.ExecuteQueryAsync(
                "MATCH (d:Database) WHERE d.id = 'workspace:db:relational:postgresql:orders' RETURN d.id AS id, d.name AS name, d.is_canonical AS is_canonical");

            using (var doc = JsonDocument.Parse(canonicalDb))
            {
                var rows = doc.RootElement.EnumerateArray().ToList();
                rows.Should().HaveCount(1);
                rows[0].GetProperty("name").GetString().Should().Be("PostgreSQL.orders");
                rows[0].GetProperty("is_canonical").GetString().Should().Be("true");
            }

            // 2. Verify non-canonical project-scoped and consolidated nodes were cleaned up
            var staleDb = await db.ExecuteQueryAsync(
                "MATCH (d:Database) WHERE d.id = 'workspace:project:order_service:db:orders' RETURN d.id AS id");

            using (var doc = JsonDocument.Parse(staleDb))
            {
                doc.RootElement.EnumerateArray().Count().Should().Be(0);
            }

            var consolidatedDb = await db.ExecuteQueryAsync(
                "MATCH (d:Database) WHERE d.id = 'workspace:database:relational:orders_db' RETURN d.id AS id");

            using (var doc = JsonDocument.Parse(consolidatedDb))
            {
                doc.RootElement.EnumerateArray().Count().Should().Be(0);
            }

            // 3. Verify direct USES_DB relationships point to the canonical database nodes
            var orderUsesDb = await db.ExecuteQueryAsync(
                "MATCH (p:Project)-[r:USES_DB]->(d:Database) WHERE p.id = 'workspace:project:order_service' RETURN d.id AS dbId, r.is_canonical AS isCanonical");

            using (var doc = JsonDocument.Parse(orderUsesDb))
            {
                var rows = doc.RootElement.EnumerateArray().ToList();
                rows.Should().HaveCount(1);

                rows[0].GetProperty("dbId").GetString().Should().Be("workspace:db:relational:postgresql:orders");
                rows[0].GetProperty("isCanonical").GetString().Should().Be("true");
            }

            var billingUsesDb = await db.ExecuteQueryAsync(
                "MATCH (p:Project)-[r:USES_DB]->(d:Database) WHERE p.id = 'workspace:project:billing_service' RETURN d.id AS dbId ORDER BY d.id");

            using (var doc = JsonDocument.Parse(billingUsesDb))
            {
                var rows = doc.RootElement.EnumerateArray().ToList();
                rows.Should().HaveCount(1);
                rows[0].GetProperty("dbId").GetString().Should().Be("workspace:db:relational:postgresql:orders");
            }
        }
        finally
        {
            try { Directory.Delete(tempWorkspace, true); } catch { }
        }
    }

    [Test]
    public async Task Test_Transitive_ProjectReference_Resolves_DatabaseEngine()
    {
        var tempWorkspace = Path.Combine(Path.GetTempPath(), "ce_transitive_db_" + Guid.NewGuid().ToString("N")).Replace('\\', '/');
        Directory.CreateDirectory(tempWorkspace);
        try
        {
            var coreDataDir = Path.Combine(tempWorkspace, "Core", "Core.Data");
            Directory.CreateDirectory(coreDataDir);
            var coreCsproj = Path.Combine(coreDataDir, "Core.Data.csproj");
            await File.WriteAllTextAsync(coreCsproj, @"<Project Sdk=""Microsoft.NET.Sdk"">
  <ItemGroup>
    <PackageReference Include=""Npgsql.EntityFrameworkCore.PostgreSQL"" Version=""6.0.0"" />
  </ItemGroup>
</Project>");

            var appDir = Path.Combine(tempWorkspace, "Apps", "MyApp");
            Directory.CreateDirectory(appDir);
            var appCsproj = Path.Combine(appDir, "MyApp.csproj");
            await File.WriteAllTextAsync(appCsproj, @"<Project Sdk=""Microsoft.NET.Sdk"">
  <ItemGroup>
    <ProjectReference Include=""../../Core/Core.Data/Core.Data.csproj"" />
  </ItemGroup>
</Project>");

            var entityFile = Path.Combine(appDir, "MyEntityConfiguration.cs");
            await File.WriteAllTextAsync(entityFile, @"using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using AwesomeAssertions;

public class MyEntityConfiguration : IEntityTypeConfiguration<MyEntity>
{
    public void Configure(EntityTypeBuilder<MyEntity> builder)
    {
        builder.ToTable(""my_entities"", ""myapp"");
    }
}
public class MyEntity { public int Id { get; set; } }
");

            var channel = System.Threading.Channels.Channel.CreateUnbounded<Func<Task>>();

            await using var client = new InMemoryGraphClient();

            var ctx = new ParsingContext(tempWorkspace, tempWorkspace, client, channel);

            var csharpParser = new CSharpParser();

            using var syntaxTree = await csharpParser.ParseAsync(entityFile, "parent-id", ctx.WorkspaceId,
                ctx.AbsoluteWorkspacePath);

            Core.Parser.Layers.Layer3SyntacticParser.ProcessVisitor(syntaxTree, ctx.WorkspaceId,
                ctx.AbsoluteWorkspacePath);

            var projectNode = new CodeExplorer.Core.Common.Nodes.Layer2_Boundaries.ProjectNode(
                "workspace:project:MyApp", "MyApp", appDir, "csharp", new Dictionary<string, string>());
            await ResourceSemanticAnalyzer.EnrichAsync(syntaxTree, projectNode, csharpParser, ctx);

            var resources = ctx.ResourceRegistry.AllResources.ToList();
            resources.Should().HaveCount(1);

            resources[0].Name.Should().Be("PostgreSQL.myapp");
            resources[0].Engine.Should().Be("PostgreSQL");
            resources[0].DbType.Should().Be("relational");
        }
        finally
        {
            try { Directory.Delete(tempWorkspace, true); } catch { }
        }
    }

    [Test]
    public void Test_AllSemanticExtensions_AcrossAllAssemblies_AreImplemented()
    {
        var parserAssemblies = new[]
        {
            typeof(CSharpParser).Assembly,
            typeof(Parser.Go.GoParser).Assembly,
            typeof(Parser.Python.PythonParser).Assembly,
            typeof(JavaParser).Assembly,
            typeof(TypeScriptParser).Assembly
        };

        var allExtensions = parserAssemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => !t.IsAbstract && !t.IsInterface && typeof(ISemanticExtension).IsAssignableFrom(t))
            .Select(t => (ISemanticExtension)Activator.CreateInstance(t)!)
            .ToList();

        allExtensions.Should().HaveCountGreaterThanOrEqualTo(80, "Expected at least 80 library extensions.");
        var notImplemented = allExtensions.Where(e => !e.IsImplemented).Select(e => $"{e.GetType().Name} ({e.Id})").ToList();
        notImplemented.Should().BeEmpty($"Found unimplemented extensions: {string.Join(", ", notImplemented)}");
    }
}
