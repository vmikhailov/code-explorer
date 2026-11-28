using CodeExplorer.Core.Analysis.Subgraph;
using CodeExplorer.Core.Protocol;

namespace CodeExplorer.Core.Analysis;

internal sealed record ClusteredTopologySnapshot(
    DomainArchitectureDto RawDomain,
    WorkspaceClusterPartitionReport Partition,
    CoarsenedWorkspaceGraph CoarsenedGraph,
    IReadOnlyList<SubgraphUnit> NonTestUnits,
    Dictionary<string, SubgraphUnit> UnitById,
    Dictionary<string, string> UnitToClusterId,
    Dictionary<string, string> UnitToSubClusterId,
    Dictionary<string, DomainEntityDto> PrimaryEntityByUnitId,
    Dictionary<string, string> EntityToUnitId,
    Dictionary<string, string> EntityToClusterId,
    Dictionary<string, string> EntityToSubClusterId,
    Dictionary<string, WorkspaceClusterDto> ClusterById,
    Dictionary<string, (string ParentId, WorkspaceSubClusterDto Sub)> SubClusterById,
    Dictionary<string, List<DomainEntityDto>> EntitiesByClusterId,
    Dictionary<string, HashSet<string>> InfraTouchingClusters,
    Dictionary<string, (int Db, int Topic, int Ext)> PrivateInfraCountsByCluster);
