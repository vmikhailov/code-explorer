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
}
