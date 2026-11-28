namespace CodeExplorer.Core.Analysis.Subgraph;

public sealed record WorkspaceClusterPartitionReport
{
    public required int RawProjectCount { get; init; }
    public required int CoarsenedUnitCount { get; init; }
    public required double Resolution { get; init; }
    public required double HubThreshold { get; init; }
    public required double ElapsedMs { get; init; }
    public required IReadOnlyList<PlatformHubDto> PlatformHubs { get; init; }
    public WorkspaceClusterDto? SharedLibraries { get; init; }
    public required IReadOnlyList<WorkspaceClusterDto> Clusters { get; init; }
    public required IReadOnlyList<SubgraphUnit> StandaloneUnits { get; init; }
    public required IReadOnlyList<ClusterBoundaryEdgeDto> BoundaryEdges { get; init; }
}
