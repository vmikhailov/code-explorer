using CodeExplorer.Core.Analysis;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Parser;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
public class CodeIntentAnalyzerTests
{
    [Test]
    public async Task SqliteGraphClient_LoadIntentCandidates_ReturnsArchitecturalNodes()
    {
        using var client = new SqliteGraphClient(":memory:");
        await client.CreateIndicesAsync();

        var nodes = new List<Node>
        {
            new("ws:type:1", OntologyConstants.NodeLabels.Type, new Dictionary<string, object>
            {
                ["id"] = "ws:type:1",
                ["name"] = "ScheduledActionService",
                ["path"] = "src/services/scheduled-action.service.ts",
                ["kind"] = "class"
            }),
            new("ws:type:2", OntologyConstants.NodeLabels.Type, new Dictionary<string, object>
            {
                ["id"] = "ws:type:2",
                ["name"] = "OrderController",
                ["path"] = "src/controllers/order.controller.cs",
                ["kind"] = "class"
            }),
            new("ws:type:3", OntologyConstants.NodeLabels.Type, new Dictionary<string, object>
            {
                ["id"] = "ws:type:3",
                ["name"] = "SomeHelperUtilsSpec",
                ["path"] = "src/utils.spec.ts",
                ["kind"] = "class"
            })
        };

        await client.UploadNodesAsync(nodes);

        var candidates = await client.LoadIntentCandidatesAsync("ws");

        Assert.That(candidates.Any(c => c.Name == "ScheduledActionService"), Is.True);
        Assert.That(candidates.Any(c => c.Name == "OrderController"), Is.True);
        Assert.That(candidates.Any(c => c.Name == "SomeHelperUtilsSpec"), Is.False);
    }

    [Test]
    public async Task SqliteGraphClient_SaveIntentPredictions_UpdatesNodeAndCreatesDomainEdge()
    {
        using var client = new SqliteGraphClient(":memory:");
        await client.CreateIndicesAsync();

        var node = new Node("ws:type:svc", OntologyConstants.NodeLabels.Type, new Dictionary<string, object>
        {
            ["id"] = "ws:type:svc",
            ["name"] = "BillingService",
            ["path"] = "src/services/billing.service.ts",
            ["kind"] = "class"
        });
        await client.UploadNodesAsync([node]);

        var predictions = new List<CodeIntentPredictionResult>
        {
            new(
                Id: "ws:type:svc",
                FilePath: "src/services/billing.service.ts",
                Domain: "Billing",
                Layer: "Application",
                Pattern: "Service",
                OperationType: "Command",
                CapabilityTag: "ProcessPayment",
                IntentSummary: "Handles invoices and payment execution.",
                IsPureDomain: false,
                TargetEntities: ["Invoice", "Payment"],
                EmittedEvents: ["InvoicePaidEvent"]
            )
        };

        await client.SaveIntentPredictionsAsync("ws", predictions);

        // Verify updated properties via Cypher query
        var queryResult = await client.ExecuteQueryAsync("MATCH (n:Type {id: 'ws:type:svc'}) RETURN n.intent_domain AS domain, n.intent_layer AS layer, n.intent_summary AS summary");
        Assert.That(queryResult.Contains("Billing"), Is.True);
        Assert.That(queryResult.Contains("Application"), Is.True);
        Assert.That(queryResult.Contains("Handles invoices and payment execution."), Is.True);

        // Verify Domain node creation and relationship
        var domainResult = await client.ExecuteQueryAsync("MATCH (d:Domain) RETURN d.id AS id, d.name AS name");
        Assert.That(domainResult.Contains("Billing"), Is.True);

        var relResult = await client.ExecuteQueryAsync("MATCH (n:Type)-[r:BELONGS_TO_DOMAIN]->(d:Domain) RETURN n.id AS from_id, d.id AS to_id");
        Assert.That(relResult.Contains("ws:type:svc"), Is.True);
        Assert.That(domainResult.ToLowerInvariant().Contains("ws:dom:billing"), Is.True);
    }

    [Test]
    public async Task CodeIntentAnalyzer_EnrichAsync_FallbackWhenNoModel_RunsCleanlyWithoutError()
    {
        using var client = new SqliteGraphClient(":memory:");
        await client.CreateIndicesAsync();

        var channel = System.Threading.Channels.Channel.CreateUnbounded<Func<Task>>();
        var tempDir = Path.GetTempPath();
        var ctx = new ParsingContext(tempDir, tempDir, client, channel);
        ctx.WorkspaceId = "test_ws";

        // Call EnrichAsync - should not throw, should log fallback message
        await CodeIntentAnalyzer.EnrichAsync(ctx);

        Assert.Pass();
    }

    [Test]
    public void ModelManager_ResolveModelPath_FindsExistingModel()
    {
        var path = ModelManager.ResolveModelPath();
        if (File.Exists(ModelManager.DefaultModelPath))
        {
            Assert.That(path, Is.Not.Null);
            Assert.That(File.Exists(path), Is.True);
        }
        else
        {
            Assert.Pass("Model not present on machine in default path; skipping check.");
        }
    }

    [Test]
    public async Task NativeIntentPredictor_Inference_ReturnsValidDomainAndIntent()
    {
        var modelPath = ModelManager.ResolveModelPath();
        if (string.IsNullOrEmpty(modelPath) || !File.Exists(modelPath))
        {
            Assert.Ignore("GGUF model not available on this environment; skipping inference test.");
            return;
        }

        using var predictor = new NativeIntentPredictor(modelPath, contextSize: 2048, gpuLayers: 0);

        var sampleCode = @"
namespace OrderSystem.Services;

public class OrderPlacementService : IOrderService
{
    private readonly IPaymentGateway _paymentGateway;
    private readonly IEventBus _eventBus;

    public async Task PlaceOrderAsync(Order order)
    {
        await _paymentGateway.ChargeAsync(order.Total);
        await _eventBus.PublishAsync(new OrderPlacedEvent(order.Id));
    }
}
";

        var (result, raw) = await predictor.PredictWithRawAsync("OrderPlacementService.cs", sampleCode);
        TestContext.Out.WriteLine($"RAW MODEL OUTPUT:\n{raw}");

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Domain, Is.Not.Null.Or.Empty);
        Assert.That(result.IntentSummary, Is.Not.Null.Or.Empty);
        TestContext.Out.WriteLine($"Inferred Domain: {result.Domain}, Layer: {result.Layer}, Pattern: {result.Pattern}, Summary: {result.IntentSummary}");
    }

    [Test]
    public async Task SqliteGraphClient_IntentCacheLifecycle_PreservedAcrossClearDatabase()
    {
        using var client = new SqliteGraphClient(":memory:");
        await client.CreateIndicesAsync();

        var node = new Node("ws:f:src/service.ts", OntologyConstants.NodeLabels.File, new Dictionary<string, object>
        {
            ["id"] = "ws:f:src/service.ts",
            ["name"] = "service.ts",
            ["path"] = "src/service.ts"
        });
        await client.UploadNodesAsync([node]);

        var record = new IntentRecord(
            FilePath: "src/service.ts",
            WorkspaceId: "ws",
            FileId: "ws:f:src/service.ts",
            ContentHash: "hash123",
            LastModifiedUtc: DateTime.UtcNow,
            Domain: "Payments",
            Layer: "Application",
            Pattern: "Service",
            OperationType: "Command",
            CapabilityTag: "CapturePayment",
            IntentSummary: "Processes payments securely.",
            TargetEntities: ["Payment"],
            EmittedEvents: ["PaymentProcessed"],
            IsPureDomain: false,
            ErrorCount: 0,
            LastError: null,
            AnalyzedAtUtc: DateTime.UtcNow
        );

        await client.SaveIntentRecordAsync(record);

        // Verify loaded
        var existing = await client.LoadExistingIntentsAsync("ws");
        Assert.That(existing.Count, Is.EqualTo(1));
        Assert.That(existing[0].Domain, Is.EqualTo("Payments"));

        // Apply to graph
        var applied = await client.ApplyCachedIntentsToGraphAsync("ws");
        Assert.That(applied, Is.GreaterThanOrEqualTo(1));

        var domainQuery = await client.ExecuteQueryAsync("MATCH (d:Domain) RETURN d.id AS id, d.name AS name");
        Assert.That(domainQuery.Contains("Payments"), Is.True);

        // Clear database (e.g. ce scan --clear)
        await client.ClearDatabaseAsync();

        // Node should be deleted
        var candidatesAfterClear = await client.LoadIntentCandidatesAsync("ws");
        Assert.That(candidatesAfterClear.Count, Is.EqualTo(0));

        // BUT Intent record should STILL BE PRESERVED
        var intentsAfterClear = await client.LoadExistingIntentsAsync("ws");
        Assert.That(intentsAfterClear.Count, Is.EqualTo(1));
        Assert.That(intentsAfterClear[0].ContentHash, Is.EqualTo("hash123"));

        // Test error increment & reset
        await client.IncrementIntentErrorAsync("src/service.ts", "ws", "ws:f:src/service.ts", "hash123", DateTime.UtcNow, "LLM timeout");
        var withError = await client.LoadExistingIntentsAsync("ws");
        Assert.That(withError[0].ErrorCount, Is.EqualTo(1));
        Assert.That(withError[0].LastError, Is.EqualTo("LLM timeout"));

        await client.ResetIntentErrorsAsync("ws");
        var resetErrors = await client.LoadExistingIntentsAsync("ws");
        Assert.That(resetErrors[0].ErrorCount, Is.EqualTo(0));
        Assert.That(resetErrors[0].LastError, Is.Null);

        // Test explicit ClearIntents
        await client.ClearIntentsAsync("ws");
        var emptyIntents = await client.LoadExistingIntentsAsync("ws");
        Assert.That(emptyIntents.Count, Is.EqualTo(0));
    }
}

