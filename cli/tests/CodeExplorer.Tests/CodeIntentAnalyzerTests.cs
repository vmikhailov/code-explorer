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
}
