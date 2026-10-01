using CodeExplorer.Core.Analysis;
using CodeExplorer.Core.Database;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
public class ArchitectureQueryServiceTests
{
    private string _tempDir = null!;
    private SqliteGraphClient _db = null!;
    private IArchitectureQueryService _service = null!;

    [SetUp]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ce_arch_query_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        var dbPath = Path.Combine(_tempDir, "graph.db").Replace('\\', '/');
        _db = new SqliteGraphClient(dbPath);
        _service = new ArchitectureQueryService(_db);
    }

    [TearDown]
    public void TearDown()
    {
        _db.Dispose();
        if (Directory.Exists(_tempDir))
        {
            try { Directory.Delete(_tempDir, true); } catch { }
        }
    }

    [Test]
    public async Task GetViewAsync_DelegatesToEngine_Successfully()
    {
        await _db.UploadNodesAsync(new List<Node>
        {
            new("proj:test", "Project", new Dictionary<string, object>
            {
                ["name"] = "TestService",
                ["role"] = "Service",
                ["is_library"] = "false"
            })
        });

        var result = await _service.GetViewAsync(new ArchitectureViewRequest
        {
            ViewType = ArchitectureViewType.SystemContext
        });

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Nodes.Any(n => n.Name == "TestService"), Is.True);
    }

    [Test]
    public async Task GetAllProjectsAsync_ReturnsProjectNames()
    {
        await _db.UploadNodesAsync(new List<Node>
        {
            new("p1", "Project", new Dictionary<string, object> { ["name"] = "OrderService" }),
            new("p2", "Project", new Dictionary<string, object> { ["name"] = "BillingService" })
        });

        var projects = await _service.GetAllProjectsAsync();

        Assert.That(projects, Does.Contain("OrderService"));
        Assert.That(projects, Does.Contain("BillingService"));
    }

    [Test]
    public async Task GetMetadataAsync_ReturnsAggregates()
    {
        await _db.UploadNodesAsync(new List<Node>
        {
            new("p1", "Project", new Dictionary<string, object> { ["name"] = "AuthService" }),
            new("db1", "Database", new Dictionary<string, object> { ["name"] = "users_db" })
        });

        await _db.UploadRelationshipsAsync(new List<Relationship>
        {
            new("p1", "db1", "USES_DB", new Dictionary<string, object>())
        });

        var meta = await _service.GetMetadataAsync();

        Assert.That(meta, Is.Not.Null);
        Assert.That(meta.TotalNodes, Is.EqualTo(2));
        Assert.That(meta.TotalEdges, Is.EqualTo(1));
    }

    [Test]
    public async Task GetNodeUsagesAsync_ReturnsAllUsagesOfDatabase()
    {
        await _db.UploadNodesAsync(new List<Node>
        {
            new("p1", "Project", new Dictionary<string, object> { ["name"] = "OrderService" }),
            new("p2", "Project", new Dictionary<string, object> { ["name"] = "BillingService" }),
            new("f1", "File", new Dictionary<string, object> { ["name"] = "OrderRepository.cs", ["path"] = "src/OrderRepository.cs", ["line"] = 42 }),
            new("db1", "Database", new Dictionary<string, object> { ["name"] = "orders_db", ["path"] = "appsettings.json", ["line"] = 10 })
        });

        await _db.UploadRelationshipsAsync(new List<Relationship>
        {
            new("p1", "db1", "USES_DB", new Dictionary<string, object>()),
            new("p2", "db1", "USES_DB", new Dictionary<string, object>()),
            new("f1", "db1", "WRITES_DATA", new Dictionary<string, object>())
        });

        var result = await _service.GetNodeUsagesAsync("db1");

        Assert.That(result, Is.Not.Null);
        Assert.That(result.TargetName, Is.EqualTo("orders_db"));
        Assert.That(result.Usages.Count, Is.GreaterThanOrEqualTo(3));
        Assert.That(result.Usages.Any(u => u.SourceName == "OrderService" && u.Relationship == "USES_DB"), Is.True);
        Assert.That(result.Usages.Any(u => u.SourceName == "BillingService" && u.Relationship == "USES_DB"), Is.True);
        Assert.That(result.Usages.Any(u => u.SourceName == "OrderRepository.cs" && u.FilePath == "src/OrderRepository.cs"), Is.True);
    }
}
