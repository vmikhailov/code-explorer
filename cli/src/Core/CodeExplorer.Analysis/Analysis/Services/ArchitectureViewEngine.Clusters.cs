using CodeExplorer.Core.Analysis.Subgraph;
using CodeExplorer.Core.Protocol;

namespace CodeExplorer.Core.Analysis;

public partial class ArchitectureViewEngine
{
    private const int AutoClusterEntityThreshold = 22;
    private const int MaxCachedClusterSnapshots = 16;
    private const string MetaIsClustered = "isClustered";
    private const string MetaAutoClusterEligible = "autoClusterEligible";
    private const string MetaTotalEntities = "totalRawEntities";
    private const string MetaClusterCount = "clusterCount";
    private const string BoolTrue = "true";
    private const string BoolFalse = "false";
    private const string MemoryDbMarker = ":memory:";

    private static readonly BoundedViewCache<ClusteredTopologySnapshot> ClusterSnapshotCache = new(MaxCachedClusterSnapshots);

    private async Task<GraphDataDto> GetDomainOrClusteredGraphAsync(
        ArchitectureViewRequest request,
        CancellationToken ct)
    {
        var snapshot = await GetOrBuildClusterSnapshotAsync(request.WorkspaceRoot, ct);
        var rawServiceAndLibCount = snapshot.RawDomain.Nodes.Count(n =>
            n.Kind is NodeKind.Service or NodeKind.App or NodeKind.Ingress or NodeKind.Worker or NodeKind.CliTool or NodeKind.Library);
        var autoEligible = rawServiceAndLibCount >= AutoClusterEntityThreshold && snapshot.ClusterById.Count >= 2;
        var useClusters = request.Clustered ?? autoEligible;

        if (useClusters)
        {
            return ClusterGraphProjector.ProjectClusteredGraph(
                snapshot,
                request.IncludeLibraries,
                request.SharedDatabasesOnly,
                request.ExpandedClusters,
                autoEligible);
        }

        var unclustered = ClusterGraphProjector.BuildDomainGraphFromDto(
            snapshot.RawDomain,
            request.IncludeLibraries,
            request.SharedDatabasesOnly);
        unclustered.Metadata ??= new Dictionary<string, string>();
        unclustered.Metadata[MetaIsClustered] = BoolFalse;
        unclustered.Metadata[MetaAutoClusterEligible] = autoEligible ? BoolTrue : BoolFalse;
        unclustered.Metadata[MetaTotalEntities] = rawServiceAndLibCount.ToString();
        unclustered.Metadata[MetaClusterCount] = snapshot.ClusterById.Count.ToString();
        return unclustered;
    }

    public async Task<GraphDataDto> GetClusteredArchitectureGraphAsync(
        bool includeLibraries = true,
        bool sharedDatabasesOnly = false,
        IReadOnlyList<string>? expandedClusters = null,
        string? workspaceRoot = null,
        CancellationToken ct = default)
    {
        var snapshot = await GetOrBuildClusterSnapshotAsync(workspaceRoot, ct);
        return ClusterGraphProjector.ProjectClusteredGraph(
            snapshot,
            includeLibraries,
            sharedDatabasesOnly,
            expandedClusters,
            autoEligible: true);
    }

    private async Task<ClusteredTopologySnapshot> GetOrBuildClusterSnapshotAsync(
        string? workspaceRoot,
        CancellationToken ct)
    {
        var effectiveRoot = ResolveEffectiveWorkspaceRoot(workspaceRoot);
        var cacheKey = $"{ResolveDbCachePrefix()}:cluster_snap:{effectiveRoot ?? string.Empty}";
        if (ClusterSnapshotCache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        var rawDomain = await GetDomainArchitectureAsync(includeLibraries: true, ct);
        var subgraphService = new WorkspaceSubgraphService();
        var (partition, coarsenedGraph) = await subgraphService.PartitionClustersWithGraphAsync(db, 1.0, 0.15, effectiveRoot, ct);

        var built = ClusterSnapshotBuilder.Build(rawDomain, partition, coarsenedGraph);
        ClusterSnapshotCache.Set(cacheKey, built);
        return built;
    }

    private string? ResolveEffectiveWorkspaceRoot(string? explicitRoot)
    {
        if (!string.IsNullOrWhiteSpace(explicitRoot)) return explicitRoot;
        if (!string.IsNullOrWhiteSpace(db.DbPath) && !string.Equals(db.DbPath, MemoryDbMarker, StringComparison.OrdinalIgnoreCase))
        {
            var found = WorkspaceLocator.Find(Path.GetDirectoryName(db.DbPath));
            if (found != null) return found.RootDirectory;
        }
        return null;
    }
}
