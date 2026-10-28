using System.Threading.Channels;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Parser.Layers;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
[NonParallelizable]
public class ConfigurationIndexTests
{
    private string _tempDir = null!;
    private IDisposable? _lock;

    [SetUp]
    public async Task SetUp()
    {
        _lock = await TestRegistryLock.AcquireAsync();
        _tempDir = Path.Combine(Path.GetTempPath(), "ce_configstore_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        ConfigurationIndex.Clear();
        ConstantRegistry.Clear();
    }

    [TearDown]
    public void TearDown()
    {
        ConfigurationIndex.Clear();
        ConstantRegistry.Clear();
        _lock?.Dispose();
        _lock = null;
        if (Directory.Exists(_tempDir))
        {
            try { Directory.Delete(_tempDir, true); } catch { }
        }
    }

    [Test]
    public void Test_ConfigurationIndex_ParsesAppSettings_HierarchicalAndFlatKeys()
    {
        var appsettings = """
        {
          "ConnectionStrings": {
            "DefaultConnection": "Server=127.0.0.1;Database=ShopDb;User Id=sa;Password=secret;"
          },
          "Kafka": {
            "Topics": {
              "OrderCreated": "shop.orders.v1.created",
              "InventoryUpdated": "shop.inventory.v1.updated"
            }
          },
          "UrlsSettings": {
            "PaymentService": "http://payment-service:8080",
            "NotificationApi": "http://notification-api:5000"
          },
          "PaymentGateways": {
            "CloudPayments": {
              "BaseUrl": "https://api.cloudpayments.ru"
            }
          }
        }
        """;

        var filePath = Path.Combine(_tempDir, "appsettings.json");
        File.WriteAllText(filePath, appsettings);

        ConfigTestHelper.Load(filePath, "Shop.Host");

        // 1. Hierarchical colon lookup
        ConfigurationIndex.TryGetConfig("Shop.Host", "ConnectionStrings:DefaultConnection", out var connStr).Should().BeTrue();
        connStr.Should().Contain("ShopDb");

        ConfigurationIndex.TryGetConfig("Shop.Host", "Kafka:Topics:OrderCreated", out var topic).Should().BeTrue();
        topic.Should().Be("shop.orders.v1.created");

        // 2. Integration with ConstantRegistry
        ConstantRegistry.TryResolve("Shop.Host", "ConnectionStrings:DefaultConnection", out var regConn).Should().BeTrue();
        regConn.Should().Contain("ShopDb");

        // Short key for connection strings
        ConstantRegistry.TryResolve("Shop.Host", "DefaultConnection", out var shortConn).Should().BeTrue();
        shortConn.Should().Contain("ShopDb");

        // Short key for topics
        ConstantRegistry.TryResolve("Shop.Host", "OrderCreated", out var shortTopic).Should().BeTrue();
        shortTopic.Should().Be("shop.orders.v1.created");

        // Screaming snake / Environment variable notation
        ConstantRegistry.TryResolve("Shop.Host", "KAFKA_TOPICS_ORDERCREATED", out var envTopic).Should().BeTrue();
        envTopic.Should().Be("shop.orders.v1.created");

        // 3. Generic Discovered URLs
        var urls = ConfigurationIndex.GetDiscoveredUrls("Shop.Host");
        urls.Count.Should().Be(3, "Should discover PaymentService, NotificationApi, and CloudPayments URLs");

        var paymentEp = urls.FirstOrDefault(u => u.ServiceName == "PaymentService");
        paymentEp.Should().NotBeNull();
        paymentEp!.IsExternal.Should().BeFalse("payment-service without dot should be classified as internal");
        paymentEp.Url.Should().Be("http://payment-service:8080");

        var cpEp = urls.FirstOrDefault(u => u.ServiceName == "CloudPayments" || u.Host.Contains("cloudpayments"));
        cpEp.Should().NotBeNull();
        cpEp!.IsExternal.Should().BeTrue("api.cloudpayments.ru with dot should be classified as external");
        cpEp.Url.Should().Be("https://api.cloudpayments.ru");
    }

    [Test]
    public void Test_ConfigurationIndex_ParsesDotEnv_WithExportsAndDunderKeys()
    {
        var dotEnv = """
        # Database & Cache
        DATABASE_URL=postgres://user:pass@localhost:5432/orders
        REDIS_URL=redis://localhost:6379

        # ASP.NET style double underscore
        KAFKA__TOPICS__ORDER_SUBMITTED=orders.submitted.v2

        # Service endpoints
        export BILLING_SERVICE_URL=http://billing-service:9000
        STRIPE_API_URL=https://api.stripe.com
        """;

        var filePath = Path.Combine(_tempDir, ".env");
        File.WriteAllText(filePath, dotEnv);

        ConfigTestHelper.Load(filePath, "OrdersApp");

        // 1. Direct key
        ConfigurationIndex.TryGetConfig("OrdersApp", "DATABASE_URL", out var dbUrl).Should().BeTrue();
        dbUrl.Should().Contain("postgres://");

        // 2. Dunder to colon conversion
        ConstantRegistry.TryResolve("OrdersApp", "Kafka:Topics:OrderSubmitted", out var topic).Should().BeTrue();
        topic.Should().Be("orders.submitted.v2");

        // 3. Discovered URLs
        var urls = ConfigurationIndex.GetDiscoveredUrls("OrdersApp");
        (urls.Any(u => u.Url == "http://billing-service:9000" && !u.IsExternal)).Should().BeTrue();
        (urls.Any(u => u.Url == "https://api.stripe.com" && u.IsExternal)).Should().BeTrue();
    }

    [Test]
    public async Task Test_Layer3SyntacticParser_PreloadsConfigsBeforeAST()
    {
        var projDir = Path.Combine(_tempDir, "PaymentGateway");
        Directory.CreateDirectory(projDir);
        await File.WriteAllTextAsync(Path.Combine(projDir, "PaymentGateway.csproj"),
            "<Project Sdk=\"Microsoft.NET.Sdk.Web\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");

        var appsettings = """
        {
          "ConnectionStrings": {
            "PaymentDb": "Server=localhost;Database=payments;User Id=sa;Password=secret;"
          },
          "Kafka": {
            "PaymentProcessedTopic": "payments.processed.v1"
          },
          "CloudPayments": {
            "BaseUrl": "https://api.cloudpayments.ru"
          },
          "UrlsSettings": {
            "OrderService": "http://order-service:5000"
          }
        }
        """;
        await File.WriteAllTextAsync(Path.Combine(projDir, "appsettings.json"), appsettings);

        var channel = Channel.CreateUnbounded<Func<Task>>();

        await using var client = new InMemoryGraphClient();

        var ctx = new ParsingContext(_tempDir, _tempDir, client, channel);

        WorkspaceIndexer.AutoRegisterParsers();
        var l1 = await new Layer1PhysicalParser().ParseAsync(ctx);
        var l2 = await new Layer2ProjectParser().ParseAsync(l1, ctx);
        var l3 = await new Layer3SyntacticParser().ParseAsync(l2, ctx);

        // Verification: Even before Layer 4 runs, ConstantRegistry MUST contain values from appsettings.json!
        ConstantRegistry.TryResolve("PaymentGateway", "PaymentDb", out var dbVal).Should().BeTrue("ConstantRegistry should have PaymentDb preloaded during Layer 3");
        dbVal.Should().Contain("payments");

        ConstantRegistry.TryResolve("PaymentGateway", "Kafka:PaymentProcessedTopic", out var topicVal).Should().BeTrue("ConstantRegistry should have Kafka:PaymentProcessedTopic preloaded during Layer 3");
        topicVal.Should().Be("payments.processed.v1");

        // Now run Layer 4 to verify semantic nodes and relationships
        var l4 = await new Layer4SemanticParser().ParseAsync(l3, ctx);

        // ExternalServiceNode for CloudPayments should be created generically
        var extServices = l4.SemanticNodes.OfType<ExternalServiceNode>().ToList();

        (extServices.Any(e => e.DomainOrService == "api.cloudpayments.ru" || e.Name == "CloudPayments")).Should().BeTrue("ExternalServiceNode for api.cloudpayments.ru should be created generically without hardcoded parser");

        // ServiceCall relationship for OrderService should be created generically
        var serviceCalls = l4.SemanticRelationships.Where(r => r.Kind == "SERVICE_CALL").ToList();

        serviceCalls.Any(r => r.To.Contains("service_target:orderservice")).Should().BeTrue("ServiceCall for order-service target should be created generically");
    }

    [Test]
    public void Test_ConfigurationIndex_ParsesAppSettings_WithCommentsAndTrailingCommas()
    {
        var appsettingsWithComments = """
        {
          // Logging configuration
          "Logging": {
            "LogLevel": {
              "Default": "Information", // Default level
              "Microsoft.AspNetCore": "Warning",
            },
          },
          /* Commented out Kestrel
          "Kestrel": {
            "Endpoints": { "Http": { "Url": "http://localhost:5000" } }
          },
          */
          "ConnectionStrings": {
            //"LegacyConnection": "Server=old;...",
            "DefaultConnection": "Server=localhost;Database=VSBS_Retail;Trusted_Connection=True;",
          },
        }
        """;

        var filePath = Path.Combine(_tempDir, "appsettings_comments.json");
        File.WriteAllText(filePath, appsettingsWithComments);

        ConfigTestHelper.Load(filePath, "RetailService");

        ConfigurationIndex.TryGetConfig("RetailService", "ConnectionStrings:DefaultConnection", out var connStr).Should().BeTrue();
        connStr.Should().Contain("VSBS_Retail");
        ConfigurationIndex.TryGetConfig("RetailService", "Logging:LogLevel:Default", out var logVal).Should().BeTrue();
        logVal.Should().Be("Information");
    }

    [Test]
    public void Test_ConfigurationIndex_DoesNotExtractNumericAccountIdsOrQueueUrlsAsServices()
    {
        var dotEnv = """
        AWS_QUEUE_URL=http://sqs.us-east-1.localhost.localstack.cloud:4566/000000000000/requests-queue
        PARTNER_API_URL=https://api.partner.io/000000000000/orders
        """;

        var filePath = Path.Combine(_tempDir, ".env");
        File.WriteAllText(filePath, dotEnv);

        ConfigTestHelper.Load(filePath, "MpcService");

        var urls = ConfigurationIndex.GetDiscoveredUrls("MpcService");
        urls.Should().NotContain(u => u.ServiceName == "000000000000");
        urls.Should().NotContain(u => u.Host.StartsWith("sqs.", StringComparison.OrdinalIgnoreCase));
        urls.Should().ContainSingle(u => u.Host == "api.partner.io" && u.ServiceName == "PARTNER");
    }
}
