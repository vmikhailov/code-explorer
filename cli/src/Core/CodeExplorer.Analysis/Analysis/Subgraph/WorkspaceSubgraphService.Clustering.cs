using System.Diagnostics;

namespace CodeExplorer.Core.Analysis.Subgraph;

public sealed partial class WorkspaceSubgraphService
{
    private const string SharedLibrariesClusterId = "shared-libraries";
    private const string SharedLibrariesDisplayName = "Shared Libraries";
    private const int SubClusterThreshold = 24;
    private const int TargetSubClusterSize = 18;
    private const int MinFanOutTwinCount = 3;
    private const int MinConnectorTwinCount = 2;

    private static readonly HashSet<string> ConnectorStemKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "integration", "integrations", "connector", "connectors", "adapter", "adapters",
        "webhook", "webhooks", "sync", "importer", "exporter", "plugin", "plugins", "provider", "providers"
    };

    public async Task<WorkspaceClusterPartitionReport> PartitionClustersAsync(
        IGraphClient db,
        double resolution = 1.0,
        double hubThreshold = 0.15,
        string? workspaceRoot = null,
        CancellationToken cancellationToken = default)
    {
        var (report, _) = await PartitionClustersWithGraphAsync(
            db,
            resolution,
            hubThreshold,
            workspaceRoot,
            cancellationToken);
        return report;
    }

    internal async Task<(WorkspaceClusterPartitionReport Report, CoarsenedWorkspaceGraph Graph)> PartitionClustersWithGraphAsync(
        IGraphClient db,
        double resolution = 1.0,
        double hubThreshold = 0.15,
        string? workspaceRoot = null,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var graph = await WorkspaceCoarsener.BuildAsync(db, cancellationToken);
        var units = graph.Units.Where(u => !u.IsTestOnly).ToList();

        if (units.Count == 0)
        {
            return (new WorkspaceClusterPartitionReport
            {
                RawProjectCount = graph.RawProjectCount,
                CoarsenedUnitCount = 0,
                Resolution = resolution,
                HubThreshold = hubThreshold,
                ElapsedMs = sw.Elapsed.TotalMilliseconds,
                PlatformHubs = [],
                SharedLibraries = null,
                Clusters = [],
                StandaloneUnits = [],
                BoundaryEdges = []
            }, graph);
        }

        var config = ClusterManagementService.LoadConfig(workspaceRoot);
        var manualAssignments = ResolveManualClusterAssignments(units, config);
        var autoUnits = manualAssignments.Count == 0
            ? units
            : units.Where(u => !manualAssignments.ContainsKey(u.Id)).ToList();

        var (platformHubIds, platformHubs) = IdentifyPlatformHubs(autoUnits, graph.Edges, hubThreshold);
        var (rawClusters, rawStandalones) = DetectNonHubClusters(autoUnits, graph.Edges, platformHubIds, resolution);

        var mergedRawClusters = MergeSplitSiblingClusters(rawClusters, graph.Edges);
        var (serviceClusters, fanOutPools, sharedLibMembers, remainingStandalones) =
            ClassifyStandalonesAndLibraries(units, mergedRawClusters, rawStandalones, platformHubIds, graph.Edges);

        var unitToClusterId = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var usedClusterIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { SharedLibrariesClusterId };
        var customDisplayByCluster = BuildConfiguredDisplayNames(config);

        var finalClusters = BuildAllClusterDtos(
            units,
            manualAssignments,
            serviceClusters,
            fanOutPools,
            graph.Edges,
            unitToClusterId,
            usedClusterIds,
            customDisplayByCluster);

        WorkspaceClusterDto? sharedLibrariesDto = null;
        if (sharedLibMembers.Count > 0)
        {
            var orderedLibs = sharedLibMembers.OrderBy(u => u.Name, StringComparer.OrdinalIgnoreCase).ToList();
            foreach (var lib in orderedLibs)
            {
                unitToClusterId[lib.Id] = SharedLibrariesClusterId;
            }

            sharedLibrariesDto = CreateClusterDto(
                SharedLibrariesClusterId,
                SharedLibrariesClusterId,
                customDisplayByCluster.GetValueOrDefault(SharedLibrariesClusterId) ?? SharedLibrariesDisplayName,
                ClusterKind.SharedLibraries,
                orderedLibs,
                graph.Edges);
        }

        var unitById = units.ToDictionary(u => u.Id, u => u, StringComparer.OrdinalIgnoreCase);
        var boundaryEdges = BuildClusterBoundaryEdges(graph.Edges, unitToClusterId, unitById);

        sw.Stop();
        var report = new WorkspaceClusterPartitionReport
        {
            RawProjectCount = graph.RawProjectCount,
            CoarsenedUnitCount = units.Count,
            Resolution = resolution,
            HubThreshold = hubThreshold,
            ElapsedMs = Math.Round(sw.Elapsed.TotalMilliseconds, 2),
            PlatformHubs = platformHubs,
            SharedLibraries = sharedLibrariesDto,
            Clusters = finalClusters,
            StandaloneUnits = remainingStandalones.OrderBy(u => u.Name, StringComparer.OrdinalIgnoreCase).ToList(),
            BoundaryEdges = boundaryEdges
        };
        return (report, graph);
    }

    private static Dictionary<string, string> ResolveManualClusterAssignments(
        IReadOnlyList<SubgraphUnit> units,
        WorkspaceClustersConfigDto config)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (config.Overrides.Count == 0 && config.Clusters.Count == 0)
        {
            return result;
        }

        var explicitByMemberName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var def in config.Clusters)
        {
            if (string.IsNullOrWhiteSpace(def.Name) || def.Members == null) continue;
            foreach (var m in def.Members)
            {
                if (!string.IsNullOrWhiteSpace(m))
                {
                    explicitByMemberName[m.Trim()] = def.Name.Trim();
                }
            }
        }

        foreach (var (memberKey, ovr) in config.Overrides)
        {
            if (!string.IsNullOrWhiteSpace(memberKey) && !string.IsNullOrWhiteSpace(ovr.Cluster))
            {
                explicitByMemberName[memberKey.Trim()] = ovr.Cluster.Trim();
            }
        }

        if (explicitByMemberName.Count == 0)
        {
            return result;
        }

        foreach (var u in units)
        {
            if (explicitByMemberName.TryGetValue(u.Name, out var assigned) ||
                explicitByMemberName.TryGetValue(u.Id, out assigned))
            {
                result[u.Id] = assigned;
                continue;
            }

            foreach (var mp in u.MemberProjects)
            {
                if (explicitByMemberName.TryGetValue(mp, out assigned))
                {
                    result[u.Id] = assigned;
                    break;
                }
            }
        }

        return result;
    }

    private static Dictionary<string, string> BuildConfiguredDisplayNames(WorkspaceClustersConfigDto config)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var c in config.Clusters)
        {
            if (!string.IsNullOrWhiteSpace(c.Name) && !string.IsNullOrWhiteSpace(c.DisplayName))
            {
                map[c.Name.Trim()] = c.DisplayName.Trim();
            }
        }
        return map;
    }

    private static List<List<SubgraphUnit>> MergeSplitSiblingClusters(
        List<List<SubgraphUnit>> rawClusters,
        IReadOnlyList<SubgraphWeightedEdge> edges)
    {
        if (rawClusters.Count <= 1) return rawClusters;

        var unitClusterIdx = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var baseLabels = new string[rawClusters.Count];
        for (var i = 0; i < rawClusters.Count; i++)
        {
            baseLabels[i] = SynthesizeClusterLabel(rawClusters[i]);
            foreach (var u in rawClusters[i])
            {
                unitClusterIdx[u.Id] = i;
            }
        }

        var parent = Enumerable.Range(0, rawClusters.Count).ToArray();
        int Find(int x)
        {
            while (parent[x] != x)
            {
                parent[x] = parent[parent[x]];
                x = parent[x];
            }
            return x;
        }

        foreach (var e in edges)
        {
            if (unitClusterIdx.TryGetValue(e.FromUnitId, out var cA) &&
                unitClusterIdx.TryGetValue(e.ToUnitId, out var cB) &&
                cA != cB &&
                string.Equals(baseLabels[cA], baseLabels[cB], StringComparison.OrdinalIgnoreCase))
            {
                var rA = Find(cA);
                var rB = Find(cB);
                if (rA != rB)
                {
                    if (rA < rB) parent[rB] = rA;
                    else parent[rA] = rB;
                }
            }
        }

        var slotByRoot = new int[rawClusters.Count];
        Array.Fill(slotByRoot, -1);
        var merged = new List<List<SubgraphUnit>>(rawClusters.Count);
        for (var i = 0; i < rawClusters.Count; i++)
        {
            var root = Find(i);
            var slot = slotByRoot[root];
            if (slot < 0)
            {
                slot = merged.Count;
                slotByRoot[root] = slot;
                merged.Add([]);
            }
            merged[slot].AddRange(rawClusters[i]);
        }

        return merged;
    }

    private static (
        List<List<SubgraphUnit>> ServiceClusters,
        List<(string Stem, List<SubgraphUnit> Members)> FanOutPools,
        List<SubgraphUnit> SharedLibMembers,
        List<SubgraphUnit> RemainingStandalones)
        ClassifyStandalonesAndLibraries(
            IReadOnlyList<SubgraphUnit> allUnits,
            List<List<SubgraphUnit>> serviceClusters,
            List<SubgraphUnit> rawStandalones,
            HashSet<string> platformHubIds,
            IReadOnlyList<SubgraphWeightedEdge> edges)
    {
        var sharedLibs = new List<SubgraphUnit>();
        var nonLibHubs = new List<SubgraphUnit>();
        foreach (var u in allUnits)
        {
            if (!platformHubIds.Contains(u.Id)) continue;
            if (u.IsLibraryOnly)
            {
                sharedLibs.Add(u);
            }
            else
            {
                nonLibHubs.Add(u);
            }
        }

        // Build lookup from synthesized 2-token stem -> service cluster list
        var clusterByStem = new Dictionary<string, List<SubgraphUnit>>(StringComparer.OrdinalIgnoreCase);
        foreach (var cluster in serviceClusters)
        {
            var label = SynthesizeClusterLabel(cluster);
            if (label.Contains('-'))
            {
                clusterByStem.TryAdd(label, cluster);
            }
        }

        var unitHubNeighbors = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var inboundCountByUnit = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in edges)
        {
            if (!string.Equals(e.FromUnitId, e.ToUnitId, StringComparison.OrdinalIgnoreCase))
            {
                inboundCountByUnit[e.ToUnitId] = inboundCountByUnit.GetValueOrDefault(e.ToUnitId) + 1;
            }

            if (platformHubIds.Contains(e.ToUnitId))
            {
                if (!unitHubNeighbors.TryGetValue(e.FromUnitId, out var set))
                {
                    set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    unitHubNeighbors[e.FromUnitId] = set;
                }
                set.Add(e.ToUnitId);
            }
        }

        // Attach non-library platform hubs to matching stem cluster or as standalone/own service cluster
        foreach (var hubUnit in nonLibHubs)
        {
            var stem = ExtractUnitStem(hubUnit.Name, out _);
            if (!string.IsNullOrEmpty(stem) && clusterByStem.TryGetValue(stem, out var targetCluster))
            {
                targetCluster.Add(hubUnit);
            }
            else
            {
                serviceClusters.Add([hubUnit]);
            }
        }

        var candidateTwinsByStem = new Dictionary<string, (bool IsConnectorStem, List<SubgraphUnit> Items)>(StringComparer.OrdinalIgnoreCase);
        var ungroupedStandalones = new List<SubgraphUnit>();

        foreach (var sUnit in rawStandalones)
        {
            if (sUnit.IsLibraryOnly && inboundCountByUnit.GetValueOrDefault(sUnit.Id) > 0)
            {
                sharedLibs.Add(sUnit);
                continue;
            }

            var stem = ExtractUnitStem(sUnit.Name, out var isConnectorStem);
            if (!string.IsNullOrEmpty(stem) &&
                !isConnectorStem &&
                clusterByStem.TryGetValue(stem, out var matchingCluster) &&
                unitHubNeighbors.ContainsKey(sUnit.Id))
            {
                matchingCluster.Add(sUnit);
                continue;
            }

            if (!string.IsNullOrEmpty(stem))
            {
                if (!candidateTwinsByStem.TryGetValue(stem, out var bucket))
                {
                    bucket = (isConnectorStem, []);
                    candidateTwinsByStem[stem] = bucket;
                }
                else if (isConnectorStem && !bucket.IsConnectorStem)
                {
                    bucket = (true, bucket.Items);
                    candidateTwinsByStem[stem] = bucket;
                }
                bucket.Items.Add(sUnit);
            }
            else
            {
                ungroupedStandalones.Add(sUnit);
            }
        }

        var fanOutPools = new List<(string Stem, List<SubgraphUnit> Members)>();
        foreach (var (stem, bucket) in candidateTwinsByStem)
        {
            var minRequired = bucket.IsConnectorStem ? MinConnectorTwinCount : MinFanOutTwinCount;
            if (bucket.Items.Count >= minRequired)
            {
                fanOutPools.Add((stem, bucket.Items));
            }
            else
            {
                ungroupedStandalones.AddRange(bucket.Items);
            }
        }

        return (serviceClusters, fanOutPools, sharedLibs, ungroupedStandalones);
    }

    private static string ExtractUnitStem(string unitName, out bool isConnectorStem)
    {
        const char dash = '-';
        isConnectorStem = false;
        if (string.IsNullOrWhiteSpace(unitName)) return string.Empty;

        var parts = unitName.Split(['-', '_', '.'], StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            if (ConnectorStemKeywords.Contains(part))
            {
                isConnectorStem = true;
                break;
            }
        }

        if (parts.Length >= 3)
        {
            var p0 = parts[0].ToLowerInvariant();
            var p1 = parts[1].ToLowerInvariant();
            if (ConnectorStemKeywords.Contains(p0) || ConnectorStemKeywords.Contains(p1) || !StopDomainTokens.Contains(p1))
            {
                return $"{p0}{dash}{p1}";
            }
        }

        if (parts.Length >= 2)
        {
            var p0 = parts[0].ToLowerInvariant();
            if (ConnectorStemKeywords.Contains(p0))
            {
                return p0;
            }
            if (!StopDomainTokens.Contains(p0) && p0.Length >= 3 && parts.Length >= 3)
            {
                return p0;
            }
        }

        return string.Empty;
    }

    private static List<WorkspaceClusterDto> BuildAllClusterDtos(
        IReadOnlyList<SubgraphUnit> allUnits,
        Dictionary<string, string> manualAssignments,
        List<List<SubgraphUnit>> serviceClusters,
        List<(string Stem, List<SubgraphUnit> Members)> fanOutPools,
        IReadOnlyList<SubgraphWeightedEdge> edges,
        Dictionary<string, string> unitToClusterId,
        HashSet<string> usedClusterIds,
        Dictionary<string, string> customDisplayByCluster)
    {
        var result = new List<WorkspaceClusterDto>();

        // 1. Manual clusters first
        if (manualAssignments.Count > 0)
        {
            var manualBuckets = new Dictionary<string, List<SubgraphUnit>>(StringComparer.OrdinalIgnoreCase);
            foreach (var u in allUnits)
            {
                if (!manualAssignments.TryGetValue(u.Id, out var cName)) continue;
                if (!manualBuckets.TryGetValue(cName, out var list))
                {
                    list = [];
                    manualBuckets[cName] = list;
                }
                list.Add(u);
            }

            foreach (var (cName, members) in manualBuckets.OrderByDescending(kv => kv.Value.Count).ThenBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase))
            {
                var ordered = members.OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase).ToList();
                var uniqueId = EnsureUniqueName(cName.ToLowerInvariant(), usedClusterIds);
                foreach (var m in ordered) unitToClusterId[m.Id] = uniqueId;
                var disp = customDisplayByCluster.GetValueOrDefault(cName) ?? FormatClusterDisplayName(cName);
                result.Add(CreateClusterDto(uniqueId, cName, disp, ClusterKind.ManualCluster, ordered, edges));
            }
        }

        // 2. Leiden service clusters
        foreach (var members in serviceClusters.OrderByDescending(c => c.Count).ThenBy(c => c[0].Name, StringComparer.OrdinalIgnoreCase))
        {
            var ordered = members.OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase).ToList();
            var rawLabel = SynthesizeClusterLabel(ordered);
            var uniqueId = EnsureUniqueName(rawLabel, usedClusterIds);
            foreach (var m in ordered) unitToClusterId[m.Id] = uniqueId;
            var disp = customDisplayByCluster.GetValueOrDefault(uniqueId) ?? FormatClusterDisplayName(uniqueId);
            result.Add(CreateClusterDto(uniqueId, uniqueId, disp, ClusterKind.ServiceCluster, ordered, edges));
        }

        // 3. Structural twin / fan-out pools (e.g. 150 integration-service-*)
        foreach (var (stem, members) in fanOutPools.OrderByDescending(p => p.Members.Count).ThenBy(p => p.Stem, StringComparer.OrdinalIgnoreCase))
        {
            var ordered = members.OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase).ToList();
            var uniqueId = EnsureUniqueName(stem, usedClusterIds);
            foreach (var m in ordered) unitToClusterId[m.Id] = uniqueId;
            var disp = customDisplayByCluster.GetValueOrDefault(uniqueId) ?? FormatClusterDisplayName(uniqueId);
            result.Add(CreateClusterDto(uniqueId, uniqueId, disp, ClusterKind.FanOutPool, ordered, edges));
        }

        return result;
    }

    private static WorkspaceClusterDto CreateClusterDto(
        string clusterId,
        string name,
        string displayName,
        ClusterKind kind,
        List<SubgraphUnit> orderedMembers,
        IReadOnlyList<SubgraphWeightedEdge> edges)
    {
        var memberSet = orderedMembers.Select(m => m.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var internalWeight = 0.0;
        foreach (var e in edges)
        {
            if (memberSet.Contains(e.FromUnitId) && memberSet.Contains(e.ToUnitId))
            {
                internalWeight += e.Weight;
            }
        }

        var langs = orderedMembers
            .SelectMany(m => m.Languages)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var subClusters = orderedMembers.Count > SubClusterThreshold
            ? BuildSubClusters(clusterId, orderedMembers)
            : null;

        return new WorkspaceClusterDto
        {
            ClusterId = clusterId,
            Name = name,
            DisplayName = displayName,
            Kind = kind,
            UnitCount = orderedMembers.Count,
            ProjectCount = orderedMembers.Sum(m => m.MemberProjectCount),
            InternalWeight = Math.Round(internalWeight, 2),
            Languages = langs,
            Members = orderedMembers,
            SubClusters = subClusters
        };
    }

    private static List<WorkspaceSubClusterDto> BuildSubClusters(
        string parentClusterId,
        List<SubgraphUnit> orderedMembers)
    {
        const char dash = '-';
        var parentTokens = parentClusterId.Split(['-', '_', '.'], StringSplitOptions.RemoveEmptyEntries);
        var bySubToken = new Dictionary<string, List<SubgraphUnit>>(StringComparer.OrdinalIgnoreCase);

        foreach (var m in orderedMembers)
        {
            var parts = m.Name.Split(['-', '_', '.'], StringSplitOptions.RemoveEmptyEntries);
            var subIdx = Math.Min(parentTokens.Length, parts.Length - 1);
            var subTok = subIdx >= 0 && subIdx < parts.Length ? parts[subIdx].ToLowerInvariant() : m.Name[..1].ToLowerInvariant();
            if (!bySubToken.TryGetValue(subTok, out var list))
            {
                list = [];
                bySubToken[subTok] = list;
            }
            list.Add(m);
        }

        // If sub-tokens produce well-sized groups (not 150 singletons), use sub-token groups + alphabetical fallback
        var meaningfulGroups = bySubToken.Where(kv => kv.Value.Count >= 3 && kv.Value.Count <= SubClusterThreshold * 2).ToList();
        var subClusters = new List<WorkspaceSubClusterDto>();
        if (meaningfulGroups.Count >= 2 && meaningfulGroups.Sum(kv => kv.Value.Count) >= orderedMembers.Count / 2)
        {
            var remainder = new List<SubgraphUnit>();
            foreach (var (tok, list) in bySubToken.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase))
            {
                if (list.Count >= 3)
                {
                    var subId = $"{parentClusterId}{dash}{tok}";
                    subClusters.Add(new WorkspaceSubClusterDto
                    {
                        SubClusterId = subId,
                        Name = subId,
                        DisplayName = $"{FormatClusterDisplayName(parentClusterId)}: {FormatClusterDisplayName(tok)}",
                        UnitCount = list.Count,
                        ProjectCount = list.Sum(x => x.MemberProjectCount),
                        Members = list
                    });
                }
                else
                {
                    remainder.AddRange(list);
                }
            }

            if (remainder.Count > 0)
            {
                AppendAlphabeticalChunks(parentClusterId, remainder, subClusters);
            }
            return subClusters;
        }

        AppendAlphabeticalChunks(parentClusterId, orderedMembers, subClusters);
        return subClusters;
    }

    private static void AppendAlphabeticalChunks(
        string parentClusterId,
        List<SubgraphUnit> sortedMembers,
        List<WorkspaceSubClusterDto> target)
    {
        var chunkCount = Math.Max(2, (int)Math.Ceiling((double)sortedMembers.Count / TargetSubClusterSize));
        var chunkSize = (int)Math.Ceiling((double)sortedMembers.Count / chunkCount);
        var parentDisplay = FormatClusterDisplayName(parentClusterId);

        for (var i = 0; i < sortedMembers.Count; i += chunkSize)
        {
            var slice = sortedMembers.GetRange(i, Math.Min(chunkSize, sortedMembers.Count - i));
            var firstLabel = TrimCommonPrefix(slice[0].Name, parentClusterId);
            var lastLabel = TrimCommonPrefix(slice[^1].Name, parentClusterId);
            var rangeTag = firstLabel.Length > 0 && lastLabel.Length > 0
                ? $"{char.ToUpperInvariant(firstLabel[0])}-{char.ToUpperInvariant(lastLabel[0])}"
                : $"{(i / chunkSize) + 1}";
            var subId = $"{parentClusterId}-part-{(i / chunkSize) + 1}";
            target.Add(new WorkspaceSubClusterDto
            {
                SubClusterId = subId,
                Name = subId,
                DisplayName = $"{parentDisplay} [{rangeTag}]",
                UnitCount = slice.Count,
                ProjectCount = slice.Sum(x => x.MemberProjectCount),
                Members = slice
            });
        }
    }

    private static string TrimCommonPrefix(string name, string prefix)
    {
        if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && name.Length > prefix.Length)
        {
            return name[prefix.Length..].TrimStart('-', '_', '.');
        }
        return name;
    }

    internal static string FormatClusterDisplayName(string slug)
    {
        const char space = ' ';
        if (string.IsNullOrWhiteSpace(slug)) return string.Empty;
        var parts = slug.Split(['-', '_', '.'], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return slug;
        return string.Join(space, parts.Select(p =>
            p.Length <= 3 && p.All(char.IsLetter) && p.Equals(p.ToUpperInvariant(), StringComparison.Ordinal)
                ? p
                : char.ToUpperInvariant(p[0]) + p[1..]));
    }

    private static List<ClusterBoundaryEdgeDto> BuildClusterBoundaryEdges(
        IReadOnlyList<SubgraphWeightedEdge> edges,
        Dictionary<string, string> unitToClusterId,
        Dictionary<string, SubgraphUnit> unitById)
    {
        var boundaryAgg = new Dictionary<(string From, string To), (double Weight, int Count, HashSet<string> Reasons)>();
        foreach (var e in edges)
        {
            if (!unitToClusterId.TryGetValue(e.FromUnitId, out var cFrom) ||
                !unitToClusterId.TryGetValue(e.ToUnitId, out var cTo) ||
                string.Equals(cFrom, cTo, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var bKey = (cFrom, cTo);
            if (!boundaryAgg.TryGetValue(bKey, out var cur))
            {
                cur = (0.0, 0, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            }
            if (unitById.TryGetValue(e.FromUnitId, out var fromUnit) &&
                unitById.TryGetValue(e.ToUnitId, out var toUnit))
            {
                foreach (var r in e.Reasons)
                {
                    if (cur.Reasons.Count < 5)
                    {
                        cur.Reasons.Add($"{fromUnit.Name} -> {toUnit.Name} ({r})");
                    }
                }
            }
            boundaryAgg[bKey] = (cur.Weight + e.Weight, cur.Count + 1, cur.Reasons);
        }

        return boundaryAgg
            .OrderByDescending(kv => kv.Value.Weight)
            .Select(kv => new ClusterBoundaryEdgeDto
            {
                FromCluster = kv.Key.From,
                ToCluster = kv.Key.To,
                Weight = Math.Round(kv.Value.Weight, 2),
                CrossEdgeCount = kv.Value.Count,
                SampleReasons = kv.Value.Reasons.ToList()
            })
            .ToList();
    }
}
