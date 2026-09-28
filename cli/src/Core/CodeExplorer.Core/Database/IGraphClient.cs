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

    Task<List<IntentCandidate>> LoadIntentCandidatesAsync(
        string workspaceId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new List<IntentCandidate>());

    Task SaveIntentPredictionsAsync(
        string workspaceId,
        List<CodeIntentPredictionResult> predictions,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    int SchemaVersion => 0;
    bool IsSchemaOutdated => false;
    Task SetSchemaVersionAsync(int version) => Task.CompletedTask;
}

public record IntentCandidate(string Id, string Kind, string Name, string RelativePath, string? FullPath);

public record CodeIntentPredictionResult(
    string Id,
    string? FilePath,
    string? Domain,
    string? Layer,
    string? Pattern,
    string? OperationType,
    string? CapabilityTag,
    string? IntentSummary,
    bool? IsPureDomain,
    List<string>? TargetEntities,
    List<string>? EmittedEvents
);
