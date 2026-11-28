using System.Diagnostics;

namespace CodeExplorer.Core.Analysis.Subgraph;

/// <summary>
/// Fast in-memory hierarchy coarsening, hub-separated Leiden cluster partitioning (<c>ce clusters</c>),
/// and Personalized PageRank subgraph slicing (<c>ce slice</c>) for multi-project and multi-repo workspaces.
/// </summary>
public sealed partial class WorkspaceSubgraphService
{
    private const string ColPath = "path";
    private const string ColTable = "tbl";
    private const string ColRoute = "route";
    private const string SharedPlatformLabel = "shared-platform";
    private const string SlashSep = "/";

    private const string QueryEndpoints =
        "MATCH (e:Endpoint) RETURN coalesce(e.route_template, e.name, '') AS route, coalesce(e.file_path, e.path, '') AS path";

    private static readonly HashSet<string> StopDomainTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "service", "services", "app", "apps", "api", "ui", "mfe", "bff", "web",
        "core", "common", "commons", "shared", "lib", "libs", "utils", "client", "server", "repo"
    };

    public Task<CoarsenedWorkspaceGraph> BuildCoarsenedGraphAsync(
        IGraphClient db,
        CancellationToken cancellationToken = default) =>
        WorkspaceCoarsener.BuildAsync(db, cancellationToken);

    public async Task<WorkspaceDomainPartitionReport> PartitionDomainsAsync(
        IGraphClient db,
        double resolution = 1.0,
        double hubThreshold = 0.15,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var graph = await WorkspaceCoarsener.BuildAsync(db, cancellationToken);
        var units = graph.Units;

        if (units.Count == 0)
        {
            return new WorkspaceDomainPartitionReport
            {
                RawProjectCount = 0,
                CoarsenedUnitCount = 0,
                Resolution = resolution,
                HubThreshold = hubThreshold,
                ElapsedMs = sw.Elapsed.TotalMilliseconds,
                PlatformHubs = [],
                Domains = [],
                StandaloneUnits = [],
                BoundaryEdges = []
            };
        }

        var (platformHubIds, platformHubs) = IdentifyPlatformHubs(units, graph.Edges, hubThreshold);
        var (rawClusters, standaloneUnits) = DetectNonHubClusters(units, graph.Edges, platformHubIds, resolution);

        var unitToDomainName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var hubId in platformHubIds) unitToDomainName[hubId] = SharedPlatformLabel;

        var domains = BuildDomainDtos(rawClusters, graph.Edges, unitToDomainName);
        var unitById = units.ToDictionary(u => u.Id, u => u, StringComparer.OrdinalIgnoreCase);
        var boundaryEdges = BuildBoundaryEdges(graph.Edges, unitToDomainName, unitById);

        sw.Stop();
        return new WorkspaceDomainPartitionReport
        {
            RawProjectCount = graph.RawProjectCount,
            CoarsenedUnitCount = units.Count,
            Resolution = resolution,
            HubThreshold = hubThreshold,
            ElapsedMs = Math.Round(sw.Elapsed.TotalMilliseconds, 2),
            PlatformHubs = platformHubs,
            Domains = domains,
            StandaloneUnits = standaloneUnits.OrderBy(u => u.Name, StringComparer.OrdinalIgnoreCase).ToList(),
            BoundaryEdges = boundaryEdges
        };
    }

    public async Task<WorkspaceSliceReport> SliceSubgraphAsync(
        IGraphClient db,
        IReadOnlyList<string> seeds,
        int maxRepos = 10,
        double alpha = 0.85,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var graph = await WorkspaceCoarsener.BuildAsync(db, cancellationToken);
        var units = graph.Units;
        var n = units.Count;
        maxRepos = Math.Max(1, maxRepos);
        alpha = Math.Clamp(alpha, 0.1, 0.99);

        var seedUnitIndices = await ResolveSeedUnitIndicesAsync(db, units, seeds, cancellationToken);
        if (seedUnitIndices.Count == 0 || n == 0)
        {
            return new WorkspaceSliceReport
            {
                RequestedSeeds = seeds,
                ResolvedSeedUnits = [],
                MaxRepos = maxRepos,
                Alpha = alpha,
                ElapsedMs = Math.Round(sw.Elapsed.TotalMilliseconds, 2),
                SelectedUnits = []
            };
        }

        var (adj, reasonsMap) = BuildRandomWalkAdjacency(units, graph.Edges);
        var scores = RunPersonalizedPageRank(n, adj, seedUnitIndices, alpha);
        var hopDistances = ComputeHopDistances(n, adj, seedUnitIndices);
        var selected = RankSelectedUnits(units, scores, hopDistances, seedUnitIndices, reasonsMap, maxRepos);

        sw.Stop();
        return new WorkspaceSliceReport
        {
            RequestedSeeds = seeds,
            ResolvedSeedUnits = seedUnitIndices.Select(i => units[i].Name).ToList(),
            MaxRepos = maxRepos,
            Alpha = alpha,
            ElapsedMs = Math.Round(sw.Elapsed.TotalMilliseconds, 2),
            SelectedUnits = selected
        };
    }

    private static (HashSet<string> HubIds, List<PlatformHubDto> Hubs) IdentifyPlatformHubs(
        IReadOnlyList<SubgraphUnit> units,
        IReadOnlyList<SubgraphWeightedEdge> edges,
        double hubThreshold)
    {
        var inboundCallers = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in edges)
        {
            if (!inboundCallers.TryGetValue(e.ToUnitId, out var set))
            {
                set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                inboundCallers[e.ToUnitId] = set;
            }
            set.Add(e.FromUnitId);
        }

        var hubIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var hubs = new List<PlatformHubDto>();
        if (units.Count >= 5 && hubThreshold > 0)
        {
            var minInbound = Math.Max(3, (int)Math.Ceiling(hubThreshold * units.Count));
            foreach (var u in units)
            {
                var inDeg = inboundCallers.TryGetValue(u.Id, out var callers) ? callers.Count : 0;
                if (inDeg >= minInbound && (inDeg > hubThreshold * units.Count || (u.IsLibraryOnly && inDeg >= minInbound)))
                {
                    hubIds.Add(u.Id);
                    hubs.Add(new PlatformHubDto
                    {
                        Unit = u,
                        InboundDependentUnits = inDeg,
                        InboundRatio = Math.Round((double)inDeg / units.Count, 3)
                    });
                }
            }
            hubs = hubs.OrderByDescending(h => h.InboundDependentUnits).ThenBy(h => h.Unit.Name, StringComparer.OrdinalIgnoreCase).ToList();
        }
        return (hubIds, hubs);
    }

    private static (List<List<SubgraphUnit>> Clusters, List<SubgraphUnit> Standalone) DetectNonHubClusters(
        IReadOnlyList<SubgraphUnit> units,
        IReadOnlyList<SubgraphWeightedEdge> edges,
        HashSet<string> platformHubIds,
        double resolution)
    {
        var nonHubUnits = units.Where(u => !platformHubIds.Contains(u.Id)).ToList();
        var idxById = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < nonHubUnits.Count; i++) idxById[nonHubUnits[i].Id] = i;

        var prunedEdges = new List<(int U, int V, double Weight)>();
        foreach (var e in edges)
        {
            if (idxById.TryGetValue(e.FromUnitId, out var uIdx) && idxById.TryGetValue(e.ToUnitId, out var vIdx))
            {
                prunedEdges.Add((uIdx, vIdx, e.Weight));
            }
        }

        var wccs = LeidenCommunityDetector.FindWeaklyConnectedComponents(nonHubUnits.Count, prunedEdges);
        var standaloneUnits = new List<SubgraphUnit>();
        var rawClusters = new List<List<SubgraphUnit>>();

        foreach (var comp in wccs)
        {
            if (comp.Count == 1)
            {
                standaloneUnits.Add(nonHubUnits[comp[0]]);
                continue;
            }

            var localIdx = new Dictionary<int, int>(comp.Count);
            for (var i = 0; i < comp.Count; i++) localIdx[comp[i]] = i;

            var compEdges = prunedEdges
                .Where(e => localIdx.ContainsKey(e.U) && localIdx.ContainsKey(e.V))
                .Select(e => (localIdx[e.U], localIdx[e.V], e.Weight))
                .ToList();

            var comm = LeidenCommunityDetector.Detect(comp.Count, compEdges, resolution);
            var grouped = new Dictionary<int, List<SubgraphUnit>>();
            for (var i = 0; i < comp.Count; i++)
            {
                var cid = comm[i];
                if (!grouped.TryGetValue(cid, out var list))
                {
                    list = [];
                    grouped[cid] = list;
                }
                list.Add(nonHubUnits[comp[i]]);
            }
            rawClusters.AddRange(grouped.Values);
        }

        return (rawClusters, standaloneUnits);
    }

    private static List<DomainClusterDto> BuildDomainDtos(
        List<List<SubgraphUnit>> rawClusters,
        IReadOnlyList<SubgraphWeightedEdge> edges,
        Dictionary<string, string> unitToDomainName)
    {
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var domains = new List<DomainClusterDto>();

        foreach (var members in rawClusters.OrderByDescending(c => c.Count).ThenBy(c => c[0].Name, StringComparer.OrdinalIgnoreCase))
        {
            var orderedMembers = members.OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase).ToList();
            var uniqueName = EnsureUniqueName(SynthesizeClusterLabel(orderedMembers), usedNames);
            var memberSet = orderedMembers.Select(m => m.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);

            var internalWeight = edges
                .Where(e => memberSet.Contains(e.FromUnitId) && memberSet.Contains(e.ToUnitId))
                .Sum(e => e.Weight);

            foreach (var m in orderedMembers) unitToDomainName[m.Id] = uniqueName;

            domains.Add(new DomainClusterDto
            {
                DomainId = uniqueName,
                Name = uniqueName,
                UnitCount = orderedMembers.Count,
                ProjectCount = orderedMembers.Sum(m => m.MemberProjectCount),
                InternalWeight = Math.Round(internalWeight, 2),
                Languages = orderedMembers.SelectMany(m => m.Languages).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList(),
                Members = orderedMembers
            });
        }

        return domains;
    }

    private static List<DomainBoundaryEdgeDto> BuildBoundaryEdges(
        IReadOnlyList<SubgraphWeightedEdge> edges,
        Dictionary<string, string> unitToDomainName,
        Dictionary<string, SubgraphUnit> unitById)
    {
        var boundaryAgg = new Dictionary<(string From, string To), (double Weight, int Count, HashSet<string> Reasons)>();
        foreach (var e in edges)
        {
            if (!unitToDomainName.TryGetValue(e.FromUnitId, out var dFrom) ||
                !unitToDomainName.TryGetValue(e.ToUnitId, out var dTo) ||
                string.Equals(dFrom, dTo, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var bKey = (dFrom, dTo);
            if (!boundaryAgg.TryGetValue(bKey, out var cur))
            {
                cur = (0.0, 0, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            }
            foreach (var r in e.Reasons)
            {
                if (cur.Reasons.Count < 5)
                {
                    cur.Reasons.Add($"{unitById[e.FromUnitId].Name} -> {unitById[e.ToUnitId].Name} ({r})");
                }
            }
            boundaryAgg[bKey] = (cur.Weight + e.Weight, cur.Count + 1, cur.Reasons);
        }

        return boundaryAgg
            .OrderByDescending(kv => kv.Value.Weight)
            .Select(kv => new DomainBoundaryEdgeDto
            {
                FromDomain = kv.Key.From,
                ToDomain = kv.Key.To,
                Weight = Math.Round(kv.Value.Weight, 2),
                CrossEdgeCount = kv.Value.Count,
                SampleReasons = kv.Value.Reasons.ToList()
            })
            .ToList();
    }

    private static (Dictionary<int, double>[] Adj, Dictionary<(int A, int B), List<string>> ReasonsMap) BuildRandomWalkAdjacency(
        IReadOnlyList<SubgraphUnit> units,
        IReadOnlyList<SubgraphWeightedEdge> edges)
    {
        var n = units.Count;
        var idxById = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < n; i++) idxById[units[i].Id] = i;

        var adj = new Dictionary<int, double>[n];
        var reasonsMap = new Dictionary<(int A, int B), List<string>>();
        for (var i = 0; i < n; i++) adj[i] = [];

        foreach (var e in edges)
        {
            if (!idxById.TryGetValue(e.FromUnitId, out var u) || !idxById.TryGetValue(e.ToUnitId, out var v)) continue;
            adj[u][v] = adj[u].GetValueOrDefault(v) + e.Weight;
            adj[v][u] = adj[v].GetValueOrDefault(u) + 0.85 * e.Weight;

            var pairKey = u < v ? (u, v) : (v, u);
            if (!reasonsMap.TryGetValue(pairKey, out var rList))
            {
                rList = [];
                reasonsMap[pairKey] = rList;
            }
            foreach (var r in e.Reasons)
            {
                var desc = $"{units[u].Name} -> {units[v].Name} ({r})";
                if (rList.Count < 6 && !rList.Contains(desc, StringComparer.OrdinalIgnoreCase))
                {
                    rList.Add(desc);
                }
            }
        }

        return (adj, reasonsMap);
    }

    private static double[] RunPersonalizedPageRank(
        int n,
        Dictionary<int, double>[] adj,
        IReadOnlyList<int> seedUnitIndices,
        double alpha)
    {
        var outSum = new double[n];
        for (var i = 0; i < n; i++) outSum[i] = adj[i].Values.Sum();

        var teleport = new double[n];
        var invSeedCount = 1.0 / seedUnitIndices.Count;
        foreach (var sIdx in seedUnitIndices) teleport[sIdx] = invSeedCount;

        var scores = (double[])teleport.Clone();
        var next = new double[n];

        for (var iter = 0; iter < 60; iter++)
        {
            Array.Clear(next, 0, n);
            var danglingMass = 0.0;
            for (var u = 0; u < n; u++)
            {
                if (outSum[u] <= 0)
                {
                    danglingMass += scores[u];
                    continue;
                }
                var factor = alpha * scores[u] / outSum[u];
                foreach (var (v, w) in adj[u])
                {
                    next[v] += factor * w;
                }
            }

            var teleportScale = (1.0 - alpha) + alpha * danglingMass;
            var diff = 0.0;
            for (var i = 0; i < n; i++)
            {
                next[i] += teleportScale * teleport[i];
                diff += Math.Abs(next[i] - scores[i]);
                scores[i] = next[i];
            }
            if (diff < 1e-9) break;
        }

        return scores;
    }

    private static List<SlicedUnitEntry> RankSelectedUnits(
        IReadOnlyList<SubgraphUnit> units,
        double[] scores,
        int[] hopDistances,
        IReadOnlyList<int> seedUnitIndices,
        Dictionary<(int A, int B), List<string>> reasonsMap,
        int maxRepos)
    {
        var seedSet = seedUnitIndices.ToHashSet();
        var ranked = Enumerable.Range(0, units.Count)
            .Where(i => seedSet.Contains(i) || (scores[i] > 1e-6 && hopDistances[i] < int.MaxValue))
            .OrderByDescending(i => seedSet.Contains(i) ? 1 : 0)
            .ThenByDescending(i => scores[i])
            .ThenBy(i => hopDistances[i])
            .ThenBy(i => units[i].Name, StringComparer.OrdinalIgnoreCase)
            .Take(Math.Max(maxRepos, seedSet.Count))
            .ToList();

        var rankedSet = ranked.ToHashSet();
        var selected = new List<SlicedUnitEntry>(ranked.Count);
        foreach (var idx in ranked)
        {
            var connReasons = new List<string>();
            if (!seedSet.Contains(idx))
            {
                foreach (var other in rankedSet)
                {
                    if (other == idx) continue;
                    var pk = idx < other ? (idx, other) : (other, idx);
                    if (reasonsMap.TryGetValue(pk, out var rl))
                    {
                        foreach (var item in rl)
                        {
                            if (connReasons.Count < 5 && !connReasons.Contains(item, StringComparer.OrdinalIgnoreCase))
                            {
                                connReasons.Add(item);
                            }
                        }
                    }
                }
            }

            selected.Add(new SlicedUnitEntry
            {
                Unit = units[idx],
                Score = Math.Round(scores[idx], 6),
                IsSeed = seedSet.Contains(idx),
                HopDistance = hopDistances[idx] == int.MaxValue ? -1 : hopDistances[idx],
                ConnectionReasons = connReasons
            });
        }
        return selected;
    }

    private static async Task<List<int>> ResolveSeedUnitIndicesAsync(
        IGraphClient db,
        IReadOnlyList<SubgraphUnit> units,
        IReadOnlyList<string> seeds,
        CancellationToken ct)
    {
        var matched = new List<int>();
        void AddIdx(int idx)
        {
            if (idx >= 0 && !matched.Contains(idx)) matched.Add(idx);
        }

        foreach (var rawSeed in seeds)
        {
            if (string.IsNullOrWhiteSpace(rawSeed)) continue;
            var seed = rawSeed.Trim();

            var exactIdx = FindExactUnitMatch(units, seed);
            if (exactIdx >= 0)
            {
                AddIdx(exactIdx);
                continue;
            }

            if (seed.StartsWith('/'))
            {
                var epRes = await db.ExecuteCypherAsync(QueryEndpoints, cancellationToken: ct);
                foreach (var row in epRes.Rows)
                {
                    if (row[ColRoute].AsString().Contains(seed, StringComparison.OrdinalIgnoreCase))
                    {
                        AddIdx(FindUnitIndexByFilePath(units, row[ColPath].AsString().Replace('\\', '/')));
                    }
                }
                continue;
            }

            var tblRes = await db.ExecuteCypherAsync(WorkspaceCoarsener.QueryTableAccess, cancellationToken: ct);
            var matchedTbl = false;
            foreach (var row in tblRes.Rows)
            {
                if (string.Equals(row[ColTable].AsString(), seed, StringComparison.OrdinalIgnoreCase))
                {
                    var uIdx = FindUnitIndexByFilePath(units, row[ColPath].AsString().Replace('\\', '/'));
                    if (uIdx >= 0)
                    {
                        AddIdx(uIdx);
                        matchedTbl = true;
                    }
                }
            }
            if (matchedTbl) continue;

            for (var i = 0; i < units.Count; i++)
            {
                if (units[i].Name.Contains(seed, StringComparison.OrdinalIgnoreCase) ||
                    units[i].MemberProjects.Any(mp => mp.Contains(seed, StringComparison.OrdinalIgnoreCase)))
                {
                    AddIdx(i);
                }
            }
        }

        return matched;
    }

    private static int FindExactUnitMatch(IReadOnlyList<SubgraphUnit> units, string seed)
    {
        var trimmedSeed = seed.Trim('/');
        for (var i = 0; i < units.Count; i++)
        {
            if (string.Equals(units[i].Name, seed, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(units[i].RootPath, trimmedSeed, StringComparison.OrdinalIgnoreCase) ||
                units[i].MemberProjects.Any(mp => string.Equals(mp, seed, StringComparison.OrdinalIgnoreCase)))
            {
                return i;
            }
        }
        return -1;
    }

    private static int FindUnitIndexByFilePath(IReadOnlyList<SubgraphUnit> units, string normPath)
    {
        var clean = normPath.Trim('/');
        var bestIdx = -1;
        var bestLen = -1;
        for (var i = 0; i < units.Count; i++)
        {
            var rp = units[i].RootPath;
            if (string.IsNullOrEmpty(rp)) continue;
            if ((clean.Equals(rp, StringComparison.OrdinalIgnoreCase) || clean.StartsWith(rp + SlashSep, StringComparison.OrdinalIgnoreCase)) &&
                rp.Length > bestLen)
            {
                bestIdx = i;
                bestLen = rp.Length;
            }
        }
        return bestIdx;
    }

    private static int[] ComputeHopDistances(int n, Dictionary<int, double>[] adj, IReadOnlyList<int> seeds)
    {
        var dist = new int[n];
        Array.Fill(dist, int.MaxValue);
        var q = new Queue<int>();
        foreach (var s in seeds)
        {
            dist[s] = 0;
            q.Enqueue(s);
        }
        while (q.Count > 0)
        {
            var u = q.Dequeue();
            foreach (var v in adj[u].Keys)
            {
                if (dist[v] == int.MaxValue)
                {
                    dist[v] = dist[u] + 1;
                    q.Enqueue(v);
                }
            }
        }
        return dist;
    }

    private static string SynthesizeClusterLabel(IReadOnlyList<SubgraphUnit> members)
    {
        const char dash = '-';
        if (members.Count == 1) return members[0].Name;

        var twoTokenCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var oneTokenCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var m in members)
        {
            var parts = m.Name.Split(['-', '_', '.'], StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                var p2 = $"{parts[0].ToLowerInvariant()}{dash}{parts[1].ToLowerInvariant()}";
                twoTokenCounts[p2] = twoTokenCounts.GetValueOrDefault(p2) + 1;
            }
            foreach (var tok in parts)
            {
                if (tok.Length >= 3 && !StopDomainTokens.Contains(tok))
                {
                    var lower = tok.ToLowerInvariant();
                    oneTokenCounts[lower] = oneTokenCounts.GetValueOrDefault(lower) + 1;
                }
            }
        }

        var bestTwo = twoTokenCounts.OrderByDescending(kv => kv.Value).FirstOrDefault();
        if (bestTwo.Key != null && bestTwo.Value >= Math.Max(2, (members.Count + 1) / 2))
        {
            return bestTwo.Key;
        }

        var bestOne = oneTokenCounts.OrderByDescending(kv => kv.Value).FirstOrDefault();
        return bestOne.Key ?? members[0].Name.ToLowerInvariant();
    }

    private static string EnsureUniqueName(string baseName, HashSet<string> used)
    {
        if (used.Add(baseName)) return baseName;
        var idx = 2;
        while (!used.Add($"{baseName}-{idx}")) idx++;
        return $"{baseName}-{idx}";
    }
}
