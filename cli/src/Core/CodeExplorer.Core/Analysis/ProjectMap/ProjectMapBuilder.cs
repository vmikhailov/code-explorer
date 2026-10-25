namespace CodeExplorer.Core.Analysis.ProjectMap;

/// <summary>
/// Builds the Project Map (C3 internal module view) from already-scoped inputs:
/// folder partitioning → symbol-edge lifting → resource links → topology-first tiers → SCC cycle detection.
/// Pure and deterministic; nothing is written back to the graph.
/// </summary>
public static class ProjectMapBuilder
{
    public const int MaxSamplesPerEdge = 8;
    public const int MaxResources = 40;

    private static readonly HashSet<string> DataResourceKinds = new(StringComparer.OrdinalIgnoreCase)
    {
        "Table", "Database", "Query", "DataSet", "Procedure"
    };

    public static ProjectMapResult Build(ProjectMapInput input)
    {
        var sourceFiles = input.Files
            .Where(f => !f.IsTest && !ProjectMapPartitioner.IsIgnoredProjectMapFile(f.Path))
            .ToList();
        if (input.ExcludedSubpaths.Count > 0)
        {
            var normProjPrefix = (input.ProjectPath ?? "").Replace('\\', '/').Trim('/');
            var fullExcludedPrefixes = input.ExcludedSubpaths
                .Select(ex => string.IsNullOrEmpty(normProjPrefix)
                    ? ex.Replace('\\', '/').Trim('/') + "/"
                    : normProjPrefix + "/" + ex.Replace('\\', '/').Trim('/') + "/")
                .ToList();
            sourceFiles = sourceFiles
                .Where(f => !fullExcludedPrefixes.Any(pre => f.Path.Replace('\\', '/').TrimStart('/').StartsWith(pre, StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }

        if (input.IsBuildOrchestrator || input.SubProjects.Count > 0)
        {
            var nonBuildFiles = sourceFiles.Where(f => !ProjectMapPartitioner.IsBuildToolScript(f.Path)).ToList();
            if (nonBuildFiles.Count > 0 || input.SubProjects.Count > 0)
            {
                sourceFiles = nonBuildFiles;
            }
        }

        if (sourceFiles.Count == 0 && input.SubProjects.Count == 0)
        {
            sourceFiles = input.Files.Where(f => !f.IsTest).ToList();
        }

        var partition = sourceFiles.Count > 0
            ? ProjectMapPartitioner.Partition(input.ProjectPath ?? "", sourceFiles)
            : new ProjectMapPartitioner.PartitionResult("", ProjectMapGranularity.Module, [], new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

        var result = new ProjectMapResult
        {
            Input = input,
            SourceRoot = partition.SourceRoot,
            Granularity = partition.Granularity,
            TestFilesExcluded = input.Files.Count - input.Files.Count(f => !f.IsTest)
        };
        result.Modules.AddRange(partition.Modules);

        var fileToModule = new Dictionary<string, string>(partition.FileToModule, StringComparer.OrdinalIgnoreCase);
        foreach (var sub in input.SubProjects)
        {
            var subNonTest = sub.Files.Where(f => !f.IsTest && !ProjectMapPartitioner.IsIgnoredProjectMapFile(f.Path)).ToList();
            var subPath = sub.Path.Replace('\\', '/').Trim('/');
            var subModId = $"pm:sub:{sub.Name}";
            var subMod = new ProjectMapModule
            {
                Id = subModId,
                Name = sub.Name,
                Path = subPath,
                IsSubProject = true,
                SubProjectId = sub.ProjectId,
                SubProjectKind = sub.Kind,
                LineCount = subNonTest.Sum(f => f.LineCount ?? 0)
            };
            foreach (var sf in subNonTest)
            {
                subMod.Files.Add(sf.Path);
                fileToModule.TryAdd(sf.Path, subModId);
            }
            result.Modules.Add(subMod);
        }

        LiftSymbolEdges(result, input.SymbolEdges, fileToModule);
        LinkResources(result, input.ResourceLinks, fileToModule);
        ApplyEntryPoints(result, input.EntryPointsByFile, fileToModule);
        ProjectMapClassifier.Classify(result);
        DetectCycles(result);

        return result;
    }

    private static void LiftSymbolEdges(
        ProjectMapResult result,
        IReadOnlyList<ProjectMapSymbolEdge> symbolEdges,
        Dictionary<string, string> fileToModule)
    {
        var edges = new Dictionary<(string, string), ProjectMapEdge>();

        foreach (var e in symbolEdges)
        {
            if (!fileToModule.TryGetValue(e.FromFile, out var from) || !fileToModule.TryGetValue(e.ToFile, out var to)) continue;
            if (string.Equals(from, to, StringComparison.OrdinalIgnoreCase)) continue;

            if (!edges.TryGetValue((from, to), out var edge))
            {
                edge = new ProjectMapEdge { SourceId = from, TargetId = to };
                edges[(from, to)] = edge;
            }

            Accumulate(edge, e.Kind, e.FromFile, e.ToFile);
            if (edge.Samples.Count < MaxSamplesPerEdge)
            {
                edge.Samples.Add(new ProjectMapEvidenceSample(e.FromSymbol, e.ToSymbol, e.FromFile, e.FromLine, e.Kind));
            }
        }

        result.Edges.AddRange(edges.Values
            .OrderByDescending(e => e.Count)
            .ThenBy(e => e.SourceId, StringComparer.Ordinal)
            .ThenBy(e => e.TargetId, StringComparer.Ordinal));
    }

    private static void LinkResources(
        ProjectMapResult result,
        IReadOnlyList<ProjectMapResourceLink> links,
        Dictionary<string, string> fileToModule)
    {
        var scoped = links
            .Where(l => fileToModule.ContainsKey(l.File) && !string.IsNullOrWhiteSpace(l.ResourceId))
            .ToList();

        var topResources = scoped
            .GroupBy(l => l.ResourceId, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key, StringComparer.Ordinal)
            .Take(MaxResources)
            .ToList();

        var edges = new Dictionary<(string, string), ProjectMapEdge>();
        var dataFiles = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in topResources)
        {
            var first = group.First();
            result.Resources.Add(new ProjectMapResource { Id = first.ResourceId, Kind = first.ResourceKind, Name = first.ResourceName });

            foreach (var link in group)
            {
                var moduleId = fileToModule[link.File];
                if (!edges.TryGetValue((moduleId, link.ResourceId), out var edge))
                {
                    edge = new ProjectMapEdge { SourceId = moduleId, TargetId = link.ResourceId, IsResourceLink = true };
                    edges[(moduleId, link.ResourceId)] = edge;
                }

                Accumulate(edge, link.Kind, link.File, null);
                if (edge.Samples.Count < MaxSamplesPerEdge)
                {
                    edge.Samples.Add(new ProjectMapEvidenceSample(link.Symbol, link.ResourceName, link.File, link.Line, link.Kind));
                }

                if (DataResourceKinds.Contains(link.ResourceKind))
                {
                    if (!dataFiles.TryGetValue(moduleId, out var set))
                    {
                        set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        dataFiles[moduleId] = set;
                    }
                    set.Add(link.File);
                }
            }
        }

        foreach (var m in result.Modules)
        {
            if (dataFiles.TryGetValue(m.Id, out var files))
            {
                m.TouchesData = true;
                m.DataFileCount = files.Count;
            }
        }

        result.Edges.AddRange(edges.Values.OrderByDescending(e => e.Count).ThenBy(e => e.TargetId, StringComparer.Ordinal));
    }

    private static void ApplyEntryPoints(
        ProjectMapResult result,
        IReadOnlyDictionary<string, int> entryPointsByFile,
        Dictionary<string, string> fileToModule)
    {
        var byId = result.Modules.ToDictionary(m => m.Id, StringComparer.OrdinalIgnoreCase);
        foreach (var (file, count) in entryPointsByFile)
        {
            if (fileToModule.TryGetValue(file, out var moduleId) && byId.TryGetValue(moduleId, out var module))
            {
                module.EntryPointCount += count;
            }
        }
    }

    private static void Accumulate(ProjectMapEdge edge, string kind, string fromFile, string? toFile)
    {
        edge.Count++;
        edge.Files.Add(fromFile);
        if (toFile != null) edge.Files.Add(toFile);
        var k = string.IsNullOrWhiteSpace(kind) ? "RELATED" : kind.ToUpperInvariant();
        edge.Kinds[k] = edge.Kinds.GetValueOrDefault(k) + 1;
    }

    /// <summary>
    /// Tarjan's strongly connected components over module→module edges.
    /// Every SCC with ≥ 2 modules is a dependency cycle; all edges inside it are marked circular.
    /// </summary>
    private static void DetectCycles(ProjectMapResult result)
    {
        var moduleIds = result.Modules.Select(m => m.Id).ToList();
        var adjacency = moduleIds.ToDictionary(id => id, _ => new List<string>(), StringComparer.OrdinalIgnoreCase);
        foreach (var e in result.Edges.Where(e => !e.IsResourceLink))
        {
            if (adjacency.TryGetValue(e.SourceId, out var list)) list.Add(e.TargetId);
        }

        var sccs = TarjanScc.Find(moduleIds, adjacency)
            .Where(c => c.Count >= 2)
            .Select(c => c.OrderBy(x => x, StringComparer.Ordinal).ToList())
            .OrderByDescending(c => c.Count)
            .ThenBy(c => c[0], StringComparer.Ordinal)
            .ToList();

        var groupOf = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < sccs.Count; i++)
        {
            foreach (var id in sccs[i]) groupOf[id] = i + 1;
            result.Cycles.Add(sccs[i]);
        }

        foreach (var m in result.Modules)
        {
            if (groupOf.TryGetValue(m.Id, out var g)) m.CycleGroup = g;
        }

        foreach (var e in result.Edges.Where(e => !e.IsResourceLink))
        {
            e.IsCircular = groupOf.TryGetValue(e.SourceId, out var gs) && groupOf.TryGetValue(e.TargetId, out var gt) && gs == gt;
        }
    }
}

/// <summary>
/// Iterative Tarjan SCC (no recursion, safe for large graphs).
/// </summary>
internal static class TarjanScc
{
    public static List<List<string>> Find(IReadOnlyList<string> nodes, Dictionary<string, List<string>> adjacency)
    {
        var index = 0;
        var indices = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var lowLink = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var onStack = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var stack = new Stack<string>();
        var result = new List<List<string>>();

        foreach (var start in nodes)
        {
            if (indices.ContainsKey(start)) continue;

            var work = new Stack<(string Node, int NextChild)>();
            work.Push((start, 0));

            while (work.Count > 0)
            {
                var (node, childIdx) = work.Pop();
                if (childIdx == 0)
                {
                    indices[node] = lowLink[node] = index++;
                    stack.Push(node);
                    onStack.Add(node);
                }

                var children = adjacency.TryGetValue(node, out var c) ? c : [];
                var descended = false;
                for (var i = childIdx; i < children.Count; i++)
                {
                    var child = children[i];
                    if (!adjacency.ContainsKey(child)) continue;

                    if (!indices.ContainsKey(child))
                    {
                        work.Push((node, i + 1));
                        work.Push((child, 0));
                        descended = true;
                        break;
                    }

                    if (onStack.Contains(child)) lowLink[node] = Math.Min(lowLink[node], indices[child]);
                }

                if (descended) continue;

                if (lowLink[node] == indices[node])
                {
                    var component = new List<string>();
                    string member;
                    do
                    {
                        member = stack.Pop();
                        onStack.Remove(member);
                        component.Add(member);
                    } while (!string.Equals(member, node, StringComparison.OrdinalIgnoreCase));
                    result.Add(component);
                }

                if (work.Count > 0)
                {
                    var parent = work.Peek().Node;
                    lowLink[parent] = Math.Min(lowLink[parent], lowLink[node]);
                }
            }
        }

        return result;
    }
}
