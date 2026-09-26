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

    [Test]
    public async Task GetOntologyLayers_FiltersOutSystemNodes()
    {
        // Add system nodes to graph
        var systemNodes = new List<Node>
        {
            new("ws:counter:1", "Counter", new Dictionary<string, object> { ["name"] = "counter", ["value"] = 42 }),
            new("ws:files_structure", "FilesStructure", new Dictionary<string, object> { ["name"] = "FilesStructure" }),
            new("ws:projects_structure", "ProjectsStructure", new Dictionary<string, object> { ["name"] = "ProjectsStructure" }),
            new("ws:syntax_structure", "SyntaxStructure", new Dictionary<string, object> { ["name"] = "SyntaxStructure" }),
            new("ws:semantic_structure", "SemanticStructure", new Dictionary<string, object> { ["name"] = "SemanticStructure" })
        };
        await _db.UploadNodesAsync(systemNodes);

        var layersResponse = await _engine.GetOntologyLayersAsync();

        foreach (var layer in layersResponse.Layers)
        {
            foreach (var category in layer.Categories)
            {
                Assert.That(category.Kind, Is.Not.EqualTo("Counter"), "Counter should be filtered out from layers");
                Assert.That(category.Kind, Is.Not.EqualTo("FilesStructure"), "FilesStructure should be filtered out from layers");
                Assert.That(category.Kind, Is.Not.EqualTo("ProjectsStructure"), "ProjectsStructure should be filtered out from layers");
                Assert.That(category.Kind, Is.Not.EqualTo("SyntaxStructure"), "SyntaxStructure should be filtered out from layers");
                Assert.That(category.Kind, Is.Not.EqualTo("SemanticStructure"), "SemanticStructure should be filtered out from layers");
                Assert.That(category.Kind.EndsWith("Structure", StringComparison.OrdinalIgnoreCase), Is.False, $"No structure nodes should be in category list: {category.Kind}");
            }
        }
    }

    [Test]
    public async Task GetMetadata_And_GetNodes_FilterOutSystemNodes()
    {
        var systemNodes = new List<Node>
        {
            new("ws:counter:1", "Counter", new Dictionary<string, object> { ["name"] = "counter", ["value"] = 42 }),
            new("ws:files_structure", "FilesStructure", new Dictionary<string, object> { ["name"] = "FilesStructure" })
        };
        await _db.UploadNodesAsync(systemNodes);

        var meta = await _engine.GetMetadataAsync();
        Assert.That(meta.NodeCounts.ContainsKey("Counter"), Is.False, "NodeCounts must not contain Counter");
        Assert.That(meta.NodeCounts.ContainsKey("FilesStructure"), Is.False, "NodeCounts must not contain FilesStructure");

        var counterNodes = await _engine.GetNodesAsync(kind: "Counter");
        Assert.That(counterNodes.Total, Is.EqualTo(0), "GetNodes for Counter must return 0 total");
        Assert.That(counterNodes.Nodes, Is.Empty, "GetNodes for Counter must be empty");

        var filesStructureNodes = await _engine.GetNodesAsync(kind: "FilesStructure");
        Assert.That(filesStructureNodes.Total, Is.EqualTo(0), "GetNodes for FilesStructure must return 0 total");
        Assert.That(filesStructureNodes.Nodes, Is.Empty, "GetNodes for FilesStructure must be empty");
    }

    [Test]
    public async Task GetMetadataAsync_DoesNotDoubleCountSemanticWorkloadNodes()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ce_double_count_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var dbPath = Path.Combine(tempDir, "graph.db").Replace('\\', '/');
            using var db = new SqliteGraphClient(dbPath);
            var engine = new ArchitectureViewEngine(db);

            var sampleNodes = new List<Node>
            {
                new("ws:project:ad-hub:", "Project", new() { ["name"] = "ad-hub", ["role"] = "Service" }),
                new("ws:service:ad-hub", "Service", new() { ["name"] = "ad-hub" }),
                new("ws:project:action-scheduler:", "Project", new() { ["name"] = "action-scheduler", ["role"] = "Worker" }),
                new("ws:worker:action-scheduler", "Worker", new() { ["name"] = "action-scheduler" }),
                new("ws:project:ats-front:", "Project", new() { ["name"] = "ats-front", ["role"] = "FrontendApp" }),
                new("ws:app:ats-front", "App", new() { ["name"] = "ats-front" }),
                new("ws:project:adhub-cli:", "Project", new() { ["name"] = "adhub-cli", ["role"] = "CliTool" }),
                new("ws:cli:adhub-cli", "CliTool", new() { ["name"] = "adhub-cli" })
            };
            await db.UploadNodesAsync(sampleNodes);

            var meta = await engine.GetMetadataAsync();
            Assert.That(meta.NodeCounts["Service"], Is.EqualTo(1), "Service node count must be 1, not doubled");
            Assert.That(meta.NodeCounts["Worker"], Is.EqualTo(1), "Worker node count must be 1, not doubled");
            Assert.That(meta.NodeCounts["App"], Is.EqualTo(1), "App node count must be 1, not doubled");
            Assert.That(meta.NodeCounts["CliTool"], Is.EqualTo(1), "CliTool node count must be 1, not doubled");
            Assert.That(meta.NodeCounts["Project"], Is.EqualTo(4), "Project node count must be 4, not doubled");
            Assert.That(meta.TotalNodes, Is.EqualTo(8), "Total nodes must be 8");

            var layers = await engine.GetOntologyLayersAsync();
            var l4 = layers.Layers.FirstOrDefault(l => l.LayerId == 4);
            Assert.That(l4, Is.Not.Null);

            var appCat = l4!.Categories.FirstOrDefault(c => c.Kind == "App");
            Assert.That(appCat, Is.Not.Null);
            Assert.That(appCat!.Count, Is.EqualTo(1));

            var svcCat = l4.Categories.FirstOrDefault(c => c.Kind == "Service");
            Assert.That(svcCat, Is.Not.Null);
            Assert.That(svcCat!.Count, Is.EqualTo(1));

            var workerCat = l4.Categories.FirstOrDefault(c => c.Kind == "Worker");
            Assert.That(workerCat, Is.Not.Null);
            Assert.That(workerCat!.Count, Is.EqualTo(1));

            var cliCat = l4.Categories.FirstOrDefault(c => c.Kind == "CliTool");
            Assert.That(cliCat, Is.Not.Null);
            Assert.That(cliCat!.Count, Is.EqualTo(1));
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }
}
