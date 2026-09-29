using NUnit.Framework;
using CodeExplorer.Core.Common;

namespace CodeExplorer.Tests;

[TestFixture]
public class WorkspaceConventionsTests
{
    [SetUp]
    public void SetUp()
    {
        WorkspaceConventions.Clear();
    }

    [Test]
    public void NormalizeTopicName_AlgorithmicKebabCase_WorksGenericly()
    {
        Assert.That(WorkspaceConventions.NormalizeTopicName("USER_CREATED_TOPIC"), Is.EqualTo("user-created-topic"));
        Assert.That(WorkspaceConventions.NormalizeTopicName("orderStatusChangedTopicName"), Is.EqualTo("order-status-changed-topic"));
        Assert.That(WorkspaceConventions.NormalizeTopicName("paymentReceiptSubName"), Is.EqualTo("payment-receipt-sub"));
        Assert.That(WorkspaceConventions.NormalizeTopicName("orders-v1-stream"), Is.EqualTo("orders-v1-stream"));
    }

    [Test]
    public void LoadFromWorkspace_CustomConventionsJson_LoadsTopicAliases()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ce_conv_test_" + Guid.NewGuid().ToString("N"));
        var configDir = Path.Combine(tempDir, ".codeexplorer");
        Directory.CreateDirectory(configDir);

        try
        {
            var conventionsJson = """
            {
              "topics": {
                "MY_CUSTOM_TOPIC": "orders-feed-v2",
                "RAW_STREAM": "raw-events-topic"
              }
            }
            """;
            File.WriteAllText(Path.Combine(configDir, "conventions.json"), conventionsJson);

            WorkspaceConventions.LoadFromWorkspace(tempDir);

            Assert.That(WorkspaceConventions.NormalizeTopicName("MY_CUSTOM_TOPIC"), Is.EqualTo("orders-feed-v2"));
            Assert.That(WorkspaceConventions.NormalizeTopicName("RAW_STREAM"), Is.EqualTo("raw-events-topic"));
            // Fallback still works for unknown
            Assert.That(WorkspaceConventions.NormalizeTopicName("OTHER_EVENT_TOPIC"), Is.EqualTo("other-event-topic"));
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Test]
    public void TryMatchRouteFunction_MatchesStandardPatterns()
    {
        Assert.That(WorkspaceConventions.TryMatchRouteFunction("getServiceDomainByRoute('auth')", out var r1), Is.True);
        Assert.That(r1, Is.EqualTo("auth"));

        Assert.That(WorkspaceConventions.TryMatchRouteFunction("resolveRoute(\"billing\")", out var r2), Is.True);
        Assert.That(r2, Is.EqualTo("billing"));

        Assert.That(WorkspaceConventions.TryMatchRouteFunction("routeFor('orders')", out var r3), Is.True);
        Assert.That(r3, Is.EqualTo("orders"));

        Assert.That(WorkspaceConventions.TryMatchRouteFunction("someOtherFunc('foo')", out _), Is.False);
    }

    [Test]
    public void LoadFromWorkspace_DomainsJson_LoadsDomains()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ce_dom_test_" + Guid.NewGuid().ToString("N"));
        var configDir = Path.Combine(tempDir, ".codeexplorer");
        Directory.CreateDirectory(configDir);

        try
        {
            var domainsJson = """
            {
              "domains": {
                "Billing": ["Billing.Service", "PaymentGateway"],
                "OrderManagement": ["Orders.Api", "OrderWorker"]
              }
            }
            """;
            File.WriteAllText(Path.Combine(configDir, "domains.json"), domainsJson);

            WorkspaceConventions.LoadFromWorkspace(tempDir);

            Assert.That(WorkspaceConventions.TryGetConfiguredDomain("Billing.Service", null, out var d1), Is.True);
            Assert.That(d1, Is.EqualTo("Billing"));

            Assert.That(WorkspaceConventions.TryGetConfiguredDomain("PaymentGateway", null, out var d2), Is.True);
            Assert.That(d2, Is.EqualTo("Billing"));

            Assert.That(WorkspaceConventions.TryGetConfiguredDomain("OrderWorker", null, out var d3), Is.True);
            Assert.That(d3, Is.EqualTo("OrderManagement"));

            // Path-based match
            Assert.That(WorkspaceConventions.TryGetConfiguredDomain(null, "src/Services/Orders.Api/Orders.Api.csproj", out var d4), Is.True);
            Assert.That(d4, Is.EqualTo("OrderManagement"));

            Assert.That(WorkspaceConventions.TryGetConfiguredDomain("UnknownService", null, out _), Is.False);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Test]
    public void LoadFromWorkspace_ConventionsJsonDomains_LoadsDomains()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ce_conv_dom_test_" + Guid.NewGuid().ToString("N"));
        var configDir = Path.Combine(tempDir, ".codeexplorer");
        Directory.CreateDirectory(configDir);

        try
        {
            var conventionsJson = """
            {
              "topics": {
                "TEST_TOPIC": "test-v1"
              },
              "domains": {
                "Inventory": ["Inventory.Api", "StockWorker"]
              }
            }
            """;
            File.WriteAllText(Path.Combine(configDir, "conventions.json"), conventionsJson);

            WorkspaceConventions.LoadFromWorkspace(tempDir);

            Assert.That(WorkspaceConventions.NormalizeTopicName("TEST_TOPIC"), Is.EqualTo("test-v1"));
            Assert.That(WorkspaceConventions.TryGetConfiguredDomain("Inventory.Api", null, out var d), Is.True);
            Assert.That(d, Is.EqualTo("Inventory"));
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }
}
