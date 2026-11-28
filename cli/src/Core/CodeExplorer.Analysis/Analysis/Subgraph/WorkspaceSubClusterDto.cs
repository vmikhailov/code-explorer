namespace CodeExplorer.Core.Analysis.Subgraph;

public sealed record WorkspaceSubClusterDto
{
    public required string SubClusterId { get; init; }
    public required string Name { get; init; }
    public required string DisplayName { get; init; }
    public required int UnitCount { get; init; }
    public required int ProjectCount { get; init; }
    public required IReadOnlyList<SubgraphUnit> Members { get; init; }
}
