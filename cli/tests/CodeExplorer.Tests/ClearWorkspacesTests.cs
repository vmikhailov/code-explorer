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
        success.Should().BeTrue();

        // Check workspace 1 is gone
        var checkWs1 = await _client.ClearWorkspaceAsync("1");
        checkWs1.Should().BeFalse("Workspace 1 should already be deleted.");

        // Check workspace 2 is still present
        var checkWs2 = await _client.ClearWorkspaceAsync("2");
        checkWs2.Should().BeTrue("Workspace 2 should still exist.");
    }

    [Test]
    public async Task ClearWorkspaceByPath_DeletesTargetWorkspace()
    {
        var success = await _repository.ClearWorkspaceAsync("/workspaces/proj-b");
        success.Should().BeTrue();

        // Check workspace 2 is gone
        var checkWs2 = await _client.ClearWorkspaceAsync("2");
        checkWs2.Should().BeFalse("Workspace 2 should already be deleted.");

        // Check workspace 1 is still present
        var checkWs1 = await _client.ClearWorkspaceAsync("1");
        checkWs1.Should().BeTrue("Workspace 1 should still exist.");
    }

    [Test]
    public async Task ClearWorkspace_NotFound_ReturnsFalse()
    {
        var success = await _repository.ClearWorkspaceAsync("nonexistent");
        success.Should().BeFalse();
    }

    [Test]
    public async Task ClearAll_DeletesAllWorkspacesAndNodes()
    {
        await _repository.ClearAllAsync();

        var checkWs1 = await _client.ClearWorkspaceAsync("1");
        var checkWs2 = await _client.ClearWorkspaceAsync("2");
        checkWs1.Should().BeFalse();
        checkWs2.Should().BeFalse();
    }

    [Test]
    public async Task ClearWorkspaces_WithMultipleWorkspaces_ClearsSpecified()
    {
        var (cleared, notFound) = await _repository.ClearWorkspacesAsync(["1", "nonexistent"]);
        cleared.Should().Contain("1");
        notFound.Should().Contain("nonexistent");

        var checkWs1 = await _client.ClearWorkspaceAsync("1");
        checkWs1.Should().BeFalse("Workspace 1 should be deleted.");

        var checkWs2 = await _client.ClearWorkspaceAsync("2");
        checkWs2.Should().BeTrue("Workspace 2 should remain.");
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
        cleared.Should().BeTrue();

        var (nodeCount, relCount) = await client.GetGraphCountsAsync();
        // ws, service-b project, service-b file = 3 nodes remain
        nodeCount.Should().Be(3);
        relCount.Should().Be(2);
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

        cleared.Should().BeTrue();
        sw.ElapsedMilliseconds.Should().BeLessThan(2000, $"Clear took {sw.ElapsedMilliseconds} ms, expected under 2000 ms.");

        var (nodeCount, relCount) = await client.GetGraphCountsAsync();
        nodeCount.Should().Be(0);
        relCount.Should().Be(0);
    }

    [Test]
    public async Task ClearWorkspace_Subpath_DeletesAssociatedSymbolsAndCalls()
    {
        await using var client = new SqliteGraphClient($"Data Source={Guid.NewGuid():N};Mode=Memory;Cache=Shared");
        await client.SaveEmptyWorkspaceNodeAsync("ws", "/workspace");

        var nodes = new List<Node>
        {
            new("ws:p:src/service-a:", "Project", new Dictionary<string, object> { ["path"] = "/workspace/src/service-a", ["name"] = "service-a" }),
            new("ws:f:src/service-a/file.cs", "File", new Dictionary<string, object> { ["path"] = "/workspace/src/service-a/file.cs", ["name"] = "file.cs" }),
            new("ws:sym:src/service-a/file.cs:Type:OrderService", "Type", new Dictionary<string, object> { ["name"] = "OrderService" }),
            new("ws:sym:src/service-a/file.cs:Function:Process", "Function", new Dictionary<string, object> { ["name"] = "Process" }),
            new("ws:lit:src/service-a/file.cs:10", "CallLiteral", new Dictionary<string, object> { ["name"] = "log" }),
            // Another service that must remain
            new("ws:p:src/service-b:", "Project", new Dictionary<string, object> { ["path"] = "/workspace/src/service-b", ["name"] = "service-b" }),
            new("ws:f:src/service-b/file.cs", "File", new Dictionary<string, object> { ["path"] = "/workspace/src/service-b/file.cs", ["name"] = "file.cs" }),
            new("ws:sym:src/service-b/file.cs:Type:UserService", "Type", new Dictionary<string, object> { ["name"] = "UserService" })
        };
        await client.UploadNodesAsync(nodes);

        var emptyProps = new Dictionary<string, object>();
        var rels = new List<Relationship>
        {
            new("ws", "ws:p:src/service-a:", "CONTAINS", emptyProps),
            new("ws:p:src/service-a:", "ws:f:src/service-a/file.cs", "CONTAINS", emptyProps),
            new("ws:sym:src/service-a/file.cs:Type:OrderService", "ws:sym:src/service-a/file.cs:Function:Process", "HAS_METHOD", emptyProps),
            new("ws", "ws:p:src/service-b:", "CONTAINS", emptyProps),
            new("ws:p:src/service-b:", "ws:f:src/service-b/file.cs", "CONTAINS", emptyProps)
        };
        await client.UploadRelationshipsAsync(rels);

        var cleared = await client.ClearWorkspaceAsync("/workspace/src/service-a");
        cleared.Should().BeTrue();

        var (nodeCount, relCount) = await client.GetGraphCountsAsync();
        // ws, service-b project, service-b file, service-b type = 4 nodes remain
        nodeCount.Should().Be(4);
        relCount.Should().Be(2);

        var remainingNodes = await client.ExecuteQueryAsync("MATCH (n) RETURN n.id AS id ORDER BY id");
        remainingNodes.Should().NotContain("service-a");
        remainingNodes.Should().NotContain("OrderService");
        remainingNodes.Should().NotContain("Process");
    }
}
