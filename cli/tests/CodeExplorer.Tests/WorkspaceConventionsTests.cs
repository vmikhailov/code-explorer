using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
[NonParallelizable]
public class WorkspaceConventionsTests
{
    private IDisposable? _lock;

    [SetUp]
    public async Task SetUp()
    {
        _lock = await TestRegistryLock.AcquireAsync();
        WorkspaceConventions.Clear();
        WorkspaceConventions.DomainDetectionEnabled = true;
    }

    [TearDown]
    public void TearDown()
    {
        WorkspaceConventions.DomainDetectionEnabled = false;
        WorkspaceConventions.Clear();
        _lock?.Dispose();
        _lock = null;
    }

    [Test]
    public void NormalizeTopicName_PreservesLiteralNames()
    {
        WorkspaceConventions.NormalizeTopicName("REQUEST_RECORDER_EVENTS_OUTPUT").Should().Be("REQUEST_RECORDER_EVENTS_OUTPUT");
        WorkspaceConventions.NormalizeTopicName("orderStatusChangedTopicName").Should().Be("orderStatusChangedTopicName");
        WorkspaceConventions.NormalizeTopicName("orders-v1-stream").Should().Be("orders-v1-stream");
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

            WorkspaceConventions.NormalizeTopicName("MY_CUSTOM_TOPIC").Should().Be("orders-feed-v2");
            WorkspaceConventions.NormalizeTopicName("RAW_STREAM").Should().Be("raw-events-topic");
            // Unknown names are preserved as-is
            WorkspaceConventions.NormalizeTopicName("OTHER_EVENT_TOPIC").Should().Be("OTHER_EVENT_TOPIC");
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Test]
    public void TryMatchRouteFunction_MatchesStandardPatterns()
    {
        WorkspaceConventions.TryMatchRouteFunction("serviceRoute('auth')", out var r1).Should().BeTrue();
        r1.Should().Be("auth");

        WorkspaceConventions.TryMatchRouteFunction("resolveRoute(\"billing\")", out var r2).Should().BeTrue();
        r2.Should().Be("billing");

        WorkspaceConventions.TryMatchRouteFunction("routeFor('orders')", out var r3).Should().BeTrue();
        r3.Should().Be("orders");

        WorkspaceConventions.TryMatchRouteFunction("getRoute('payments')", out var r4).Should().BeTrue();
        r4.Should().Be("payments");

        WorkspaceConventions.TryMatchRouteFunction("someOtherFunc('foo')", out _).Should().BeFalse();
    }

    [Test]
    public void LoadFromWorkspace_ConventionsJson_LoadsCustomRouteFunctionsPrefixesAndDbAliases()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ce_conv_custom_" + Guid.NewGuid().ToString("N"));
        var configDir = Path.Combine(tempDir, ".codeexplorer");
        Directory.CreateDirectory(configDir);

        try
        {
            var conventionsJson = """
            {
              "service_prefixes": ["ats-", "ats_", "internal-bundle-"],
              "route_functions": ["getServiceDomainByRoute", "customRouter"],
              "database_aliases": {
                "BigQuery.default": "BigQuery.defaults"
              }
            }
            """;
            File.WriteAllText(Path.Combine(configDir, "conventions.json"), conventionsJson);

            WorkspaceConventions.LoadFromWorkspace(tempDir);

            // Custom route function
            WorkspaceConventions.TryMatchRouteFunction("getServiceDomainByRoute('reporting')", out var r1).Should().BeTrue();
            r1.Should().Be("reporting");

            WorkspaceConventions.TryMatchRouteFunction("customRouter('telecom')", out var r2).Should().BeTrue();
            r2.Should().Be("telecom");

            // Custom service prefix
            WorkspaceConventions.NormalizeServiceName("ats-service-tbmap").Should().Be("tbmap");
            WorkspaceConventions.NormalizeServiceName("ats_ad_manager").Should().Be("ad_manager");
            WorkspaceConventions.NormalizeServiceName("internal-bundle-optimizer").Should().Be("optimizer");

            // Database alias
            WorkspaceConventions.TryGetDatabaseAlias("BigQuery.default", out var dbAlias).Should().BeTrue();
            dbAlias.Should().Be("BigQuery.defaults");
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
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

            WorkspaceConventions.TryGetConfiguredDomain("Billing.Service", null, out var d1).Should().BeTrue();
            d1.Should().Be("Billing");

            WorkspaceConventions.TryGetConfiguredDomain("PaymentGateway", null, out var d2).Should().BeTrue();
            d2.Should().Be("Billing");

            WorkspaceConventions.TryGetConfiguredDomain("OrderWorker", null, out var d3).Should().BeTrue();
            d3.Should().Be("OrderManagement");

            // Path-based match
            WorkspaceConventions.TryGetConfiguredDomain(null, "src/Services/Orders.Api/Orders.Api.csproj", out var d4).Should().BeTrue();
            d4.Should().Be("OrderManagement");

            WorkspaceConventions.TryGetConfiguredDomain("UnknownService", null, out _).Should().BeFalse();
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

            WorkspaceConventions.NormalizeTopicName("TEST_TOPIC").Should().Be("test-v1");
            WorkspaceConventions.TryGetConfiguredDomain("Inventory.Api", null, out var d).Should().BeTrue();
            d.Should().Be("Inventory");
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }
}
