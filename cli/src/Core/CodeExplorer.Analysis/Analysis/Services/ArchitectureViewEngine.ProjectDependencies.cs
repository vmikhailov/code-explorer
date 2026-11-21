using CodeExplorer.Core.Protocol;
using CodeExplorer.Cypher.Common;

namespace CodeExplorer.Core.Analysis;

public partial class ArchitectureViewEngine
{
    private const string ProjDepsViewKey = "project-dependencies";
    private const string ProjDepsMetaView = "view";
    private const string ProjDepsMetaScope = "scope";
    private const string ProjDepsMetaCycleCount = "cycle_count";
    private const string ProjDepsMetaProjectCount = "project_count";
    private const string ProjDepsPropInCycle = "in_cycle";
    private const string ProjDepsPropCycleGroup = "cycle_group";
    private const string ProjDepsPropLanguage = "language";
    private const string ProjDepsPropEntityKind = "entity_kind";
    private const string ProjDepsPropIsLibrary = "is_library";
    private const string ProjDepsPropIsTest = "is_test";
    private const string ProjDepsPropVersion = "version";
    private const string ProjDepsPropEcosystem = "ecosystem";
    private const string ProjDepsBoolTrue = "true";
    private const string ProjDepsBoolFalse = "false";

    private const string QueryAllProjectsForDeps =
        "MATCH (p:Project) " +
        "RETURN p.id AS id, p.name AS name, coalesce(p.path, '') AS path, " +
        "coalesce(p.project_type, p.language, '') AS language, " +
        "coalesce(p.entity_kind, '') AS entity_kind, " +
        "coalesce(p.is_library, false) AS is_library, " +
        "coalesce(p.is_test, false) AS is_test";

    private const string QueryProjectToProjectDeps =
        "MATCH (src:Project)-[r:DEPENDS_ON|PROJECT_REFERENCE]->(tgt:Project) " +
        "WHERE src.id <> tgt.id " +
        "RETURN src.id AS srcId, tgt.id AS tgtId, type(r) AS relType";

    private const string QueryProjectToExternalPackages =
        "MATCH (src:Project)-[r:DEPENDS_ON]->(pkg:Package) " +
        "WHERE coalesce(pkg.is_external, true) = true " +
        "RETURN src.id AS srcId, pkg.id AS pkgId, pkg.name AS pkgName, " +
        "coalesce(pkg.version, '') AS version, coalesce(pkg.ecosystem, '') AS ecosystem";

    /// <summary>
    /// Builds the Modules -> Project Dependencies projection with Tarjan SCC cycle detection.
    /// </summary>
    public async Task<GraphDataDto> GetProjectDependenciesViewAsync(
        string? scope = null,
        bool includeExternalPackages = false,
        CancellationToken ct = default)
    {
        var projRes = await db.ExecuteCypherAsync(QueryAllProjectsForDeps, null, ct);
        var relRes = await db.ExecuteCypherAsync(QueryProjectToProjectDeps, null, ct);

        var nodesById = new Dictionary<string, GraphNodeDto>(StringComparer.Ordinal);
        var adjacency = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

        foreach (var row in projRes.Rows)
        {
            var id = row.GetStringProp("id");
            var name = row.GetStringProp("name");
            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(name)) continue;

            var entityKind = row.GetStringProp(ProjDepsPropEntityKind);
            var isLib = row.GetStringProp(ProjDepsPropIsLibrary).Equals(ProjDepsBoolTrue, StringComparison.OrdinalIgnoreCase);
            var isTest = row.GetStringProp(ProjDepsPropIsTest).Equals(ProjDepsBoolTrue, StringComparison.OrdinalIgnoreCase);
            var nodeKind = MapProjectEntityKindToNodeKind(entityKind, isLib, isTest);

            nodesById[id] = new GraphNodeDto
            {
                Id = id,
                Kind = nodeKind,
                Name = name,
                DisplayName = name,
                FilePath = row.GetStringProp("path"),
                Properties = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [ProjDepsPropLanguage] = row.GetStringProp(ProjDepsPropLanguage),
                    [ProjDepsPropEntityKind] = entityKind,
                    [ProjDepsPropIsLibrary] = isLib ? ProjDepsBoolTrue : ProjDepsBoolFalse,
                    [ProjDepsPropIsTest] = isTest ? ProjDepsBoolTrue : ProjDepsBoolFalse
                }
            };
            adjacency[id] = new HashSet<string>(StringComparer.Ordinal);
        }

        var edges = new List<GraphEdgeDto>();
        var edgeKeys = new HashSet<(string Src, string Tgt, RelationshipKind Kind)>();

        foreach (var row in relRes.Rows)
        {
            var srcId = row.GetStringProp("srcId");
            var tgtId = row.GetStringProp("tgtId");
            if (!nodesById.ContainsKey(srcId) || !nodesById.ContainsKey(tgtId)) continue;

            var relKind = EnumExtensions.ParseRelationshipKind(row.GetStringProp("relType"));
            if (!edgeKeys.Add((srcId, tgtId, relKind))) continue;

            adjacency[srcId].Add(tgtId);
            edges.Add(new GraphEdgeDto
            {
                Id = $"{srcId}->{tgtId}:{relKind}",
                Source = srcId,
                Target = tgtId,
                Kind = relKind,
                Category = GraphEdgeCategory.Library,
                Properties = new Dictionary<string, string>(StringComparer.Ordinal)
            });
        }

        // Tarjan strongly connected components to highlight circular project dependencies
        var cycles = FindStronglyConnectedCycles(adjacency);
        var cycleNodeToGroup = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 0; i < cycles.Count; i++)
        {
            var groupLabel = $"cycle-{i + 1}";
            foreach (var nodeId in cycles[i])
            {
                cycleNodeToGroup[nodeId] = groupLabel;
                if (nodesById.TryGetValue(nodeId, out var node) && node.Properties != null)
                {
                    node.Properties[ProjDepsPropInCycle] = ProjDepsBoolTrue;
                    node.Properties[ProjDepsPropCycleGroup] = groupLabel;
                }
            }
        }

        foreach (var edge in edges)
        {
            if (cycleNodeToGroup.TryGetValue(edge.Source, out var srcGroup) &&
                cycleNodeToGroup.TryGetValue(edge.Target, out var tgtGroup) &&
                string.Equals(srcGroup, tgtGroup, StringComparison.Ordinal))
            {
                edge.Properties ??= new Dictionary<string, string>(StringComparer.Ordinal);
                edge.Properties[ProjDepsPropInCycle] = ProjDepsBoolTrue;
                edge.Properties[ProjDepsPropCycleGroup] = srcGroup;
            }
        }

        // Optional scope filter: keep focused project + direct 1-hop upstream/downstream neighbors
        if (!string.IsNullOrWhiteSpace(scope))
        {
            var trimmedScope = scope.Trim();
            var matchedIds = nodesById.Values
                .Where(n => string.Equals(n.Id, trimmedScope, StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(n.Name, trimmedScope, StringComparison.OrdinalIgnoreCase))
                .Select(n => n.Id)
                .ToHashSet(StringComparer.Ordinal);

            if (matchedIds.Count > 0)
            {
                var keepIds = new HashSet<string>(matchedIds, StringComparer.Ordinal);
                foreach (var edge in edges)
                {
                    if (matchedIds.Contains(edge.Source)) keepIds.Add(edge.Target);
                    if (matchedIds.Contains(edge.Target)) keepIds.Add(edge.Source);
                }

                foreach (var id in nodesById.Keys.Where(k => !keepIds.Contains(k)).ToList())
                {
                    nodesById.Remove(id);
                }
                edges = edges.Where(e => keepIds.Contains(e.Source) && keepIds.Contains(e.Target)).ToList();
            }
        }

        if (includeExternalPackages)
        {
            var pkgRes = await db.ExecuteCypherAsync(QueryProjectToExternalPackages, null, ct);
            foreach (var row in pkgRes.Rows)
            {
                var srcId = row.GetStringProp("srcId");
                var pkgId = row.GetStringProp("pkgId");
                var pkgName = row.GetStringProp("pkgName");
                if (!nodesById.ContainsKey(srcId) || string.IsNullOrEmpty(pkgId) || string.IsNullOrEmpty(pkgName))
                {
                    continue;
                }

                if (!nodesById.ContainsKey(pkgId))
                {
                    nodesById[pkgId] = new GraphNodeDto
                    {
                        Id = pkgId,
                        Kind = NodeKind.Package,
                        Name = pkgName,
                        DisplayName = pkgName,
                        Properties = new Dictionary<string, string>(StringComparer.Ordinal)
                        {
                            [ProjDepsPropVersion] = row.GetStringProp(ProjDepsPropVersion),
                            [ProjDepsPropEcosystem] = row.GetStringProp(ProjDepsPropEcosystem)
                        }
                    };
                }

                if (edgeKeys.Add((srcId, pkgId, RelationshipKind.DependsOn)))
                {
                    edges.Add(new GraphEdgeDto
                    {
                        Id = $"{srcId}->{pkgId}:DependsOn",
                        Source = srcId,
                        Target = pkgId,
                        Kind = RelationshipKind.DependsOn,
                        Category = GraphEdgeCategory.Library
                    });
                }
            }
        }

        return new GraphDataDto
        {
            Nodes = [.. nodesById.Values.OrderBy(n => n.Name, StringComparer.OrdinalIgnoreCase)],
            Edges = edges,
            Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [ProjDepsMetaView] = ProjDepsViewKey,
                [ProjDepsMetaScope] = scope ?? string.Empty,
                [ProjDepsMetaCycleCount] = cycles.Count.ToString(),
                [ProjDepsMetaProjectCount] = projRes.Count.ToString()
            }
        };
    }

    private static NodeKind MapProjectEntityKindToNodeKind(string entityKind, bool isLibrary, bool isTest)
    {
        if (isTest || string.Equals(entityKind, "Test", StringComparison.OrdinalIgnoreCase)) return NodeKind.TestSuite;
        if (string.Equals(entityKind, "Service", StringComparison.OrdinalIgnoreCase)) return NodeKind.Service;
        if (string.Equals(entityKind, "App", StringComparison.OrdinalIgnoreCase)) return NodeKind.App;
        if (string.Equals(entityKind, "Worker", StringComparison.OrdinalIgnoreCase)) return NodeKind.Worker;
        if (string.Equals(entityKind, "CliTool", StringComparison.OrdinalIgnoreCase)) return NodeKind.CliTool;
        if (isLibrary || string.Equals(entityKind, "Library", StringComparison.OrdinalIgnoreCase)) return NodeKind.Library;
        return NodeKind.Project;
    }

    private static List<List<string>> FindStronglyConnectedCycles(Dictionary<string, HashSet<string>> adjacency)
    {
        var index = 0;
        var indices = new Dictionary<string, int>(StringComparer.Ordinal);
        var lowLink = new Dictionary<string, int>(StringComparer.Ordinal);
        var onStack = new HashSet<string>(StringComparer.Ordinal);
        var stack = new Stack<string>();
        var cycles = new List<List<string>>();

        void StrongConnect(string v)
        {
            indices[v] = index;
            lowLink[v] = index;
            index++;
            stack.Push(v);
            onStack.Add(v);

            if (adjacency.TryGetValue(v, out var neighbors))
            {
                foreach (var w in neighbors)
                {
                    if (!indices.ContainsKey(w))
                    {
                        StrongConnect(w);
                        lowLink[v] = Math.Min(lowLink[v], lowLink[w]);
                    }
                    else if (onStack.Contains(w))
                    {
                        lowLink[v] = Math.Min(lowLink[v], indices[w]);
                    }
                }
            }

            if (lowLink[v] == indices[v])
            {
                var component = new List<string>();
                while (true)
                {
                    var w = stack.Pop();
                    onStack.Remove(w);
                    component.Add(w);
                    if (string.Equals(w, v, StringComparison.Ordinal)) break;
                }

                if (component.Count > 1)
                {
                    cycles.Add(component);
                }
            }
        }

        foreach (var node in adjacency.Keys)
        {
            if (!indices.ContainsKey(node))
            {
                StrongConnect(node);
            }
        }

        return cycles;
    }
}
