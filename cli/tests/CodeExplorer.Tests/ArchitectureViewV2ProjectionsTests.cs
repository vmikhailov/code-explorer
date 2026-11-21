using CodeExplorer.Core.Analysis;
using CodeExplorer.Core.Database;
using CodeExplorer.Cypher.Common;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
public class ArchitectureViewV2ProjectionsTests
{
    [Test]
    public async Task ProjectDependencies_DetectsTarjanCyclesAndRespectsScope()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), "ce-v2-projdeps-" + Guid.NewGuid().ToString("N") + ".db");
        try
        {
            await using var client = new SqliteGraphClient(dbPath);

            await client.UploadNodesAsync(
            [
                new Node("ws:p:api:", NodeKind.Project, new() { ["name"] = "Api", ["path"] = "src/Api", ["project_type"] = "csharp", ["entity_kind"] = "Service", ["is_library"] = false }),
                new Node("ws:p:core:", NodeKind.Project, new() { ["name"] = "Core", ["path"] = "src/Core", ["project_type"] = "csharp", ["entity_kind"] = "Library", ["is_library"] = true }),
                new Node("ws:p:infra:", NodeKind.Project, new() { ["name"] = "Infra", ["path"] = "src/Infra", ["project_type"] = "csharp", ["entity_kind"] = "Library", ["is_library"] = true }),
                new Node("ws:p:cli:", NodeKind.Project, new() { ["name"] = "Cli", ["path"] = "src/Cli", ["project_type"] = "csharp", ["entity_kind"] = "CliTool", ["is_library"] = false })
            ]);

            await client.UploadRelationshipsAsync(
            [
                new Relationship("ws:p:api:", "ws:p:core:", RelationshipKind.DependsOn),
                new Relationship("ws:p:core:", "ws:p:infra:", RelationshipKind.DependsOn),
                new Relationship("ws:p:infra:", "ws:p:core:", RelationshipKind.DependsOn),
                new Relationship("ws:p:cli:", "ws:p:api:", RelationshipKind.DependsOn)
            ]);

            var engine = new ArchitectureViewEngine(client);
            var full = await engine.GetViewAsync(new ArchitectureViewRequest
            {
                ViewType = ArchitectureViewType.ProjectDependencies
            });

            full.Nodes.Should().HaveCount(4);
            full.Metadata!["cycle_count"].Should().Be("1");

            var coreNode = full.Nodes.Single(n => n.Name == "Core");
            var infraNode = full.Nodes.Single(n => n.Name == "Infra");
            var apiNode = full.Nodes.Single(n => n.Name == "Api");
            coreNode.Properties!["in_cycle"].Should().Be("true");
            infraNode.Properties!["in_cycle"].Should().Be("true");
            apiNode.Properties!.ContainsKey("in_cycle").Should().BeFalse();

            full.Edges.Should().Contain(e => e.Source == "ws:p:core:" && e.Target == "ws:p:infra:" && e.Properties!["in_cycle"] == "true");
            full.Edges.Should().Contain(e => e.Source == "ws:p:infra:" && e.Target == "ws:p:core:" && e.Properties!["in_cycle"] == "true");

            var scoped = await engine.GetViewAsync(new ArchitectureViewRequest
            {
                ViewType = ArchitectureViewType.ProjectDependencies,
                Scope = "Cli"
            });
            scoped.Nodes.Select(n => n.Name).Should().BeEquivalentTo(["Cli", "Api"]);
        }
        finally
        {
            if (File.Exists(dbPath))
            {
                try { File.Delete(dbPath); } catch { }
            }
        }
    }

    [Test]
    public async Task CallFlow_DatabaseMap_And_DataFlow_ProjectExpectedGraphsAndCatalogs()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), "ce-v2-flows-" + Guid.NewGuid().ToString("N") + ".db");
        try
        {
            await using var client = new SqliteGraphClient(dbPath);

            await client.UploadNodesAsync(
            [
                new Node("ws:svc:orders", NodeKind.Service, new() { ["name"] = "OrdersService" }),
                new Node("ws:svc:billing", NodeKind.Service, new() { ["name"] = "BillingService" }),
                new Node("ws:db:main", NodeKind.Database, new() { ["name"] = "OrdersDb", ["engine"] = "postgresql" }),
                new Node("ws:db:cache", NodeKind.Database, new() { ["name"] = "BillingCache", ["engine"] = "redis" }),
                new Node("ws:tbl:orders", NodeKind.Table, new() { ["name"] = "orders" }),
                new Node("ws:ep:post-orders", NodeKind.Endpoint, new() { ["name"] = "POST /api/orders", ["file_path"] = "OrdersController.cs", ["start_line"] = 12 }),
                new Node("ws:fn:create", NodeKind.Function, new() { ["name"] = "CreateOrder", ["file_path"] = "OrderService.cs", ["start_line"] = 25 }),
                new Node("ws:fn:read", NodeKind.Function, new() { ["name"] = "GetOrder", ["file_path"] = "OrderQueryService.cs", ["start_line"] = 18 })
            ]);

            await client.UploadRelationshipsAsync(
            [
                new Relationship("ws:svc:orders", "ws:db:main", RelationshipKind.UsesDb),
                new Relationship("ws:svc:billing", "ws:db:main", RelationshipKind.UsesDb),
                new Relationship("ws:svc:billing", "ws:db:cache", RelationshipKind.UsesDb),
                new Relationship("ws:ep:post-orders", "ws:fn:create", RelationshipKind.Triggers),
                new Relationship("ws:fn:create", "ws:tbl:orders", RelationshipKind.WritesTo),
                new Relationship("ws:fn:read", "ws:tbl:orders", RelationshipKind.ReadsFrom)
            ]);

            var engine = new ArchitectureViewEngine(client);

            // 1. CallFlow traces Endpoint -> Function -> Table
            var callFlow = await engine.GetViewAsync(new ArchitectureViewRequest
            {
                ViewType = ArchitectureViewType.CallFlow,
                Scope = "POST /api/orders"
            });
            callFlow.Nodes.Select(n => n.Name).Should().BeEquivalentTo(["POST /api/orders", "CreateOrder", "orders"]);
            callFlow.Edges.Should().HaveCount(2);

            // 2. DatabaseMap flags shared DB and respects SharedDatabasesOnly
            var dbMapAll = await engine.GetViewAsync(new ArchitectureViewRequest
            {
                ViewType = ArchitectureViewType.DatabaseMap,
                SharedDatabasesOnly = false
            });
            dbMapAll.Metadata!["shared_db_count"].Should().Be("1");
            dbMapAll.Nodes.Single(n => n.Name == "OrdersDb").Properties!["is_shared"].Should().Be("true");
            dbMapAll.Nodes.Single(n => n.Name == "BillingCache").Properties!["is_shared"].Should().Be("false");

            var dbMapShared = await engine.GetViewAsync(new ArchitectureViewRequest
            {
                ViewType = ArchitectureViewType.DatabaseMap,
                SharedDatabasesOnly = true
            });
            dbMapShared.Nodes.Select(n => n.Name).Should().BeEquivalentTo(["BillingService", "OrdersDb", "OrdersService"]);

            // 3. DataFlow orients edges as Writer -> Table -> Reader
            var dataFlow = await engine.GetViewAsync(new ArchitectureViewRequest
            {
                ViewType = ArchitectureViewType.DataFlow,
                Scope = "orders"
            });
            dataFlow.Edges.Should().Contain(e => e.Source == "ws:fn:create" && e.Target == "ws:tbl:orders" && e.Properties!["direction"] == "write");
            dataFlow.Edges.Should().Contain(e => e.Source == "ws:tbl:orders" && e.Target == "ws:fn:read" && e.Properties!["direction"] == "read");

            // 4. Catalog queries return entry points and data assets
            var entryPoints = await engine.GetCatalogEntryPointsAsync();
            entryPoints.Select(e => e.Name).Should().Contain("POST /api/orders");

            var databases = await engine.GetCatalogDatabasesAsync();
            databases.Select(d => d.Name).Should().Contain(["OrdersDb", "BillingCache", "orders"]);
        }
        finally
        {
            if (File.Exists(dbPath))
            {
                try { File.Delete(dbPath); } catch { }
            }
        }
    }
}
