namespace CodeExplorer.Core.Analysis.Subgraph;

/// <summary>
/// Deterministic Leiden community detection (Traag, Waltman &amp; van Eck, 2019) on weighted
/// undirected graphs using modularity with configurable resolution parameter gamma.
/// Includes fast queue-based local moving, within-community refinement to guarantee
/// internally well-connected communities, and multi-level graph aggregation.
/// </summary>
public static class LeidenCommunityDetector
{
    private const int MaxHierarchyLevels = 15;
    private const int MaxLocalMovePasses = 30;
    private const double GainEpsilon = 1e-9;

    public static int[] Detect(
        int nodeCount,
        IReadOnlyList<(int U, int V, double Weight)> undirectedEdges,
        double resolution = 1.0)
    {
        if (nodeCount <= 0) return [];
        if (nodeCount == 1) return [0];

        var adj = BuildAdjacency(nodeCount, undirectedEdges, out var selfLoops, out var twoM);
        if (twoM <= GainEpsilon)
        {
            var singletons = new int[nodeCount];
            for (var i = 0; i < nodeCount; i++) singletons[i] = i;
            return singletons;
        }

        // Map original node index -> current supernode index
        var origToCurrent = new int[nodeCount];
        for (var i = 0; i < nodeCount; i++) origToCurrent[i] = i;

        var curCount = nodeCount;
        var curAdj = adj;
        var curSelf = selfLoops;

        for (var level = 0; level < MaxHierarchyLevels; level++)
        {
            var partition = InitializeSingletons(curCount);
            var changed = MoveNodesFast(curCount, curAdj, curSelf, twoM, resolution, partition);
            partition = NormalizeCommunityIds(partition, out var coarseCount);

            if (!changed && coarseCount == curCount)
            {
                break;
            }

            // Leiden refinement: split any poorly connected sub-communities inside each coarse community
            var refined = RefinePartition(curCount, curAdj, curSelf, twoM, resolution, partition, coarseCount);
            refined = NormalizeCommunityIds(refined, out var refinedCount);

            for (var i = 0; i < nodeCount; i++)
            {
                origToCurrent[i] = refined[origToCurrent[i]];
            }

            if (refinedCount == curCount)
            {
                break;
            }

            // Aggregate graph onto refined communities and initialize next level with coarse partition projection
            AggregateGraph(curCount, curAdj, curSelf, refined, refinedCount, out curAdj, out curSelf);
            curCount = refinedCount;
        }

        return NormalizeCommunityIds(origToCurrent, out _);
    }

    public static List<List<int>> FindWeaklyConnectedComponents(
        int nodeCount,
        IReadOnlyList<(int U, int V, double Weight)> edges)
    {
        var adj = new List<int>[nodeCount];
        for (var i = 0; i < nodeCount; i++) adj[i] = [];
        foreach (var (u, v, w) in edges)
        {
            if (u == v || w <= 0) continue;
            adj[u].Add(v);
            adj[v].Add(u);
        }

        var visited = new bool[nodeCount];
        var components = new List<List<int>>();
        var queue = new Queue<int>();

        for (var start = 0; start < nodeCount; start++)
        {
            if (visited[start]) continue;
            visited[start] = true;
            var comp = new List<int>();
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                var curr = queue.Dequeue();
                comp.Add(curr);
                foreach (var next in adj[curr])
                {
                    if (!visited[next])
                    {
                        visited[next] = true;
                        queue.Enqueue(next);
                    }
                }
            }
            components.Add(comp);
        }

        return components;
    }

    private static List<(int Neighbor, double Weight)>[] BuildAdjacency(
        int n,
        IReadOnlyList<(int U, int V, double Weight)> edges,
        out double[] selfLoops,
        out double twoM)
    {
        var map = new Dictionary<int, double>[n];
        for (var i = 0; i < n; i++) map[i] = [];
        selfLoops = new double[n];
        twoM = 0.0;

        foreach (var (u, v, w) in edges)
        {
            if (w <= 0 || u < 0 || v < 0 || u >= n || v >= n) continue;
            if (u == v)
            {
                selfLoops[u] += 2.0 * w;
                twoM += 2.0 * w;
                continue;
            }
            map[u][v] = map[u].GetValueOrDefault(v) + w;
            map[v][u] = map[v].GetValueOrDefault(u) + w;
            twoM += 2.0 * w;
        }

        var adj = new List<(int Neighbor, double Weight)>[n];
        for (var i = 0; i < n; i++)
        {
            adj[i] = map[i].OrderBy(kv => kv.Key).Select(kv => (kv.Key, kv.Value)).ToList();
        }
        return adj;
    }

    private static bool MoveNodesFast(
        int n,
        List<(int Neighbor, double Weight)>[] adj,
        double[] selfLoops,
        double twoM,
        double resolution,
        int[] comm)
    {
        var strength = new double[n];
        var commTot = new double[n];
        for (var i = 0; i < n; i++)
        {
            var k = selfLoops[i];
            foreach (var (_, w) in adj[i]) k += w;
            strength[i] = k;
            commTot[comm[i]] += k;
        }

        var inQueue = new bool[n];
        var queue = new Queue<int>(n);
        for (var i = 0; i < n; i++)
        {
            queue.Enqueue(i);
            inQueue[i] = true;
        }

        var anyMoved = false;
        var neighborWeights = new Dictionary<int, double>();
        var passes = 0;

        while (queue.Count > 0 && passes < n * MaxLocalMovePasses)
        {
            passes++;
            var u = queue.Dequeue();
            inQueue[u] = false;

            var oldComm = comm[u];
            var ku = strength[u];
            if (ku <= 0) continue;

            neighborWeights.Clear();
            neighborWeights[oldComm] = 0.0;
            foreach (var (v, w) in adj[u])
            {
                var cv = comm[v];
                neighborWeights[cv] = neighborWeights.GetValueOrDefault(cv) + w;
            }

            var wOld = neighborWeights[oldComm];
            var totOldWithoutU = commTot[oldComm] - ku;
            var baseRemoveDelta = -wOld + resolution * ku * totOldWithoutU / twoM;

            var bestComm = oldComm;
            var bestGain = 0.0;

            foreach (var (candComm, wCand) in neighborWeights.OrderBy(kv => kv.Key))
            {
                if (candComm == oldComm) continue;
                var gain = baseRemoveDelta + wCand - resolution * ku * commTot[candComm] / twoM;
                if (gain > bestGain + GainEpsilon)
                {
                    bestGain = gain;
                    bestComm = candComm;
                }
            }

            if (bestComm != oldComm)
            {
                commTot[oldComm] -= ku;
                commTot[bestComm] += ku;
                comm[u] = bestComm;
                anyMoved = true;

                foreach (var (v, _) in adj[u])
                {
                    if (comm[v] != bestComm && !inQueue[v])
                    {
                        queue.Enqueue(v);
                        inQueue[v] = true;
                    }
                }
            }
        }

        return anyMoved;
    }

    private static int[] RefinePartition(
        int n,
        List<(int Neighbor, double Weight)>[] adj,
        double[] selfLoops,
        double twoM,
        double resolution,
        int[] coarseComm,
        int coarseCount)
    {
        var refined = InitializeSingletons(n);
        var strength = new double[n];
        for (var i = 0; i < n; i++)
        {
            var k = selfLoops[i];
            foreach (var (_, w) in adj[i]) k += w;
            strength[i] = k;
        }

        var membersByCoarse = new List<int>[coarseCount];
        for (var c = 0; c < coarseCount; c++) membersByCoarse[c] = [];
        for (var i = 0; i < n; i++) membersByCoarse[coarseComm[i]].Add(i);

        var subTot = new double[n];
        for (var i = 0; i < n; i++) subTot[i] = strength[i];
        var subSize = new int[n];
        Array.Fill(subSize, 1);

        var candWeights = new Dictionary<int, double>();

        for (var c = 0; c < coarseCount; c++)
        {
            var members = membersByCoarse[c];
            if (members.Count <= 1) continue;

            var coarseStrength = 0.0;
            foreach (var u in members) coarseStrength += strength[u];

            foreach (var u in members)
            {
                // Leiden singleton eligibility: only move nodes that are still in singleton refined subcommunities
                if (subSize[refined[u]] != 1) continue;

                var ku = strength[u];
                var wWithinCoarse = 0.0;
                candWeights.Clear();

                foreach (var (v, w) in adj[u])
                {
                    if (coarseComm[v] != c) continue;
                    wWithinCoarse += w;
                    var rv = refined[v];
                    candWeights[rv] = candWeights.GetValueOrDefault(rv) + w;
                }

                // Well-connectedness gate within P
                if (wWithinCoarse + GainEpsilon < resolution * ku * (coarseStrength - ku) / twoM)
                {
                    continue;
                }

                var oldSub = refined[u];
                var bestSub = oldSub;
                var bestGain = 0.0;

                foreach (var (candSub, wCand) in candWeights.OrderBy(kv => kv.Key))
                {
                    if (candSub == oldSub) continue;
                    var gain = wCand - resolution * ku * subTot[candSub] / twoM;
                    if (gain > bestGain + GainEpsilon)
                    {
                        bestGain = gain;
                        bestSub = candSub;
                    }
                }

                if (bestSub != oldSub)
                {
                    subSize[oldSub]--;
                    subSize[bestSub]++;
                    subTot[oldSub] -= ku;
                    subTot[bestSub] += ku;
                    refined[u] = bestSub;
                }
            }
        }

        return refined;
    }

    private static void AggregateGraph(
        int n,
        List<(int Neighbor, double Weight)>[] adj,
        double[] selfLoops,
        int[] comm,
        int newCount,
        out List<(int Neighbor, double Weight)>[] nextAdj,
        out double[] nextSelf)
    {
        var nextMap = new Dictionary<int, double>[newCount];
        for (var i = 0; i < newCount; i++) nextMap[i] = [];
        nextSelf = new double[newCount];

        for (var u = 0; u < n; u++)
        {
            var cu = comm[u];
            nextSelf[cu] += selfLoops[u];
            foreach (var (v, w) in adj[u])
            {
                var cv = comm[v];
                if (cu == cv)
                {
                    nextSelf[cu] += w;
                }
                else
                {
                    nextMap[cu][cv] = nextMap[cu].GetValueOrDefault(cv) + w;
                }
            }
        }

        nextAdj = new List<(int Neighbor, double Weight)>[newCount];
        for (var i = 0; i < newCount; i++)
        {
            nextAdj[i] = nextMap[i].OrderBy(kv => kv.Key).Select(kv => (kv.Key, kv.Value)).ToList();
        }
    }

    private static int[] InitializeSingletons(int n)
    {
        var arr = new int[n];
        for (var i = 0; i < n; i++) arr[i] = i;
        return arr;
    }

    private static int[] NormalizeCommunityIds(int[] comm, out int count)
    {
        var map = new Dictionary<int, int>();
        var result = new int[comm.Length];
        var next = 0;
        for (var i = 0; i < comm.Length; i++)
        {
            if (!map.TryGetValue(comm[i], out var id))
            {
                id = next++;
                map[comm[i]] = id;
            }
            result[i] = id;
        }
        count = next;
        return result;
    }
}
