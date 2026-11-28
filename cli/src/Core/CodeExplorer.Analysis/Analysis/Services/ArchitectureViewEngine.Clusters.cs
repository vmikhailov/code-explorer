using System.Text.Json;
using CodeExplorer.Core.Analysis.Subgraph;
using CodeExplorer.Core.Protocol;

namespace CodeExplorer.Core.Analysis;

public partial class ArchitectureViewEngine
{
    private const int AutoClusterEntityThreshold = 22;
    private const int MaxStandaloneBeforePool = 8;
    private const int MaxCachedClusterSnapshots = 16;
    private const int MaxSampleBundledItems = 8;
    private const string ClusterNodePrefix = "cluster:";
    private const string SubClusterNodePrefix = "subcluster:";
    private const string SharedLibsClusterId = "shared-libraries";
    private const string StandalonePoolClusterId = "standalone-services";
    private const string StandalonePoolDisplayName = "Standalone Services";
    private const string PropIsCluster = "is_cluster";
    private const string PropIsSubCluster = "is_sub_cluster";
    private const string PropClusterId = "cluster_id";
    private const string PropParentClusterId = "parent_cluster_id";
    private const string PropClusterKind = "cluster_kind";
    private const string PropUnitCount = "unit_count";
    private const string PropProjectCount = "project_count";
    private const string PropMemberCount = "member_count";
    private const string PropSubClusterCount = "sub_cluster_count";
    private const string PropPrivateDbCount = "private_db_count";
    private const string PropPrivateTopicCount = "private_topic_count";
    private const string PropPrivateExtCount = "private_ext_count";
    private const string PropMembersPreviewJson = "members_preview_json";
    private const string PropBundledItemsJson = "bundled_items_json";
    private const string MetaIsClustered = "isClustered";
    private const string MetaAutoClusterEligible = "autoClusterEligible";
    private const string MetaTotalEntities = "totalRawEntities";
    private const string MetaClusterCount = "clusterCount";
    private const string MetaExpandedClusters = "expandedClusters";
    private const string BoolTrue = "true";
    private const string BoolFalse = "false";
    private const string MemoryDbMarker = ":memory:";
    private const string SlashSep = "/";

    private sealed record ClusteredTopologySnapshot(
        DomainArchitectureDto RawDomain,
        WorkspaceClusterPartitionReport Partition,
        Dictionary<string, string> EntityToClusterId,
        Dictionary<string, string> EntityToSubClusterId,
        Dictionary<string, WorkspaceClusterDto> ClusterById,
        Dictionary<string, (string ParentId, WorkspaceSubClusterDto Sub)> SubClusterById,
        Dictionary<string, List<DomainEntityDto>> EntitiesByClusterId,
        Dictionary<string, HashSet<string>> InfraTouchingClusters);

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
            return ProjectClusteredGraphFromSnapshot(
                snapshot,
                request.IncludeLibraries,
                request.SharedDatabasesOnly,
                request.ExpandedClusters,
                autoEligible);
        }

        var unclustered = BuildDomainGraphFromDto(snapshot.RawDomain, request.IncludeLibraries, request.SharedDatabasesOnly);
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
        return ProjectClusteredGraphFromSnapshot(snapshot, includeLibraries, sharedDatabasesOnly, expandedClusters, autoEligible: true);
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
        var partition = await subgraphService.PartitionClustersAsync(db, 1.0, 0.15, effectiveRoot, ct);

        var built = BuildClusterSnapshot(rawDomain, partition);
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

    private static ClusteredTopologySnapshot BuildClusterSnapshot(
        DomainArchitectureDto rawDomain,
        WorkspaceClusterPartitionReport partition)
    {
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

        rootPathToCluster.Sort((a, b) => b.NormRoot.Length.CompareTo(a.NormRoot.Length));
        rootPathToSubCluster.Sort((a, b) => b.NormRoot.Length.CompareTo(a.NormRoot.Length));

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

            var matchedCluster = MatchEntityCluster(entity, projNameToCluster, rootPathToCluster);
            if (matchedCluster == null && entity.Kind == NodeKind.Library)
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

                var matchedSub = MatchEntityCluster(entity, projNameToSubCluster, rootPathToSubCluster);
                if (matchedSub != null)
                {
                    entityToSubCluster[entity.Id] = matchedSub;
                }
            }
            else
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
                DisplayName = "Shared Libraries",
                Kind = ClusterKind.SharedLibraries,
                UnitCount = extraLibs.Count,
                ProjectCount = extraLibs.Sum(e => Math.Max(1, e.Projects.Count)),
                InternalWeight = 0,
                Languages = extraLibs.Select(e => e.Language ?? string.Empty).Where(s => !string.IsNullOrEmpty(s)).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                Members = []
            };
        }

        if (unclusteredStandalones.Count > MaxStandaloneBeforePool)
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

        var infraTouching = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var edge in rawDomain.Edges)
        {
            var srcCluster = entityToCluster.GetValueOrDefault(edge.Source) ?? edge.Source;
            var tgtCluster = entityToCluster.GetValueOrDefault(edge.Target) ?? edge.Target;

            if (!entityToCluster.ContainsKey(edge.Target))
            {
                if (!infraTouching.TryGetValue(edge.Target, out var set))
                {
                    set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    infraTouching[edge.Target] = set;
                }
                set.Add(srcCluster);
            }

            if (!entityToCluster.ContainsKey(edge.Source))
            {
                if (!infraTouching.TryGetValue(edge.Source, out var set))
                {
                    set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    infraTouching[edge.Source] = set;
                }
                set.Add(tgtCluster);
            }
        }

        return new ClusteredTopologySnapshot(
            rawDomain,
            partition,
            entityToCluster,
            entityToSubCluster,
            clusterById,
            subClusterById,
            entitiesByCluster,
            infraTouching);
    }

    private static string? MatchEntityCluster(
        DomainEntityDto entity,
        Dictionary<string, string> byName,
        List<(string NormRoot, string ClusterId)> byRootPath)
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
            var norm = WindowsDrivePrefixRegex.Replace(p.FilePath.Replace('\\', '/'), string.Empty).Trim('/');
            foreach (var (rootPath, cid) in byRootPath)
            {
                if (norm.Equals(rootPath, StringComparison.OrdinalIgnoreCase) ||
                    norm.StartsWith(rootPath + SlashSep, StringComparison.OrdinalIgnoreCase) ||
                    norm.Contains(SlashSep + rootPath + SlashSep, StringComparison.OrdinalIgnoreCase))
                {
                    return cid;
                }
            }
        }

        return null;
    }

    private static GraphDataDto ProjectClusteredGraphFromSnapshot(
        ClusteredTopologySnapshot snap,
        bool includeLibraries,
        bool sharedDatabasesOnly,
        IReadOnlyList<string>? expandedClusters,
        bool autoEligible)
    {
        var expandedSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (expandedClusters != null)
        {
            foreach (var raw in expandedClusters)
            {
                if (string.IsNullOrWhiteSpace(raw)) continue;
                var clean = raw.Trim();
                if (clean.StartsWith(ClusterNodePrefix, StringComparison.OrdinalIgnoreCase))
                {
                    clean = clean[ClusterNodePrefix.Length..];
                }
                else if (clean.StartsWith(SubClusterNodePrefix, StringComparison.OrdinalIgnoreCase))
                {
                    clean = clean[SubClusterNodePrefix.Length..];
                }
                expandedSet.Add(clean);
            }
        }

        var entityById = snap.RawDomain.Nodes.ToDictionary(n => n.Id, n => n, StringComparer.OrdinalIgnoreCase);
        var entityOwnerNodeId = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var emittedNodes = new Dictionary<string, GraphNodeDto>(StringComparer.OrdinalIgnoreCase);

        // 1. Project Clusters, SubClusters, Expanded Members, and Unclustered Standalones
        foreach (var entity in snap.RawDomain.Nodes)
        {
            if (entity.Kind is NodeKind.Database or NodeKind.Topic or NodeKind.ExternalService)
            {
                continue;
            }

            if (!includeLibraries && entity.Kind == NodeKind.Library)
            {
                continue;
            }

            if (!snap.EntityToClusterId.TryGetValue(entity.Id, out var clusterId) ||
                !snap.ClusterById.TryGetValue(clusterId, out var clusterDto))
            {
                var standaloneNode = ConvertDomainEntityToGraphNode(entity);
                emittedNodes[standaloneNode.Id] = standaloneNode;
                entityOwnerNodeId[entity.Id] = standaloneNode.Id;
                continue;
            }

            if (!includeLibraries && clusterDto.Kind == ClusterKind.SharedLibraries)
            {
                continue;
            }

            var isClusterExpanded = expandedSet.Contains(clusterId);
            if (!isClusterExpanded)
            {
                var clusterNodeId = ClusterNodePrefix + clusterId;
                entityOwnerNodeId[entity.Id] = clusterNodeId;
                if (!emittedNodes.ContainsKey(clusterNodeId))
                {
                    emittedNodes[clusterNodeId] = BuildClusterGraphNode(clusterNodeId, clusterDto, snap);
                }
                continue;
            }

            // Cluster IS expanded: check if it has sub-clusters (> 24 units)
            if (clusterDto.SubClusters is { Count: > 0 } &&
                snap.EntityToSubClusterId.TryGetValue(entity.Id, out var subId) &&
                snap.SubClusterById.TryGetValue(subId, out var subMeta) &&
                !expandedSet.Contains(subId))
            {
                var subNodeId = SubClusterNodePrefix + subId;
                entityOwnerNodeId[entity.Id] = subNodeId;
                if (!emittedNodes.ContainsKey(subNodeId))
                {
                    emittedNodes[subNodeId] = BuildSubClusterGraphNode(subNodeId, clusterDto, subMeta.Sub, snap);
                }
                continue;
            }

            var memberNode = ConvertDomainEntityToGraphNode(entity);
            memberNode.Properties ??= new Dictionary<string, string>();
            memberNode.Properties[PropParentClusterId] = clusterId;
            emittedNodes[memberNode.Id] = memberNode;
            entityOwnerNodeId[entity.Id] = memberNode.Id;
        }

        // 2. Project Shared Infrastructure + Infrastructure touched by Expanded/Standalone services
        foreach (var entity in snap.RawDomain.Nodes)
        {
            if (entity.Kind is not (NodeKind.Database or NodeKind.Topic or NodeKind.ExternalService))
            {
                continue;
            }

            var touchingClusters = snap.InfraTouchingClusters.GetValueOrDefault(entity.Id);
            if (touchingClusters == null || touchingClusters.Count == 0)
            {
                continue;
            }

            var isSharedAcrossClusters = entity.Kind is NodeKind.Database or NodeKind.Topic && touchingClusters.Count >= 2;
            var touchedByExpandedOrStandalone = HasExpandedOrStandaloneAccessor(entity.Id, snap, expandedSet);

            if (sharedDatabasesOnly && entity.Kind == NodeKind.Database && touchingClusters.Count < 2)
            {
                continue;
            }

            if (isSharedAcrossClusters || touchedByExpandedOrStandalone)
            {
                var infraNode = ConvertDomainEntityToGraphNode(entity);
                emittedNodes[infraNode.Id] = infraNode;
                entityOwnerNodeId[entity.Id] = infraNode.Id;
            }
        }

        // 3. Aggregate & Bundle Visible Edges
        var aggregatedEdges = AggregateClusteredEdges(snap.RawDomain.Edges, entityOwnerNodeId, entityById, emittedNodes);

        var rawCount = snap.RawDomain.Nodes.Count(n =>
            n.Kind is NodeKind.Service or NodeKind.App or NodeKind.Ingress or NodeKind.Worker or NodeKind.CliTool or NodeKind.Library);

        return new GraphDataDto
        {
            Nodes = emittedNodes.Values.ToList(),
            Edges = aggregatedEdges,
            Metadata = new Dictionary<string, string>
            {
                ["view"] = "DomainMap",
                ["level"] = "Clusters",
                ["graphType"] = "architecture",
                [MetaIsClustered] = BoolTrue,
                [MetaAutoClusterEligible] = autoEligible ? BoolTrue : BoolFalse,
                [MetaTotalEntities] = rawCount.ToString(),
                [MetaClusterCount] = snap.ClusterById.Count.ToString(),
                [MetaExpandedClusters] = string.Join(",", expandedSet.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            }
        };
    }

    private static bool HasExpandedOrStandaloneAccessor(
        string infraId,
        ClusteredTopologySnapshot snap,
        HashSet<string> expandedSet)
    {
        foreach (var edge in snap.RawDomain.Edges)
        {
            string? peerId = null;
            if (string.Equals(edge.Target, infraId, StringComparison.OrdinalIgnoreCase)) peerId = edge.Source;
            else if (string.Equals(edge.Source, infraId, StringComparison.OrdinalIgnoreCase)) peerId = edge.Target;
            if (peerId == null) continue;

            if (!snap.EntityToClusterId.TryGetValue(peerId, out var cid))
            {
                return true;
            }

            if (expandedSet.Contains(cid))
            {
                if (!snap.EntityToSubClusterId.TryGetValue(peerId, out var subId) || expandedSet.Contains(subId))
                {
                    return true;
                }
            }
        }
        return false;
    }

    private static GraphNodeDto BuildClusterGraphNode(
        string clusterNodeId,
        WorkspaceClusterDto cluster,
        ClusteredTopologySnapshot snap)
    {
        var members = snap.EntitiesByClusterId.GetValueOrDefault(cluster.ClusterId) ?? [];
        var isSharedLibs = cluster.Kind == ClusterKind.SharedLibraries;
        var isFanOut = cluster.Kind == ClusterKind.FanOutPool;
        var allIngress = !isSharedLibs && members.Count > 0 && members.All(m => m.Kind is NodeKind.App or NodeKind.Ingress);

        var nodeKind = isSharedLibs
            ? NodeKind.Library
            : (allIngress ? NodeKind.App : NodeKind.Service);
        var bgColor = isSharedLibs
            ? "#475569"
            : (isFanOut ? "#0284c7" : (allIngress ? "#0288d1" : "#4f46e5"));
        var borderColor = isSharedLibs
            ? "#334155"
            : (isFanOut ? "#0369a1" : (allIngress ? "#01579b" : "#312e81"));
        var displayTag = isSharedLibs ? "LIB" : (isFanOut ? "POOL" : "CLU");

        var (privDb, privTopic, privExt) = CountPrivateInfraForCluster(cluster.ClusterId, snap);
        var memberCount = Math.Max(cluster.UnitCount, members.Count);
        var previewItems = members
            .Take(40)
            .Select(m => new
            {
                id = m.Id,
                name = m.DisplayName ?? m.Name,
                kind = m.Kind.ToString(),
                filePath = m.PrimaryFilePath
            })
            .ToList();

        return new GraphNodeDto
        {
            Id = clusterNodeId,
            Kind = nodeKind,
            Name = cluster.ClusterId,
            DisplayName = $"{cluster.DisplayName} ({memberCount})",
            FilePath = members.FirstOrDefault()?.PrimaryFilePath,
            Properties = new Dictionary<string, string>
            {
                [PropIsCluster] = BoolTrue,
                [PropClusterId] = cluster.ClusterId,
                [PropClusterKind] = cluster.Kind.ToString(),
                [PropUnitCount] = cluster.UnitCount.ToString(),
                [PropProjectCount] = Math.Max(cluster.ProjectCount, members.Sum(m => Math.Max(1, m.Projects.Count))).ToString(),
                [PropMemberCount] = memberCount.ToString(),
                [PropSubClusterCount] = (cluster.SubClusters?.Count ?? 0).ToString(),
                [PropPrivateDbCount] = privDb.ToString(),
                [PropPrivateTopicCount] = privTopic.ToString(),
                [PropPrivateExtCount] = privExt.ToString(),
                [PropMembersPreviewJson] = JsonSerializer.Serialize(previewItems),
                ["displayTag"] = displayTag,
                ["zone"] = allIngress ? "ingress" : "service",
                ["bgColor"] = bgColor,
                ["borderColor"] = borderColor,
                ["size"] = "66",
                ["language"] = string.Join(", ", cluster.Languages),
                ["role"] = isSharedLibs ? "Library" : "Cluster",
                ["is_library"] = isSharedLibs ? BoolTrue : BoolFalse,
                ["tier"] = isSharedLibs ? "5" : (allIngress ? "0" : "1"),
                ["tier_reason"] = isSharedLibs
                    ? "Separated Shared Libraries / Platform Hubs cluster"
                    : $"Automatic {cluster.Kind} ({memberCount} units, {cluster.ProjectCount} projects)"
            }
        };
    }

    private static GraphNodeDto BuildSubClusterGraphNode(
        string subNodeId,
        WorkspaceClusterDto parentCluster,
        WorkspaceSubClusterDto sub,
        ClusteredTopologySnapshot snap)
    {
        var subEntities = (snap.EntitiesByClusterId.GetValueOrDefault(parentCluster.ClusterId) ?? [])
            .Where(e => snap.EntityToSubClusterId.TryGetValue(e.Id, out var sid) && string.Equals(sid, sub.SubClusterId, StringComparison.OrdinalIgnoreCase))
            .ToList();
        var count = Math.Max(sub.UnitCount, subEntities.Count);
        var previewItems = subEntities
            .Take(30)
            .Select(m => new
            {
                id = m.Id,
                name = m.DisplayName ?? m.Name,
                kind = m.Kind.ToString(),
                filePath = m.PrimaryFilePath
            })
            .ToList();

        return new GraphNodeDto
        {
            Id = subNodeId,
            Kind = NodeKind.Service,
            Name = sub.SubClusterId,
            DisplayName = $"{sub.DisplayName} ({count})",
            FilePath = subEntities.FirstOrDefault()?.PrimaryFilePath,
            Properties = new Dictionary<string, string>
            {
                [PropIsCluster] = BoolTrue,
                [PropIsSubCluster] = BoolTrue,
                [PropClusterId] = sub.SubClusterId,
                [PropParentClusterId] = parentCluster.ClusterId,
                [PropClusterKind] = parentCluster.Kind.ToString(),
                [PropUnitCount] = sub.UnitCount.ToString(),
                [PropProjectCount] = sub.ProjectCount.ToString(),
                [PropMemberCount] = count.ToString(),
                [PropMembersPreviewJson] = JsonSerializer.Serialize(previewItems),
                ["displayTag"] = "SUB",
                ["zone"] = "service",
                ["bgColor"] = "#0369a1",
                ["borderColor"] = "#075985",
                ["size"] = "60",
                ["language"] = string.Join(", ", parentCluster.Languages),
                ["role"] = "SubCluster",
                ["is_library"] = BoolFalse,
                ["tier"] = "1",
                ["tier_reason"] = $"Sub-cluster of {parentCluster.DisplayName} ({count} units)"
            }
        };
    }

    private static (int Db, int Topic, int Ext) CountPrivateInfraForCluster(
        string clusterId,
        ClusteredTopologySnapshot snap)
    {
        var dbCount = 0;
        var topicCount = 0;
        var extCount = 0;
        foreach (var entity in snap.RawDomain.Nodes)
        {
            if (entity.Kind is not (NodeKind.Database or NodeKind.Topic or NodeKind.ExternalService)) continue;
            if (!snap.InfraTouchingClusters.TryGetValue(entity.Id, out var touching) || touching.Count != 1 || !touching.Contains(clusterId))
            {
                continue;
            }
            if (entity.Kind == NodeKind.Database) dbCount++;
            else if (entity.Kind == NodeKind.Topic) topicCount++;
            else extCount++;
        }
        return (dbCount, topicCount, extCount);
    }

    private static List<GraphEdgeDto> AggregateClusteredEdges(
        IReadOnlyList<DomainMacroEdgeDto> rawEdges,
        Dictionary<string, string> entityOwnerNodeId,
        Dictionary<string, DomainEntityDto> entityById,
        Dictionary<string, GraphNodeDto> emittedNodes)
    {
        var bundled = new Dictionary<(string Src, string Tgt, GraphEdgeCategory Cat), (int Count, string Label, List<object> SampleItems)>();
        var inCalls = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var outCalls = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var edge in rawEdges)
        {
            if (!entityOwnerNodeId.TryGetValue(edge.Source, out var effSrc) ||
                !entityOwnerNodeId.TryGetValue(edge.Target, out var effTgt) ||
                string.Equals(effSrc, effTgt, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!emittedNodes.ContainsKey(effSrc) || !emittedNodes.ContainsKey(effTgt))
            {
                continue;
            }

            var key = (effSrc, effTgt, edge.Category);
            if (!bundled.TryGetValue(key, out var cur))
            {
                cur = (0, edge.Label, []);
            }

            if (cur.SampleItems.Count < MaxSampleBundledItems)
            {
                var sName = entityById.TryGetValue(edge.Source, out var sEnt) ? (sEnt.DisplayName ?? sEnt.Name) : edge.Source;
                var tName = entityById.TryGetValue(edge.Target, out var tEnt) ? (tEnt.DisplayName ?? tEnt.Name) : edge.Target;
                cur.SampleItems.Add(new
                {
                    sourceId = edge.Source,
                    sourceName = sName,
                    targetId = edge.Target,
                    targetName = tName,
                    label = edge.Label,
                    category = edge.Category.ToCanonicalString(),
                    count = edge.Count
                });
            }

            bundled[key] = (cur.Count + edge.Count, cur.Label, cur.SampleItems);
            outCalls[effSrc] = outCalls.GetValueOrDefault(effSrc) + edge.Count;
            inCalls[effTgt] = inCalls.GetValueOrDefault(effTgt) + edge.Count;
        }

        foreach (var (nid, node) in emittedNodes)
        {
            node.Properties ??= new Dictionary<string, string>();
            node.Properties["inboundCalls"] = inCalls.GetValueOrDefault(nid).ToString();
            node.Properties["outboundCalls"] = outCalls.GetValueOrDefault(nid).ToString();
        }

        var result = new List<GraphEdgeDto>(bundled.Count);
        foreach (var ((src, tgt, cat), agg) in bundled)
        {
            result.Add(new GraphEdgeDto
            {
                Id = $"cedge:{src}->{tgt}:{cat.ToCanonicalString()}",
                Source = src,
                Target = tgt,
                Kind = EnumExtensions.ParseRelationshipKind(agg.Label),
                Category = cat,
                Properties = new Dictionary<string, string>
                {
                    ["count"] = agg.Count.ToString(),
                    ["call_count"] = agg.Count.ToString(),
                    ["label"] = agg.Label,
                    ["category"] = cat.ToCanonicalString(),
                    [PropBundledItemsJson] = JsonSerializer.Serialize(agg.SampleItems)
                }
            });
        }

        return result;
    }

    private static GraphDataDto BuildDomainGraphFromDto(
        DomainArchitectureDto domainDto,
        bool includeLibraries,
        bool sharedDatabasesOnly)
    {
        var graph = new GraphDataDto
        {
            Metadata = new Dictionary<string, string>
            {
                ["view"] = "DomainMap",
                ["level"] = "DomainMap",
                ["graphType"] = "architecture"
            }
        };

        foreach (var d in domainDto.Nodes)
        {
            if (!includeLibraries && d.Kind == NodeKind.Library) continue;
            graph.Nodes.Add(ConvertDomainEntityToGraphNode(d));
        }

        var allowedIds = graph.Nodes.Select(n => n.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var e in domainDto.Edges)
        {
            if (!allowedIds.Contains(e.Source) || !allowedIds.Contains(e.Target)) continue;
            graph.Edges.Add(new GraphEdgeDto
            {
                Id = e.Id,
                Source = e.Source,
                Target = e.Target,
                Kind = EnumExtensions.ParseRelationshipKind(e.Label),
                Category = e.Category,
                Properties = new Dictionary<string, string>
                {
                    ["count"] = e.Count.ToString(),
                    ["call_count"] = e.Count.ToString(),
                    ["label"] = e.Label,
                    ["category"] = e.Category.ToCanonicalString()
                }
            });
        }

        if (sharedDatabasesOnly)
        {
            var dbInDegree = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var edge in graph.Edges)
            {
                if (edge.Category == GraphEdgeCategory.Database || edge.Kind == RelationshipKind.UsesDb)
                {
                    dbInDegree[edge.Target] = dbInDegree.GetValueOrDefault(edge.Target) + 1;
                }
            }

            var singleConnDbIds = graph.Nodes
                .Where(n => n.Kind == NodeKind.Database && dbInDegree.GetValueOrDefault(n.Id) <= 1)
                .Select(n => n.Id)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (singleConnDbIds.Count > 0)
            {
                graph.Nodes.RemoveAll(n => singleConnDbIds.Contains(n.Id));
                graph.Edges.RemoveAll(e => singleConnDbIds.Contains(e.Target) || singleConnDbIds.Contains(e.Source));
            }
        }

        return graph;
    }

    private static GraphNodeDto ConvertDomainEntityToGraphNode(DomainEntityDto d)
    {
        return new GraphNodeDto
        {
            Id = d.Id,
            Kind = d.Kind,
            Name = d.Name,
            DisplayName = d.DisplayName,
            FilePath = d.PrimaryFilePath,
            Properties = new Dictionary<string, string>
            {
                ["displayTag"] = d.DisplayTag,
                ["zone"] = d.Zone,
                ["bgColor"] = d.BgColor,
                ["borderColor"] = d.BorderColor,
                ["size"] = d.Size.ToString(),
                ["inboundCalls"] = d.InboundCallsCount.ToString(),
                ["outboundCalls"] = d.OutboundCallsCount.ToString(),
                ["dbCount"] = d.DbCount.ToString(),
                ["messagingCount"] = d.MessagingCount.ToString(),
                ["framework"] = d.Framework ?? string.Empty,
                ["language"] = d.Language ?? string.Empty,
                ["entity_type"] = d.Kind switch
                {
                    NodeKind.App => "app",
                    NodeKind.Service => "service",
                    NodeKind.Worker => "worker",
                    NodeKind.CliTool => "clitool",
                    NodeKind.Database => "database",
                    NodeKind.Topic => "topic",
                    NodeKind.ExternalService => "external",
                    NodeKind.TestSuite => "testsuite",
                    NodeKind.Library => "library",
                    _ => d.Kind.ToString().ToLowerInvariant()
                },
                ["role"] = d.Kind switch
                {
                    NodeKind.App => "App",
                    NodeKind.Service => "Service",
                    NodeKind.Worker => "Worker",
                    NodeKind.CliTool => "CliTool",
                    NodeKind.TestSuite => "Test",
                    NodeKind.Library => "Library",
                    _ => d.Kind.ToString()
                },
                ["is_library"] = d.Kind == NodeKind.Library ? BoolTrue : BoolFalse,
                ["domain"] = d.Domain ?? string.Empty,
                ["domainDisplayName"] = d.DomainDisplayName ?? string.Empty
            }
        };
    }
}
