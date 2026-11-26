namespace CodeExplorer.Core.Analysis.Subgraph;

public enum SubgraphUnitTier
{
    DomainMember,
    PlatformHub,
    Standalone
}

public sealed record SubgraphUnit
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string RootPath { get; init; }
    public required string PrimaryLanguage { get; init; }
    public required IReadOnlyList<string> Languages { get; init; }
    public required int MemberProjectCount { get; init; }
    public required IReadOnlyList<string> MemberProjects { get; init; }
    public bool IsLibraryOnly { get; init; }
    public bool IsTestOnly { get; init; }
}

public sealed record SubgraphWeightedEdge
{
    public required string FromUnitId { get; init; }
    public required string ToUnitId { get; init; }
    public required double Weight { get; init; }
    public required IReadOnlyList<string> Reasons { get; init; }
}

public sealed record CoarsenedWorkspaceGraph
{
    public required int RawProjectCount { get; init; }
    public required IReadOnlyList<SubgraphUnit> Units { get; init; }
    public required IReadOnlyList<SubgraphWeightedEdge> Edges { get; init; }
    public required double ElapsedMs { get; init; }
}

public sealed record DomainClusterDto
{
    public required string DomainId { get; init; }
    public required string Name { get; init; }
    public required int UnitCount { get; init; }
    public required int ProjectCount { get; init; }
    public required double InternalWeight { get; init; }
    public required IReadOnlyList<string> Languages { get; init; }
    public required IReadOnlyList<SubgraphUnit> Members { get; init; }
}

public sealed record PlatformHubDto
{
    public required SubgraphUnit Unit { get; init; }
    public required int InboundDependentUnits { get; init; }
    public required double InboundRatio { get; init; }
}

public sealed record DomainBoundaryEdgeDto
{
    public required string FromDomain { get; init; }
    public required string ToDomain { get; init; }
    public required double Weight { get; init; }
    public required int CrossEdgeCount { get; init; }
    public required IReadOnlyList<string> SampleReasons { get; init; }
}

public sealed record WorkspaceDomainPartitionReport
{
    public required int RawProjectCount { get; init; }
    public required int CoarsenedUnitCount { get; init; }
    public required double Resolution { get; init; }
    public required double HubThreshold { get; init; }
    public required double ElapsedMs { get; init; }
    public required IReadOnlyList<PlatformHubDto> PlatformHubs { get; init; }
    public required IReadOnlyList<DomainClusterDto> Domains { get; init; }
    public required IReadOnlyList<SubgraphUnit> StandaloneUnits { get; init; }
    public required IReadOnlyList<DomainBoundaryEdgeDto> BoundaryEdges { get; init; }
}

public sealed record SlicedUnitEntry
{
    public required SubgraphUnit Unit { get; init; }
    public required double Score { get; init; }
    public required bool IsSeed { get; init; }
    public required int HopDistance { get; init; }
    public required IReadOnlyList<string> ConnectionReasons { get; init; }
}

public sealed record WorkspaceSliceReport
{
    public required IReadOnlyList<string> RequestedSeeds { get; init; }
    public required IReadOnlyList<string> ResolvedSeedUnits { get; init; }
    public required int MaxRepos { get; init; }
    public required double Alpha { get; init; }
    public required double ElapsedMs { get; init; }
    public required IReadOnlyList<SlicedUnitEntry> SelectedUnits { get; init; }
    public string? WorkspaceFileWritten { get; set; }
}
