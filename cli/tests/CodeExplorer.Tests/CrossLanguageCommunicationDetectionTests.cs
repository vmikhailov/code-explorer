using System.Threading.Channels;
using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Parser;
using CodeExplorer.Core.Parser.Layers;
using CodeExplorer.Core.Protocol;
using CodeExplorer.Parser.CSharp;
using CodeExplorer.Parser.Go;
using CodeExplorer.Parser.Java;
using CodeExplorer.Parser.Python;
using CodeExplorer.Parser.SQL;
using CodeExplorer.Parser.TypeScript;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
public class CrossLanguageCommunicationDetectionTests
{
    private static void FindAllNodes<T>(IEnumerable<IOntologyNode> nodes, List<T> result) where T : IOntologyNode
    {
        foreach (var node in nodes)
        {
            if (node is T match) result.Add(match);
            FindAllNodes(node.Children, result);
        }
    }

    private static async Task<(SyntaxTree Tree, ParsingContext Context, string TempDir)> ParseCodeAsync(
        IFileParser parser, string code, string fileName, string language)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"ce_comms_{language}_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var filePath = Path.Combine(tempDir, fileName);
        await File.WriteAllTextAsync(filePath, code);

        var channel = Channel.CreateUnbounded<Func<Task>>();
        var client = new InMemoryGraphClient();
        var ctx = new ParsingContext(tempDir, tempDir, client, channel);

        var syntaxTree = await parser.ParseAsync(filePath, "proj:root", ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
        Layer3SyntacticParser.ProcessVisitor(syntaxTree, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);

        return (syntaxTree, ctx, tempDir);
    }

    // =========================================================================
    // 1. TypeScript Communication Detection
    // =========================================================================
    [Test]
    public async Task Test_TypeScript_Communication_Detection_Http_And_Orm()
    {
        var parser = new TypeScriptParser();

        // 1a. HTTP Client Call (Axios)
        var tsHttpCode = """
        import axios from 'axios';
        export async function fetchUser(userId: string) {
            const res = await axios.get(`http://user-service/api/v1/users/${userId}`);
            return res.data;
        }
        """;
        var (httpTree, httpCtx, httpDir) = await ParseCodeAsync(parser, tsHttpCode, "userClient.ts", "ts");
        try
        {
            var extServices = new List<ExternalServiceNode>();
            FindAllNodes(httpTree.FileNode.Children, extServices);
            Assert.That(extServices, Has.Count.GreaterThan(0), "TypeScript should detect ExternalServiceNode from axios.get");
            var svc = extServices.First();
            Assert.That(svc.DomainOrService, Is.EqualTo("user-service"));
        }
        finally
        {
            httpTree.Dispose();
            try { Directory.Delete(httpDir, true); } catch { }
        }

        // 1b. Database Usage (TypeORM)
        var tsOrmCode = """
        import { Entity, PrimaryGeneratedColumn, Column } from 'typeorm';
        @Entity('orders')
        export class Order {
            @PrimaryGeneratedColumn()
            id: number;
            @Column()
            status: string;
        }
        """;
        var (ormTree, ormCtx, ormDir) = await ParseCodeAsync(parser, tsOrmCode, "order.entity.ts", "ts");
        try
        {
            var proj = new ProjectNode("proj:order-svc", "order-svc", "/order-svc", "typescript");
            var enricher = parser.GetSyntaxEnricher(ormTree);
            await enricher.EnrichAsync(proj, ormCtx);

            Assert.That(ormCtx.GlobalProjectDependencies.Any(d => d.Kind == "USES_DB"), Is.True, "TypeScript TypeORM should produce USES_DB relationship");
        }
        finally
        {
            ormTree.Dispose();
            try { Directory.Delete(ormDir, true); } catch { }
        }
    }

    // =========================================================================
    // 2. C# Communication Detection
    // =========================================================================
    [Test]
    public async Task Test_CSharp_Communication_Detection_Http_And_EfCore()
    {
        var parser = new CSharpParser();

        // 2a. HTTP Client Call
        var csHttpCode = """
        using System.Net.Http;
        using System.Threading.Tasks;

        public class PaymentClient
        {
            private readonly HttpClient _http;
            public PaymentClient(HttpClient http) => _http = http;

            public async Task PayAsync(string id)
            {
                await _http.PostAsync($"http://payment-service/api/v1/payments/{id}", null);
            }
        }
        """;
        var (httpTree, httpCtx, httpDir) = await ParseCodeAsync(parser, csHttpCode, "PaymentClient.cs", "cs");
        try
        {
            var extServices = new List<ExternalServiceNode>();
            FindAllNodes(httpTree.FileNode.Children, extServices);
            Assert.That(extServices, Has.Count.GreaterThan(0), "C# should detect ExternalServiceNode from HttpClient.PostAsync");
            var svc = extServices.First();
            Assert.That(svc.DomainOrService, Is.EqualTo("payment-service"));
        }
        finally
        {
            httpTree.Dispose();
            try { Directory.Delete(httpDir, true); } catch { }
        }

        // 2b. Database Usage (Entity Framework Core)
        var csOrmCode = """
        using Microsoft.EntityFrameworkCore;

        public class AppDbContext : DbContext
        {
            public DbSet<User> Users { get; set; }
        }
        public class User { public int Id { get; set; } }
        """;
        var (ormTree, ormCtx, ormDir) = await ParseCodeAsync(parser, csOrmCode, "AppDbContext.cs", "cs");
        try
        {
            var proj = new ProjectNode("proj:user-svc", "user-svc", "/user-svc", "csharp");
            var enricher = parser.GetSyntaxEnricher(ormTree);
            await enricher.EnrichAsync(proj, ormCtx);

            Assert.That(ormCtx.GlobalProjectDependencies.Any(d => d.Kind == "USES_DB"), Is.True, "C# EF Core should produce USES_DB relationship");
        }
        finally
        {
            ormTree.Dispose();
            try { Directory.Delete(ormDir, true); } catch { }
        }
    }

    // =========================================================================
    // 3. Go Communication Detection
    // =========================================================================
    [Test]
    public async Task Test_Go_Communication_Detection_Http_And_Gorm()
    {
        var parser = new GoParser();

        // 3a. HTTP Client Call
        var goHttpCode = """
        package client
        import "net/http"

        func GetProfile(id string) (*http.Response, error) {
            return http.Get("http://profile-service/api/v1/profiles/" + id)
        }
        """;
        var (httpTree, httpCtx, httpDir) = await ParseCodeAsync(parser, goHttpCode, "profile.go", "go");
        try
        {
            var extServices = new List<ExternalServiceNode>();
            FindAllNodes(httpTree.FileNode.Children, extServices);
            Assert.That(extServices, Has.Count.GreaterThan(0), "Go should detect ExternalServiceNode from http.Get");
            var svc = extServices.First();
            Assert.That(svc.DomainOrService, Is.EqualTo("profile-service"));
        }
        finally
        {
            httpTree.Dispose();
            try { Directory.Delete(httpDir, true); } catch { }
        }

        // 3b. EntryPoint / Endpoint (Router)
        var goRouterCode = """
        package main
        import "net/http"

        func SetupRoutes() {
            http.HandleFunc("/api/v1/orders", nil)
        }
        """;
        var (epTree, epCtx, epDir) = await ParseCodeAsync(parser, goRouterCode, "main.go", "go");
        try
        {
            var endpoints = new List<EndpointNode>();
            FindAllNodes(epTree.FileNode.Children, endpoints);
            Assert.That(endpoints, Has.Count.GreaterThan(0), "Go should detect EndpointNode from http.HandleFunc");
            Assert.That(endpoints.Any(ep => ep.RouteTemplate.Contains("/api/v1/orders")), Is.True);
        }
        finally
        {
            epTree.Dispose();
            try { Directory.Delete(epDir, true); } catch { }
        }
    }

    // =========================================================================
    // 4. Python Communication Detection
    // =========================================================================
    [Test]
    public async Task Test_Python_Communication_Detection_Http_And_SqlAlchemy()
    {
        var parser = new PythonParser();

        // 4a. HTTP Client Call (Requests)
        var pyHttpCode = """
        import requests

        def fetch_inventory(item_id):
            return requests.get(f"http://inventory-service/api/v1/items/{item_id}").json()
        """;
        var (httpTree, httpCtx, httpDir) = await ParseCodeAsync(parser, pyHttpCode, "inventory.py", "py");
        try
        {
            var extServices = new List<ExternalServiceNode>();
            FindAllNodes(httpTree.FileNode.Children, extServices);
            Assert.That(extServices, Has.Count.GreaterThan(0), "Python should detect ExternalServiceNode from requests.get");
            var svc = extServices.First();
            Assert.That(svc.DomainOrService, Is.EqualTo("inventory-service"));
        }
        finally
        {
            httpTree.Dispose();
            try { Directory.Delete(httpDir, true); } catch { }
        }

        // 4b. Database Usage (SQLAlchemy)
        var pyOrmCode = """
        from sqlalchemy import create_engine, Column, Integer, String
        from sqlalchemy.ext.declarative import declarative_base

        Base = declarative_base()

        class Product(Base):
            __tablename__ = 'products'
            id = Column(Integer, primary_key=True)
            name = Column(String)
        """;
        var (ormTree, ormCtx, ormDir) = await ParseCodeAsync(parser, pyOrmCode, "models.py", "py");
        try
        {
            var proj = new ProjectNode("proj:catalog-svc", "catalog-svc", "/catalog-svc", "python");
            var enricher = parser.GetSyntaxEnricher(ormTree);
            await enricher.EnrichAsync(proj, ormCtx);

            Assert.That(ormCtx.GlobalProjectDependencies.Any(d => d.Kind == "USES_DB"), Is.True, "Python SQLAlchemy should produce USES_DB relationship");
        }
        finally
        {
            ormTree.Dispose();
            try { Directory.Delete(ormDir, true); } catch { }
        }
    }

    // =========================================================================
    // 5. Java Communication Detection
    // =========================================================================
    [Test]
    public async Task Test_Java_Communication_Detection_Http_And_Jpa()
    {
        var parser = new JavaParser();

        // 5a. HTTP Client Call (RestTemplate)
        var javaHttpCode = """
        package com.example.client;
        import org.springframework.web.client.RestTemplate;

        public class NotificationClient {
            private RestTemplate restTemplate = new RestTemplate();

            public void sendAlert(String alertId) {
                restTemplate.postForObject("http://alert-service/api/v1/alerts/" + alertId, null, String.class);
            }
        }
        """;
        var (httpTree, httpCtx, httpDir) = await ParseCodeAsync(parser, javaHttpCode, "NotificationClient.java", "java");
        try
        {
            var extServices = new List<ExternalServiceNode>();
            FindAllNodes(httpTree.FileNode.Children, extServices);
            Assert.That(extServices, Has.Count.GreaterThan(0), "Java should detect ExternalServiceNode from RestTemplate call");
            var svc = extServices.First();
            Assert.That(svc.DomainOrService, Is.EqualTo("alert-service"));
        }
        finally
        {
            httpTree.Dispose();
            try { Directory.Delete(httpDir, true); } catch { }
        }

        // 5b. Database Usage (JPA)
        var javaOrmCode = """
        package com.example.model;
        import jakarta.persistence.Entity;
        import jakarta.persistence.Id;
        import jakarta.persistence.Table;

        @Entity
        @Table(name = "customers")
        public class Customer {
            @Id
            private Long id;
        }
        """;
        var (ormTree, ormCtx, ormDir) = await ParseCodeAsync(parser, javaOrmCode, "Customer.java", "java");
        try
        {
            var proj = new ProjectNode("proj:customer-svc", "customer-svc", "/customer-svc", "java");
            var enricher = parser.GetSyntaxEnricher(ormTree);
            await enricher.EnrichAsync(proj, ormCtx);

            Assert.That(ormCtx.GlobalProjectDependencies.Any(d => d.Kind == "USES_DB"), Is.True, "Java JPA should produce USES_DB relationship");
        }
        finally
        {
            ormTree.Dispose();
            try { Directory.Delete(ormDir, true); } catch { }
        }
    }

    // =========================================================================
    // 6. SQL DDL Table & Lineage Communication Detection
    // =========================================================================
    [Test]
    public async Task Test_Sql_Table_Declaration_And_Resolution()
    {
        var parser = new SqlParser();
        var sqlCode = """
        CREATE TABLE dbo.orders (
            order_id INT PRIMARY KEY,
            customer_id INT NOT NULL,
            total DECIMAL(10,2)
        );

        CREATE PROCEDURE dbo.sp_get_orders
        AS
        BEGIN
            SELECT order_id, total FROM dbo.orders;
        END;
        """;
        var (sqlTree, sqlCtx, sqlDir) = await ParseCodeAsync(parser, sqlCode, "schema.sql", "sql");
        try
        {
            var tables = new List<TableNode>();
            FindAllNodes(sqlTree.FileNode.Children, tables);
            Assert.That(tables, Has.Count.GreaterThan(0), "SQL should detect TableNode from CREATE TABLE");
            Assert.That(tables.First().Name, Does.Contain("orders"));
        }
        finally
        {
            sqlTree.Dispose();
            try { Directory.Delete(sqlDir, true); } catch { }
        }
    }

    // =========================================================================
    // 7. GraphDataConverter Multi-Type Classification & DB Consolidation
    // =========================================================================
    [Test]
    public async Task Test_GraphDataConverter_Categorizes_All_Communication_Types_Accurately()
    {
        var tempWorkspace = Path.Combine(Path.GetTempPath(), "ce_graph_comms_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempWorkspace);
        try
        {
            var dbPath = Path.Combine(tempWorkspace, "graph.db").Replace('\\', '/');
            using var db = new SqliteGraphClient(dbPath);

            // Seed Projects, Libraries, Databases, ExternalServices
            var nodes = new List<CodeExplorer.Core.Database.Node>
            {
                new("proj:gateway", "Project", new Dictionary<string, object> { ["name"] = "GatewayService", ["path"] = "/src/gateway", ["project_type"] = "typescript" }),
                new("proj:auth", "Project", new Dictionary<string, object> { ["name"] = "AuthService", ["path"] = "/src/auth", ["project_type"] = "csharp" }),
                new("proj:order", "Project", new Dictionary<string, object> { ["name"] = "OrderService", ["path"] = "/src/order", ["project_type"] = "go" }),
                new("proj:common_lib", "Project", new Dictionary<string, object> { ["name"] = "CommonLib", ["path"] = "/src/libs/common", ["project_type"] = "library" }),
                // Two projects using the same project-scoped TypeORM DB
                new("proj:gatewaydb:typeorm", "Database", new Dictionary<string, object> { ["name"] = "TypeORM", ["db_type"] = "relational" }),
                new("proj:authdb:typeorm", "Database", new Dictionary<string, object> { ["name"] = "TypeORM", ["db_type"] = "relational" }),
                // Standalone Redis DB
                new("db:redis_cache", "Database", new Dictionary<string, object> { ["name"] = "Redis", ["db_type"] = "keyvalue" }),
                // External Service (Messaging)
                new("svc:kafka", "ExternalService", new Dictionary<string, object> { ["name"] = "KafkaCluster", ["service_type"] = "MessageBroker" }),
            };
            await db.UploadNodesAsync(nodes);

            // Seed Relationships
            var rels = new List<CodeExplorer.Core.Database.Relationship>
            {
                // Service call: Gateway -> Auth
                new("proj:gateway", "proj:auth", "DEPENDS_ON", new Dictionary<string, object> { ["dependency_type"] = "service_call", ["kind"] = "DEPENDS_ON" }),
                // Service call: Gateway -> Order
                new("proj:gateway", "proj:order", "DEPENDS_ON", new Dictionary<string, object> { ["dependency_type"] = "service_call", ["kind"] = "DEPENDS_ON" }),
                // Library usage: Auth -> CommonLib
                new("proj:auth", "proj:common_lib", "DEPENDS_ON", new Dictionary<string, object> { ["dependency_type"] = "library", ["kind"] = "DEPENDS_ON" }),
                // Database usage: Gateway -> TypeORM
                new("proj:gateway", "proj:gatewaydb:typeorm", "USES_DB", new Dictionary<string, object> { ["kind"] = "USES_DB" }),
                // Database usage: Auth -> TypeORM
                new("proj:auth", "proj:authdb:typeorm", "USES_DB", new Dictionary<string, object> { ["kind"] = "USES_DB" }),
                // Database usage: Gateway -> Redis
                new("proj:gateway", "db:redis_cache", "USES_DB", new Dictionary<string, object> { ["kind"] = "USES_DB" }),
                // Messaging: Order -> Kafka
                new("proj:order", "svc:kafka", "TRIGGERS", new Dictionary<string, object> { ["kind"] = "TRIGGERS" }),
            };
            await db.UploadRelationshipsAsync(rels);

            // Execute GraphDataConverter
            var graph = await GraphDataConverter.GetArchitectureGraphAsync(db);

            // 1. Verify Database Consolidation (Only 1 canonical TypeORM node instead of 2!)
            var typeOrmNodes = graph.Nodes.Where(n => n.Name.Equals("TypeORM", StringComparison.OrdinalIgnoreCase)).ToList();
            Assert.That(typeOrmNodes, Has.Count.EqualTo(1), "Duplicate TypeORM database nodes MUST be collapsed into 1 canonical node");
            var canonicalTypeOrm = typeOrmNodes.First();
            Assert.That(canonicalTypeOrm.Id, Is.EqualTo("workspace:database:relational:typeorm"));

            // 2. Verify Standalone Redis DB node preserved
            var redisNode = graph.Nodes.FirstOrDefault(n => n.Id == "db:redis_cache");
            Assert.That(redisNode, Is.Not.Null, "Standalone database node must be preserved");

            // 3. Verify Edge Categories
            // Service Call
            var svcCallEdge = graph.Edges.FirstOrDefault(e => e.Source == "proj:gateway" && e.Target == "proj:auth");
            Assert.That(svcCallEdge, Is.Not.Null);
            Assert.That(svcCallEdge!.Properties?["dependency_type"], Is.EqualTo("service_call"));

            // Library
            var libEdge = graph.Edges.FirstOrDefault(e => e.Source == "proj:auth" && e.Target == "proj:common_lib");
            Assert.That(libEdge, Is.Not.Null);
            Assert.That(libEdge!.Properties?["dependency_type"], Is.EqualTo("library"));

            // Consolidated Database edges
            var gwDbEdge = graph.Edges.FirstOrDefault(e => e.Source == "proj:gateway" && e.Target == canonicalTypeOrm.Id);
            Assert.That(gwDbEdge, Is.Not.Null, "Gateway should connect to canonical TypeORM node");
            Assert.That(gwDbEdge!.Properties?["dependency_type"], Is.EqualTo("database"));

            var authDbEdge = graph.Edges.FirstOrDefault(e => e.Source == "proj:auth" && e.Target == canonicalTypeOrm.Id);
            Assert.That(authDbEdge, Is.Not.Null, "Auth should connect to canonical TypeORM node");
            Assert.That(authDbEdge!.Properties?["dependency_type"], Is.EqualTo("database"));

            // Messaging edge
            var msgEdge = graph.Edges.FirstOrDefault(e => e.Source == "proj:order" && e.Target == "svc:kafka");
            Assert.That(msgEdge, Is.Not.Null, "Order should connect to Kafka ExternalService");
            Assert.That(msgEdge!.Properties?["dependency_type"], Is.EqualTo("messaging"));
        }
        finally
        {
            try { Directory.Delete(tempWorkspace, true); } catch { }
        }
    }
}
