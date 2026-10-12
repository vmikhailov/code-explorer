using CodeExplorer.Core.Database;
using CodeExplorer.Core.Mcp;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
public class ClearWorkspacesTests
{
    private SqliteGraphClient _client = null!;
    private CodeExplorerRepository _repository = null!;

    [SetUp]
    public async Task SetUp()
    {
        _client = new SqliteGraphClient(":memory:");
        _repository = new CodeExplorerRepository(_client);

        // Seed 2 workspaces with nodes and relationships
        await _client.SaveEmptyWorkspaceNodeAsync("1", "/workspaces/proj-a");
        await _client.SaveEmptyWorkspaceNodeAsync("2", "/workspaces/proj-b");

        var nodes = new List<Node>
        {
            new("1:proj:a", "Project", new Dictionary<string, object> { ["name"] = "a" }),
            new("1:file:a.cs", "File", new Dictionary<string, object> { ["name"] = "a.cs" }),
            new("2:proj:b", "Project", new Dictionary<string, object> { ["name"] = "b" }),
            new("2:file:b.cs", "File", new Dictionary<string, object> { ["name"] = "b.cs" })
        };
        await _client.UploadNodesAsync(nodes);

        var emptyProps = new Dictionary<string, object>();
        var rels = new List<Relationship>
        {
            new("1", "1:proj:a", "CONTAINS", emptyProps),
            new("1:proj:a", "1:file:a.cs", "CONTAINS", emptyProps),
            new("2", "2:proj:b", "CONTAINS", emptyProps),
            new("2:proj:b", "2:file:b.cs", "CONTAINS", emptyProps)
        };
        await _client.UploadRelationshipsAsync(rels);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _client.DisposeAsync();
    }

    [Test]
    public async Task ClearWorkspaceById_DeletesOnlyTargetWorkspace()
    {
        var success = await _repository.ClearWorkspaceAsync("1");
        Assert.That(success, Is.True);

        // Check workspace 1 is gone
        var checkWs1 = await _client.ClearWorkspaceAsync("1");
        Assert.That(checkWs1, Is.False, "Workspace 1 should already be deleted.");

        // Check workspace 2 is still present
        var checkWs2 = await _client.ClearWorkspaceAsync("2");
        Assert.That(checkWs2, Is.True, "Workspace 2 should still exist.");
    }

    [Test]
    public async Task ClearWorkspaceByPath_DeletesTargetWorkspace()
    {
        var success = await _repository.ClearWorkspaceAsync("/workspaces/proj-b");
        Assert.That(success, Is.True);

        // Check workspace 2 is gone
        var checkWs2 = await _client.ClearWorkspaceAsync("2");
        Assert.That(checkWs2, Is.False, "Workspace 2 should already be deleted.");

        // Check workspace 1 is still present
        var checkWs1 = await _client.ClearWorkspaceAsync("1");
        Assert.That(checkWs1, Is.True, "Workspace 1 should still exist.");
    }

    [Test]
    public async Task ClearWorkspace_NotFound_ReturnsFalse()
    {
        var success = await _repository.ClearWorkspaceAsync("nonexistent");
        Assert.That(success, Is.False);
    }

    [Test]
    public async Task ClearAll_DeletesAllWorkspacesAndNodes()
    {
        await _repository.ClearAllAsync();

        var checkWs1 = await _client.ClearWorkspaceAsync("1");
        var checkWs2 = await _client.ClearWorkspaceAsync("2");
        Assert.That(checkWs1, Is.False);
        Assert.That(checkWs2, Is.False);
    }

    [Test]
    public async Task ClearWorkspaces_WithMultipleWorkspaces_ClearsSpecified()
    {
        var (cleared, notFound) = await _repository.ClearWorkspacesAsync(["1", "nonexistent"]);
        Assert.That(cleared, Contains.Item("1"));
        Assert.That(notFound, Contains.Item("nonexistent"));

        var checkWs1 = await _client.ClearWorkspaceAsync("1");
        Assert.That(checkWs1, Is.False, "Workspace 1 should be deleted.");

        var checkWs2 = await _client.ClearWorkspaceAsync("2");
        Assert.That(checkWs2, Is.True, "Workspace 2 should remain.");
    }

    [Test]
    public async Task ClearWorkspace_Subpath_DeletesOnlySubpathHierarchy()
    {
        await using var client = new SqliteGraphClient($"Data Source={Guid.NewGuid():N};Mode=Memory;Cache=Shared");
        await client.SaveEmptyWorkspaceNodeAsync("ws", "/workspace");

        var nodes = new List<Node>
        {
            new("ws:p:service-a", "Project", new Dictionary<string, object> { ["path"] = "/workspace/src/service-a", ["name"] = "service-a" }),
            new("ws:f:service-a/file.cs", "File", new Dictionary<string, object> { ["path"] = "/workspace/src/service-a/file.cs", ["name"] = "file.cs" }),
            new("ws:p:service-b", "Project", new Dictionary<string, object> { ["path"] = "/workspace/src/service-b", ["name"] = "service-b" }),
            new("ws:f:service-b/file.cs", "File", new Dictionary<string, object> { ["path"] = "/workspace/src/service-b/file.cs", ["name"] = "file.cs" })
        };
        await client.UploadNodesAsync(nodes);

        var emptyProps = new Dictionary<string, object>();
        var rels = new List<Relationship>
        {
            new("ws", "ws:p:service-a", "CONTAINS", emptyProps),
            new("ws:p:service-a", "ws:f:service-a/file.cs", "CONTAINS", emptyProps),
            new("ws", "ws:p:service-b", "CONTAINS", emptyProps),
            new("ws:p:service-b", "ws:f:service-b/file.cs", "CONTAINS", emptyProps)
        };
        await client.UploadRelationshipsAsync(rels);

        var cleared = await client.ClearWorkspaceAsync("/workspace/src/service-a");
        Assert.That(cleared, Is.True);

        var (nodeCount, relCount) = await client.GetGraphCountsAsync();
        // ws, service-b project, service-b file = 3 nodes remain
        Assert.That(nodeCount, Is.EqualTo(3));
        Assert.That(relCount, Is.EqualTo(2));
    }

    [Test]
    public async Task ClearWorkspace_SingleWorkspace_FastWipe()
    {
        await using var client = new SqliteGraphClient($"Data Source={Guid.NewGuid():N};Mode=Memory;Cache=Shared");
        await client.SaveEmptyWorkspaceNodeAsync("ws", "/big-workspace");

        var nodes = new List<Node>();
        var rels = new List<Relationship>();
        var emptyProps = new Dictionary<string, object>();

        for (var i = 0; i < 2000; i++)
        {
            var pId = $"ws:p:{i}";
            var fId = $"ws:f:{i}";
            nodes.Add(new(pId, "Project", new Dictionary<string, object> { ["path"] = $"/big-workspace/proj{i}" }));
            nodes.Add(new(fId, "File", new Dictionary<string, object> { ["path"] = $"/big-workspace/proj{i}/file.cs" }));
            rels.Add(new("ws", pId, "CONTAINS", emptyProps));
            rels.Add(new(pId, fId, "CONTAINS", emptyProps));
        }

        await client.UploadNodesAsync(nodes);
        await client.UploadRelationshipsAsync(rels);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var cleared = await client.ClearWorkspaceAsync("/big-workspace");
        sw.Stop();

        Assert.That(cleared, Is.True);
        Assert.That(sw.ElapsedMilliseconds, Is.LessThan(2000), $"Clear took {sw.ElapsedMilliseconds} ms, expected under 2000 ms.");

        var (nodeCount, relCount) = await client.GetGraphCountsAsync();
        Assert.That(nodeCount, Is.EqualTo(0));
        Assert.That(relCount, Is.EqualTo(0));
    }
}
