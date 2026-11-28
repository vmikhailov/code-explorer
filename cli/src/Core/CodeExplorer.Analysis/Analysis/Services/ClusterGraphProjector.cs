using System.Text.Json;
using CodeExplorer.Core.Analysis.Subgraph;
using CodeExplorer.Core.Protocol;

namespace CodeExplorer.Core.Analysis;

internal static class ClusterGraphProjector
{
    private const int MaxSampleBundledItems = 8;
    private const string ClusterNodePrefix = "cluster:";
    private const string SubClusterNodePrefix = "subcluster:";
    private const string UnitServicePrefix = "service:";
    private const string UnitLibraryPrefix = "library:";
    private const string ReasonServiceCallPrefix = "service_call";
    private const string ReasonSharedTopicPrefix = "shared_topic";
    private const string ReasonSharedTablePrefix = "shared_table";
    private const string LabelCalls = "CALLS";
    private const string LabelDependsOn = "DEPENDS_ON";
    private const string LabelUsesDb = "USES_DB";
    private const string LabelPublishesTo = "PUBLISHES_TO";
    private const string PropDomain = "domain";
    private const string PropDomainDisplayName = "domainDisplayName";
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
    private const string PropDisplayTag = "displayTag";
    private const string PropZone = "zone";
    private const string PropBgColor = "bgColor";
    private const string PropBorderColor = "borderColor";
    private const string PropSize = "size";
    private const string PropLanguage = "language";
    private const string PropRole = "role";
    private const string PropIsLibrary = "is_library";
    private const string PropTier = "tier";
    private const string PropTierReason = "tier_reason";
    private const string PropInboundCalls = "inboundCalls";
    private const string PropOutboundCalls = "outboundCalls";
    private const string PropDbCount = "dbCount";
    private const string PropMessagingCount = "messagingCount";
    private const string PropFramework = "framework";
    private const string PropEntityType = "entity_type";
    private const string PropCount = "count";
    private const string PropCallCount = "call_count";
    private const string PropLabel = "label";
    private const string PropCategory = "category";
    private const string MetaView = "view";
    private const string MetaLevel = "level";
    private const string MetaGraphType = "graphType";
    private const string MetaIsClustered = "isClustered";
    private const string MetaAutoClusterEligible = "autoClusterEligible";
    private const string MetaTotalEntities = "totalRawEntities";
    private const string MetaClusterCount = "clusterCount";
    private const string MetaExpandedClusters = "expandedClusters";
    private const string BoolTrue = "true";
    private const string BoolFalse = "false";

    internal static GraphDataDto ProjectClusteredGraph(
        ClusteredTopologySnapshot snap,
        bool includeLibraries,
        bool sharedDatabasesOnly,
        IReadOnlyList<string>? expandedClusters,
        bool autoEligible)
    {
        var expandedSet = ParseExpandedSet(expandedClusters);
        var entityById = snap.RawDomain.Nodes.ToDictionary(n => n.Id, n => n, StringComparer.OrdinalIgnoreCase);
        var unitOwnerNodeId = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var entityOwnerNodeId = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var emittedNodes = new Dictionary<string, GraphNodeDto>(StringComparer.OrdinalIgnoreCase);
        var emittedNodeList = new List<GraphNodeDto>();

        ProjectCoarsenedUnits(snap, includeLibraries, expandedSet, unitOwnerNodeId, emittedNodes, emittedNodeList);
        ProjectFallbackAndMapDomainEntities(snap, includeLibraries, expandedSet, unitOwnerNodeId, entityOwnerNodeId, emittedNodes, emittedNodeList);
        ProjectInfrastructureNodes(snap, sharedDatabasesOnly, expandedSet, entityOwnerNodeId, emittedNodes, emittedNodeList);

        var aggregatedEdges = AggregateClusteredEdges(snap, unitOwnerNodeId, entityOwnerNodeId, entityById, emittedNodes, emittedNodeList);

        var rawCount = snap.RawDomain.Nodes.Count(n =>
            n.Kind is NodeKind.Service or NodeKind.App or NodeKind.Ingress or NodeKind.Worker or NodeKind.CliTool or NodeKind.Library);

        return new GraphDataDto
        {
            Nodes = emittedNodeList,
            Edges = aggregatedEdges,
            Metadata = new Dictionary<string, string>
            {
                [MetaView] = "DomainMap",
                [MetaLevel] = "Clusters",
                [MetaGraphType] = "architecture",
                [MetaIsClustered] = BoolTrue,
                [MetaAutoClusterEligible] = autoEligible ? BoolTrue : BoolFalse,
                [MetaTotalEntities] = rawCount.ToString(),
                [MetaClusterCount] = snap.ClusterById.Count.ToString(),
                [MetaExpandedClusters] = string.Join(",", expandedSet.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            }
        };
    }

    private static HashSet<string> ParseExpandedSet(IReadOnlyList<string>? expandedClusters)
    {
        var expandedSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (expandedClusters == null) return expandedSet;
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
        return expandedSet;
    }

    private static void ProjectCoarsenedUnits(
        ClusteredTopologySnapshot snap,
        bool includeLibraries,
        HashSet<string> expandedSet,
        Dictionary<string, string> unitOwnerNodeId,
        Dictionary<string, GraphNodeDto> emittedNodes,
        List<GraphNodeDto> emittedNodeList)
    {
        foreach (var unit in snap.NonTestUnits)
        {
            if (!includeLibraries && unit.IsLibraryOnly)
            {
                continue;
            }

            if (!snap.UnitToClusterId.TryGetValue(unit.Id, out var uClusterId) ||
                !snap.ClusterById.TryGetValue(uClusterId, out var uClusterDto))
            {
                var standaloneUnitNode = BuildUnitGraphNode(unit, snap.PrimaryEntityByUnitId.GetValueOrDefault(unit.Id), null, null, emittedNodes);
                AddEmittedNode(standaloneUnitNode, emittedNodes, emittedNodeList);
                unitOwnerNodeId[unit.Id] = standaloneUnitNode.Id;
                continue;
            }

            if (!includeLibraries && uClusterDto.Kind == ClusterKind.SharedLibraries)
            {
                continue;
            }

            if (!expandedSet.Contains(uClusterId))
            {
                var clusterNodeId = ClusterNodePrefix + uClusterId;
                unitOwnerNodeId[unit.Id] = clusterNodeId;
                if (!emittedNodes.ContainsKey(clusterNodeId))
                {
                    AddEmittedNode(BuildClusterGraphNode(clusterNodeId, uClusterDto, snap), emittedNodes, emittedNodeList);
                }
                continue;
            }

            if (uClusterDto.SubClusters is { Count: > 0 } &&
                snap.UnitToSubClusterId.TryGetValue(unit.Id, out var uSubId) &&
                snap.SubClusterById.TryGetValue(uSubId, out var uSubMeta) &&
                !expandedSet.Contains(uSubId))
            {
                var subNodeId = SubClusterNodePrefix + uSubId;
                unitOwnerNodeId[unit.Id] = subNodeId;
                if (!emittedNodes.ContainsKey(subNodeId))
                {
                    AddEmittedNode(BuildSubClusterGraphNode(subNodeId, uClusterDto, uSubMeta.Sub, snap), emittedNodes, emittedNodeList);
                }
                continue;
            }

            var unitMemberNode = BuildUnitGraphNode(
                unit,
                snap.PrimaryEntityByUnitId.GetValueOrDefault(unit.Id),
                uClusterId,
                uClusterDto.DisplayName,
                emittedNodes);
            AddEmittedNode(unitMemberNode, emittedNodes, emittedNodeList);
            unitOwnerNodeId[unit.Id] = unitMemberNode.Id;
        }
    }

    private static void ProjectFallbackAndMapDomainEntities(
        ClusteredTopologySnapshot snap,
        bool includeLibraries,
        HashSet<string> expandedSet,
        Dictionary<string, string> unitOwnerNodeId,
        Dictionary<string, string> entityOwnerNodeId,
        Dictionary<string, GraphNodeDto> emittedNodes,
        List<GraphNodeDto> emittedNodeList)
    {
        foreach (var entity in snap.RawDomain.Nodes)
        {
            if (entity.Kind is NodeKind.Database or NodeKind.Topic or NodeKind.ExternalService)
            {
                continue;
            }

            if (snap.EntityToUnitId.TryGetValue(entity.Id, out var mappedUnitId))
            {
                if (unitOwnerNodeId.TryGetValue(mappedUnitId, out var mappedOwner))
                {
                    entityOwnerNodeId[entity.Id] = mappedOwner;
                }
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
                AddEmittedNode(standaloneNode, emittedNodes, emittedNodeList);
                entityOwnerNodeId[entity.Id] = standaloneNode.Id;
                continue;
            }

            if (!includeLibraries && clusterDto.Kind == ClusterKind.SharedLibraries)
            {
                continue;
            }

            if (!expandedSet.Contains(clusterId))
            {
                var clusterNodeId = ClusterNodePrefix + clusterId;
                entityOwnerNodeId[entity.Id] = clusterNodeId;
                if (!emittedNodes.ContainsKey(clusterNodeId))
                {
                    AddEmittedNode(BuildClusterGraphNode(clusterNodeId, clusterDto, snap), emittedNodes, emittedNodeList);
                }
                continue;
            }

            if (clusterDto.SubClusters is { Count: > 0 } &&
                snap.EntityToSubClusterId.TryGetValue(entity.Id, out var subId) &&
                snap.SubClusterById.TryGetValue(subId, out var subMeta) &&
                !expandedSet.Contains(subId))
            {
                var subNodeId = SubClusterNodePrefix + subId;
                entityOwnerNodeId[entity.Id] = subNodeId;
                if (!emittedNodes.ContainsKey(subNodeId))
                {
                    AddEmittedNode(BuildSubClusterGraphNode(subNodeId, clusterDto, subMeta.Sub, snap), emittedNodes, emittedNodeList);
                }
                continue;
            }

            var memberNode = ConvertDomainEntityToGraphNode(entity);
            memberNode.Properties ??= new Dictionary<string, string>();
            memberNode.Properties[PropParentClusterId] = clusterId;
            memberNode.Properties[PropDomain] = clusterId;
            memberNode.Properties[PropDomainDisplayName] = clusterDto.DisplayName;
            AddEmittedNode(memberNode, emittedNodes, emittedNodeList);
            entityOwnerNodeId[entity.Id] = memberNode.Id;
        }
    }

    private static void AddEmittedNode(
        GraphNodeDto node,
        Dictionary<string, GraphNodeDto> emittedNodes,
        List<GraphNodeDto> emittedNodeList)
    {
        if (!emittedNodes.ContainsKey(node.Id))
        {
            emittedNodeList.Add(node);
        }
        emittedNodes[node.Id] = node;
    }

    private static void ProjectInfrastructureNodes(
        ClusteredTopologySnapshot snap,
        bool sharedDatabasesOnly,
        HashSet<string> expandedSet,
        Dictionary<string, string> entityOwnerNodeId,
        Dictionary<string, GraphNodeDto> emittedNodes,
        List<GraphNodeDto> emittedNodeList)
    {
        var expandedTouchedInfraIds = CollectExpandedOrStandaloneInfraIds(snap, expandedSet);
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
            var touchedByExpandedOrStandalone = expandedTouchedInfraIds.Contains(entity.Id);

            if (sharedDatabasesOnly && entity.Kind == NodeKind.Database && touchingClusters.Count < 2)
            {
                continue;
            }

            if (isSharedAcrossClusters || touchedByExpandedOrStandalone)
            {
                var infraNode = ConvertDomainEntityToGraphNode(entity);
                AddEmittedNode(infraNode, emittedNodes, emittedNodeList);
                entityOwnerNodeId[entity.Id] = infraNode.Id;
            }
        }
    }

    private static HashSet<string> CollectExpandedOrStandaloneInfraIds(
        ClusteredTopologySnapshot snap,
        HashSet<string> expandedSet)
    {
        var touched = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var edge in snap.RawDomain.Edges)
        {
            if (snap.InfraTouchingClusters.ContainsKey(edge.Target) && IsExpandedOrStandaloneEntity(edge.Source, snap, expandedSet))
            {
                touched.Add(edge.Target);
            }
            if (snap.InfraTouchingClusters.ContainsKey(edge.Source) && IsExpandedOrStandaloneEntity(edge.Target, snap, expandedSet))
            {
                touched.Add(edge.Source);
            }
        }
        return touched;
    }

    private static bool IsExpandedOrStandaloneEntity(
        string entityId,
        ClusteredTopologySnapshot snap,
        HashSet<string> expandedSet)
    {
        if (!snap.EntityToClusterId.TryGetValue(entityId, out var cid))
        {
            return true;
        }
        if (!expandedSet.Contains(cid))
        {
            return false;
        }
        return !snap.EntityToSubClusterId.TryGetValue(entityId, out var subId) || expandedSet.Contains(subId);
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

        var (privDb, privTopic, privExt) = snap.PrivateInfraCountsByCluster.GetValueOrDefault(cluster.ClusterId);
        var memberCount = cluster.Members.Count > 0 ? cluster.Members.Count : Math.Max(cluster.UnitCount, members.Count);
        var previewItems = BuildPreviewItems(cluster.Members, members, snap, 40);

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
                [PropDomain] = cluster.ClusterId,
                [PropDomainDisplayName] = cluster.DisplayName,
                [PropDisplayTag] = displayTag,
                [PropZone] = allIngress ? "ingress" : "service",
                [PropBgColor] = bgColor,
                [PropBorderColor] = borderColor,
                [PropSize] = "66",
                [PropLanguage] = string.Join(", ", cluster.Languages),
                [PropRole] = isSharedLibs ? "Library" : "Cluster",
                [PropIsLibrary] = isSharedLibs ? BoolTrue : BoolFalse,
                [PropTier] = isSharedLibs ? "5" : (allIngress ? "0" : "1"),
                [PropTierReason] = isSharedLibs
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
        var count = sub.Members.Count > 0 ? sub.Members.Count : Math.Max(sub.UnitCount, subEntities.Count);
        var previewItems = BuildPreviewItems(sub.Members, subEntities, snap, 30);

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
                [PropDomain] = parentCluster.ClusterId,
                [PropDomainDisplayName] = parentCluster.DisplayName,
                [PropDisplayTag] = "SUB",
                [PropZone] = "service",
                [PropBgColor] = "#0369a1",
                [PropBorderColor] = "#075985",
                [PropSize] = "60",
                [PropLanguage] = string.Join(", ", parentCluster.Languages),
                [PropRole] = "SubCluster",
                [PropIsLibrary] = BoolFalse,
                [PropTier] = "1",
                [PropTierReason] = $"Sub-cluster of {parentCluster.DisplayName} ({count} units)"
            }
        };
    }

    private static List<object> BuildPreviewItems(
        IReadOnlyList<SubgraphUnit> units,
        IReadOnlyList<DomainEntityDto> fallbackEntities,
        ClusteredTopologySnapshot snap,
        int limit)
    {
        if (units.Count > 0)
        {
            return units
                .Take(limit)
                .Select(u =>
                {
                    var prim = snap.PrimaryEntityByUnitId.GetValueOrDefault(u.Id);
                    var resolvedKind = prim != null && (!u.IsLibraryOnly || prim.Kind == NodeKind.Library)
                        ? prim.Kind
                        : (u.IsLibraryOnly ? NodeKind.Library : NodeKind.Service);
                    return (object)new
                    {
                        id = prim?.Id ?? ((u.IsLibraryOnly ? UnitLibraryPrefix : UnitServicePrefix) + u.Name),
                        name = u.Name,
                        kind = resolvedKind.ToString(),
                        filePath = prim?.PrimaryFilePath ?? u.RootPath
                    };
                })
                .ToList();
        }

        return fallbackEntities
            .Take(limit)
            .Select(m => (object)new
            {
                id = m.Id,
                name = m.DisplayName ?? m.Name,
                kind = m.Kind.ToString(),
                filePath = m.PrimaryFilePath
            })
            .ToList();
    }

    private static GraphNodeDto BuildUnitGraphNode(
        SubgraphUnit unit,
        DomainEntityDto? primaryEntity,
        string? parentClusterId,
        string? clusterDisplayName,
        Dictionary<string, GraphNodeDto> emittedNodes)
    {
        var preferredId = primaryEntity != null && !emittedNodes.ContainsKey(primaryEntity.Id)
            ? primaryEntity.Id
            : (unit.IsLibraryOnly ? UnitLibraryPrefix : UnitServicePrefix) + unit.Name;
        if (emittedNodes.ContainsKey(preferredId))
        {
            preferredId = unit.Id;
        }

        GraphNodeDto node;
        if (primaryEntity != null)
        {
            node = ConvertDomainEntityToGraphNode(primaryEntity);
            node.Id = preferredId;
            node.Name = unit.Name;
            if (!unit.IsLibraryOnly && node.Kind == NodeKind.Library)
            {
                node.Kind = NodeKind.Service;
                node.Properties![PropIsLibrary] = BoolFalse;
                node.Properties[PropRole] = "Service";
                node.Properties[PropEntityType] = "service";
            }
        }
        else
        {
            var isLib = unit.IsLibraryOnly;
            node = new GraphNodeDto
            {
                Id = preferredId,
                Kind = isLib ? NodeKind.Library : NodeKind.Service,
                Name = unit.Name,
                DisplayName = unit.Name,
                FilePath = unit.RootPath,
                Properties = new Dictionary<string, string>
                {
                    [PropDisplayTag] = isLib ? ":Library" : ":Service",
                    [PropZone] = isLib ? "library" : "service",
                    [PropBgColor] = isLib ? "#475569" : "#2563eb",
                    [PropBorderColor] = isLib ? "#334155" : "#1d4ed8",
                    [PropSize] = isLib ? "46" : "52",
                    [PropInboundCalls] = "0",
                    [PropOutboundCalls] = "0",
                    [PropDbCount] = "0",
                    [PropMessagingCount] = "0",
                    [PropFramework] = string.Empty,
                    [PropLanguage] = unit.PrimaryLanguage,
                    [PropEntityType] = isLib ? "library" : "service",
                    [PropRole] = isLib ? "Library" : "Service",
                    [PropIsLibrary] = isLib ? BoolTrue : BoolFalse
                }
            };
        }

        node.Properties ??= new Dictionary<string, string>();
        node.Properties[PropProjectCount] = unit.MemberProjectCount.ToString();
        if (!string.IsNullOrEmpty(parentClusterId))
        {
            node.Properties[PropParentClusterId] = parentClusterId;
            node.Properties[PropDomain] = parentClusterId;
            node.Properties[PropDomainDisplayName] = clusterDisplayName ?? parentClusterId;
        }
        return node;
    }

    private static List<GraphEdgeDto> AggregateClusteredEdges(
        ClusteredTopologySnapshot snap,
        Dictionary<string, string> unitOwnerNodeId,
        Dictionary<string, string> entityOwnerNodeId,
        Dictionary<string, DomainEntityDto> entityById,
        Dictionary<string, GraphNodeDto> emittedNodes,
        List<GraphNodeDto> emittedNodeList)
    {
        var bundledList = new List<(string Src, string Tgt, GraphEdgeCategory Cat, int Count, string Label, List<object> SampleItems)>();
        var bundledIndex = new Dictionary<(string Src, string Tgt, GraphEdgeCategory Cat), int>();
        var coarsenedPairs = new HashSet<(string Src, string Tgt, GraphEdgeCategory Cat)>();
        var inCalls = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var outCalls = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var uEdge in snap.CoarsenedGraph.Edges)
        {
            if (!unitOwnerNodeId.TryGetValue(uEdge.FromUnitId, out var effSrc) ||
                !unitOwnerNodeId.TryGetValue(uEdge.ToUnitId, out var effTgt) ||
                string.Equals(effSrc, effTgt, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!emittedNodes.ContainsKey(effSrc) || !emittedNodes.ContainsKey(effTgt))
            {
                continue;
            }

            var sName = snap.UnitById.TryGetValue(uEdge.FromUnitId, out var sUnit) ? sUnit.Name : uEdge.FromUnitId;
            var tName = snap.UnitById.TryGetValue(uEdge.ToUnitId, out var tUnit) ? tUnit.Name : uEdge.ToUnitId;

            var reasons = uEdge.Reasons.Count > 0 ? uEdge.Reasons : [LabelDependsOn];
            foreach (var reason in reasons)
            {
                var (cat, baseLabel) = ClassifyCoarsenedReason(reason);
                var key = (effSrc, effTgt, cat);
                coarsenedPairs.Add(key);

                if (!bundledIndex.TryGetValue(key, out var idx))
                {
                    idx = bundledList.Count;
                    bundledIndex[key] = idx;
                    bundledList.Add((effSrc, effTgt, cat, 0, baseLabel, []));
                }

                var cur = bundledList[idx];
                if (cur.SampleItems.Count < MaxSampleBundledItems)
                {
                    cur.SampleItems.Add(new
                    {
                        sourceId = uEdge.FromUnitId,
                        sourceName = sName,
                        targetId = uEdge.ToUnitId,
                        targetName = tName,
                        label = reason,
                        category = cat.ToCanonicalString(),
                        count = 1
                    });
                }

                bundledList[idx] = (effSrc, effTgt, cat, cur.Count + 1, cur.Label, cur.SampleItems);
                outCalls[effSrc] = outCalls.GetValueOrDefault(effSrc) + 1;
                inCalls[effTgt] = inCalls.GetValueOrDefault(effTgt) + 1;
            }
        }

        foreach (var edge in snap.RawDomain.Edges)
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
            if (coarsenedPairs.Contains(key))
            {
                continue;
            }

            if (!bundledIndex.TryGetValue(key, out var idx))
            {
                idx = bundledList.Count;
                bundledIndex[key] = idx;
                bundledList.Add((effSrc, effTgt, edge.Category, 0, edge.Label, []));
            }

            var cur = bundledList[idx];
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

            bundledList[idx] = (effSrc, effTgt, edge.Category, cur.Count + edge.Count, cur.Label, cur.SampleItems);
            outCalls[effSrc] = outCalls.GetValueOrDefault(effSrc) + edge.Count;
            inCalls[effTgt] = inCalls.GetValueOrDefault(effTgt) + edge.Count;
        }

        foreach (var node in emittedNodeList)
        {
            node.Properties ??= new Dictionary<string, string>();
            node.Properties[PropInboundCalls] = inCalls.GetValueOrDefault(node.Id).ToString();
            node.Properties[PropOutboundCalls] = outCalls.GetValueOrDefault(node.Id).ToString();
        }

        var result = new List<GraphEdgeDto>(bundledList.Count);
        foreach (var (src, tgt, cat, count, label, sampleItems) in bundledList)
        {
            result.Add(new GraphEdgeDto
            {
                Id = $"cedge:{src}->{tgt}:{cat.ToCanonicalString()}",
                Source = src,
                Target = tgt,
                Kind = EnumExtensions.ParseRelationshipKind(label),
                Category = cat,
                Properties = new Dictionary<string, string>
                {
                    [PropCount] = count.ToString(),
                    [PropCallCount] = count.ToString(),
                    [PropLabel] = label,
                    [PropCategory] = cat.ToCanonicalString(),
                    [PropBundledItemsJson] = JsonSerializer.Serialize(sampleItems)
                }
            });
        }

        return result;
    }

    private static (GraphEdgeCategory Category, string Label) ClassifyCoarsenedReason(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return (GraphEdgeCategory.Library, LabelDependsOn);
        }

        if (reason.StartsWith(ReasonServiceCallPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return (GraphEdgeCategory.ServiceCall, LabelCalls);
        }

        if (reason.StartsWith(ReasonSharedTopicPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return (GraphEdgeCategory.Messaging, LabelPublishesTo);
        }

        if (reason.StartsWith(ReasonSharedTablePrefix, StringComparison.OrdinalIgnoreCase))
        {
            return (GraphEdgeCategory.Database, LabelUsesDb);
        }

        return (GraphEdgeCategory.Library, LabelDependsOn);
    }

    internal static GraphDataDto BuildDomainGraphFromDto(
        DomainArchitectureDto domainDto,
        bool includeLibraries,
        bool sharedDatabasesOnly)
    {
        var graph = new GraphDataDto
        {
            Metadata = new Dictionary<string, string>
            {
                [MetaView] = "DomainMap",
                [MetaLevel] = "DomainMap",
                [MetaGraphType] = "architecture"
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
                    [PropCount] = e.Count.ToString(),
                    [PropCallCount] = e.Count.ToString(),
                    [PropLabel] = e.Label,
                    [PropCategory] = e.Category.ToCanonicalString()
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

    internal static GraphNodeDto ConvertDomainEntityToGraphNode(DomainEntityDto d)
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
                [PropDisplayTag] = d.DisplayTag,
                [PropZone] = d.Zone,
                [PropBgColor] = d.BgColor,
                [PropBorderColor] = d.BorderColor,
                [PropSize] = d.Size.ToString(),
                [PropInboundCalls] = d.InboundCallsCount.ToString(),
                [PropOutboundCalls] = d.OutboundCallsCount.ToString(),
                [PropDbCount] = d.DbCount.ToString(),
                [PropMessagingCount] = d.MessagingCount.ToString(),
                [PropFramework] = d.Framework ?? string.Empty,
                [PropLanguage] = d.Language ?? string.Empty,
                [PropEntityType] = d.Kind switch
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
                [PropRole] = d.Kind switch
                {
                    NodeKind.App => "App",
                    NodeKind.Service => "Service",
                    NodeKind.Worker => "Worker",
                    NodeKind.CliTool => "CliTool",
                    NodeKind.TestSuite => "Test",
                    NodeKind.Library => "Library",
                    _ => d.Kind.ToString()
                },
                [PropIsLibrary] = d.Kind == NodeKind.Library ? BoolTrue : BoolFalse,
                [PropDomain] = d.Domain ?? string.Empty,
                [PropDomainDisplayName] = d.DomainDisplayName ?? string.Empty
            }
        };
    }
}
