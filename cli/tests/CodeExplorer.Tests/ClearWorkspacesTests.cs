using CodeExplorer.Core.Database;
using CodeExplorer.Core.Mcp;
using NUnit.Framework;
using AwesomeAssertions;

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
}
