using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;

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
    Task<string> ExecuteQueryAsync(string query, object? parameters = null, CancellationToken cancellationToken = default);
    Task ExecuteWriteAsync(string query, object? parameters = null, CancellationToken cancellationToken = default);

    Task<Dictionary<(string Kind, string Name), string>> LoadSymbolsByNamesAsync(
        IEnumerable<string> names,
        string workspaceId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new Dictionary<(string Kind, string Name), string>());

    Task<Dictionary<string, List<string>>> LoadImplementationsForTypesAsync(
        IEnumerable<string> typeNames,
        string workspaceId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new Dictionary<string, List<string>>());

    Task<(List<EndpointNode> Endpoints, List<EntryPointNode> EntryPoints, List<ExternalServiceNode> ExternalServices)> LoadLateBindingCandidatesAsync(
        string workspaceId,
        bool needEndpoints,
        bool needExternalServices,
        CancellationToken cancellationToken = default) =>
        Task.FromResult((
            new List<EndpointNode>(),
            new List<EntryPointNode>(),
            new List<ExternalServiceNode>()
        ));
}
