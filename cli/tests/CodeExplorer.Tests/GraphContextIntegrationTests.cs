using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Database;
using CodeExplorer.Cypher.Linq;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
public class GraphContextIntegrationTests
{
    private SqliteGraphClient _client = null!;

    [SetUp]
    public async Task SetUp()
    {
        _client = new SqliteGraphClient(":memory:");
        await _client.CreateIndicesAsync();

        var nodes = new List<Node>
        {
            Node.FromNode(new ServiceNode("ws:s:bff", "bff", "services/bff", "typescript")
            {
                Extensions = new() { ["framework"] = "nestjs", ["role"] = "Service", ["is_library"] = "false" }
            }),
            Node.FromNode(new ServiceNode("ws:s:billing", "billing", "services/billing", "csharp")
            {
                Extensions = new() { ["framework"] = "aspnetcore", ["role"] = "Service", ["is_library"] = "false" }
            }),
            Node.FromNode(new ServiceNode("ws:s:inventory", "inventory", "services/inventory", "csharp")
            {
                Extensions = new() { ["framework"] = "aspnetcore", ["role"] = "Service", ["is_library"] = "false" }
            }),
            Node.FromNode(new AppNode("ws:app:portal", "web-portal", "apps/portal", "react")
            {
                Extensions = new() { ["app_type"] = "spa" }
            }),
            Node.FromNode(new WorkerNode("ws:w:jobs", "job-runner", "workers/jobs", "dotnet")
            {
                Extensions = new() { ["queue_type"] = "rabbitmq" }
            }),
            Node.FromNode(new CliToolNode("ws:cli:ce", "ce", "src/cli", "csharp")
            {
                Extensions = new() { ["command_name"] = "ce" }
            }),
            Node.FromNode(new DatabaseNode("ws:db:main", "main_postgres", "databases/main", "relational")
            {
                Extensions = new() { ["engine"] = "postgresql" }
            }),
            Node.FromNode(new TopicNode("ws:top:events", "events-topic", "kafka/events", "kafka")),
            Node.FromNode(new ExternalServiceNode("ws:ext:stripe", "stripe", "https", "stripe.com", "")
            {
                Extensions = new() { ["category"] = "Payment" }
            }),
            Node.FromNode(new LibraryNode("ws:lib:ui", "ui-kit", "libs/ui-kit", "typescript")
            {
                Extensions = new() { ["is_library"] = "true" }
            }),
            Node.FromNode(new EndpointNode("ws:ep:orders", "CreateOrder", "controllers/Orders.cs", "POST", "/orders"))
        };

        await _client.UploadNodesAsync(nodes);
    }

    [TearDown]
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
    public async Task SchemaVersion_Migrator_UpgradesV3TextToV4Jsonb_AndFlagsOutdatedWhenUnsupported()
    {
        _client.SchemaVersion.Should().Be(SqliteGraphClient.CurrentSchemaVersion);
        _client.IsSchemaOutdated.Should().BeFalse();

        // Simulate legacy v3 TEXT JSON row
        await _client.ExecuteWriteAsync("UPDATE nodes SET properties = json(properties); PRAGMA user_version = 3;");
        var migrated = _client.TryRunSchemaMigrator(3, SqliteGraphClient.CurrentSchemaVersion);
        migrated.Should().BeTrue();

        var typeAfterMigration = await _client.ExecuteRawAsync(async conn =>
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT typeof(properties) FROM nodes LIMIT 1;";
            return (string?)await cmd.ExecuteScalarAsync();
        });
        typeAfterMigration.Should().Be("blob");

        // Simulate unsupported future version -> migrator returns false so full rebuild is triggered
        _client.TryRunSchemaMigrator(3, 999).Should().BeFalse();
    }

    [Test]
    public async Task SchemaVersion_OpenLegacyDb_AutomaticallyUpgrades_AndSetsMigrationFlags()
    {
        var tempDb = Path.Combine(Path.GetTempPath(), $"ce_migration_test_{Guid.NewGuid():N}.db");
        try
        {
            // 1. Create a legacy v3 database
            await using (var seedClient = new SqliteGraphClient(tempDb))
            {
                await seedClient.ExecuteWriteAsync("UPDATE nodes SET properties = json(properties); PRAGMA user_version = 3;");
            }

            // 2. Open with a fresh SqliteGraphClient to trigger CheckSchemaVersion() on open
            await using var upgradedClient = new SqliteGraphClient(tempDb);
            upgradedClient.WasMigratedOnOpen.Should().BeTrue();
            upgradedClient.MigratedFromVersion.Should().Be(3);
            upgradedClient.SchemaVersion.Should().Be(SqliteGraphClient.CurrentSchemaVersion);
            upgradedClient.IsSchemaOutdated.Should().BeFalse();
        }
        finally
        {
            if (File.Exists(tempDb)) File.Delete(tempDb);
        }
    }
}
