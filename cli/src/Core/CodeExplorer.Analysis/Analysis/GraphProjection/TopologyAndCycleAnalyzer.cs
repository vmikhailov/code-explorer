using System.Globalization;
using CodeExplorer.Core.Protocol;
using CodeExplorer.Cypher.Common;

namespace CodeExplorer.Core.Analysis.GraphProjection;

/// <summary>
/// Architectural tiers for both Project Map (C3) and Architecture Map (C2).
/// </summary>
public enum ArchitecturalTier
{
    /// <summary>Tier 0: Inbound entry points, HTTP endpoints, CLI commands, controllers.</summary>
    Ingress = 0,

    /// <summary>Tier 1: Application services, orchestrators, command handlers.</summary>
    Application = 1,

    /// <summary>Tier 2: Core domain models, business logic, engines.</summary>
    Core = 2,

    /// <summary>Tier 3: Data access, repositories, persistence adapters.</summary>
    DataAccess = 3,

    /// <summary>Tier 4: Foundation utilities, helpers, shared logging/telemetry.</summary>
    Foundation = 4,

    /// <summary>Tier 5: External message topics, message buses, external APIs, libraries.</summary>
    TransportsAndLibraries = 5,

    /// <summary>Tier 6: Relational/NoSQL databases, physical tables, storage.</summary>
    DatabasesAndStorage = 6
}

/// <summary>
/// Result of cycle detection and tier analysis.
/// </summary>
public sealed class TopologyAnalysisResult
{
    public required List<List<string>> Cycles { get; init; }
    public required Dictionary<string, ArchitecturalTier> NodeTiers { get; init; }
    public required Dictionary<string, string> TierReasons { get; init; }
    public required HashSet<string> CircularEdgeIds { get; init; }
}

/// <summary>
/// Performs Tarjan's SCC cycle detection and BFS topological tier classification over view graphs.
/// </summary>
public static class TopologyAndCycleAnalyzer
{
    public static readonly IReadOnlyList<(ArchitecturalTier Tier, string Label, string Description)> StandardTierDefinitions =
    [
        (ArchitecturalTier.Ingress, "EntryPoints & Endpoints", "CLI commands, HTTP endpoints, and controllers (Orbit 0)"),
        (ArchitecturalTier.Application, "Application & Handlers", "Application workflows, handlers, and orchestrators (Orbit 1)"),
        (ArchitecturalTier.Core, "Core Domain & Logic", "Domain models, business logic, and engines (Orbit 2)"),
        (ArchitecturalTier.DataAccess, "Data Access & Repositories", "Repositories, persistence layers, and queries (Orbit 3)"),
        (ArchitecturalTier.Foundation, "Foundation & Utilities", "Shared helpers, utilities, and common infrastructure (Orbit 4)"),
        (ArchitecturalTier.TransportsAndLibraries, "Transports & Libraries", "Message brokers, topics, external APIs, and libraries (Orbit 5)"),
        (ArchitecturalTier.DatabasesAndStorage, "Databases & Storage", "Databases, tables, procedures, and datasets (Orbit 6)")
    ];

    public static string GetTierLabel(ArchitecturalTier tier)
    {
        for (var i = 0; i < StandardTierDefinitions.Count; i++)
        {
            if (StandardTierDefinitions[i].Tier == tier)
                return StandardTierDefinitions[i].Label;
        }
        return tier.ToString();
    }

    /// <summary>
    /// Analyzes the directed graph formed by nodes and edges, finding circular cycles and classifying tiers.
    /// </summary>
    public static TopologyAnalysisResult Analyze(
        IReadOnlyList<GraphNodeDto> nodes,
        IReadOnlyList<GraphEdgeDto> edges)
    {
        var nodeIds = nodes.Select(n => n.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Build adjacency lists for code/container nodes (excluding resource/data nodes from code cycle detection)
        var adj = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var inDegree = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var outDegree = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var nid in nodeIds)
        {
            adj[nid] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            inDegree[nid] = 0;
            outDegree[nid] = 0;
        }

        var edgesByPair = new Dictionary<(string Source, string Target), List<GraphEdgeDto>>();

        foreach (var edge in edges)
        {
            if (!nodeIds.Contains(edge.Source) || !nodeIds.Contains(edge.Target)) continue;

            var key = (edge.Source, edge.Target);
            if (!edgesByPair.TryGetValue(key, out var list))
            {
                list = [];
                edgesByPair[key] = list;
            }
            list.Add(edge);

            if (!edge.Source.Equals(edge.Target, StringComparison.OrdinalIgnoreCase))
            {
                if (adj[edge.Source].Add(edge.Target))
                {
                    outDegree[edge.Source]++;
                    inDegree[edge.Target]++;
                }
            }
        }

        // 1. Tarjan SCC Cycle Detection
        var sccComponents = RunTarjanScc(nodeIds, adj);
        var cycles = new List<List<string>>();
        var circularEdgeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var nodeCycleGroup = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        var cycleIndex = 1;
        foreach (var comp in sccComponents)
        {
            if (comp.Count > 1)
            {
                cycles.Add(comp);
                var compSet = comp.ToHashSet(StringComparer.OrdinalIgnoreCase);
                foreach (var member in comp)
                {
                    nodeCycleGroup[member] = cycleIndex;
                }

                // Mark edges within SCC
                foreach (var u in comp)
                {
                    foreach (var v in adj[u])
                    {
                        if (compSet.Contains(v) && edgesByPair.TryGetValue((u, v), out var pairEdges))
                        {
                            foreach (var pe in pairEdges)
                            {
                                if (!string.IsNullOrEmpty(pe.Id))
                                {
                                    circularEdgeIds.Add(pe.Id);
                                }
                            }
                        }
                    }
                }
                cycleIndex++;
            }
        }

        // 2. Classify Tiers
        var nodeTiers = new Dictionary<string, ArchitecturalTier>(StringComparer.OrdinalIgnoreCase);
        var tierReasons = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Pre-classify external resource nodes
        var codeNodes = new List<GraphNodeDto>();
        foreach (var n in nodes)
        {
            if (IsDatabaseOrStorage(n))
            {
                nodeTiers[n.Id] = ArchitecturalTier.DatabasesAndStorage;
                tierReasons[n.Id] = "Database / Storage";
            }
            else if (IsTransportOrLibrary(n))
            {
                nodeTiers[n.Id] = ArchitecturalTier.TransportsAndLibraries;
                tierReasons[n.Id] = "Transport / Message Bus / External";
            }
            else
            {
                codeNodes.Add(n);
            }
        }

        // Identify Ingress roots among code nodes:
        // Entrypoint kinds, or nodes with inDegree == 0 (excluding isolated nodes), or matching controller/endpoint naming
        var ingressRoots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var n in codeNodes)
        {
            if (IsIngressNode(n, inDegree.GetValueOrDefault(n.Id), outDegree.GetValueOrDefault(n.Id)))
            {
                ingressRoots.Add(n.Id);
            }
        }

        // If no ingress root found and code nodes exist, pick node with smallest in-degree / highest out-degree
        if (ingressRoots.Count == 0 && codeNodes.Count > 0)
        {
            var bestCandidate = codeNodes
                .OrderBy(n => inDegree.GetValueOrDefault(n.Id))
                .ThenByDescending(n => outDegree.GetValueOrDefault(n.Id))
                .First();
            ingressRoots.Add(bestCandidate.Id);
        }

        // BFS traversal from ingress roots to assign BFS depths
        var bfsDepth = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<string>();

        foreach (var root in ingressRoots)
        {
            bfsDepth[root] = 0;
            nodeTiers[root] = ArchitecturalTier.Ingress;
            tierReasons[root] = "Ingress / EntryPoint";
            queue.Enqueue(root);
        }

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            var nextDepth = bfsDepth[current] + 1;

            foreach (var neighbor in adj[current])
            {
                if (nodeTiers.ContainsKey(neighbor)) continue; // Already assigned or resource

                bfsDepth[neighbor] = nextDepth;
                var tier = nextDepth switch
                {
                    1 => ArchitecturalTier.Application,
                    2 => ArchitecturalTier.Core,
                    3 => ArchitecturalTier.DataAccess,
                    _ => ArchitecturalTier.Foundation
                };

                nodeTiers[neighbor] = tier;
                tierReasons[neighbor] = $"BFS Depth {nextDepth} from {current}";
                queue.Enqueue(neighbor);
            }
        }

        // Any remaining unvisited code nodes fallback to Foundation or Core based on out-degree
        foreach (var n in codeNodes)
        {
            if (!nodeTiers.ContainsKey(n.Id))
            {
                if (outDegree.GetValueOrDefault(n.Id) == 0)
                {
                    nodeTiers[n.Id] = ArchitecturalTier.Foundation;
                    tierReasons[n.Id] = "Leaf node (no outgoing calls)";
                }
                else
                {
                    nodeTiers[n.Id] = ArchitecturalTier.Core;
                    tierReasons[n.Id] = "Disconnected component";
                }
            }
        }

        return new TopologyAnalysisResult
        {
            Cycles = cycles,
            NodeTiers = nodeTiers,
            TierReasons = tierReasons,
            CircularEdgeIds = circularEdgeIds
        };
    }

    private static bool IsIngressNode(GraphNodeDto n, int inDeg, int outDeg)
    {
        if (n.Kind is NodeKind.Endpoint or NodeKind.EntryPoint or NodeKind.App or NodeKind.CliTool)
            return true;

        var name = (n.Name ?? "").ToLowerInvariant();

        if (name.StartsWith("post ") || name.StartsWith("get ") || name.StartsWith("put ") || name.StartsWith("delete "))
            return true;

        // Controllers, endpoints and commands only serve as Ingress roots if nothing calls them (inDeg == 0)
        if (inDeg == 0 && (name.EndsWith("controller") || name.EndsWith("endpoint") || name.EndsWith("command") || name.EndsWith("api")))
            return true;

        // If it has 0 incoming calls and at least 1 outgoing call, it's an ingress orchestrator
        if (inDeg == 0 && outDeg > 0)
            return true;

        return false;
    }

    private static bool IsDatabaseOrStorage(GraphNodeDto n) =>
        n.Kind is NodeKind.Database or NodeKind.Table or NodeKind.Query or NodeKind.Procedure;

    private static bool IsTransportOrLibrary(GraphNodeDto n) =>
        n.Kind is NodeKind.Topic or NodeKind.ExternalService or NodeKind.CloudService or NodeKind.Library or NodeKind.Package;

    private static List<List<string>> RunTarjanScc(
        HashSet<string> vertices,
        Dictionary<string, HashSet<string>> adj)
    {
        var index = 0;
        var stack = new Stack<string>();
        var inStack = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var indices = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var lowlink = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var result = new List<List<string>>();

        void StrongConnect(string v)
        {
            indices[v] = index;
            lowlink[v] = index;
            index++;
            stack.Push(v);
            inStack.Add(v);

            if (adj.TryGetValue(v, out var neighbors))
            {
                foreach (var w in neighbors)
                {
                    if (!indices.ContainsKey(w))
                    {
                        StrongConnect(w);
                        lowlink[v] = Math.Min(lowlink[v], lowlink[w]);
                    }
                    else if (inStack.Contains(w))
                    {
                        lowlink[v] = Math.Min(lowlink[v], indices[w]);
                    }
                }
            }

            if (lowlink[v] == indices[v])
            {
                var component = new List<string>();
                string w;
                do
                {
                    w = stack.Pop();
                    inStack.Remove(w);
                    component.Add(w);
                } while (!w.Equals(v, StringComparison.OrdinalIgnoreCase));

                result.Add(component);
            }
        }

        foreach (var v in vertices)
        {
            if (!indices.ContainsKey(v))
            {
                StrongConnect(v);
            }
        }

        return result;
    }
}
