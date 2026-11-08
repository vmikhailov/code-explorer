using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Cypher.Ast;
using CodeExplorer.Cypher.Common;

namespace CodeExplorer.Core.Database;

public interface IGraphClient : IAsyncDisposable, ICypherQueryExecutor
{
    GraphContext Graph => new(this);
    IGraphRepository AsRepository() => this is IGraphRepository repo ? repo : new PersistedGraphRepository(this);

    Task BeginBulkLoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    Task CommitBulkLoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    Task CreateIndicesAsync();
    Task ClearDatabaseAsync();
    Task<bool> ClearWorkspaceAsync(string workspacePath);
    Task<string> GetOrCreateWorkspaceIdAsync(string workspacePath);
    Task SaveEmptyWorkspaceNodeAsync(string id, string path);
    Task UploadNodesAsync(List<Node> nodes);
    Task UploadRelationshipsAsync(List<Relationship> rels);
    Task ExecuteWriteAsync(string query, object? parameters = null, CancellationToken cancellationToken = default);
    Task<CypherQueryResult> ExecuteCypherAsync(string query, object? parameters = null, CancellationToken cancellationToken = default);

    async Task<List<T>> QueryAsync<T>(string query, object? parameters = null, CancellationToken cancellationToken = default)
    {
        var res = await ExecuteCypherAsync(query, parameters, cancellationToken);
        return DbRowMaterializer.MaterializeList<T>(res);
    }

    async Task<T?> QueryFirstOrDefaultAsync<T>(string query, object? parameters = null, CancellationToken cancellationToken = default)
    {
        var res = await ExecuteCypherAsync(query, parameters, cancellationToken);
        return res.Rows.Count > 0 ? DbRowMaterializer.MaterializeRow<T>(res.Rows[0]) : default;
    }

    async Task<T?> QueryScalarAsync<T>(string query, object? parameters = null, CancellationToken cancellationToken = default)
    {
        var res = await ExecuteCypherAsync(query, parameters, cancellationToken);
        if (res.Rows.Count == 0 || res.Rows[0].FieldCount == 0) return default;
        return (T?)DbRowMaterializer.ConvertCypherValue(res.Rows[0][0], typeof(T));
    }


    Task<int> ICypherQueryExecutor.ExecuteCountAsync(CypherQuery query, IReadOnlyDictionary<string, object?>? parameters, CancellationToken ct) =>
        throw new NotSupportedException();

    Task<bool> ICypherQueryExecutor.ExecuteAnyAsync(CypherQuery query, IReadOnlyDictionary<string, object?>? parameters, CancellationToken ct) =>
        throw new NotSupportedException();

    Task<List<TElement>> ICypherQueryExecutor.ExecuteListAsync<TElement>(CypherQuery query, IReadOnlyDictionary<string, object?>? parameters, CancellationToken ct) =>
        throw new NotSupportedException();

    Task<TElement?> ICypherQueryExecutor.ExecuteFirstOrDefaultAsync<TElement>(CypherQuery query, IReadOnlyDictionary<string, object?>? parameters, CancellationToken ct) where TElement : default =>
        throw new NotSupportedException();

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

    Task<(List<EndpointNode> Endpoints, List<EntryPointNode> EntryPoints, List<ExternalServiceNode> ExternalServices, List<ProjectNode> Projects)> LoadLateBindingCandidatesAsync(
        string workspaceId,
        bool needEndpoints,
        bool needExternalServices,
        CancellationToken cancellationToken = default) =>
        Task.FromResult((
            new List<EndpointNode>(),
            new List<EntryPointNode>(),
            new List<ExternalServiceNode>(),
            new List<ProjectNode>()
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
        IEnumerable<(string RelativePath, string ContentHash, DateTime LastModifiedUtc, long SizeBytes, string ProjectPath, string? SnapshotJson)> entries,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    Task SaveFileRegistryEntriesAsync(
        IEnumerable<(string RelativePath, string ContentHash, DateTime LastModifiedUtc, string ProjectPath, string? SnapshotJson)> entries,
        CancellationToken cancellationToken = default) =>
        SaveFileRegistryEntriesAsync(entries.Select(e => (e.RelativePath, e.ContentHash, e.LastModifiedUtc, -1L, e.ProjectPath, e.SnapshotJson)), cancellationToken);

    Task SaveFileRegistryEntriesAsync(
        IEnumerable<(string RelativePath, string ContentHash, DateTime LastModifiedUtc, string ProjectPath)> entries,
        CancellationToken cancellationToken = default) =>
        SaveFileRegistryEntriesAsync(entries.Select(e => (e.RelativePath, e.ContentHash, e.LastModifiedUtc, -1L, e.ProjectPath, (string?)null)), cancellationToken);

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

    const int CurrentSchemaVersion = 6;
    int SchemaVersion => 0;
    bool IsSchemaOutdated => false;
    bool WasMigratedOnOpen => false;
    int? MigratedFromVersion => null;
    Task SetSchemaVersionAsync(int version) => Task.CompletedTask;

    Task<PostIndexGraphData> LoadPostIndexGraphDataAsync(string widPrefix) =>
        Task.FromResult(new PostIndexGraphData(
            new Dictionary<string, List<string>>(),
            new Dictionary<string, string>(),
            new Dictionary<string, string?>(),
            [],
            new Dictionary<string, List<string>>(),
            [],
            new Dictionary<string, List<string>>()
        ));

    Task SavePostIndexResultsAsync(string widPrefix, PostIndexAnalysisResult result)
    {
        var tcRels = result.TransitivelyCalls.Select(tc => new Relationship(
            tc.From,
            tc.To,
            OntologyConstants.Relationships.TransitivelyCalls
        )
        {
            Hops = tc.Hops
        }).ToList();

        var attrRels = result.AttributedTo.Select(attr => new Relationship(
            attr.EpId,
            attr.SinkId,
            OntologyConstants.Relationships.AttributedTo
        )
        {
            Hops = attr.Hops,
            TargetSinkClassification = attr.SinkKind
        }).ToList();

        var tasks = new List<Task>();
        if (tcRels.Count > 0) tasks.Add(UploadRelationshipsAsync(tcRels));
        if (attrRels.Count > 0) tasks.Add(UploadRelationshipsAsync(attrRels));
        return Task.WhenAll(tasks);
    }

    Task WriteFallbackReachabilityAsync(string widPrefix, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    Task<EdgeNormalizationSnapshot> LoadEdgeNormalizationSnapshotAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new EdgeNormalizationSnapshot(new Dictionary<string, (NodeKind Kind, bool IsLibrary)>(StringComparer.OrdinalIgnoreCase), []));

    Task<int> ApplyEdgeNormalizationUpdatesAsync(IReadOnlyList<EdgeNormalizationUpdate> updates, CancellationToken cancellationToken = default) =>
        Task.FromResult(0);

    Task<ExternalServicePurgeSnapshot> LoadExternalServicePurgeSnapshotAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new ExternalServicePurgeSnapshot([], [], new HashSet<string>(StringComparer.OrdinalIgnoreCase), []));

    Task<List<(string Id, string Name)>> LoadTopicsForPurgeAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new List<(string Id, string Name)>());

    Task DeleteNodesAndConnectedEdgesAsync(IReadOnlyList<string> nodeIds, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    Task DeduplicateLegacyProjectNodesAsync(string widPrefix = "", CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    Task<List<DbProjectSnapshot>> LoadProjectsForDbCanonicalizationAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new List<DbProjectSnapshot>());

    Task<List<DbAccessEdgeSnapshot>> LoadAccessEdgesForDbCanonicalizationAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new List<DbAccessEdgeSnapshot>());

    Task<(List<RawDatabaseNodeSnapshot> Databases, List<(string Id, string Name)> DataSets)> LoadRawDatabasesAndDataSetsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult((new List<RawDatabaseNodeSnapshot>(), new List<(string Id, string Name)>()));

    Task<List<Relationship>> LoadDatabaseCandidateRelationshipsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new List<Relationship>());

    Task ReplaceProjectDatabaseRelationshipsAndRemapAsync(
        IReadOnlyList<Relationship> relationshipsToUpload,
        IReadOnlyList<DomainEdgeTargetRemap> domainRemaps,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    Task CleanupAndPropagateDatabaseEntitiesAsync(
        IReadOnlyDictionary<string, string> rawToCanonical,
        IReadOnlyList<CanonicalDatabasePropagationInfo> canonicalDatabases,
        bool propagateChildSchemaProperties,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    Task<Dictionary<string, int>> GetNodesBreakdownAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new Dictionary<string, int>());

    Task<(int NodesCount, int RelationshipsCount)> GetGraphCountsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult((0, 0));

    Task<string?> GetTaxonomyJsonAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<string?>(null);
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

public record IntentCandidate(string Id, CodeExplorer.Cypher.Common.NodeKind Kind, string Name, string RelativePath, string? FullPath)
{
    public IntentCandidate(string Id, string Kind, string Name, string RelativePath, string? FullPath)
        : this(Id, CodeExplorer.Cypher.Common.EnumExtensions.ParseNodeKind(Kind), Name, RelativePath, FullPath)
    {
    }
}

public record CodeIntentPredictionResult(
    string Id,
    string? FilePath,
    string? Domain,
    string? Layer,
    string? Pattern,
    CodeExplorer.Cypher.Common.OperationType? OperationType,
    string? CapabilityTag,
    string? IntentSummary,
    bool? IsPureDomain,
    List<string>? TargetEntities,
    List<string>? EmittedEvents
)
{
    public CodeIntentPredictionResult(
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
        List<string>? EmittedEvents)
        : this(Id, FilePath, Domain, Layer, Pattern, CodeExplorer.Cypher.Common.TaxonomyEnumExtendedExtensions.ParseNullableOperationType(OperationType), CapabilityTag, IntentSummary, IsPureDomain, TargetEntities, EmittedEvents)
    {
    }
}

public record IntentRecord(
    string FilePath,
    string WorkspaceId,
    string FileId,
    string ContentHash,
    DateTime LastModifiedUtc,
    string? Domain,
    string? Layer,
    string? Pattern,
    CodeExplorer.Cypher.Common.OperationType? OperationType,
    string? CapabilityTag,
    string? IntentSummary,
    List<string>? TargetEntities,
    List<string>? EmittedEvents,
    bool? IsPureDomain,
    int ErrorCount,
    string? LastError,
    DateTime? AnalyzedAtUtc
)
{
    public IntentRecord(
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
        DateTime? AnalyzedAtUtc)
        : this(FilePath, WorkspaceId, FileId, ContentHash, LastModifiedUtc, Domain, Layer, Pattern, CodeExplorer.Cypher.Common.TaxonomyEnumExtendedExtensions.ParseNullableOperationType(OperationType), CapabilityTag, IntentSummary, TargetEntities, EmittedEvents, IsPureDomain, ErrorCount, LastError, AnalyzedAtUtc)
    {
    }
}

public record CrossDomainInteractionRecord(
    string SourceDomain,
    string TargetDomain,
    CodeExplorer.Cypher.Common.RelationshipKind EdgeKind,
    int InteractionCount
)
{
    public CrossDomainInteractionRecord(
        string SourceDomain,
        string TargetDomain,
        string EdgeKind,
        int InteractionCount)
        : this(SourceDomain, TargetDomain, CodeExplorer.Cypher.Common.EnumExtensions.ParseRelationshipKind(EdgeKind), InteractionCount)
    {
    }
}

public record DomainInfrastructureLinkRecord(
    string Domain,
    string InfraId,
    string InfraName,
    CodeExplorer.Cypher.Common.NodeKind InfraKind,
    CodeExplorer.Cypher.Common.RelationshipKind EdgeKind
)
{
    public DomainInfrastructureLinkRecord(
        string Domain,
        string InfraId,
        string InfraName,
        string InfraKind,
        string EdgeKind)
        : this(Domain, InfraId, InfraName, CodeExplorer.Cypher.Common.EnumExtensions.ParseNodeKind(InfraKind), CodeExplorer.Cypher.Common.EnumExtensions.ParseRelationshipKind(EdgeKind))
    {
    }
}

public record FileRegistryEntry(
    string ContentHash,
    DateTime LastModifiedUtc,
    string ProjectPath,
    string? SnapshotJson = null,
    long SizeBytes = -1
);

