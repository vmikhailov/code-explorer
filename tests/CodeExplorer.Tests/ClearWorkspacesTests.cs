using CodeExplorer.Core.Database;
using CodeExplorer.Core.Mcp;
using CodeExplorer.Core.Parser;
using CodeExplorer.Core.Web.Controllers;
using Microsoft.AspNetCore.Mvc;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
public class ClearWorkspacesTests
{
    private SqliteGraphClient _client = null!;
    private CodeExplorerRepository _repository = null!;
    private WorkspacesController _controller = null!;

    [SetUp]
    public async Task SetUp()
    {
        _client = new SqliteGraphClient(":memory:");
        var indexer = new WorkspaceIndexer(_client);
        var taskManager = new IndexingTaskManager(indexer);
        _repository = new CodeExplorerRepository(_client);
        _controller = new WorkspacesController(_repository, taskManager);

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
        var result = await _controller.DeleteWorkspaceAsync("1");
        Assert.That(result, Is.InstanceOf<OkObjectResult>());

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
        var result = await _controller.DeleteWorkspaceAsync("/workspaces/proj-b");
        Assert.That(result, Is.InstanceOf<OkObjectResult>());

        // Check workspace 2 is gone
        var checkWs2 = await _client.ClearWorkspaceAsync("2");
        Assert.That(checkWs2, Is.False, "Workspace 2 should already be deleted.");

        // Check workspace 1 is still present
        var checkWs1 = await _client.ClearWorkspaceAsync("1");
        Assert.That(checkWs1, Is.True, "Workspace 1 should still exist.");
    }

    [Test]
    public async Task ClearWorkspace_NotFound_Returns404()
    {
        var result = await _controller.DeleteWorkspaceAsync("nonexistent");
        Assert.That(result, Is.InstanceOf<NotFoundObjectResult>());
    }

    [Test]
    public async Task ClearAll_DeletesAllWorkspacesAndNodes()
    {
        var result = await _controller.ClearAllAsync();
        Assert.That(result, Is.InstanceOf<OkObjectResult>());

        var checkWs1 = await _client.ClearWorkspaceAsync("1");
        var checkWs2 = await _client.ClearWorkspaceAsync("2");
        Assert.That(checkWs1, Is.False);
        Assert.That(checkWs2, Is.False);
    }

    [Test]
    public async Task PostClear_WithMultipleWorkspaces_ClearsSpecified()
    {
        var request = new ClearWorkspacesRequest
        {
            Workspaces = ["1", "nonexistent"]
        };

        var result = await _controller.ClearAsync(request, all: null);
        Assert.That(result, Is.InstanceOf<OkObjectResult>());

        var checkWs1 = await _client.ClearWorkspaceAsync("1");
        Assert.That(checkWs1, Is.False, "Workspace 1 should be deleted.");

        var checkWs2 = await _client.ClearWorkspaceAsync("2");
        Assert.That(checkWs2, Is.True, "Workspace 2 should remain.");
    }

    [Test]
    public async Task PostClear_WithAllFlag_ClearsEverything()
    {
        var request = new ClearWorkspacesRequest { All = true };
        var result = await _controller.ClearAsync(request, all: null);
        Assert.That(result, Is.InstanceOf<OkObjectResult>());

        var checkWs1 = await _client.ClearWorkspaceAsync("1");
        var checkWs2 = await _client.ClearWorkspaceAsync("2");
        Assert.That(checkWs1, Is.False);
        Assert.That(checkWs2, Is.False);
    }
}
