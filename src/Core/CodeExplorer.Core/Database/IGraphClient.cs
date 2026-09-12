namespace CodeExplorer.Core.Database;

public interface IGraphClient : IAsyncDisposable
{
    Task CreateIndicesAsync();
    Task ClearDatabaseAsync();
    Task<bool> ClearWorkspaceAsync(string workspacePath);
    Task<string> GetOrCreateWorkspaceIdAsync(string workspacePath);
    Task SaveEmptyWorkspaceNodeAsync(string id, string path);
    Task UploadNodesAsync(List<Node> nodes);
    Task UploadRelationshipsAsync(List<Relationship> rels);
    Task<string> ExecuteQueryAsync(string query, object? parameters = null);
    Task ExecuteWriteAsync(string query, object? parameters = null);
}
