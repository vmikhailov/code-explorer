using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
public class GraphContextIntegrationTests
{
    private SqliteGraphClient _client = null!;

    [OneTimeSetUp]
    public async Task SetUp()
    {
        _client = new SqliteGraphClient($"Data Source=GraphCtx_{Guid.NewGuid():N};Mode=Memory;Cache=Shared");

        var nodes = new List<Node>
        {
            Node.FromNode(new ServiceNode("ws:s:bff", "bff", "services/bff", "typescript")
            {
                CustomProperties = new() { ["framework"] = "nestjs", ["role"] = "Service", ["is_library"] = "false" }
            }),
            Node.FromNode(new ServiceNode("ws:s:billing", "billing", "services/billing", "csharp")
            {
                CustomProperties = new() { ["framework"] = "aspnetcore", ["role"] = "Service", ["is_library"] = "false" }
            }),
            Node.FromNode(new ServiceNode("ws:s:inventory", "inventory", "services/inventory", "csharp")
            {
                CustomProperties = new() { ["framework"] = "aspnetcore", ["role"] = "Service", ["is_library"] = "false" }
            }),
            Node.FromNode(new AppNode("ws:app:portal", "web-portal", "apps/portal", "react")
            {
                CustomProperties = new() { ["app_type"] = "spa" }
            }),
            Node.FromNode(new WorkerNode("ws:w:jobs", "job-runner", "workers/jobs", "dotnet")
            {
                CustomProperties = new() { ["queue_type"] = "rabbitmq" }
            }),
            Node.FromNode(new CliToolNode("ws:cli:ce", "ce", "src/cli", "csharp")
            {
                CustomProperties = new() { ["command_name"] = "ce" }
            }),
            Node.FromNode(new DatabaseNode("ws:db:main", "main_postgres", "databases/main", "relational")
            {
                CustomProperties = new() { ["engine"] = "postgresql" }
            }),
            Node.FromNode(new TopicNode("ws:top:events", "events-topic", "kafka/events", "kafka")),
            Node.FromNode(new ExternalServiceNode("ws:ext:stripe", "stripe", "https", "stripe.com", "")
            {
                CustomProperties = new() { ["category"] = "Payment" }
            }),
            Node.FromNode(new LibraryNode("ws:lib:ui", "ui-kit", "libs/ui-kit", "typescript")
            {
                CustomProperties = new() { ["is_library"] = "true" }
            }),
            Node.FromNode(new EndpointNode("ws:ep:orders", "CreateOrder", "controllers/Orders.cs", "POST", "/orders"))
        };

        await _client.UploadNodesAsync(nodes);
    }

    [OneTimeTearDown]
    public async Task TearDown()
    {
        await _client.DisposeAsync();
    }

    [Test]
    public async Task GraphContext_Services_FilterAndCount()
    {
        var allServices = await _client.Graph.Services.ToListAsync();
        allServices.Should().HaveCount(3);

        var csharpServices = await _client.Graph.Services
            .Where(s => s.Language == "csharp")
            .ToListAsync();

        csharpServices.Should().HaveCount(2);
        csharpServices.Select(s => s.Name).Should().BeEquivalentTo(new[] { "billing", "inventory" });

        var count = await _client.Graph.Services
            .Where(s => s.Framework == "aspnetcore")
            .CountAsync();

        count.Should().Be(2);
    }

    [Test]
    public async Task GraphContext_Services_AnyAsync()
    {
        var hasBff = await _client.Graph.Services
            .Where(s => s.Name == "bff")
            .AnyAsync();

        hasBff.Should().BeTrue();

        var hasGhost = await _client.Graph.Services
            .Where(s => s.Name == "ghost-service")
            .AnyAsync();

        hasGhost.Should().BeFalse();
    }

    [Test]
    public async Task GraphContext_QueryAllEntityTypes()
    {
        var apps = await _client.Graph.Apps.ToListAsync();
        apps.Should().HaveCount(1);
        apps[0].Name.Should().Be("web-portal");

        var workers = await _client.Graph.Workers.ToListAsync();
        workers.Should().HaveCount(1);
        workers[0].Name.Should().Be("job-runner");

        var cliTools = await _client.Graph.CliTools.ToListAsync();
        cliTools.Should().HaveCount(1);
        cliTools[0].Name.Should().Be("ce");

        var databases = await _client.Graph.Databases.ToListAsync();
        databases.Should().HaveCount(1);
        databases[0].Name.Should().Be("main_postgres");

        var topics = await _client.Graph.Topics.ToListAsync();
        topics.Should().HaveCount(1);
        topics[0].Name.Should().Be("events-topic");

        var externals = await _client.Graph.ExternalServices.ToListAsync();
        externals.Should().HaveCount(1);
        externals[0].Name.Should().Be("stripe");

        var libraries = await _client.Graph.Libraries.ToListAsync();
        libraries.Should().HaveCount(1);
        libraries[0].Name.Should().Be("ui-kit");

        var endpoints = await _client.Graph.Endpoints.ToListAsync();
        endpoints.Should().HaveCount(1);
        endpoints[0].Name.Should().Be("CreateOrder");
    }

    [Test]
    public async Task GraphContext_PagingAndSorting()
    {
        var paged = await _client.Graph.Services
            .OrderBy(s => s.Name)
            .Skip(1)
            .Take(1)
            .ToListAsync();

        paged.Should().HaveCount(1);
        paged[0].Name.Should().Be("billing");
    }

    [Test]
    public async Task GraphContext_ExcludeLibraries()
    {
        var nonLibs = await _client.Graph.Services
            .Where(s => !s.IsLibrary)
            .ToListAsync();

        nonLibs.Should().HaveCount(3);
    }

    [Test]
    public async Task SchemaVersion_TracksCurrentVersion_AndUpgradesOnOpen()
    {
        _client.SchemaVersion.Should().Be(SqliteGraphClient.CurrentSchemaVersion);
        _client.IsSchemaOutdated.Should().BeFalse();
        _client.TryRunSchemaMigrator(SqliteGraphClient.CurrentSchemaVersion, 999).Should().BeFalse();

        var tempDb = Path.Combine(Path.GetTempPath(), $"ce_migration_test_{Guid.NewGuid():N}.db");
        try
        {
            const int previousVersion = SqliteGraphClient.CurrentSchemaVersion - 1;
            await using (var seedClient = new SqliteGraphClient(tempDb))
            {
                await seedClient.SetSchemaVersionAsync(previousVersion);
            }

            await using var upgradedClient = new SqliteGraphClient(tempDb);
            upgradedClient.WasMigratedOnOpen.Should().BeTrue();
            upgradedClient.MigratedFromVersion.Should().Be(previousVersion);
            upgradedClient.SchemaVersion.Should().Be(SqliteGraphClient.CurrentSchemaVersion);
            upgradedClient.IsSchemaOutdated.Should().BeFalse();
        }
        finally
        {
            if (File.Exists(tempDb)) File.Delete(tempDb);
        }
    }
}
