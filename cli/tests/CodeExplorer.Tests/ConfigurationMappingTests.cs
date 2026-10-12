using System.Threading.Channels;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Parser;
using CodeExplorer.Core.Parser.Layers;
using CodeExplorer.Parser.CSharp;
using NUnit.Framework;
using AwesomeAssertions;

namespace CodeExplorer.Tests;

[TestFixture]
public class ConfigurationMappingTests
{
    private string _tempDir = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ce_config_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDir))
        {
            try { Directory.Delete(_tempDir, true); } catch { }
        }
    }

    [Test]
    public async Task Test_AppSettingsJson_ExtractsDatabasesAndCloudServices()
    {
        var projDir = Path.Combine(_tempDir, "Backend");
        Directory.CreateDirectory(projDir);
        await File.WriteAllTextAsync(Path.Combine(projDir, "Backend.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup></Project>");

        var appsettings = """
        {
          "ConnectionStrings": {
            "DefaultConnection": "Host=localhost;Port=5432;Database=orders_db;Username=postgres;Password=secret;",
            "Redis": "redis://localhost:6379",
            "RabbitMQ": "amqp://guest:guest@localhost:5672"
          },
          "Stripe": {
            "ApiKey": "sk_test_12345"
          },
          "Auth0": {
            "Domain": "mycompany.auth0.com"
          }
        }
        """;
        await File.WriteAllTextAsync(Path.Combine(projDir, "appsettings.json"), appsettings);

        var channel = Channel.CreateUnbounded<Func<Task>>();

        await using var client = new InMemoryGraphClient();

        var ctx = new ParsingContext(_tempDir, _tempDir, client, channel);

        WorkspaceIndexer.Register(new CSharpParser());
        var l1 = await new Layer1PhysicalParser().ParseAsync(ctx);
        var l2 = await new Layer2ProjectParser().ParseAsync(l1, ctx);
        var l3 = await new Layer3SyntacticParser().ParseAsync(l2, ctx);
        var l4 = await new Layer4SemanticParser().ParseAsync(l3, ctx);

        var databases = l4.SemanticNodes.OfType<DatabaseNode>().ToList();

        (databases.Any(d =>
                d.DbType == "relational" && (d.Name.Contains("DefaultConnection") || d.Name.Contains("orders_db")))).Should().BeTrue("Postgres DefaultConnection / orders_db should be extracted");

        (databases.Any(d => d.DbType == "cache" && d.Name.Contains("Redis"))).Should().BeTrue("Redis database should be extracted");

        var topics = l4.SemanticNodes.OfType<TopicNode>().ToList();

        (topics.Any(t => t.BrokerType == "rabbitmq")).Should().BeTrue("RabbitMQ topic/broker should be extracted");

        var cloud = l4.SemanticNodes.OfType<CloudServiceNode>().ToList();
        (cloud.Any(c => c.Name == "Stripe")).Should().BeTrue("Stripe cloud service should be extracted");
        (cloud.Any(c => c.Name == "Auth0")).Should().BeTrue("Auth0 cloud service should be extracted");

        var configRels = l4.SemanticRelationships.Where(r => r.Kind == "CONFIGURES").ToList();

        configRels.Count.Should().BeGreaterThanOrEqualTo(4, "CONFIGURES edges should link appsettings.json to configured services");
    }

    [Test]
    public async Task Test_DockerCompose_DoesNotExtractFalseInfrastructureServices()
    {
        var composeContent = """
        version: '3.8'
        services:
          postgres:
            image: postgres:15-alpine
            environment:
              POSTGRES_DB: main_db
          redis:
            image: redis:7-alpine
          rabbitmq:
            image: rabbitmq:3-management
          kafka:
            image: confluentinc/cp-kafka:7.0.1
        """;
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "docker-compose.yml"), composeContent);

        var channel = Channel.CreateUnbounded<Func<Task>>();

        await using var client = new InMemoryGraphClient();

        var ctx = new ParsingContext(_tempDir, _tempDir, client, channel);

        WorkspaceIndexer.Register(new CSharpParser());
        var l1 = await new Layer1PhysicalParser().ParseAsync(ctx);
        var l2 = await new Layer2ProjectParser().ParseAsync(l1, ctx);
        var l3 = await new Layer3SyntacticParser().ParseAsync(l2, ctx);
        var l4 = await new Layer4SemanticParser().ParseAsync(l3, ctx);

        var databases = l4.SemanticNodes.OfType<DatabaseNode>().ToList();
        databases.Should().BeEmpty("Docker compose should not generate false DatabaseNode dependencies");

        var topics = l4.SemanticNodes.OfType<TopicNode>().ToList();
        topics.Should().BeEmpty("Docker compose should not generate false TopicNode dependencies");
    }

    [Test]
    public async Task Test_DotEnv_ExtractsServices()
    {
        var envContent = """
        DATABASE_URL=postgres://user:password@localhost:5432/my_app_db
        REDIS_URL=redis://localhost:6379
        STRIPE_SECRET_KEY=sk_live_dummykey
        """;
        await File.WriteAllTextAsync(Path.Combine(_tempDir, ".env"), envContent);

        var channel = Channel.CreateUnbounded<Func<Task>>();

        await using var client = new InMemoryGraphClient();

        var ctx = new ParsingContext(_tempDir, _tempDir, client, channel);

        WorkspaceIndexer.Register(new CSharpParser());
        var l1 = await new Layer1PhysicalParser().ParseAsync(ctx);
        var l2 = await new Layer2ProjectParser().ParseAsync(l1, ctx);
        var l3 = await new Layer3SyntacticParser().ParseAsync(l2, ctx);
        var l4 = await new Layer4SemanticParser().ParseAsync(l3, ctx);

        var databases = l4.SemanticNodes.OfType<DatabaseNode>().ToList();

        (databases.Any(d => d.DbType == "relational")).Should().BeTrue("Postgres DATABASE_URL in .env should be extracted");
        (databases.Any(d => d.DbType == "cache")).Should().BeTrue("REDIS_URL in .env should be extracted");

        var cloud = l4.SemanticNodes.OfType<CloudServiceNode>().ToList();
        (cloud.Any(c => c.Name == "Stripe")).Should().BeTrue("Stripe in .env should be extracted");
    }
}
