using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
public class EdgeEvidenceTests
{
    private string _tempDir = null!;
    private SqliteGraphClient _db = null!;
    private ArchitectureViewEngine _engine = null!;

    private const string Order = "ws:project:order-service:";
    private const string Legacy = "ws:project:order-service-legacy:";
    private const string Payment = "ws:project:payment-service:";
    private const string OrdersDb = "ws:res:db:relational:orders_db";
    private const string OrdersTopic = "ws:res:topic:kafka:orders";

    [SetUp]
    public async Task SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ce_edge_evidence_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _db = new SqliteGraphClient(Path.Combine(_tempDir, "graph.db").Replace('\\', '/'));
        _engine = new ArchitectureViewEngine(_db);

        static Node N(string id, string label, params (string Key, object Value)[] props) =>
            new(id, label, props.ToDictionary(p => p.Key, p => p.Value));

        await _db.UploadNodesAsync(
        [
            // The project points at its manifest, which previously became the match prefix.
            N(Order, "Project", ("name", "order-service"), ("file_path", "services/order-service/Order.Service.csproj")),
            N(Legacy, "Project", ("name", "order-service-legacy"), ("path", "services/order-service-legacy")),
            N(Payment, "Project", ("name", "payment-service"), ("path", "services/payment-service")),
            N(OrdersDb, "Database", ("name", "orders_db"), ("engine", "postgres")),
            N(OrdersTopic, "Topic", ("name", "orders"), ("broker_type", "kafka")),

            N("ws:table:orders", "Table", ("name", "orders"), ("path", "db/orders.sql")),
            N("ws:proc:sp_close", "Procedure", ("name", "sp_close_order"), ("path", "db/procs.sql")),
            N("ws:type:order", "Type", ("name", "OrderEntity"), ("path", "services/order-service/Entities/Order.cs"), ("start_line", 12), ("end_line", 30)),
            N("ws:type:legacy-order", "Type", ("name", "LegacyOrderEntity"), ("path", "services/order-service-legacy/Order.cs"), ("start_line", 3)),
            N("ws:fn:close", "Function", ("name", "CloseOrder"), ("path", "services/order-service/Repo.cs"), ("start_line", 40), ("end_line", 55)),

            N("ws:fn:publish", "Function", ("name", "PublishCreated"), ("path", "services/order-service/Publisher.cs"), ("start_line", 21), ("end_line", 28)),
            N("ws:fn:consume", "Function", ("name", "OnOrderCreated"), ("path", "services/payment-service/Consumer.cs"), ("start_line", 9), ("end_line", 15)),

            N("ws:fn:call-pay", "Function", ("name", "ChargeAsync"), ("path", "services/order-service/PaymentClient.cs"), ("start_line", 33), ("end_line", 36)),
            N("ws:ep:charge", "Endpoint", ("name", "POST /charge"), ("path", "services/payment-service/ChargeController.cs"), ("http_method", "POST"), ("route_template", "/charge"), ("protocol", "REST"))
        ]);

        await _db.UploadRelationshipsAsync(
        [
            new(Order, OrdersDb, OntologyConstants.Relationships.UsesDb, new()),
            new(Order, OrdersTopic, OntologyConstants.Relationships.PublishesTo, new()),
            new(OrdersTopic, Payment, OntologyConstants.Relationships.Triggers, new()),
            new(Order, Payment, OntologyConstants.Relationships.ServiceCall, new()),

            new("ws:table:orders", OrdersDb, OntologyConstants.Relationships.DependsOn, new()),
            new("ws:proc:sp_close", OrdersDb, OntologyConstants.Relationships.DependsOn, new()),
            new("ws:type:order", "ws:table:orders", OntologyConstants.Relationships.PersistedIn, new()),
            new("ws:type:legacy-order", "ws:table:orders", OntologyConstants.Relationships.PersistedIn, new()),
            new("ws:fn:close", "ws:proc:sp_close", OntologyConstants.Relationships.Calls, new()),

            new("ws:fn:publish", OrdersTopic, OntologyConstants.Relationships.PublishesTo, new()),
            new("ws:fn:consume", OrdersTopic, OntologyConstants.Relationships.SubscribesTo, new()),

            new("ws:fn:call-pay", "ws:ep:charge", OntologyConstants.Relationships.CallsEndpoint, new())
        ]);
    }

    [TearDown]
    public void TearDown()
    {
        _db.Dispose();
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    [TestCase("services/order-service/Order.Service.csproj", "services/order-service")]
    [TestCase("services\\order-service\\", "services/order-service")]
    [TestCase("services/order-service", "services/order-service")]
    [TestCase("package.json", null)]
    public void ScopeDirectory_IsDerivedFromManifest(string input, string? expected) =>
        Assert.That(EvidenceScope.NormalizeDirectory(input), Is.EqualTo(expected));

    [Test]
    public void Scope_MatchesOnDirectoryBoundaries()
    {
        var scope = new EvidenceScope("id", "order-service", "services/order-service/Order.Service.csproj");
        Assert.Multiple(() =>
        {
            Assert.That(scope.Matches("services/order-service/Repo.cs"), Is.True);
            Assert.That(scope.Matches("C:\\src\\services\\order-service\\Repo.cs"), Is.True);
            Assert.That(scope.Matches("services/order-service-legacy/Repo.cs"), Is.False);
            Assert.That(scope.Matches("other/order-service-notes.md"), Is.False);
        });
    }

    [Test]
    public async Task DatabaseEvidence_UsesProjectDirectory_NotManifestPath()
    {
        var result = await _engine.GetDatabaseEvidenceAsync(OrdersDb, Order);

        var paths = result.Items.Select(i => i.FilePath).ToList();
        Assert.That(paths, Does.Contain("services/order-service/Entities/Order.cs"));
        Assert.That(paths, Does.Not.Contain("services/order-service-legacy/Order.cs"));
    }

    [Test]
    public async Task DatabaseEvidence_TraversesTablesAndProcedures()
    {
        var result = await _engine.GetDatabaseEvidenceAsync(OrdersDb, Order);

        var orm = result.Items.Single(i => i.Kind == "orm");
        Assert.That(orm.LineStart, Is.EqualTo(12));
        Assert.That(orm.TargetSymbol, Is.EqualTo("orders"));

        var proc = result.Items.Single(i => i.Kind == "procedure");
        Assert.That(proc.CallerSymbol, Is.EqualTo("CloseOrder"));
        Assert.That(proc.FilePath, Is.EqualTo("services/order-service/Repo.cs"));
    }

    [Test]
    public async Task EdgeEvidence_ServiceToTopic_ReturnsPublisher()
    {
        var result = await _engine.GetEdgeEvidenceAsync([Order], [OrdersTopic], "PUBLISHES_TO");

        var item = result.Items.Single();
        Assert.Multiple(() =>
        {
            Assert.That(item.Kind, Is.EqualTo("publish"));
            Assert.That(item.CallerSymbol, Is.EqualTo("PublishCreated"));
            Assert.That(item.LineStart, Is.EqualTo(21));
            Assert.That(result.TotalCount, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task EdgeEvidence_TopicToService_ReturnsSubscriber()
    {
        var result = await _engine.GetEdgeEvidenceAsync([OrdersTopic], [Payment], "TRIGGERS");

        var item = result.Items.Single();
        Assert.That(item.Kind, Is.EqualTo("subscribe"));
        Assert.That(item.FilePath, Is.EqualTo("services/payment-service/Consumer.cs"));
    }

    [Test]
    public async Task EdgeEvidence_ServiceToService_ReturnsEndpointCall()
    {
        var result = await _engine.GetEdgeEvidenceAsync([Order], [Payment], "SERVICE_CALL");

        var item = result.Items.Single();
        Assert.Multiple(() =>
        {
            Assert.That(item.Kind, Is.EqualTo("http_call"));
            Assert.That(item.Operation, Is.EqualTo("POST"));
            Assert.That(item.CallerSymbol, Is.EqualTo("ChargeAsync"));
            Assert.That(item.TargetSymbol, Is.EqualTo("POST /charge"));
        });
    }

    [Test]
    public async Task EdgeEvidence_ServiceToDatabase_AggregatesDatabaseEvidence()
    {
        var result = await _engine.GetEdgeEvidenceAsync([Order], [OrdersDb], "USES_DB");

        Assert.That(result.Items.Select(i => i.Kind), Does.Contain("orm").And.Contain("procedure"));
    }

    [Test]
    public async Task EdgeEvidence_GroupEdge_AggregatesMembersWithoutDuplicates()
    {
        var result = await _engine.GetEdgeEvidenceAsync([Order, Legacy], [Payment, Payment], "SERVICE_CALL");

        Assert.That(result.Items, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task EdgeEvidence_Directional_DoesNotReturnReverseEvidence()
    {
        var result = await _engine.GetEdgeEvidenceAsync([Payment], [Order], "SERVICE_CALL");

        Assert.That(result.Items, Is.Empty, "A -> B must never show B -> A evidence");
    }

    [Test]
    public async Task EdgeEvidence_NestedProjects_ParentDoesNotClaimChildEvidence()
    {
        const string childProj = "ws:project:order-child:";
        await _db.UploadNodesAsync(
        [
            new Node(childProj, "Project", new Dictionary<string, object>
            {
                ["name"] = "order-child",
                ["path"] = "services/order-service/child-worker"
            }),
            new Node("ws:fn:child-pay", "Function", new Dictionary<string, object>
            {
                ["name"] = "ChildChargeAsync",
                ["path"] = "services/order-service/child-worker/WorkerClient.cs",
                ["start_line"] = 18,
                ["end_line"] = 24
            })
        ]);
        await _db.UploadRelationshipsAsync(
        [
            new Relationship("ws:fn:child-pay", "ws:ep:charge", OntologyConstants.Relationships.CallsEndpoint, new())
        ]);

        var parentEvidence = await _engine.GetEdgeEvidenceAsync([Order], [Payment], "SERVICE_CALL");
        var childEvidence = await _engine.GetEdgeEvidenceAsync([childProj], [Payment], "SERVICE_CALL");

        Assert.Multiple(() =>
        {
            Assert.That(parentEvidence.Items.Select(i => i.CallerSymbol), Does.Contain("ChargeAsync"));
            Assert.That(parentEvidence.Items.Select(i => i.CallerSymbol), Does.Not.Contain("ChildChargeAsync"));
            Assert.That(childEvidence.Items.Select(i => i.CallerSymbol), Does.Contain("ChildChargeAsync"));
            Assert.That(childEvidence.Items.Select(i => i.CallerSymbol), Does.Not.Contain("ChargeAsync"));
        });
    }

    [Test]
    public async Task DatabaseEvidence_UpdateAndDeleteQueries_ReportRealOperation()
    {
        await _db.UploadNodesAsync(
        [
            new Node("ws:q:update-order", "Query", new Dictionary<string, object>
            {
                ["name"] = "UpdateOrderStatus",
                ["path"] = "services/order-service/OrderRepo.cs",
                ["start_line"] = 70,
                ["end_line"] = 72,
                ["query_text"] = "UPDATE orders SET status = 'closed' WHERE id = 1",
                ["verb"] = "UPDATE"
            }),
            new Node("ws:q:delete-order", "Query", new Dictionary<string, object>
            {
                ["name"] = "DeleteOrder",
                ["path"] = "services/order-service/OrderRepo.cs",
                ["start_line"] = 80,
                ["end_line"] = 82,
                ["query_text"] = "DELETE FROM orders WHERE id = 1",
                ["verb"] = "DELETE"
            })
        ]);
        await _db.UploadRelationshipsAsync(
        [
            new Relationship("ws:q:update-order", "ws:table:orders", OntologyConstants.Relationships.WritesTo, new Dictionary<string, object>
            {
                [OntologyConstants.PropertyNames.FilePath] = "services/order-service/OrderRepo.cs",
                [OntologyConstants.PropertyNames.Line] = 70,
                [OntologyConstants.PropertyNames.Operation] = "UPDATE",
                [OntologyConstants.PropertyNames.EvidenceKind] = OntologyConstants.EvidenceKinds.Query
            }),
            new Relationship("ws:q:delete-order", "ws:table:orders", OntologyConstants.Relationships.WritesTo, new Dictionary<string, object>
            {
                [OntologyConstants.PropertyNames.FilePath] = "services/order-service/OrderRepo.cs",
                [OntologyConstants.PropertyNames.Line] = 80,
                [OntologyConstants.PropertyNames.Operation] = "DELETE",
                [OntologyConstants.PropertyNames.EvidenceKind] = OntologyConstants.EvidenceKinds.Query
            })
        ]);

        var result = await _engine.GetDatabaseEvidenceAsync(OrdersDb, Order);
        var operations = result.Items.Where(i => i.FilePath == "services/order-service/OrderRepo.cs").Select(i => i.Operation).ToList();

        Assert.That(operations, Does.Contain("UPDATE").And.Contain("DELETE"));
        Assert.That(operations, Does.Not.Contain("INSERT"));
    }

    [Test]
    public async Task DatabaseEvidence_SchemaSubstringCollision_DoesNotFalseMatch()
    {
        const string orderDb = "ws:res:db:relational:postgres.order";
        await _db.UploadNodesAsync(
        [
            new Node(orderDb, "Database", new Dictionary<string, object>
            {
                ["name"] = "postgres.order",
                ["engine"] = "postgres",
                ["schema"] = "order"
            }),
            new Node("ws:q:plural-orders", "Query", new Dictionary<string, object>
            {
                ["name"] = "SelectPluralOrders",
                ["path"] = "services/order-service/PluralRepo.cs",
                ["start_line"] = 10,
                ["query_text"] = "SELECT * FROM orders.line_items WHERE id = 1"
            })
        ]);

        var result = await _engine.GetDatabaseEvidenceAsync(orderDb, Order);
        Assert.That(result.Items.Select(i => i.FilePath), Does.Not.Contain("services/order-service/PluralRepo.cs"));
    }

    [Test]
    public async Task RolledUpEdge_CarriesEvidenceIdsAndFlagsTruncation()
    {
        var props = new Dictionary<string, object>();
        for (var i = 0; i < 5; i++)
        {
            EvidenceProvenanceHelper.RecordContributingEdge(
                props,
                $"ws:fn:caller-{i}",
                OntologyConstants.Relationships.CallsEndpoint,
                "ws:ep:charge",
                cap: 3);
        }

        var ids = EvidenceProvenanceHelper.ParseEvidenceIds(props);
        Assert.Multiple(() =>
        {
            Assert.That(ids, Has.Count.EqualTo(3));
            Assert.That(props[OntologyConstants.PropertyNames.EvidenceCount], Is.EqualTo(5));
            Assert.That(props[OntologyConstants.PropertyNames.EvidenceTruncated], Is.EqualTo("true"));
        });

        await _db.UploadRelationshipsAsync(
        [
            new Relationship(Legacy, Payment, OntologyConstants.Relationships.ServiceCall, new Dictionary<string, object>
            {
                [OntologyConstants.PropertyNames.EvidenceIds] = System.Text.Json.JsonSerializer.Serialize(new[]
                {
                    EvidenceProvenanceHelper.FormatEdgeIdentity("ws:fn:call-pay", OntologyConstants.Relationships.CallsEndpoint, "ws:ep:charge")
                }),
                [OntologyConstants.PropertyNames.EvidenceCount] = 1
            })
        ]);

        var result = await _engine.GetEdgeEvidenceAsync([Legacy], [Payment], "SERVICE_CALL");
        var item = result.Items.Single();
        Assert.Multiple(() =>
        {
            Assert.That(item.CallerSymbol, Is.EqualTo("ChargeAsync"));
            Assert.That(item.TargetSymbol, Is.EqualTo("POST /charge"));
            Assert.That(item.Confidence, Is.EqualTo(OntologyConstants.EvidenceConfidence.Recorded));
        });
    }

    [Test]
    public void EvidenceResponse_SerializesListAsItems()
    {
        var json = System.Text.Json.JsonSerializer.Serialize(new CodeExplorer.Core.Protocol.EdgeEvidenceResponseDto());
        Assert.That(json, Does.Contain("\"items\""));
    }

    [Test]
    public async Task EdgeEvidence_SynthesizedDomainServiceIds_ResolveToProjectScope()
    {
        var result = await _engine.GetEdgeEvidenceAsync(["service:order-service"], ["service:payment-service"], "service_call");

        var item = result.Items.Single();
        Assert.Multiple(() =>
        {
            Assert.That(item.Kind, Is.EqualTo("http_call"));
            Assert.That(item.CallerSymbol, Is.EqualTo("ChargeAsync"));
            Assert.That(item.TargetSymbol, Is.EqualTo("POST /charge"));
        });
    }

    [Test]
    public async Task NodeEvidence_ReturnsEndpointsAndOrmEntitiesForService()
    {
        var payEvidence = await _engine.GetNodeEvidenceAsync(["service:payment-service"], "Service");
        Assert.That(payEvidence.Items.Select(i => i.Kind), Does.Contain("endpoint").And.Contain("config"));

        var orderEvidence = await _engine.GetNodeEvidenceAsync(["service:order-service"], "Service");
        Assert.That(orderEvidence.Items.Select(i => i.Kind), Does.Contain("orm").And.Contain("config"));
    }
}
