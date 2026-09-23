using CodeExplorer.Core.Analysis;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Database;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
public class ArchitectureViewEngineTests
{
    private string _tempDir = null!;
    private SqliteGraphClient _db = null!;
    private ArchitectureViewEngine _engine = null!;

    [SetUp]
    public async Task SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ce_view_engine_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        var dbPath = Path.Combine(_tempDir, "graph.db").Replace('\\', '/');
        _db = new SqliteGraphClient(dbPath);
        _engine = new ArchitectureViewEngine(_db);

        // Upload sample C4 architecture graph
        var nodes = new List<Node>
        {
            new("ws:project:order-service:", "Project", new Dictionary<string, object>
            {
                ["name"] = "order-service",
                ["path"] = "services/order-service",
                ["role"] = "Service",
                ["is_library"] = "false"
            }),
            new("ws:project:payment-service:", "Project", new Dictionary<string, object>
            {
                ["name"] = "payment-service",
                ["path"] = "services/payment-service",
                ["role"] = "Service",
                ["is_library"] = "false"
            }),
            new("ws:project:common-dto:", "Project", new Dictionary<string, object>
            {
                ["name"] = "common-dto",
                ["path"] = "libs/common-dto",
                ["role"] = "SharedLibrary",
                ["is_library"] = "true"
            }),
            new("ws:res:db:relational:orders_db", "Database", new Dictionary<string, object>
            {
                ["name"] = "orders_db",
                ["db_type"] = "relational"
            }),
            new("ws:res:topic:kafka:orders", "Topic", new Dictionary<string, object>
            {
                ["name"] = "orders_topic",
                ["broker"] = "kafka"
            }),
            new("ws:res:service:external:api.stripe.com", "ExternalService", new Dictionary<string, object>
            {
                ["name"] = "api.stripe.com"
            }),
            // Internal components of order-service
            new("ws:endpoint:orders:POST", "Endpoint", new Dictionary<string, object>
            {
                ["name"] = "POST /api/orders",
                ["path"] = "services/order-service/OrderController.cs"
            }),
            new("ws:type:orders:Order", "Type", new Dictionary<string, object>
            {
                ["name"] = "Order",
                ["path"] = "services/order-service/Order.cs"
            })
        };
        await _db.UploadNodesAsync(nodes);

        var rels = new List<Relationship>
        {
            // Materialized C4 Macro-edges
            new("ws:project:order-service:", "ws:project:payment-service:", OntologyConstants.Relationships.IntegratesWith, new() { ["dependency_type"] = "service_call" }),
            new("ws:project:order-service:", "ws:res:db:relational:orders_db", OntologyConstants.Relationships.UsesDb, new() { ["dependency_type"] = "database" }),
            new("ws:res:topic:kafka:orders", "ws:project:payment-service:", OntologyConstants.Relationships.Triggers, new() { ["dependency_type"] = "messaging" }),
            new("ws:project:payment-service:", "ws:res:service:external:api.stripe.com", OntologyConstants.Relationships.IntegratesWith, new() { ["dependency_type"] = "service_call" }),
            new("ws:project:order-service:", "ws:project:common-dto:", OntologyConstants.Relationships.DependsOn, new() { ["dependency_type"] = "library" }),

            // Component-level contains
            new("ws:project:order-service:", "ws:endpoint:orders:POST", OntologyConstants.Relationships.Contains, new()),
            new("ws:project:order-service:", "ws:type:orders:Order", OntologyConstants.Relationships.Contains, new())
        };
        await _db.UploadRelationshipsAsync(rels);
    }

    [TearDown]
    public void TearDown()
    {
        _db.Dispose();
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    [Test]
    public async Task GetSystemContextView_ReturnsC1SystemContext_ExcludingLibrariesByDefault()
    {
        var view = await _engine.GetSystemContextViewAsync(includeLibraries: false);

        Assert.That(view.Metadata!["view"], Is.EqualTo("SystemContext"));
        Assert.That(view.Metadata!["level"], Is.EqualTo("C1"));

        // Must contain services, db, topic, external service
        var nodeIds = view.Nodes.Select(n => n.Id).ToList();
        Assert.That(nodeIds, Does.Contain("ws:project:order-service:"));
        Assert.That(nodeIds, Does.Contain("ws:project:payment-service:"));
        Assert.That(nodeIds, Does.Contain("ws:res:db:relational:orders_db"));
        Assert.That(nodeIds, Does.Contain("ws:res:topic:kafka:orders"));
        Assert.That(nodeIds, Does.Contain("ws:res:service:external:api.stripe.com"));

        // Must NOT contain SharedLibrary
        Assert.That(nodeIds, Does.Not.Contain("ws:project:common-dto:"));

        // Edges between services, DB, topic, external service
        Assert.That(view.Edges.Any(e => e.Source == "ws:project:order-service:" && e.Target == "ws:project:payment-service:"), Is.True);
        Assert.That(view.Edges.Any(e => e.Source == "ws:project:order-service:" && e.Target == "ws:res:db:relational:orders_db"), Is.True);
        Assert.That(view.Edges.Any(e => e.Source == "ws:res:topic:kafka:orders" && e.Target == "ws:project:payment-service:"), Is.True);
    }

    [Test]
    public async Task GetServiceFlowView_ReturnsC2NeighborhoodForScopedService()
    {
        var view = await _engine.GetServiceFlowViewAsync("order-service");

        Assert.That(view.Metadata!["view"], Is.EqualTo("ServiceFlow"));
        Assert.That(view.Metadata!["level"], Is.EqualTo("C2"));

        var center = view.Nodes.FirstOrDefault(n => n.Properties?.GetValueOrDefault("column") == "center");
        Assert.That(center, Is.Not.Null);
        Assert.That(center!.Id, Is.EqualTo("ws:project:order-service:"));

        // Outbound nodes in right column
        var rightNodes = view.Nodes.Where(n => n.Properties?.GetValueOrDefault("column") == "right").Select(n => n.Id).ToList();
        Assert.That(rightNodes, Does.Contain("ws:project:payment-service:"));
        Assert.That(rightNodes, Does.Contain("ws:res:db:relational:orders_db"));
        Assert.That(rightNodes, Does.Contain("ws:project:common-dto:"));
    }

    [Test]
    public async Task GetComponentView_ReturnsC3InternalComponentsForProject()
    {
        var view = await _engine.GetComponentViewAsync("order-service");

        Assert.That(view.Metadata!["view"], Is.EqualTo("Component"));
        Assert.That(view.Metadata!["level"], Is.EqualTo("C3"));

        var compIds = view.Nodes.Select(n => n.Id).ToList();
        Assert.That(compIds, Does.Contain("ws:endpoint:orders:POST"));
        Assert.That(compIds, Does.Contain("ws:type:orders:Order"));
    }
}
