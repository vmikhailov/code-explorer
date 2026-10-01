using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Cypher.Ast;
using CodeExplorer.Cypher.Linq;

namespace CodeExplorer.Core.Database;

public interface IGraphClient : IAsyncDisposable, ICypherQueryExecutor
{
    GraphContext Graph => new(this);

    Task CreateIndicesAsync();
    Task ClearDatabaseAsync();
    Task<bool> ClearWorkspaceAsync(string workspacePath);
    Task<string> GetOrCreateWorkspaceIdAsync(string workspacePath);
    Task SaveEmptyWorkspaceNodeAsync(string id, string path);
    Task UploadNodesAsync(List<Node> nodes);
    Task UploadRelationshipsAsync(List<Relationship> rels);
    Task<string> ExecuteQueryAsync(string query, object? parameters = null, CancellationToken cancellationToken = default);
    Task ExecuteWriteAsync(string query, object? parameters = null, CancellationToken cancellationToken = default);

    Task<string> ICypherQueryExecutor.ExecuteQueryAsync(CypherQuery query, IReadOnlyDictionary<string, object?>? parameters, CancellationToken ct) =>
        ExecuteQueryAsync(query.ToString() ?? "", parameters, ct);

    Task<string> ICypherQueryExecutor.ExecuteRawAsync(string cypher, IReadOnlyDictionary<string, object?>? parameters, CancellationToken ct) =>
        ExecuteQueryAsync(cypher, parameters, ct);

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

    Task<List<ProjectSignature>> LoadProjectSignaturesAsync(
        string workspaceId,
        CancellationToken cancellationToken = default) =>
        LoadProjectSignaturesAsync(workspaceId, workloadsOnly: false, cancellationToken);

    Task<List<ProjectSignature>> LoadProjectSignaturesAsync(
        string workspaceId,
        bool workloadsOnly,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new List<ProjectSignature>());

    Task SaveProjectIntentsAsync(
        string workspaceId,
        Dictionary<string, (string Domain, string Summary, List<string> Capabilities)> projectIntents,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    Task<List<IntentCandidate>> LoadIntentCandidatesAsync(
        string workspaceId,
        int? limit = null,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new List<IntentCandidate>());

    Task SaveIntentPredictionsAsync(
        string workspaceId,
        List<CodeIntentPredictionResult> predictions,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    Task RunIncrementalVacuumAsync(int pages = 500, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    Task<Dictionary<string, FileRegistryEntry>> LoadFileRegistryAsync(
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new Dictionary<string, FileRegistryEntry>(StringComparer.OrdinalIgnoreCase));

    Task SaveFileRegistryEntriesAsync(
        IEnumerable<(string RelativePath, string ContentHash, DateTime LastModifiedUtc, string ProjectPath, string? SnapshotJson)> entries,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    Task SaveFileRegistryEntriesAsync(
        IEnumerable<(string RelativePath, string ContentHash, DateTime LastModifiedUtc, string ProjectPath)> entries,
        CancellationToken cancellationToken = default) =>
        SaveFileRegistryEntriesAsync(entries.Select(e => (e.RelativePath, e.ContentHash, e.LastModifiedUtc, e.ProjectPath, (string?)null)), cancellationToken);

    Task DeleteFileRegistryEntriesAsync(
        IEnumerable<string> relativePaths,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    Task<List<IntentRecord>> LoadExistingIntentsAsync(
        string workspaceId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new List<IntentRecord>());

    Task SaveIntentRecordAsync(
        IntentRecord record,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    Task IncrementIntentErrorAsync(
        string filePath,
        string workspaceId,
        string fileId,
        string contentHash,
        DateTime lastModifiedUtc,
        string error,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    Task<int> ApplyCachedIntentsToGraphAsync(
        string workspaceId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(0);

    Task<List<CrossDomainInteractionRecord>> LoadCrossDomainInteractionsAsync(
        string workspaceId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new List<CrossDomainInteractionRecord>());

    Task<List<DomainInfrastructureLinkRecord>> LoadDomainInfrastructureLinksAsync(
        string workspaceId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new List<DomainInfrastructureLinkRecord>());

    Task ClearIntentsAsync(
        string workspaceId,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    Task PurgeIntentsByPathsAsync(
        List<string> filePaths,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    Task ResetIntentErrorsAsync(
        string workspaceId,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    int SchemaVersion => 0;
    bool IsSchemaOutdated => false;
    Task SetSchemaVersionAsync(int version) => Task.CompletedTask;

    Task<Dictionary<string, int>> GetNodesBreakdownAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new Dictionary<string, int>());

    Task<(int NodesCount, int RelationshipsCount)> GetGraphCountsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult((0, 0));
}

public record ProjectSignature(
    string Id,
    string Name,
    string RelativePath,
    List<string> Endpoints,
    List<string> Tables,
    List<string> Topics,
    List<string> DomainTypes,
    string? ExistingDomain = null,
    string? ExistingRole = null,
    string? Role = null,
    List<string>? ReferencedLibraries = null,
    List<string>? InboundCallers = null,
    List<string>? OutboundCalls = null
);

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

public record IntentRecord(
    string FilePath,
    string WorkspaceId,
    string FileId,
    string ContentHash,
    DateTime LastModifiedUtc,
    string? Domain,
    string? Layer,
    string? Pattern,
    string? OperationType,
    string? CapabilityTag,
    string? IntentSummary,
    List<string>? TargetEntities,
    List<string>? EmittedEvents,
    bool? IsPureDomain,
    int ErrorCount,
    string? LastError,
    DateTime? AnalyzedAtUtc
);

public record CrossDomainInteractionRecord(
    string SourceDomain,
    string TargetDomain,
    string EdgeKind,
    int InteractionCount
);

public record DomainInfrastructureLinkRecord(
    string Domain,
    string InfraId,
    string InfraName,
    string InfraKind,
    string EdgeKind
);

public record FileRegistryEntry(
    string ContentHash,
    DateTime LastModifiedUtc,
    string ProjectPath,
    string? SnapshotJson = null
);

