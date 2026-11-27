using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;

namespace CodeExplorer.Core.Parser.Layers;

internal static class Layer5ComplexityPropagator
{
    public static async Task PropagateAndUploadAsync(
        Layer5Context ctx,
        IReadOnlyList<Relationship> referenceRelationships)
    {
        var functionsById = new Dictionary<string, FunctionNode>(StringComparer.Ordinal);
        var visitedNodes = new HashSet<IOntologyNode>(ReferenceEqualityComparer.Instance);

        if (ctx.SyntaxStructure != null)
        {
            CollectFunctions(ctx.SyntaxStructure, functionsById, visitedNodes);
        }

        for (var i = 0; i < ctx.Files.Count; i++)
        {
            CollectFunctions(ctx.Files[i], functionsById, visitedNodes);
        }

        if (functionsById.Count == 0)
        {
            return;
        }

        var n = functionsById.Count;
        var fnArray = new FunctionNode[n];
        var indexById = new Dictionary<string, int>(n, StringComparer.Ordinal);
        var idxCounter = 0;
        foreach (var (id, fn) in functionsById)
        {
            indexById[id] = idxCounter;
            fnArray[idxCounter] = fn;
            idxCounter++;
        }

        var edgeWeights = new Dictionary<(int From, int To), int>();
        for (var i = 0; i < referenceRelationships.Count; i++)
        {
            var rel = referenceRelationships[i];
            if (rel.Kind != RelationshipKind.Calls) continue;
            if (!indexById.TryGetValue(rel.From, out var u) ||
                !indexById.TryGetValue(rel.To, out var v) ||
                u == v)
            {
                continue;
            }

            var w = (int)rel.CallContext.LoopDepth;
            if (w == 0 && (rel.CallContext.Flags & CallExecutionFlags.InLoop) != 0)
            {
                w = 1;
            }

            var key = (u, v);
            if (!edgeWeights.TryGetValue(key, out var existingW) || w > existingW)
            {
                edgeWeights[key] = w;
            }
        }

        if (edgeWeights.Count == 0)
        {
            return;
        }

        var adj = new List<(int To, int Weight)>?[n];
        foreach (var ((u, v), w) in edgeWeights)
        {
            var list = adj[u];
            if (list == null)
            {
                list = new List<(int To, int Weight)>(2);
                adj[u] = list;
            }
            list.Add((v, w));
        }

        var (sccId, sccMembers) = ComputeTarjanSccs(n, adj);

        // Tarjan emits SCCs in reverse topological order (sink SCCs first, root SCCs last).
        var callerLoopPrefix = new int[n];
        for (var s = sccMembers.Count - 1; s >= 0; s--)
        {
            var comp = sccMembers[s];
            if (comp.Count > 1)
            {
                // Propagate non-loop incoming prefix uniformly across mutually recursive SCC members
                var compMax = 0;
                for (var i = 0; i < comp.Count; i++)
                {
                    if (callerLoopPrefix[comp[i]] > compMax)
                    {
                        compMax = callerLoopPrefix[comp[i]];
                    }
                }
                for (var i = 0; i < comp.Count; i++)
                {
                    callerLoopPrefix[comp[i]] = compMax;
                }
            }

            for (var i = 0; i < comp.Count; i++)
            {
                var u = comp[i];
                var neighbors = adj[u];
                if (neighbors == null) continue;

                var uPrefix = callerLoopPrefix[u];
                for (var e = 0; e < neighbors.Count; e++)
                {
                    var (v, weight) = neighbors[e];
                    if (sccId[u] == sccId[v])
                    {
                        // Ignore intra-SCC self/mutual recursion (e.g. AST/tree DFS walkers)
                        continue;
                    }

                    var candidatePrefix = uPrefix + weight;
                    if (candidatePrefix > callerLoopPrefix[v])
                    {
                        callerLoopPrefix[v] = candidatePrefix;
                    }
                }
            }
        }

        var updatedNodes = new List<Node>();
        for (var i = 0; i < n; i++)
        {
            var prefix = callerLoopPrefix[i];
            if (prefix <= 0) continue;

            var fn = fnArray[i];
            var selfDepth = fn.EffectiveLoopDepth ?? fn.MaxLoopDepth ?? 0;
            fn.CallerLoopDepth = prefix;
            fn.TransitiveLoopDepth = prefix + selfDepth;
            updatedNodes.Add(Node.FromNode(fn));
        }

        if (updatedNodes.Count > 0)
        {
            for (var offset = 0; offset < updatedNodes.Count; offset += 10000)
            {
                var chunk = updatedNodes.GetRange(offset, Math.Min(10000, updatedNodes.Count - offset));
                await ctx.EnqueueUploadNodesAsync(chunk);
            }
        }
    }

    private static void CollectFunctions(
        IOntologyNode node,
        Dictionary<string, FunctionNode> functionsById,
        HashSet<IOntologyNode> visited)
    {
        if (!visited.Add(node)) return;

        if (node is FunctionNode fn)
        {
            functionsById.TryAdd(fn.Id, fn);
        }

        for (var i = 0; i < node.Children.Count; i++)
        {
            CollectFunctions(node.Children[i], functionsById, visited);
        }
    }

    private static (int[] SccId, List<List<int>> SccMembers) ComputeTarjanSccs(
        int n,
        List<(int To, int Weight)>?[] adj)
    {
        var index = 0;
        var indices = new int[n];
        var lowlink = new int[n];
        var onStack = new bool[n];
        var sccId = new int[n];
        Array.Fill(indices, -1);
        Array.Fill(sccId, -1);

        var tarjanStack = new Stack<int>();
        var callStack = new Stack<(int Node, int NextEdge)>();
        var sccMembers = new List<List<int>>();

        for (var start = 0; start < n; start++)
        {
            if (indices[start] != -1) continue;

            indices[start] = lowlink[start] = index++;
            tarjanStack.Push(start);
            onStack[start] = true;
            callStack.Push((start, 0));

            while (callStack.Count > 0)
            {
                var (u, edgeIdx) = callStack.Pop();
                var neighbors = adj[u];
                if (neighbors != null && edgeIdx < neighbors.Count)
                {
                    callStack.Push((u, edgeIdx + 1));
                    var v = neighbors[edgeIdx].To;
                    if (indices[v] == -1)
                    {
                        indices[v] = lowlink[v] = index++;
                        tarjanStack.Push(v);
                        onStack[v] = true;
                        callStack.Push((v, 0));
                    }
                    else if (onStack[v] && indices[v] < lowlink[u])
                    {
                        lowlink[u] = indices[v];
                    }
                }
                else
                {
                    if (lowlink[u] == indices[u])
                    {
                        var compIdx = sccMembers.Count;
                        var comp = new List<int>();
                        while (true)
                        {
                            var w = tarjanStack.Pop();
                            onStack[w] = false;
                            sccId[w] = compIdx;
                            comp.Add(w);
                            if (w == u) break;
                        }
                        sccMembers.Add(comp);
                    }

                    if (callStack.Count > 0)
                    {
                        var parent = callStack.Peek().Node;
                        if (lowlink[u] < lowlink[parent])
                        {
                            lowlink[parent] = lowlink[u];
                        }
                    }
                }
            }
        }

        return (sccId, sccMembers);
    }
}
