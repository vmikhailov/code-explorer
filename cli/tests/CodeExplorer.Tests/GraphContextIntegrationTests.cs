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
        Assert.That(allServices, Has.Count.EqualTo(3));

        var csharpServices = await _client.Graph.Services
            .Where(s => s.Language == "csharp")
            .ToListAsync();

        Assert.That(csharpServices, Has.Count.EqualTo(2));
        Assert.That(csharpServices.Select(s => s.Name), Is.EquivalentTo(new[] { "billing", "inventory" }));

        var count = await _client.Graph.Services
            .Where(s => s.Framework == "aspnetcore")
            .CountAsync();

        Assert.That(count, Is.EqualTo(2));
    }

    [Test]
    public async Task GraphContext_Services_AnyAsync()
    {
        var hasBff = await _client.Graph.Services
            .Where(s => s.Name == "bff")
            .AnyAsync();

        Assert.That(hasBff, Is.True);

        var hasGhost = await _client.Graph.Services
            .Where(s => s.Name == "ghost-service")
            .AnyAsync();

        Assert.That(hasGhost, Is.False);
    }

    [Test]
    public async Task GraphContext_QueryAllEntityTypes()
    {
        var apps = await _client.Graph.Apps.ToListAsync();
        Assert.That(apps, Has.Count.EqualTo(1));
        Assert.That(apps[0].Name, Is.EqualTo("web-portal"));

        var workers = await _client.Graph.Workers.ToListAsync();
        Assert.That(workers, Has.Count.EqualTo(1));
        Assert.That(workers[0].Name, Is.EqualTo("job-runner"));

        var cliTools = await _client.Graph.CliTools.ToListAsync();
        Assert.That(cliTools, Has.Count.EqualTo(1));
        Assert.That(cliTools[0].Name, Is.EqualTo("ce"));

        var databases = await _client.Graph.Databases.ToListAsync();
        Assert.That(databases, Has.Count.EqualTo(1));
        Assert.That(databases[0].Name, Is.EqualTo("main_postgres"));

        var topics = await _client.Graph.Topics.ToListAsync();
        Assert.That(topics, Has.Count.EqualTo(1));
        Assert.That(topics[0].Name, Is.EqualTo("events-topic"));

        var externals = await _client.Graph.ExternalServices.ToListAsync();
        Assert.That(externals, Has.Count.EqualTo(1));
        Assert.That(externals[0].Name, Is.EqualTo("stripe"));

        var libraries = await _client.Graph.Libraries.ToListAsync();
        Assert.That(libraries, Has.Count.EqualTo(1));
        Assert.That(libraries[0].Name, Is.EqualTo("ui-kit"));

        var endpoints = await _client.Graph.Endpoints.ToListAsync();
        Assert.That(endpoints, Has.Count.EqualTo(1));
        Assert.That(endpoints[0].Name, Is.EqualTo("CreateOrder"));
    }

    [Test]
    public async Task GraphContext_PagingAndSorting()
    {
        var paged = await _client.Graph.Services
            .OrderBy(s => s.Name)
            .Skip(1)
            .Take(1)
            .ToListAsync();

        Assert.That(paged, Has.Count.EqualTo(1));
        Assert.That(paged[0].Name, Is.EqualTo("billing"));
    }

    [Test]
    public async Task GraphContext_ExcludeLibraries()
    {
        var nonLibs = await _client.Graph.Services
            .Where(s => !s.IsLibrary)
            .ToListAsync();

        Assert.That(nonLibs, Has.Count.EqualTo(3));
    }
}
