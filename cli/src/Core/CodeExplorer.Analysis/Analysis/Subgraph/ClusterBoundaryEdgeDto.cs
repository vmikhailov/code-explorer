namespace CodeExplorer.Core.Analysis.Subgraph;

public sealed record ClusterBoundaryEdgeDto
{
    public required string FromCluster { get; init; }
    public required string ToCluster { get; init; }
    public required double Weight { get; init; }
    public required int CrossEdgeCount { get; init; }
    public required IReadOnlyList<string> SampleReasons { get; init; }
}
