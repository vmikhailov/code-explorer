using System.Text.RegularExpressions;
using CodeExplorer.Core.Analysis.Subgraph;
using CodeExplorer.Core.Protocol;

namespace CodeExplorer.Core.Analysis;

internal static partial class ClusterSnapshotBuilder
{
    private const int MaxStandaloneBeforePool = 8;
    private const string SharedLibsClusterId = "shared-libraries";
    private const string SharedLibsDisplayName = "Shared Libraries";
    private const string StandalonePoolClusterId = "standalone-services";
    private const string StandalonePoolDisplayName = "Standalone Services";
    private const string SlashSep = "/";

    [GeneratedRegex(@"^[a-zA-Z]:[/\\]")]
    private static partial Regex DrivePrefixRegex();

    internal static ClusteredTopologySnapshot Build(
        DomainArchitectureDto rawDomain,
        WorkspaceClusterPartitionReport partition,
        CoarsenedWorkspaceGraph coarsenedGraph)
    {
        var nonTestUnits = coarsenedGraph.Units.Where(u => !u.IsTestOnly).ToList();
        var unitById = nonTestUnits.ToDictionary(u => u.Id, u => u, StringComparer.OrdinalIgnoreCase);

        var projNameToUnit = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var rootPathToUnit = new List<(string NormRoot, string UnitId)>();
        foreach (var u in nonTestUnits)
        {
            projNameToUnit.TryAdd(u.Name, u.Id);
            if (!string.IsNullOrEmpty(u.RootPath))
            {
                rootPathToUnit.Add((u.RootPath.Replace('\\', '/').Trim('/'), u.Id));
            }
            foreach (var mp in u.MemberProjects)
            {
                projNameToUnit.TryAdd(mp, u.Id);
            }
        }
        rootPathToUnit.Sort((a, b) => b.NormRoot.Length.CompareTo(a.NormRoot.Length));

        var unitToCluster = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var unitToSubCluster = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var projNameToCluster = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var rootPathToCluster = new List<(string NormRoot, string ClusterId)>();
        var projNameToSubCluster = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var rootPathToSubCluster = new List<(string NormRoot, string SubId)>();

        var clusterById = new Dictionary<string, WorkspaceClusterDto>(StringComparer.OrdinalIgnoreCase);
        var subClusterById = new Dictionary<string, (string ParentId, WorkspaceSubClusterDto Sub)>(StringComparer.OrdinalIgnoreCase);

        void RegisterCluster(WorkspaceClusterDto cluster)
        {
            clusterById[cluster.ClusterId] = cluster;
            foreach (var u in cluster.Members)
            {
                unitToCluster[u.Id] = cluster.ClusterId;
                projNameToCluster.TryAdd(u.Name, cluster.ClusterId);
                if (!string.IsNullOrEmpty(u.RootPath))
                {
                    rootPathToCluster.Add((u.RootPath.Replace('\\', '/').Trim('/'), cluster.ClusterId));
                }
                foreach (var mp in u.MemberProjects)
                {
                    projNameToCluster.TryAdd(mp, cluster.ClusterId);
                }
            }

            if (cluster.SubClusters == null) return;
            foreach (var sc in cluster.SubClusters)
            {
                subClusterById[sc.SubClusterId] = (cluster.ClusterId, sc);
                foreach (var u in sc.Members)
                {
                    unitToSubCluster[u.Id] = sc.SubClusterId;
                    projNameToSubCluster.TryAdd(u.Name, sc.SubClusterId);
                    if (!string.IsNullOrEmpty(u.RootPath))
                    {
                        rootPathToSubCluster.Add((u.RootPath.Replace('\\', '/').Trim('/'), sc.SubClusterId));
                    }
                    foreach (var mp in u.MemberProjects)
                    {
                        projNameToSubCluster.TryAdd(mp, sc.SubClusterId);
                    }
                }
            }
        }

        if (partition.SharedLibraries != null)
        {
            RegisterCluster(partition.SharedLibraries);
        }

        foreach (var c in partition.Clusters)
        {
            RegisterCluster(c);
        }

        if (partition.StandaloneUnits.Count > MaxStandaloneBeforePool)
        {
            var standalonePool = new WorkspaceClusterDto
            {
                ClusterId = StandalonePoolClusterId,
                Name = StandalonePoolClusterId,
                DisplayName = StandalonePoolDisplayName,
                Kind = ClusterKind.FanOutPool,
                UnitCount = partition.StandaloneUnits.Count,
                ProjectCount = partition.StandaloneUnits.Sum(u => Math.Max(1, u.MemberProjectCount)),
                InternalWeight = 0,
                Languages = partition.StandaloneUnits
                    .SelectMany(u => u.Languages)
                    .Where(s => !string.IsNullOrEmpty(s))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                Members = partition.StandaloneUnits
            };
            RegisterCluster(standalonePool);
        }

        rootPathToCluster.Sort((a, b) => b.NormRoot.Length.CompareTo(a.NormRoot.Length));
        rootPathToSubCluster.Sort((a, b) => b.NormRoot.Length.CompareTo(a.NormRoot.Length));

        var entityToUnit = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var primaryEntityByUnit = new Dictionary<string, DomainEntityDto>(StringComparer.OrdinalIgnoreCase);
        var entityToCluster = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var entityToSubCluster = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var entitiesByCluster = new Dictionary<string, List<DomainEntityDto>>(StringComparer.OrdinalIgnoreCase);
        var unclusteredStandalones = new List<DomainEntityDto>();
        var extraLibs = new List<DomainEntityDto>();

        foreach (var entity in rawDomain.Nodes)
        {
            if (entity.Kind is NodeKind.Database or NodeKind.Topic or NodeKind.ExternalService)
            {
                continue;
            }

            var matchedUnitId = MatchEntityOwner(entity, projNameToUnit, rootPathToUnit);
            if (matchedUnitId != null && unitById.TryGetValue(matchedUnitId, out var matchedUnit))
            {
                entityToUnit[entity.Id] = matchedUnitId;
                if (!primaryEntityByUnit.TryGetValue(matchedUnitId, out var currentPrimary) ||
                    ScorePrimaryEntityForUnit(entity, matchedUnit) > ScorePrimaryEntityForUnit(currentPrimary, matchedUnit))
                {
                    primaryEntityByUnit[matchedUnitId] = entity;
                }
            }

            var matchedCluster = (matchedUnitId != null ? unitToCluster.GetValueOrDefault(matchedUnitId) : null)
                ?? MatchEntityOwner(entity, projNameToCluster, rootPathToCluster);
            if (matchedCluster == null && matchedUnitId == null && entity.Kind == NodeKind.Library && entity.InboundCallsCount > 0)
            {
                matchedCluster = SharedLibsClusterId;
                extraLibs.Add(entity);
            }

            if (matchedCluster != null)
            {
                entityToCluster[entity.Id] = matchedCluster;
                if (!entitiesByCluster.TryGetValue(matchedCluster, out var list))
                {
                    list = [];
                    entitiesByCluster[matchedCluster] = list;
                }
                list.Add(entity);

                var matchedSub = (matchedUnitId != null ? unitToSubCluster.GetValueOrDefault(matchedUnitId) : null)
                    ?? MatchEntityOwner(entity, projNameToSubCluster, rootPathToSubCluster);
                if (matchedSub != null)
                {
                    entityToSubCluster[entity.Id] = matchedSub;
                }
            }
            else if (matchedUnitId == null)
            {
                unclusteredStandalones.Add(entity);
            }
        }

        if (extraLibs.Count > 0 && !clusterById.ContainsKey(SharedLibsClusterId))
        {
            clusterById[SharedLibsClusterId] = new WorkspaceClusterDto
            {
                ClusterId = SharedLibsClusterId,
                Name = SharedLibsClusterId,
                DisplayName = SharedLibsDisplayName,
                Kind = ClusterKind.SharedLibraries,
                UnitCount = extraLibs.Count,
                ProjectCount = extraLibs.Sum(e => Math.Max(1, e.Projects.Count)),
                InternalWeight = 0,
                Languages = extraLibs.Select(e => e.Language ?? string.Empty).Where(s => !string.IsNullOrEmpty(s)).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                Members = []
            };
        }

        if (unclusteredStandalones.Count > MaxStandaloneBeforePool && !clusterById.ContainsKey(StandalonePoolClusterId))
        {
            clusterById[StandalonePoolClusterId] = new WorkspaceClusterDto
            {
                ClusterId = StandalonePoolClusterId,
                Name = StandalonePoolClusterId,
                DisplayName = StandalonePoolDisplayName,
                Kind = ClusterKind.FanOutPool,
                UnitCount = unclusteredStandalones.Count,
                ProjectCount = unclusteredStandalones.Sum(e => Math.Max(1, e.Projects.Count)),
                InternalWeight = 0,
                Languages = unclusteredStandalones.Select(e => e.Language ?? string.Empty).Where(s => !string.IsNullOrEmpty(s)).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                Members = []
            };
            entitiesByCluster[StandalonePoolClusterId] = unclusteredStandalones;
            foreach (var st in unclusteredStandalones)
            {
                entityToCluster[st.Id] = StandalonePoolClusterId;
            }
        }

        var infraTouching = BuildInfraTouchingClusters(rawDomain, entityToCluster, entityToUnit);
        var privateInfraCounts = BuildPrivateInfraCounts(rawDomain, infraTouching);

        return new ClusteredTopologySnapshot(
            rawDomain,
            partition,
            coarsenedGraph,
            nonTestUnits,
            unitById,
            unitToCluster,
            unitToSubCluster,
            primaryEntityByUnit,
            entityToUnit,
            entityToCluster,
            entityToSubCluster,
            clusterById,
            subClusterById,
            entitiesByCluster,
            infraTouching,
            privateInfraCounts);
    }

    private static Dictionary<string, (int Db, int Topic, int Ext)> BuildPrivateInfraCounts(
        DomainArchitectureDto rawDomain,
        Dictionary<string, HashSet<string>> infraTouching)
    {
        var counts = new Dictionary<string, (int Db, int Topic, int Ext)>(StringComparer.OrdinalIgnoreCase);
        foreach (var entity in rawDomain.Nodes)
        {
            if (entity.Kind is not (NodeKind.Database or NodeKind.Topic or NodeKind.ExternalService)) continue;
            if (!infraTouching.TryGetValue(entity.Id, out var touching) || touching.Count != 1) continue;
            var ownerCluster = touching.First();
            var cur = counts.GetValueOrDefault(ownerCluster);
            counts[ownerCluster] = entity.Kind switch
            {
                NodeKind.Database => (cur.Db + 1, cur.Topic, cur.Ext),
                NodeKind.Topic => (cur.Db, cur.Topic + 1, cur.Ext),
                _ => (cur.Db, cur.Topic, cur.Ext + 1)
            };
        }
        return counts;
    }

    private static int ScorePrimaryEntityForUnit(DomainEntityDto entity, SubgraphUnit unit)
    {
        var score = 0;
        if (string.Equals(entity.Name, unit.Name, StringComparison.OrdinalIgnoreCase)) score += 100;
        score += entity.Kind switch
        {
            NodeKind.App or NodeKind.Ingress => 45,
            NodeKind.Service => 40,
            NodeKind.Worker => 35,
            NodeKind.CliTool => 30,
            NodeKind.Library => 10,
            _ => 20
        };
        score += Math.Min(20, entity.InboundCallsCount + entity.OutboundCallsCount);
        return score;
    }

    private static Dictionary<string, HashSet<string>> BuildInfraTouchingClusters(
        DomainArchitectureDto rawDomain,
        Dictionary<string, string> entityToCluster,
        Dictionary<string, string> entityToUnit)
    {
        var infraIds = rawDomain.Nodes
            .Where(n => n.Kind is NodeKind.Database or NodeKind.Topic or NodeKind.ExternalService)
            .Select(n => n.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var infraTouching = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var edge in rawDomain.Edges)
        {
            var srcOwner = entityToCluster.GetValueOrDefault(edge.Source)
                ?? entityToUnit.GetValueOrDefault(edge.Source)
                ?? edge.Source;
            var tgtOwner = entityToCluster.GetValueOrDefault(edge.Target)
                ?? entityToUnit.GetValueOrDefault(edge.Target)
                ?? edge.Target;

            if (infraIds.Contains(edge.Target) && !infraIds.Contains(edge.Source))
            {
                if (!infraTouching.TryGetValue(edge.Target, out var set))
                {
                    set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    infraTouching[edge.Target] = set;
                }
                set.Add(srcOwner);
            }

            if (infraIds.Contains(edge.Source) && !infraIds.Contains(edge.Target))
            {
                if (!infraTouching.TryGetValue(edge.Source, out var set))
                {
                    set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    infraTouching[edge.Source] = set;
                }
                set.Add(tgtOwner);
            }
        }

        return infraTouching;
    }

    private static string? MatchEntityOwner(
        DomainEntityDto entity,
        Dictionary<string, string> byName,
        List<(string NormRoot, string OwnerId)> byRootPath)
    {
        if (byName.TryGetValue(entity.Name, out var direct))
        {
            return direct;
        }

        foreach (var p in entity.Projects)
        {
            if (byName.TryGetValue(p.Name, out var byProj))
            {
                return byProj;
            }
        }

        foreach (var p in entity.Projects)
        {
            if (string.IsNullOrWhiteSpace(p.FilePath)) continue;
            var norm = DrivePrefixRegex().Replace(p.FilePath.Replace('\\', '/'), string.Empty).Trim('/');
            foreach (var (rootPath, oid) in byRootPath)
            {
                if (norm.Equals(rootPath, StringComparison.OrdinalIgnoreCase) ||
                    norm.StartsWith(rootPath + SlashSep, StringComparison.OrdinalIgnoreCase) ||
                    norm.Contains(SlashSep + rootPath + SlashSep, StringComparison.OrdinalIgnoreCase))
                {
                    return oid;
                }
            }
        }

        return null;
    }
}
