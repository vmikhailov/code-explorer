namespace CodeExplorer.Core.Database;

/// <summary>
/// Lightweight in-memory implementation of <see cref="IGraphClient"/> for running the Layer 1–5 ontology pipeline
/// without persistent SQLite storage overhead (used by CodePassport and unit tests).
/// </summary>
public class InMemoryGraphClient : IGraphClient
{
    public List<Node> UploadedNodes { get; } = [];
    public List<Relationship> UploadedRelationships { get; } = [];
    public int WorkspaceCounter { get; set; } = 1;

    public Task CreateIndicesAsync() => Task.CompletedTask;

    public Task ClearDatabaseAsync()
    {
        lock (UploadedNodes)
        {
            UploadedNodes.Clear();
            UploadedRelationships.Clear();
        }
        return Task.CompletedTask;
    }

    public Task<bool> ClearWorkspaceAsync(string workspacePath) => Task.FromResult(true);

    public Task<string> GetOrCreateWorkspaceIdAsync(string workspacePath) =>
        Task.FromResult(WorkspaceCounter.ToString());

    public Task SaveEmptyWorkspaceNodeAsync(string id, string path) => Task.CompletedTask;

    public Task UploadNodesAsync(List<Node> nodes)
    {
        lock (UploadedNodes)
        {
            UploadedNodes.AddRange(nodes);
        }
        return Task.CompletedTask;
    }

    public Task UploadRelationshipsAsync(List<Relationship> rels)
    {
        lock (UploadedRelationships)
        {
            UploadedRelationships.AddRange(rels);
        }
        return Task.CompletedTask;
    }

    public Task<string> ExecuteQueryAsync(string query, object? parameters = null, CancellationToken cancellationToken = default) =>
        Task.FromResult("[]");

    public Task ExecuteWriteAsync(string query, object? parameters = null, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
