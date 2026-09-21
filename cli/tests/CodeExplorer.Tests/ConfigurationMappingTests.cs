using System.Threading.Channels;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Common.Relationships;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Parser;
using CodeExplorer.Core.Parser.Layers;
using CodeExplorer.Parser.CSharp;
using NUnit.Framework;

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
        Assert.That(databases.Any(d => d.DbType == "relational" && (d.Name.Contains("DefaultConnection") || d.Name.Contains("orders_db"))), Is.True, "Postgres DefaultConnection / orders_db should be extracted");
        Assert.That(databases.Any(d => d.DbType == "cache" && d.Name.Contains("Redis")), Is.True, "Redis database should be extracted");

        var topics = l4.SemanticNodes.OfType<TopicNode>().ToList();
        Assert.That(topics.Any(t => t.BrokerType == "rabbitmq"), Is.True, "RabbitMQ topic/broker should be extracted");

        var cloud = l4.SemanticNodes.OfType<CloudServiceNode>().ToList();
        Assert.That(cloud.Any(c => c.Name == "Stripe"), Is.True, "Stripe cloud service should be extracted");
        Assert.That(cloud.Any(c => c.Name == "Auth0"), Is.True, "Auth0 cloud service should be extracted");

        var configRels = l4.SemanticRelationships.Where(r => r.Kind == "CONFIGURES").ToList();
        Assert.That(configRels.Count, Is.GreaterThanOrEqualTo(4), "CONFIGURES edges should link appsettings.json to configured services");
    }

    [Test]
    public async Task Test_DockerCompose_ExtractsInfrastructureServices()
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
        Assert.That(databases.Any(d => d.DbType == "relational" && d.Name == "postgres"), Is.True, "Postgres service from docker-compose should be extracted");
        Assert.That(databases.Any(d => d.DbType == "cache" && d.Name == "redis"), Is.True, "Redis service from docker-compose should be extracted");

        var topics = l4.SemanticNodes.OfType<TopicNode>().ToList();
        Assert.That(topics.Any(t => t.BrokerType == "rabbitmq" && t.Name == "rabbitmq"), Is.True, "RabbitMQ from docker-compose should be extracted");
        Assert.That(topics.Any(t => t.BrokerType == "kafka" && t.Name == "kafka"), Is.True, "Kafka from docker-compose should be extracted");
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
        Assert.That(databases.Any(d => d.DbType == "relational"), Is.True, "Postgres DATABASE_URL in .env should be extracted");
        Assert.That(databases.Any(d => d.DbType == "cache"), Is.True, "REDIS_URL in .env should be extracted");

        var cloud = l4.SemanticNodes.OfType<CloudServiceNode>().ToList();
        Assert.That(cloud.Any(c => c.Name == "Stripe"), Is.True, "Stripe in .env should be extracted");
    }
}
