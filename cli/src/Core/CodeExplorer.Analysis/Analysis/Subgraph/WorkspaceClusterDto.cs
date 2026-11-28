namespace CodeExplorer.Core.Analysis.Subgraph;

public sealed record WorkspaceClusterDto
{
    public required string ClusterId { get; init; }
    public required string Name { get; init; }
    public required string DisplayName { get; init; }
    public required ClusterKind Kind { get; init; }
    public required int UnitCount { get; init; }
    public required int ProjectCount { get; init; }
    public required double InternalWeight { get; init; }
    public required IReadOnlyList<string> Languages { get; init; }
    public required IReadOnlyList<SubgraphUnit> Members { get; init; }
    public IReadOnlyList<WorkspaceSubClusterDto>? SubClusters { get; init; }
}
