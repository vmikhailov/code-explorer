using CodeExplorer.Core.Analysis;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Protocol;
using CodeExplorer.Cypher.Common;
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

        view.Metadata!["view"].Should().Be("SystemContext");
        view.Metadata!["level"].Should().Be("C1");

        // Must contain services, db, topic, external service
        var nodeIds = view.Nodes.Select(n => n.Id).ToList();
        nodeIds.Should().Contain("ws:project:order-service:");
        nodeIds.Should().Contain("ws:project:payment-service:");
        nodeIds.Should().Contain("ws:res:db:relational:orders_db");
        nodeIds.Should().Contain("ws:res:topic:kafka:orders");
        nodeIds.Should().Contain("ws:res:service:external:api.stripe.com");

        // Must NOT contain SharedLibrary
        nodeIds.Should().NotContain("ws:project:common-dto:");

        // Edges between services, DB, topic, external service
        (view.Edges.Any(e => e.Source == "ws:project:order-service:" && e.Target == "ws:project:payment-service:")).Should().BeTrue();
        (view.Edges.Any(e => e.Source == "ws:project:order-service:" && e.Target == "ws:res:db:relational:orders_db")).Should().BeTrue();
        (view.Edges.Any(e => e.Source == "ws:res:topic:kafka:orders" && e.Target == "ws:project:payment-service:")).Should().BeTrue();
    }

    [Test]
    public async Task GetSystemContextView_IncludesArchitecturalSubsystemsButExcludesSharedLibraries()
    {
        await _db.UploadNodesAsync(
        [
            new Node("ws:project:cpu-engine:", "Project", new Dictionary<string, object>
            {
                ["name"] = "cpu-engine",
                ["path"] = "libs/cpu-engine",
                ["role"] = "SharedLibrary",
                ["is_library"] = "true",
                ["architectural_subsystem"] = "true"
            })
        ]);
        await _db.UploadRelationshipsAsync(
        [
            new Relationship("ws:project:order-service:", "ws:project:cpu-engine:", "DEPENDS_ON",
                new Dictionary<string, object> { ["dependency_type"] = "library" })
        ]);

        var view = await _engine.GetSystemContextViewAsync(includeLibraries: false);
        var subsystem = view.Nodes.Single(node => node.Id == "ws:project:cpu-engine:");

        ArchitectureViewEngine.IsLibraryProject(subsystem).Should().BeFalse();
        subsystem.Properties!["is_semantic_entity"].Should().Be("true");
        view.Nodes.Should().NotContain(node => node.Id == "ws:project:common-dto:");
    }

    [Test]
    public async Task GetSystemContextView_IncludesCloudServicesAndTheirUseEdges()
    {
        await _db.UploadNodesAsync(
        [
            new Node("ws:cloud:aws-sdk-s3", "CloudService", new Dictionary<string, object>
            {
                ["name"] = "AWS SDK S3"
            })
        ]);
        await _db.UploadRelationshipsAsync(
        [
            new Relationship("ws:project:order-service:", "ws:cloud:aws-sdk-s3", "USES_CLOUD",
                new Dictionary<string, object> { ["is_semantic"] = "true" })
        ]);

        var view = await _engine.GetSystemContextViewAsync(includeLibraries: false);

        view.Nodes.Should().Contain(node => node.Id == "ws:cloud:aws-sdk-s3" && node.Kind == NodeKind.CloudService);
        view.Edges.Should().Contain(edge =>
            edge.Source == "ws:project:order-service:" &&
            edge.Target == "ws:cloud:aws-sdk-s3" &&
            edge.Kind == "USES_CLOUD");
    }

    [Test]
    public async Task GetSystemContextView_IncludesPeerMeshesAndTypedCloudResourceEdges()
    {
        const string meshId = "ws:mesh:mpc-peer-mesh";
        const string bucketId = "ws:cloud:aws:s3:encrypted-shares";
        await _db.UploadNodesAsync(
        [
            new Node(meshId, "PeerMesh", new Dictionary<string, object>
            {
                ["name"] = "3-Party MPC Peer Mesh",
                ["path"] = ".",
                ["member_count"] = 3,
                ["protocols"] = "TCP/mTLS, NCCL",
                ["topology"] = "symmetric",
                ["is_semantic_entity"] = "true"
            }),
            new Node(bucketId, "CloudService", new Dictionary<string, object>
            {
                ["name"] = "AWS S3: Encrypted Shares Bucket",
                ["provider"] = "AWS",
                ["resource_type"] = "S3Bucket",
                ["resource_role"] = "encrypted_shares",
                ["is_semantic_entity"] = "true"
            })
        ]);
        await _db.UploadRelationshipsAsync(
        [
            new Relationship("ws:project:order-service:", meshId, "COORDINATES_WITH",
                new Dictionary<string, object> { ["protocols"] = "TCP/mTLS, NCCL" }),
            new Relationship("ws:project:order-service:", bucketId, "READS_FROM",
                new Dictionary<string, object> { ["category"] = "external_resource" }),
            new Relationship("ws:project:order-service:", bucketId, "WRITES_TO",
                new Dictionary<string, object> { ["category"] = "external_resource" })
        ]);

        var view = await _engine.GetSystemContextViewAsync(includeLibraries: false);

        var mesh = view.Nodes.Single(node => node.Id == meshId);
        mesh.Kind.Should().Be(NodeKind.PeerMesh);
        mesh.Properties!["member_count"].Should().Be("3");
        view.Nodes.Should().Contain(node => node.Id == bucketId && node.Kind == NodeKind.CloudService);
        view.Edges.Should().Contain(edge =>
            edge.Source == "ws:project:order-service:" &&
            edge.Target == meshId &&
            edge.Kind == "COORDINATES_WITH");
        view.Edges.Should().Contain(edge =>
            edge.Source == "ws:project:order-service:" &&
            edge.Target == bucketId &&
            edge.Kind == "READS_FROM");
        view.Edges.Should().Contain(edge =>
            edge.Source == "ws:project:order-service:" &&
            edge.Target == bucketId &&
            edge.Kind == "WRITES_TO");
    }

    [Test]
    public async Task GetServiceFlowView_ReturnsC2NeighborhoodForScopedService()
    {
        var view = await _engine.GetServiceFlowViewAsync("order-service");

        view.Metadata!["view"].Should().Be("ServiceFlow");
        view.Metadata!["level"].Should().Be("C2");

        var center = view.Nodes.FirstOrDefault(n => n.Properties?.GetValueOrDefault("column") == "center");
        center.Should().NotBeNull();
        center!.Id.Should().Be("ws:project:order-service:");

        // Outbound nodes in right column
        var rightNodes = view.Nodes.Where(n => n.Properties?.GetValueOrDefault("column") == "right").Select(n => n.Id).ToList();
        rightNodes.Should().Contain("ws:project:payment-service:");
        rightNodes.Should().Contain("ws:res:db:relational:orders_db");
        rightNodes.Should().Contain("ws:project:common-dto:");
    }

    [Test]
    public async Task GetComponentView_ReturnsC3InternalComponentsForProject()
    {
        var view = await _engine.GetComponentViewAsync("order-service");

        view.Metadata!["view"].Should().Be("Component");
        view.Metadata!["level"].Should().Be("C3");

        var compIds = view.Nodes.Select(n => n.Id).ToList();
        compIds.Should().Contain("ws:endpoint:orders:POST");
        compIds.Should().Contain("ws:type:orders:Order");
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
                category.Kind.Should().NotBe("Counter", "Counter should be filtered out from layers");
                category.Kind.Should().NotBe("FilesStructure", "FilesStructure should be filtered out from layers");
                category.Kind.Should().NotBe("ProjectsStructure", "ProjectsStructure should be filtered out from layers");
                category.Kind.Should().NotBe("SyntaxStructure", "SyntaxStructure should be filtered out from layers");
                category.Kind.Should().NotBe("SemanticStructure", "SemanticStructure should be filtered out from layers");
                category.Kind.Should().NotBe("DataSet", "DataSet should be filtered out from layers (Databases & Storage is used instead)");
                category.Kind.EndsWith("Structure", StringComparison.OrdinalIgnoreCase).Should().BeFalse($"No structure nodes should be in category list: {category.Kind}");
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
        meta.NodeCounts.ContainsKey("Counter").Should().BeFalse("NodeCounts must not contain Counter");
        meta.NodeCounts.ContainsKey("FilesStructure").Should().BeFalse("NodeCounts must not contain FilesStructure");

        var counterNodes = await _engine.GetNodesAsync(kind: "Counter");
        counterNodes.Total.Should().Be(0, "GetNodes for Counter must return 0 total");
        counterNodes.Nodes.Should().BeEmpty("GetNodes for Counter must be empty");

        var filesStructureNodes = await _engine.GetNodesAsync(kind: "FilesStructure");
        filesStructureNodes.Total.Should().Be(0, "GetNodes for FilesStructure must return 0 total");
        filesStructureNodes.Nodes.Should().BeEmpty("GetNodes for FilesStructure must be empty");
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
                new("ws:project:action-scheduler:", "Project",
                    new() { ["name"] = "action-scheduler", ["role"] = "Worker" }),
                new("ws:worker:action-scheduler", "Worker", new() { ["name"] = "action-scheduler" }),
                new("ws:project:admin-portal:", "Project",
                    new() { ["name"] = "admin-portal", ["role"] = "FrontendApp" }),
                new("ws:app:admin-portal", "App", new() { ["name"] = "admin-portal" }),
                new("ws:project:adhub-cli:", "Project", new() { ["name"] = "adhub-cli", ["role"] = "CliTool" }),
                new("ws:cli:adhub-cli", "CliTool", new() { ["name"] = "adhub-cli" })
            };
            await db.UploadNodesAsync(sampleNodes);

            var meta = await engine.GetMetadataAsync();
            meta.NodeCounts["Service"].Should().Be(1, "Service node count must be 1, not doubled");
            meta.NodeCounts["Worker"].Should().Be(1, "Worker node count must be 1, not doubled");
            meta.NodeCounts["App"].Should().Be(1, "App node count must be 1, not doubled");
            meta.NodeCounts["CliTool"].Should().Be(1, "CliTool node count must be 1, not doubled");
            meta.NodeCounts["Project"].Should().Be(4, "Project node count must be 4, not doubled");
            meta.TotalNodes.Should().Be(8, "Total nodes must be 8");

            var layers = await engine.GetOntologyLayersAsync();
            var l4 = layers.Layers.FirstOrDefault(l => l.LayerId == 4);
            l4.Should().NotBeNull();

            var appCat = l4!.Categories.FirstOrDefault(c => c.Kind == "App");
            appCat.Should().NotBeNull();
            appCat!.Count.Should().Be(1);

            var svcCat = l4.Categories.FirstOrDefault(c => c.Kind == "Service");
            svcCat.Should().NotBeNull();
            svcCat!.Count.Should().Be(1);

            var workerCat = l4.Categories.FirstOrDefault(c => c.Kind == "Worker");
            workerCat.Should().NotBeNull();
            workerCat!.Count.Should().Be(1);

            var cliCat = l4.Categories.FirstOrDefault(c => c.Kind == "CliTool");
            cliCat.Should().NotBeNull();
            cliCat!.Count.Should().Be(1);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Test]
    public async Task GetServicesOntologySummary_DistinguishesInternalServicesFromExternalApis()
    {
        var summaries = await _engine.GetServicesOntologySummaryAsync();
        var orderSummary = summaries.FirstOrDefault(s => s.ServiceName == "order-service");
        var paymentSummary = summaries.FirstOrDefault(s => s.ServiceName == "payment-service");

        orderSummary.Should().NotBeNull();
        orderSummary!.ServiceCount.Should().Be(1, "order-service should have 1 downstream service (payment-service)");
        orderSummary.ExternalCount.Should().Be(0, "order-service should have 0 external APIs");
        orderSummary.DatabaseCount.Should().Be(1, "order-service should have 1 database (orders_db)");

        paymentSummary.Should().NotBeNull();
        paymentSummary!.ServiceCount.Should().Be(0, "payment-service has 0 downstream services");
        paymentSummary.ExternalCount.Should().Be(1, "payment-service should have 1 external API (api.stripe.com)");
        paymentSummary.TopicCount.Should().Be(1, "payment-service should have 1 topic (orders_topic)");
    }

    [Test]
    public async Task GetServiceCapabilities_SeparatesServicesAndExternal_AndSortsAlphabetically()
    {
        var extraNodes = new List<Node>
        {
            new("ws:project:auth-service:", "Project", new() { ["name"] = "auth-service", ["role"] = "Service", ["framework"] = "ASP.NET Core" }),
            new("ws:es:http:environment.auth", "ExternalService", new() { ["name"] = "environment.auth" }),
            new("ws:res:service:external:api.github.com", "ExternalService", new() { ["name"] = "api.github.com" }),
            new("ws:res:service:external:api.cloudflare.com", "ExternalService", new() { ["name"] = "api.cloudflare.com" }),
            new("ws:res:db:relational:analytics_db", "Database", new() { ["name"] = "analytics_db", ["db_type"] = "clickhouse" }),
            new("ws:endpoint:orders:GET", "Endpoint", new() { ["name"] = "GET /api/orders", ["path"] = "services/order-service/OrderController.cs" }),
            new("ws:endpoint:orders:DELETE", "Endpoint", new() { ["name"] = "DELETE /api/orders/{id}", ["path"] = "services/order-service/OrderController.cs" }),
        };
        await _db.UploadNodesAsync(extraNodes);

        var extraRels = new List<Relationship>
        {
            new("ws:project:order-service:", "ws:project:auth-service:", OntologyConstants.Relationships.ServiceCall, new() { ["dependency_type"] = "service_call" }),
            new("ws:project:order-service:", "ws:es:http:environment.auth", OntologyConstants.Relationships.ServiceCall, new() { ["dependency_type"] = "service_call" }),
            new("ws:project:order-service:", "ws:res:service:external:api.github.com", OntologyConstants.Relationships.ServiceCall, new() { ["dependency_type"] = "service_call" }),
            new("ws:project:order-service:", "ws:res:service:external:api.cloudflare.com", OntologyConstants.Relationships.ServiceCall, new() { ["dependency_type"] = "service_call" }),
            new("ws:project:order-service:", "ws:res:db:relational:analytics_db", OntologyConstants.Relationships.UsesDb, new() { ["dependency_type"] = "database" }),
            new("ws:project:order-service:", "ws:endpoint:orders:GET", OntologyConstants.Relationships.Contains, new()),
            new("ws:project:order-service:", "ws:endpoint:orders:DELETE", OntologyConstants.Relationships.Contains, new()),
        };
        await _db.UploadRelationshipsAsync(extraRels);

        var details = await _engine.GetServiceCapabilitiesAsync("order-service");

        details.Should().NotBeNull();
        details.Groups.Count.Should().Be(5);

        var epGroup = details.Groups.First(g => g.CategoryKey == "endpoints");
        var svcGroup = details.Groups.First(g => g.CategoryKey == "services");
        var dbGroup = details.Groups.First(g => g.CategoryKey == "databases");
        var extGroup = details.Groups.First(g => g.CategoryKey == "external");

        // Verify group headers
        svcGroup.Label.Should().Be("Downstream Services");
        svcGroup.Icon.Should().Be("zap");

        // Verify downstream services contain auth-service and payment-service, and environment.auth was resolved
        var svcNames = svcGroup.Items.Select(i => i.Name).ToList();
        svcNames.Should().Contain("auth-service");
        svcNames.Should().Contain("payment-service");
        svcGroup.Count.Should().Be(2);

        // Verify external APIs contain api.cloudflare.com and api.github.com, and NOT internal services
        var extNames = extGroup.Items.Select(i => i.Name).ToList();
        extNames.Should().Contain("api.cloudflare.com");
        extNames.Should().Contain("api.github.com");
        extNames.Should().NotContain("auth-service");
        extNames.Should().NotContain("payment-service");
        extNames.Should().NotContain("environment.auth");

        // Verify alphabetical sorting within EACH capability group
        epGroup.Items.Select(i => i.Name).ToList().Should().Equal(epGroup.Items.Select(i => i.Name).OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList(), "Endpoints must be sorted alphabetically by Name");
        svcGroup.Items.Select(i => i.Name).ToList().Should().Equal(svcGroup.Items.Select(i => i.Name).OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList(), "Services must be sorted alphabetically by Name");
        dbGroup.Items.Select(i => i.Name).ToList().Should().Equal(dbGroup.Items.Select(i => i.Name).OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList(), "Databases must be sorted alphabetically by Name");
        extGroup.Items.Select(i => i.Name).ToList().Should().Equal(extGroup.Items.Select(i => i.Name).OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList(), "External APIs must be sorted alphabetically by Name");
    }

    [Test]
    public async Task GetDomainArchitecture_ExcludesLibrariesFromDomainMap()
    {
        // Setup already created order-service (Service), payment-service (Service), and common-dto (Library)
        var domain = await _engine.GetDomainArchitectureAsync();

        (domain.Nodes.Any(n => n.Name == "common-dto" || n.DisplayName == "common-dto")).Should().BeFalse("Libraries must NOT appear on Domain Service Map");
        (domain.Nodes.Any(n => n.Kind == NodeKind.Library)).Should().BeFalse("No nodes with kind 'Library' should exist on Domain Service Map");
        domain.Stats.Libraries.Should().Be(0);

        // Verify runnable services are present
        (domain.Nodes.Any(n => n.Name == "order-service")).Should().BeTrue("order-service should be present on Domain Service Map");
        (domain.Nodes.Any(n => n.Name == "payment-service")).Should().BeTrue("payment-service should be present on Domain Service Map");

        // Also verify GraphDataDto representation
        var graph = await _engine.GetDomainArchitectureGraphAsync();
        (graph.Nodes.Any(n => n.Name == "common-dto" || n.Kind == NodeKind.Library)).Should().BeFalse("Libraries must not appear in DomainMap GraphDataDto");
    }

    [Test]
    public async Task GetDomainArchitecture_IncludesLibrariesWhenExplicitlyRequested()
    {
        // When includeLibraries: true is passed, pure library domains must be included as Library entities
        var domain = await _engine.GetDomainArchitectureAsync(includeLibraries: true);

        domain.Nodes.Any(n => n.Name == "common-dto" || n.DisplayName == "common-dto").Should().BeTrue("Libraries must appear on Domain Service Map when includeLibraries is true");
        domain.Nodes.Any(n => n.Kind == NodeKind.Library).Should().BeTrue("Nodes with kind 'Library' should exist on Domain Service Map when includeLibraries is true");
        domain.Stats.Libraries.Should().BeGreaterThan(0);

        // Also verify GraphDataDto representation
        var graph = await _engine.GetDomainArchitectureGraphAsync(includeLibraries: true);
        graph.Nodes.Any(n => n.Name == "common-dto" || n.Kind == NodeKind.Library).Should().BeTrue("Libraries must appear in DomainMap GraphDataDto when includeLibraries is true");
    }

    [Test]
    public async Task GetBoundedContextMap_ReturnsAggregatedDomainsAndCrossContextInteractions()
    {
        // 1. Add sample intent records
        var intent1 = new IntentRecord(
            FilePath: "services/orders/OrderService.cs",
            WorkspaceId: "",
            FileId: "ws:type:orders:OrderService",
            ContentHash: "h1",
            LastModifiedUtc: DateTime.UtcNow,
            Domain: "OrderManagement",
            Layer: "Application",
            Pattern: "Service",
            OperationType: "Command",
            CapabilityTag: "PlaceOrder",
            IntentSummary: "Coordinates order placement workflows.",
            TargetEntities: ["Order", "OrderItem"],
            EmittedEvents: ["OrderPlacedEvent"],
            IsPureDomain: false,
            ErrorCount: 0,
            LastError: null,
            AnalyzedAtUtc: DateTime.UtcNow
        );

        var intent2 = new IntentRecord(
            FilePath: "services/orders/Order.cs",
            WorkspaceId: "",
            FileId: "ws:type:orders:Order",
            ContentHash: "h2",
            LastModifiedUtc: DateTime.UtcNow,
            Domain: "OrderManagement",
            Layer: "Domain",
            Pattern: "Entity",
            OperationType: "Command",
            CapabilityTag: "ManageOrderState",
            IntentSummary: "Pure business domain entity for orders.",
            TargetEntities: ["Order"],
            EmittedEvents: [],
            IsPureDomain: true,
            ErrorCount: 0,
            LastError: null,
            AnalyzedAtUtc: DateTime.UtcNow
        );

        var intent3 = new IntentRecord(
            FilePath: "services/payments/PaymentProcessor.cs",
            WorkspaceId: "",
            FileId: "ws:type:payments:PaymentProcessor",
            ContentHash: "h3",
            LastModifiedUtc: DateTime.UtcNow,
            Domain: "Payments",
            Layer: "Infrastructure",
            Pattern: "Adapter",
            OperationType: "Command",
            CapabilityTag: "ChargeCreditCard",
            IntentSummary: "Processes credit card charges via Stripe.",
            TargetEntities: ["PaymentTransaction"],
            EmittedEvents: ["PaymentProcessedEvent"],
            IsPureDomain: false,
            ErrorCount: 0,
            LastError: null,
            AnalyzedAtUtc: DateTime.UtcNow
        );

        await _db.SaveIntentRecordAsync(intent1);
        await _db.SaveIntentRecordAsync(intent2);
        await _db.SaveIntentRecordAsync(intent3);

        // Upload nodes and cross-domain edge
        await _db.UploadNodesAsync([
            new("ws:type:orders:OrderService", "Class", new() { ["name"] = "OrderService", ["path"] = "services/orders/OrderService.cs" }),
            new("ws:type:orders:Order", "Class", new() { ["name"] = "Order", ["path"] = "services/orders/Order.cs" }),
            new("ws:type:payments:PaymentProcessor", "Class", new() { ["name"] = "PaymentProcessor", ["path"] = "services/payments/PaymentProcessor.cs" })
        ]);

        await _db.UploadRelationshipsAsync([
            new("ws:type:orders:OrderService", "ws:type:payments:PaymentProcessor", "CALLS", new())
        ]);

        // 2. Query Bounded Context map
        var map = await _engine.GetBoundedContextMapAsync();

        map.HasIntents.Should().BeTrue();
        map.TotalIntents.Should().Be(3);
        map.Contexts.Count.Should().Be(2);

        var orderCtx = map.Contexts.FirstOrDefault(c => c.Name == "OrderManagement");
        orderCtx.Should().NotBeNull();
        orderCtx!.FileCount.Should().Be(2);
        orderCtx.PureDomainCount.Should().Be(1);
        orderCtx.PurityPercentage.Should().Be(50.0);
        orderCtx.TargetEntities.Should().Contain("Order");
        orderCtx.TargetEntities.Should().Contain("OrderItem");
        orderCtx.Capabilities.Should().Contain("PlaceOrder");
        orderCtx.EmittedEvents.Should().Contain("OrderPlacedEvent");

        var payCtx = map.Contexts.FirstOrDefault(c => c.Name == "Payments");
        payCtx.Should().NotBeNull();
        payCtx!.FileCount.Should().Be(1);
        payCtx.TargetEntities.Should().Contain("PaymentTransaction");

        // 3. Verify cross-domain interactions
        map.Interactions.Count.Should().BeGreaterThanOrEqualTo(1);
        var callEdge = map.Interactions.FirstOrDefault(i => i.Source == "context:ordermanagement" && i.Target == "context:payments");
        callEdge.Should().NotBeNull();
        if (callEdge != null)
        {
            callEdge.InteractionType.Should().Be("service_call");
        }

        // 4. Verify Graph Projection (GraphDataDto)
        var graph = await _engine.GetBoundedContextGraphAsync();
        graph.Metadata.Should().NotBeNull();
        graph.Metadata!["view"].Should().Be("BoundedContexts");
        graph.Nodes.Count.Should().Be(2);
        graph.Edges.Count.Should().BeGreaterThanOrEqualTo(1);

        // 5. Verify Serialization
        var mermaid = ArchitectureViewEngine.SerializeBoundedContextMap(map, "mermaid");
        mermaid.Should().Contain("Order Management");
        mermaid.Should().Contain("Payments");
    }

    [Test]
    public async Task GetProjectContracts_AccuratelyClassifiesSubscribedAndPublishedTopics()
    {
        // Add explicit publisher from order-service to orders topic
        await _db.UploadRelationshipsAsync(
        [
            new("ws:project:order-service:", "ws:res:topic:kafka:orders", OntologyConstants.Relationships.PublishesTo, new() { ["category"] = "messaging" })
        ]);

        // 1. Verify consumer contract
        var payContract = await _engine.GetServiceContractsAsync("ws:project:payment-service:");
        payContract.SubscribedTopics.Should().Contain("orders_topic");
        payContract.PublishedTopics.Should().BeEmpty();

        // 2. Verify publisher contract
        var orderContract = await _engine.GetServiceContractsAsync("ws:project:order-service:");
        orderContract.PublishedTopics.Should().Contain("orders_topic");
        orderContract.SubscribedTopics.Should().BeEmpty();

        // 3. Verify ontology details for consumer
        var payDetails = await _engine.GetServiceCapabilitiesAsync("ws:project:payment-service:");
        var topicGroup = payDetails.Groups.FirstOrDefault(g => g.CategoryKey == "topics");
        topicGroup.Should().NotBeNull();
        var subItem = topicGroup!.Items.FirstOrDefault(i => i.Name == "orders_topic");
        subItem.Should().NotBeNull();
        subItem!.Details.Should().Be("Subscribed topic");

        // 4. Verify ontology details for publisher
        var orderDetails = await _engine.GetServiceCapabilitiesAsync("ws:project:order-service:");
        var pubTopicGroup = orderDetails.Groups.FirstOrDefault(g => g.CategoryKey == "topics");
        pubTopicGroup.Should().NotBeNull();
        var pubItem = pubTopicGroup!.Items.FirstOrDefault(i => i.Name == "orders_topic");
        pubItem.Should().NotBeNull();
        pubItem!.Details.Should().Be("Published topic");
    }

    [Test]
    public async Task GetSystemContextGraphAsync_UpgradedWorkload_LiftsSymbolAndOwningProjectEdges()
    {
        // Add a workload Service node that upgrades project cpm-aggregator
        await _db.UploadNodesAsync(
        [
            new("ws:project:cpm-aggregator:", "Project", new Dictionary<string, object>
            {
                ["name"] = "cpm-aggregator",
                ["path"] = "services/cpm-aggregator",
                ["role"] = "Service",
                ["is_library"] = "false"
            }),
            new("ws:service:cpm-aggregator", "Service", new Dictionary<string, object>
            {
                ["name"] = "cpm-aggregator",
                ["associated_project_id"] = "ws:project:cpm-aggregator:",
                ["role"] = "Service"
            }),
            new("ws:sym:services/cpm-aggregator/main.go:Function:Start", "Function", new Dictionary<string, object>
            {
                ["name"] = "Start",
                ["path"] = "services/cpm-aggregator/main.go"
            }),
            new("ws:res:topic:gcp:impression-topic", "Topic", new Dictionary<string, object>
            {
                ["name"] = "impression-topic",
                ["broker"] = "gcp"
            })
        ]);

        // Symbol-level edge from a function in cpm-aggregator to impression-topic
        await _db.UploadRelationshipsAsync(
        [
            new("ws:sym:services/cpm-aggregator/main.go:Function:Start", "ws:res:topic:gcp:impression-topic",
                OntologyConstants.Relationships.SubscribesTo, new() { ["category"] = "messaging" })
        ]);

        var graph = await _engine.GetSystemContextViewAsync();

        // The edge should be lifted to the upgraded Service node ws:service:cpm-aggregator
        // and flow from Topic (source) to Service (target)
        var edge = graph.Edges.FirstOrDefault(e => e.Source == "ws:res:topic:gcp:impression-topic" && e.Target == "ws:service:cpm-aggregator");
        edge.Should().NotBeNull();
        edge!.Kind.Should().Be("SUBSCRIBES_TO");
        edge.Category.Should().Be("messaging");

        // Verify there is NO phantom outbound edge from the service to the topic
        graph.Edges.Any(e => e.Source == "ws:service:cpm-aggregator" && e.Target == "ws:res:topic:gcp:impression-topic").Should().BeFalse();
    }

    [Test]
    public void ExtractDomainKey_IgnoresRunawayDomainProperties_FallsBackToDeterministicNaming()
    {
        var runaway = "Performance & Scalability Tests For ECommerce Platform With Automated Load Generation";
        var node = new GraphNodeDto
        {
            Id = "ws:project:cartservice",
            Name = "cartservice",
            FilePath = "src/cartservice/cartservice.csproj",
            Properties = new Dictionary<string, string>
            {
                ["domain"] = runaway,
                ["intent_domain"] = runaway,
                ["domainId"] = "dom:runaway"
            }
        };

        var (domainKey, domainDisplayName, _) = ArchitectureViewEngine.ExtractDomainKey(node);

        // Runaway string should be ignored, falling back to directory structure 'cartservice'
        domainKey.Should().Be("domain:cartservice");
        domainDisplayName.Should().Be("Cartservice");
    }

    [Test]
    public async Task GetDomainArchitectureAsync_ExcludesTestSuitesDocsAndLoadGenerators()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ce_domain_arch_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var dbPath = Path.Combine(tempDir, "graph.db").Replace('\\', '/');
            using var db = new SqliteGraphClient(dbPath);
            await db.CreateIndicesAsync();
            var engine = new ArchitectureViewEngine(db);

            // Upload projects: a real service, a test suite, a load generator, and a docs project
            await db.UploadNodesAsync(
            [
                new("ws:project:order-service:", "Project", new Dictionary<string, object>
                {
                    ["name"] = "order-service",
                    ["path"] = "src/order-service",
                    ["role"] = "Service",
                    ["domainId"] = "dom:ordermanagement",
                    ["domain"] = "OrderManagement"
                }),
                new("ws:project:telemetry-docs:", "Project", new Dictionary<string, object>
                {
                    ["name"] = "telemetry-docs",
                    ["path"] = "docs/telemetry-docs",
                    ["role"] = "Service",
                    ["domainId"] = "dom:telemetrydocs",
                    ["domain"] = "TelemetryDocs",
                    ["domainDisplayName"] = "Telemetry Docs"
                }),
                new("ws:project:loadgenerator:", "Project", new Dictionary<string, object>
                {
                    ["name"] = "loadgenerator",
                    ["path"] = "src/loadgenerator",
                    ["role"] = "Service",
                    ["domainId"] = "dom:testinginfrastructure",
                    ["domain"] = "TestingInfrastructure"
                }),
                new("ws:project:tracetesting:", "Project", new Dictionary<string, object>
                {
                    ["name"] = "tracetesting",
                    ["path"] = "src/tracetesting",
                    ["role"] = "Service"
                })
            ]);

            var domainArch = await engine.GetDomainArchitectureAsync();

            // Only order-service domain should be present!
            domainArch.Nodes.Any(n => n.DisplayName == "Order Management" || n.Name == "order-service").Should().BeTrue();
            domainArch.Nodes.Any(n => n.DisplayName.Contains("Telemetry Docs") || n.Name.Contains("telemetry-docs")).Should().BeFalse();
            domainArch.Nodes.Any(n => n.DisplayName.Contains("Testing Infrastructure") || n.Id.EndsWith("testinginfrastructure")).Should().BeFalse();
            domainArch.Nodes.Any(n => n.Name.Contains("loadgenerator")).Should().BeFalse();
            domainArch.Nodes.Any(n => n.Name.Contains("tracetesting")).Should().BeFalse();
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Test]
    public async Task GetBoundedContextMap_ExcludesTestSuitesDocsAndSanitizesRunawayDomains()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ce_bc_map_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var dbPath = Path.Combine(tempDir, "graph.db").Replace('\\', '/');
            using var db = new SqliteGraphClient(dbPath);
            await db.CreateIndicesAsync();
            var engine = new ArchitectureViewEngine(db);

            var runaway = "Performance & Scalability Tests For ECommerce Platform With Automated Load Generation";

            // Upload projects: a service with runaway domain, a test suite, a load generator, and a docs project
            await db.UploadNodesAsync(
            [
                new("ws:project:cartservice:", "Project", new Dictionary<string, object>
                {
                    ["name"] = "cartservice",
                    ["path"] = "src/cartservice/cartservice.csproj",
                    ["role"] = "Service",
                    ["domainId"] = "dom:runaway",
                    ["domain"] = runaway
                }),
                new("ws:project:telemetry-docs:", "Project", new Dictionary<string, object>
                {
                    ["name"] = "telemetry-docs",
                    ["path"] = "docs/telemetry-docs",
                    ["role"] = "Service",
                    ["domainId"] = "dom:telemetrydocs",
                    ["domain"] = "TelemetryDocs",
                    ["domainDisplayName"] = "Telemetry Docs"
                }),
                new("ws:project:loadgenerator:", "Project", new Dictionary<string, object>
                {
                    ["name"] = "loadgenerator",
                    ["path"] = "src/loadgenerator",
                    ["role"] = "Service",
                    ["domainId"] = "dom:testinginfrastructure",
                    ["domain"] = "TestingInfrastructure"
                }),
                new("ws:project:frontend.tests:", "Project", new Dictionary<string, object>
                {
                    ["name"] = "frontend.tests",
                    ["path"] = "tests/frontend.tests",
                    ["role"] = "Test"
                })
            ]);

            var bcMap = await engine.GetBoundedContextMapAsync();

            // 1. Verify runaway domain is sanitized (does NOT have the 80+ char sentence)
            bcMap.Contexts.Any(c => c.Name.Contains("PerformanceScalabilityTestsForECommercePlatform")).Should().BeFalse();
            var cartCtx = bcMap.Contexts.FirstOrDefault(c => c.Projects.Contains("cartservice"));
            cartCtx.Should().NotBeNull();
            cartCtx!.DisplayName.Length.Should().BeLessThanOrEqualTo(35);

            // 2. Verify test/docs contexts and projects are excluded
            bcMap.Contexts.Any(c => c.DisplayName.Contains("Telemetry Docs") || c.Projects.Contains("telemetry-docs")).Should().BeFalse();
            bcMap.Contexts.Any(c => c.DisplayName.Contains("Testing Infrastructure") || c.Projects.Contains("loadgenerator")).Should().BeFalse();
            bcMap.Contexts.Any(c => c.Projects.Contains("frontend.tests")).Should().BeFalse();

            // 3. Verify domain aggregation is also clean
            bcMap.Domains.Any(d => d.DisplayName.Contains("Telemetry Docs") || d.DisplayName.Contains("Testing Infrastructure")).Should().BeFalse();
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Test]
    public async Task GetSystemContextView_LiftsDatabaseThroughLibrary_WhenIncludeLibrariesIsFalse()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ce_view_lift_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var dbPath = Path.Combine(tempDir, "graph.db").Replace('\\', '/');
            using var db = new SqliteGraphClient(dbPath);
            var engine = new ArchitectureViewEngine(db);

            await db.UploadNodesAsync([
                new("ws:project:catalog-service:", "Project", new()
                {
                    ["name"] = "catalog-service",
                    ["path"] = "src/catalog-service",
                    ["role"] = "Service",
                    ["is_library"] = "false"
                }),
                new("ws:project:catalog-data:", "Project", new()
                {
                    ["name"] = "catalog-data",
                    ["path"] = "src/catalog-data",
                    ["role"] = "SharedLibrary",
                    ["is_library"] = "true"
                }),
                new("ws:res:db:relational:catalog_db", "Database", new()
                {
                    ["name"] = "catalog_db",
                    ["db_type"] = "relational"
                })
            ]);

            await db.UploadRelationshipsAsync([
                new("ws:project:catalog-service:", "ws:project:catalog-data:", OntologyConstants.Relationships.DependsOn, new() { ["dependency_type"] = "library" }),
                new("ws:project:catalog-data:", "ws:res:db:relational:catalog_db", OntologyConstants.Relationships.UsesDb, new() { ["dependency_type"] = "database" })
            ]);

            var view = await engine.GetSystemContextViewAsync(includeLibraries: false);

            var nodeIds = view.Nodes.Select(n => n.Id).ToList();
            nodeIds.Should().Contain("ws:project:catalog-service:");
            nodeIds.Should().Contain("ws:res:db:relational:catalog_db");
            nodeIds.Should().NotContain("ws:project:catalog-data:", "Libraries must be pruned when includeLibraries is false");

            var edge = view.Edges.FirstOrDefault(e => e.Source == "ws:project:catalog-service:" && e.Target == "ws:res:db:relational:catalog_db");
            edge.Should().NotBeNull("Transitive database relation must be lifted to catalog-service even when includeLibraries is false");
            edge!.Kind.Should().Be("USES_DB");
            edge.IsInferred.Should().BeTrue();
            edge.Properties?.GetValueOrDefault("semantic_lifted").Should().Be("true");
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Test]
    public async Task GetSystemContextView_PreservesTestsEdges_EvenWhenTargetIsLibrary()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ce_view_tests_edge_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var dbPath = Path.Combine(tempDir, "graph.db").Replace('\\', '/');
            using var db = new SqliteGraphClient(dbPath);
            var engine = new ArchitectureViewEngine(db);

            await db.UploadNodesAsync([
                new("ws:suite:core.tests", "TestSuite", new()
                {
                    ["name"] = "core.tests",
                    ["path"] = "tests/core.tests",
                    ["role"] = "Test"
                }),
                new("ws:lib:core.library", "Library", new()
                {
                    ["name"] = "core.library",
                    ["path"] = "src/core.library",
                    ["role"] = "SharedLibrary",
                    ["is_library"] = "true"
                })
            ]);

            await db.UploadRelationshipsAsync([
                new("ws:suite:core.tests", "ws:lib:core.library", OntologyConstants.Relationships.Tests, new() { ["dependency_type"] = "tests" })
            ]);

            var view = await engine.GetSystemContextViewAsync(includeLibraries: true);

            var edge = view.Edges.FirstOrDefault(e => e.Source == "ws:suite:core.tests" && e.Target == "ws:lib:core.library");
            edge.Should().NotBeNull();
            edge!.Kind.Should().Be("TESTS", "TESTS relationship must not be converted to LIBRARY");
            edge.Properties?.GetValueOrDefault("category").Should().Be("test");
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }
}
