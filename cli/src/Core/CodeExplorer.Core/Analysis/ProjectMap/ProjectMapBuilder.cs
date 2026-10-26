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
    public const int MaxClasses = 80;
    public const int MaxEntryPointNodes = 32;

    private static readonly HashSet<string> DataResourceKinds = new(StringComparer.OrdinalIgnoreCase)
    {
        "Table", "Database", "Query", "DataSet", "Procedure"
    };

    private static readonly HashSet<string> IgnoredTypeSubKinds = new(StringComparer.OrdinalIgnoreCase)
    {
        "enum", "delegate", "namespace"
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

        var isPureSubProjectOrchestrator = input.IsBuildOrchestrator && input.SubProjects.Count > 0 && sourceFiles.Count == 0;
        if (!isPureSubProjectOrchestrator && (input.Types.Count > 0 || input.Functions.Count > 0 || input.EntryPointNodes.Count > 0))
        {
            var classResult = TryBuildClassMap(input, sourceFiles);
            if (classResult != null && classResult.Modules.Count > 0)
            {
                return classResult;
            }
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
                NodeKindOverride = string.IsNullOrEmpty(sub.Kind) ? "Service" : sub.Kind,
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

    private sealed class ClassCandidate
    {
        public required string Id { get; init; }
        public required string Name { get; init; }
        public required string SubKind { get; set; }
        public required string PrimaryFile { get; set; }
        public int? StartLine { get; set; }
        public int LineCount { get; set; }
        public HashSet<string> Files { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, ProjectMapMethod> Methods { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> ImplementedInterfaces { get; } = new(StringComparer.OrdinalIgnoreCase);
        public int EntryPointCount { get; set; }
        public int ExternalCallersCount { get; set; }
        public bool TouchesData { get; set; }
        public int DataFileCount { get; set; }
        public int ResourceLinkCount { get; set; }
        public int OutgoingCallCount { get; set; }
        public int IncomingCallCount { get; set; }
    }

    private static ProjectMapResult? TryBuildClassMap(ProjectMapInput input, List<ProjectMapFile> sourceFiles)
    {
        var allowedFiles = new HashSet<string>(sourceFiles.Select(f => f.Path), StringComparer.OrdinalIgnoreCase);
        var fileLineCounts = sourceFiles.ToDictionary(f => f.Path, f => f.LineCount ?? 0, StringComparer.OrdinalIgnoreCase);

        bool IsAllowedFile(string path) =>
            !string.IsNullOrWhiteSpace(path) &&
            !ProjectMapPartitioner.IsIgnoredProjectMapFile(path) &&
            (allowedFiles.Count == 0 || allowedFiles.Contains(path));

        var interfaces = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var classes = new Dictionary<string, ClassCandidate>(StringComparer.OrdinalIgnoreCase);
        var fileToClasses = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var t in input.Types)
        {
            if (t.IsTest || !IsAllowedFile(t.FilePath)) continue;
            var cleanName = StripGenericArity(t.Name);
            if (string.IsNullOrWhiteSpace(cleanName)) continue;

            var subKind = (t.SubKind ?? "class").Trim().ToLowerInvariant();
            if (IgnoredTypeSubKinds.Contains(subKind)) continue;

            if (subKind == "interface")
            {
                interfaces.Add(cleanName);
                continue;
            }

            if (!classes.TryGetValue(cleanName, out var cand))
            {
                cand = new ClassCandidate
                {
                    Id = $"pm:cls:{cleanName}",
                    Name = cleanName,
                    SubKind = subKind,
                    PrimaryFile = t.FilePath,
                    StartLine = t.StartLine,
                    LineCount = t.LineCount ?? 0
                };
                classes[cleanName] = cand;
            }
            else
            {
                cand.LineCount += t.LineCount ?? 0;
                if (cand.StartLine == null && t.StartLine != null)
                {
                    cand.StartLine = t.StartLine;
                    cand.PrimaryFile = t.FilePath;
                }
            }

            cand.Files.Add(t.FilePath);
            if (t.Methods != null)
            {
                foreach (var m in t.Methods)
                {
                    if (!IsConstructorOrNoiseMethod(m.Name))
                    {
                        cand.Methods.TryAdd(m.Name, m);
                    }
                }
            }

            if (!fileToClasses.TryGetValue(t.FilePath, out var fList))
            {
                fList = [];
                fileToClasses[t.FilePath] = fList;
            }
            if (!fList.Contains(cleanName, StringComparer.OrdinalIgnoreCase))
            {
                fList.Add(cleanName);
            }
        }

        // Attach functions to enclosing class, primary class in file, or synthesize file-level functional module
        var fileToPrimaryClass = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (file, clsNames) in fileToClasses)
        {
            var primary = clsNames
                .OrderByDescending(n => classes[n].Methods.Count)
                .ThenByDescending(n => classes[n].LineCount)
                .First();
            fileToPrimaryClass[file] = primary;
        }

        foreach (var fn in input.Functions)
        {
            if (fn.IsTest || !IsAllowedFile(fn.FilePath)) continue;
            if (IsConstructorOrNoiseMethod(fn.Name)) continue;

            var encName = StripGenericArity(fn.EnclosingTypeName ?? "");
            if (!string.IsNullOrEmpty(encName) && classes.TryGetValue(encName, out var encCand))
            {
                encCand.Methods[fn.Name] = new ProjectMapMethod(fn.Name, fn.FilePath, fn.StartLine);
                encCand.Files.Add(fn.FilePath);
                continue;
            }

            if (fileToPrimaryClass.TryGetValue(fn.FilePath, out var primaryInFile) &&
                classes.TryGetValue(primaryInFile, out var fileCand))
            {
                fileCand.Methods[fn.Name] = new ProjectMapMethod(fn.Name, fn.FilePath, fn.StartLine);
                continue;
            }

            // Synthesize file-level functional module (e.g. TypeScript/React/Go/Python file with top-level functions)
            var moduleName = DeriveFileModuleName(fn.FilePath, classes.ContainsKey);
            if (!classes.TryGetValue(moduleName, out var modCand))
            {
                modCand = new ClassCandidate
                {
                    Id = $"pm:cls:{moduleName}",
                    Name = moduleName,
                    SubKind = "module",
                    PrimaryFile = fn.FilePath,
                    StartLine = fn.StartLine ?? 1,
                    LineCount = fileLineCounts.GetValueOrDefault(fn.FilePath, fn.LineCount ?? 0)
                };
                classes[moduleName] = modCand;
                fileToPrimaryClass[fn.FilePath] = moduleName;
            }
            modCand.Files.Add(fn.FilePath);
            modCand.Methods[fn.Name] = new ProjectMapMethod(fn.Name, fn.FilePath, fn.StartLine);
        }

        // Ensure files with top-level entrypoints, resource links, or symbol calls (e.g. JS/TS workers without named classes/functions) get a module candidate
        void EnsureFileModuleCandidate(string? filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !IsAllowedFile(filePath)) return;
            if (fileToPrimaryClass.ContainsKey(filePath)) return;
            var moduleName = DeriveFileModuleName(filePath, classes.ContainsKey);
            if (!classes.TryGetValue(moduleName, out var modCand))
            {
                modCand = new ClassCandidate
                {
                    Id = $"pm:cls:{moduleName}",
                    Name = moduleName,
                    SubKind = "module",
                    PrimaryFile = filePath,
                    StartLine = 1,
                    LineCount = fileLineCounts.GetValueOrDefault(filePath, 1)
                };
                classes[moduleName] = modCand;
            }
            modCand.Files.Add(filePath);
            fileToPrimaryClass[filePath] = moduleName;
        }

        foreach (var ep in input.EntryPointNodes)
        {
            EnsureFileModuleCandidate(ep.FilePath);
            if (ep.TargetFiles != null)
            {
                foreach (var tf in ep.TargetFiles) EnsureFileModuleCandidate(tf);
            }
        }
        foreach (var (epFile, _) in input.EntryPointsByFile)
        {
            EnsureFileModuleCandidate(epFile);
        }
        foreach (var rl in input.ResourceLinks)
        {
            EnsureFileModuleCandidate(rl.File);
        }
        foreach (var se in input.SymbolEdges)
        {
            EnsureFileModuleCandidate(se.FromFile);
            EnsureFileModuleCandidate(se.ToFile);
        }

        // Backfill line counts from fileLineCounts when type declarations did not carry line_count
        foreach (var cand in classes.Values)
        {
            if (cand.LineCount == 0)
            {
                cand.LineCount = cand.Files.Sum(f => fileLineCounts.GetValueOrDefault(f, 0));
            }
        }

        if (classes.Count == 0) return null;

        // Build interface -> concrete class implementations map
        var interfaceToImpls = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        void RegisterImplementation(string concreteClass, string ifaceName)
        {
            if (!classes.TryGetValue(concreteClass, out var c)) return;
            c.ImplementedInterfaces.Add(ifaceName);
            if (!interfaceToImpls.TryGetValue(ifaceName, out var list))
            {
                list = [];
                interfaceToImpls[ifaceName] = list;
            }
            if (!list.Contains(concreteClass, StringComparer.OrdinalIgnoreCase))
            {
                list.Add(concreteClass);
            }
        }

        foreach (var te in input.TypeEdges)
        {
            if (!te.Kind.Equals("IMPLEMENTS", StringComparison.OrdinalIgnoreCase) &&
                !te.Kind.Equals("INHERITS_FROM", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            var fromName = StripGenericArity(te.FromSymbol);
            var toName = StripGenericArity(te.ToSymbol);
            if (string.IsNullOrEmpty(fromName) || string.IsNullOrEmpty(toName)) continue;

            if (!classes.ContainsKey(fromName) && !string.IsNullOrEmpty(te.FromFile) &&
                fileToPrimaryClass.TryGetValue(te.FromFile, out var pc))
            {
                fromName = pc;
            }
            if (string.IsNullOrEmpty(fromName) || !classes.ContainsKey(fromName)) continue;

            if (interfaces.Contains(toName) || (!classes.ContainsKey(toName) && LooksLikeInterfaceName(toName)))
            {
                RegisterImplementation(fromName, toName);
            }
        }

        foreach (var se in input.SymbolEdges)
        {
            if (!se.Kind.Equals("IMPLEMENTS", StringComparison.OrdinalIgnoreCase) &&
                !se.Kind.Equals("INHERITS_FROM", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            var fromName = StripGenericArity(se.FromSymbol ?? "");
            var toName = StripGenericArity(se.ToSymbol ?? "");
            if (!classes.ContainsKey(fromName) && !string.IsNullOrEmpty(se.FromFile))
            {
                fileToPrimaryClass.TryGetValue(se.FromFile, out fromName!);
            }
            if (string.IsNullOrEmpty(fromName) || string.IsNullOrEmpty(toName) || !classes.ContainsKey(fromName)) continue;

            if (interfaces.Contains(toName) || (!classes.ContainsKey(toName) && LooksLikeInterfaceName(toName)))
            {
                RegisterImplementation(fromName, toName);
            }
        }

        string? ResolveSourceClass(string? symbol, string? file)
        {
            var clean = StripGenericArity(symbol ?? "");
            if (!string.IsNullOrEmpty(clean) && classes.TryGetValue(clean, out var c))
            {
                if (string.IsNullOrEmpty(file) || c.Files.Contains(file)) return c.Name;
            }
            if (!string.IsNullOrEmpty(file) && fileToPrimaryClass.TryGetValue(file, out var primary) && classes.TryGetValue(primary, out var pc))
            {
                return pc.Name;
            }
            if (!string.IsNullOrEmpty(clean) && classes.TryGetValue(clean, out var fallback))
            {
                return fallback.Name;
            }
            return null;
        }

        IReadOnlyList<string> ResolveTargetClasses(string? symbol, string? file)
        {
            var clean = StripGenericArity(symbol ?? "");
            if (!string.IsNullOrEmpty(clean))
            {
                if (classes.TryGetValue(clean, out var c) && (string.IsNullOrEmpty(file) || c.Files.Contains(file))) return [c.Name];
                if (interfaceToImpls.TryGetValue(clean, out var impls) && impls.Count > 0) return impls;
            }
            if (!string.IsNullOrEmpty(file) && fileToPrimaryClass.TryGetValue(file, out var primary) && classes.TryGetValue(primary, out var pc))
            {
                return [pc.Name];
            }
            if (!string.IsNullOrEmpty(clean) && classes.TryGetValue(clean, out var fallback))
            {
                return [fallback.Name];
            }
            return [];
        }

        // Collect raw class -> class edges and external caller counts
        var rawClassEdges = new Dictionary<(string Src, string Tgt), ProjectMapEdge>();
        var inboundReferencers = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        void AddClassEdge(string srcName, string tgtName, string kind, string fromFile, string? toFile, string? fromSym, string? toSym, int? line)
        {
            if (string.Equals(srcName, tgtName, StringComparison.OrdinalIgnoreCase)) return;
            var srcCand = classes[srcName];
            var tgtCand = classes[tgtName];

            // Avoid noise where a pure DTO/record with 0 methods has a USES_TYPE property pointing to a service
            if (kind.Equals("USES_TYPE", StringComparison.OrdinalIgnoreCase) && srcCand.Methods.Count == 0)
            {
                return;
            }

            if (!inboundReferencers.TryGetValue(tgtCand.Name, out var refs))
            {
                refs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                inboundReferencers[tgtCand.Name] = refs;
            }
            refs.Add(srcCand.Name);

            if (!rawClassEdges.TryGetValue((srcCand.Id, tgtCand.Id), out var edge))
            {
                edge = new ProjectMapEdge { SourceId = srcCand.Id, TargetId = tgtCand.Id };
                rawClassEdges[(srcCand.Id, tgtCand.Id)] = edge;
            }
            Accumulate(edge, kind, fromFile, toFile);
            if (edge.Samples.Count < MaxSamplesPerEdge)
            {
                edge.Samples.Add(new ProjectMapEvidenceSample(fromSym ?? srcCand.Name, toSym ?? tgtCand.Name, fromFile, line, kind));
            }
            if (!kind.Equals("USES_TYPE", StringComparison.OrdinalIgnoreCase))
            {
                srcCand.OutgoingCallCount++;
                tgtCand.IncomingCallCount++;
            }
        }

        foreach (var te in input.TypeEdges)
        {
            if (te.IsExternalCaller)
            {
                foreach (var tgt in ResolveTargetClasses(te.ToSymbol, te.ToFile))
                {
                    classes[tgt].ExternalCallersCount++;
                }
                continue;
            }

            var src = ResolveSourceClass(te.FromSymbol, te.FromFile);
            if (src == null) continue;
            foreach (var tgt in ResolveTargetClasses(te.ToSymbol, te.ToFile))
            {
                AddClassEdge(src, tgt, te.Kind, te.FromFile, te.ToFile, te.FromSymbol, te.ToSymbol, te.Line);
            }
        }

        foreach (var se in input.SymbolEdges)
        {
            var src = ResolveSourceClass(se.FromSymbol, se.FromFile);
            if (src == null) continue;
            foreach (var tgt in ResolveTargetClasses(se.ToSymbol, se.ToFile))
            {
                AddClassEdge(src, tgt, se.Kind, se.FromFile, se.ToFile, se.FromSymbol, se.ToSymbol, se.FromLine);
            }
        }

        // Map EntryPoints to classes
        var entryPointTargets = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var ep in input.EntryPointNodes)
        {
            var targets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (ep.TargetSymbols != null)
            {
                for (var i = 0; i < ep.TargetSymbols.Count; i++)
                {
                    var sym = ep.TargetSymbols[i];
                    var tFile = ep.TargetFiles != null && i < ep.TargetFiles.Count ? ep.TargetFiles[i] : ep.FilePath;
                    foreach (var resolved in ResolveTargetClasses(sym, tFile))
                    {
                        targets.Add(resolved);
                    }
                }
            }
            if (targets.Count == 0 && ep.TargetFiles != null)
            {
                foreach (var tf in ep.TargetFiles)
                {
                    foreach (var resolved in ResolveTargetClasses(null, tf))
                    {
                        targets.Add(resolved);
                    }
                }
            }
            if (targets.Count == 0 && !string.IsNullOrEmpty(ep.FilePath))
            {
                foreach (var resolved in ResolveTargetClasses(null, ep.FilePath))
                {
                    targets.Add(resolved);
                }
            }

            if (targets.Count > 0)
            {
                entryPointTargets[ep.Id] = targets.ToList();
                foreach (var t in targets)
                {
                    classes[t].EntryPointCount++;
                }
            }
        }

        foreach (var (file, count) in input.EntryPointsByFile)
        {
            if (fileToPrimaryClass.TryGetValue(file, out var primary) && classes.TryGetValue(primary, out var c))
            {
                if (c.EntryPointCount == 0)
                {
                    c.EntryPointCount += count;
                }
            }
        }

        bool IsBehavioralCandidate(ClassCandidate c) =>
            c.Methods.Count > 0 ||
            c.OutgoingCallCount > 0 ||
            c.EntryPointCount > 0 ||
            (c.SubKind == "module" && (c.ResourceLinkCount > 0 || c.IncomingCallCount > 0));

        // Count resource links per class (forwarding ORM entity table links to behavioral classes that use them)
        foreach (var link in input.ResourceLinks)
        {
            if (string.IsNullOrWhiteSpace(link.ResourceId)) continue;
            var owner = ResolveSourceClass(link.Symbol, link.File);
            if (owner != null && classes.TryGetValue(owner, out var c))
            {
                if (IsBehavioralCandidate(c) || c.SubKind == "module")
                {
                    c.ResourceLinkCount++;
                }
                else if (inboundReferencers.TryGetValue(c.Name, out var refs))
                {
                    foreach (var refName in refs)
                    {
                        if (classes.TryGetValue(refName, out var refCand))
                        {
                            refCand.ResourceLinkCount++;
                        }
                    }
                }
            }
        }

        // Filter to behavioral classes (exclude pure DTOs/records/ORM entity models without behavior)
        var behavioralCandidates = classes.Values
            .Where(IsBehavioralCandidate)
            .ToList();

        if (behavioralCandidates.Count == 0)
        {
            return null;
        }

        var candidateIds = new HashSet<string>(behavioralCandidates.Select(c => c.Id), StringComparer.OrdinalIgnoreCase);

        // Compute provisional FanIn / FanOut among behavioral candidates
        var provFanOut = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var provFanIn = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in rawClassEdges.Values)
        {
            if (!candidateIds.Contains(e.SourceId) || !candidateIds.Contains(e.TargetId)) continue;
            provFanOut[e.SourceId] = provFanOut.GetValueOrDefault(e.SourceId) + 1;
            provFanIn[e.TargetId] = provFanIn.GetValueOrDefault(e.TargetId) + 1;
        }

        if (behavioralCandidates.Count > 15)
        {
            var connected = behavioralCandidates
                .Where(c =>
                    provFanIn.GetValueOrDefault(c.Id) > 0 ||
                    provFanOut.GetValueOrDefault(c.Id) > 0 ||
                    c.ResourceLinkCount > 0 ||
                    c.EntryPointCount > 0 ||
                    c.ExternalCallersCount > 0)
                .ToList();
            if (connected.Count > 0)
            {
                behavioralCandidates = connected;
            }
        }

        if (behavioralCandidates.Count > MaxClasses)
        {
            behavioralCandidates = behavioralCandidates
                .OrderByDescending(c => c.EntryPointCount > 0)
                .ThenByDescending(c => c.ExternalCallersCount > 0)
                .ThenByDescending(c => provFanIn.GetValueOrDefault(c.Id) + provFanOut.GetValueOrDefault(c.Id) + c.ResourceLinkCount)
                .ThenByDescending(c => c.Methods.Count)
                .ThenByDescending(c => c.LineCount)
                .ThenBy(c => c.Name, StringComparer.Ordinal)
                .Take(MaxClasses)
                .ToList();
        }

        var activeById = behavioralCandidates.ToDictionary(c => c.Id, StringComparer.OrdinalIgnoreCase);
        var activeByName = behavioralCandidates.ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase);

        var result = new ProjectMapResult
        {
            Input = input,
            SourceRoot = (input.ProjectPath ?? "").Replace('\\', '/').TrimEnd('/'),
            Granularity = ProjectMapGranularity.Class,
            TestFilesExcluded = input.Files.Count - input.Files.Count(f => !f.IsTest)
        };

        // 1. Add Orbit 0 EntryPoint & Endpoint nodes
        var activeEntryPoints = input.EntryPointNodes
            .Where(ep => entryPointTargets.TryGetValue(ep.Id, out var tgts) && tgts.Any(activeByName.ContainsKey))
            .OrderBy(ep => ep.Kind, StringComparer.OrdinalIgnoreCase)
            .ThenBy(ep => ep.Name, StringComparer.OrdinalIgnoreCase)
            .Take(MaxEntryPointNodes)
            .ToList();

        foreach (var ep in activeEntryPoints)
        {
            var epModuleId = $"pm:ep:{ep.Id}";
            var epMod = new ProjectMapModule
            {
                Id = epModuleId,
                Name = ep.Name,
                Path = ep.FilePath,
                NodeKindOverride = string.IsNullOrWhiteSpace(ep.Kind) ? "EntryPoint" : ep.Kind,
                PmType = "entrypoint",
                SubKind = ep.SubKind ?? ep.Kind,
                StartLine = ep.Line,
                BfsDepth = 0,
                LineCount = 1,
                EntryPointCount = 1,
                Tier = ProjectMapTier.Ingress,
                TierReason = $"{ep.Kind} (Orbit 0)"
            };
            if (!string.IsNullOrEmpty(ep.FilePath))
            {
                epMod.Files.Add(ep.FilePath);
            }
            result.Modules.Add(epMod);

            foreach (var tgtName in entryPointTargets[ep.Id])
            {
                if (!activeByName.TryGetValue(tgtName, out var tgtCand)) continue;
                var epEdge = new ProjectMapEdge
                {
                    SourceId = epModuleId,
                    TargetId = tgtCand.Id
                };
                Accumulate(epEdge, "EXPOSED_BY", string.IsNullOrEmpty(ep.FilePath) ? tgtCand.PrimaryFile : ep.FilePath, tgtCand.PrimaryFile);
                epEdge.Samples.Add(new ProjectMapEvidenceSample(ep.Name, tgtCand.Name, tgtCand.PrimaryFile, ep.Line ?? tgtCand.StartLine, "EXPOSED_BY"));
                result.Edges.Add(epEdge);
            }
        }

        // 2. Add Class -> Class edges among active behavioral classes
        var classAdj = activeById.Keys.ToDictionary(id => id, _ => new List<string>(), StringComparer.OrdinalIgnoreCase);
        var classInDegree = activeById.Keys.ToDictionary(id => id, _ => 0, StringComparer.OrdinalIgnoreCase);

        foreach (var edge in rawClassEdges.Values
                     .Where(e => activeById.ContainsKey(e.SourceId) && activeById.ContainsKey(e.TargetId))
                     .OrderByDescending(e => e.Count)
                     .ThenBy(e => e.SourceId, StringComparer.Ordinal)
                     .ThenBy(e => e.TargetId, StringComparer.Ordinal))
        {
            result.Edges.Add(edge);
            classAdj[edge.SourceId].Add(edge.TargetId);
            classInDegree[edge.TargetId] = classInDegree.GetValueOrDefault(edge.TargetId) + 1;
        }

        // 3. BFS over Behavioral Classes for Orbits 1..4
        var bfsDepth = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var bfsReason = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<string>();

        var directEntryPointClasses = behavioralCandidates
            .Where(c => c.EntryPointCount > 0)
            .OrderByDescending(c => c.EntryPointCount)
            .ThenBy(c => c.Name, StringComparer.Ordinal)
            .ToList();

        if (directEntryPointClasses.Count > 0)
        {
            foreach (var c in directEntryPointClasses)
            {
                bfsDepth[c.Id] = 1;
                bfsReason[c.Id] = $"Direct handler for {c.EntryPointCount} entrypoint/endpoint(s) (BFS Depth 1)";
                queue.Enqueue(c.Id);
            }
        }
        else
        {
            // Library or internal service without explicit Endpoint/EntryPoint nodes:
            // Seed Depth 1 with externally-called classes (or root orchestrator classes with FanIn == 0 && FanOut > 0)
            var externalEntryClasses = behavioralCandidates
                .Where(c => c.ExternalCallersCount > 0 && classInDegree.GetValueOrDefault(c.Id) == 0)
                .ToList();
            if (externalEntryClasses.Count == 0)
            {
                externalEntryClasses = behavioralCandidates
                    .Where(c => c.ExternalCallersCount > 0)
                    .OrderBy(c => classInDegree.GetValueOrDefault(c.Id))
                    .ThenByDescending(c => c.ExternalCallersCount)
                    .Take(Math.Max(1, behavioralCandidates.Count / 3))
                    .ToList();
            }
            if (externalEntryClasses.Count == 0)
            {
                externalEntryClasses = behavioralCandidates
                    .Where(c => classInDegree.GetValueOrDefault(c.Id) == 0 && classAdj[c.Id].Count > 0)
                    .ToList();
            }
            if (externalEntryClasses.Count == 0)
            {
                externalEntryClasses = behavioralCandidates
                    .OrderByDescending(c => classAdj[c.Id].Count)
                    .Take(Math.Max(1, Math.Min(4, behavioralCandidates.Count)))
                    .ToList();
            }

            foreach (var c in externalEntryClasses)
            {
                bfsDepth[c.Id] = 1;
                bfsReason[c.Id] = c.ExternalCallersCount > 0
                    ? $"Public API class called by external projects ({c.ExternalCallersCount} callers, BFS Depth 1)"
                    : "Root orchestrator class (BFS Depth 1)";
                queue.Enqueue(c.Id);
            }
        }

        void DrainBfsQueue()
        {
            while (queue.Count > 0)
            {
                var currId = queue.Dequeue();
                var currDepth = bfsDepth[currId];
                var currName = activeById[currId].Name;
                foreach (var nextId in classAdj[currId])
                {
                    if (bfsDepth.ContainsKey(nextId)) continue;
                    var nextDepth = currDepth + 1;
                    bfsDepth[nextId] = nextDepth;
                    bfsReason[nextId] = $"Called from {currName} (BFS Depth {nextDepth})";
                    queue.Enqueue(nextId);
                }
            }
        }

        DrainBfsQueue();

        // Secondary BFS pass for unvisited roots (e.g. startup/DI/background classes not directly linked from EntryPoint edge)
        var unvisitedRoots = behavioralCandidates
            .Where(c => !bfsDepth.ContainsKey(c.Id) && classInDegree.GetValueOrDefault(c.Id) == 0 && classAdj[c.Id].Count > 0)
            .OrderByDescending(c => classAdj[c.Id].Count)
            .ThenBy(c => c.Name, StringComparer.Ordinal)
            .ToList();
        var secondaryStartDepth = directEntryPointClasses.Count > 0 ? 2 : 1;
        foreach (var r in unvisitedRoots)
        {
            if (bfsDepth.ContainsKey(r.Id)) continue;
            bfsDepth[r.Id] = secondaryStartDepth;
            bfsReason[r.Id] = $"Internal root orchestrator (BFS Depth {secondaryStartDepth})";
            queue.Enqueue(r.Id);
            DrainBfsQueue();
        }

        // Remaining unvisited classes
        foreach (var c in behavioralCandidates)
        {
            if (bfsDepth.ContainsKey(c.Id)) continue;
            if (classInDegree.GetValueOrDefault(c.Id) > 0)
            {
                bfsDepth[c.Id] = 3;
                bfsReason[c.Id] = "Shared internal class (BFS Depth 3)";
            }
            else if (c.ResourceLinkCount > 0)
            {
                bfsDepth[c.Id] = 3;
                bfsReason[c.Id] = "Resource/data integration class (BFS Depth 3)";
            }
            else
            {
                bfsDepth[c.Id] = 4;
                bfsReason[c.Id] = "Standalone utility / foundation class (BFS Depth 4)";
            }
        }

        // 4. Link resources to active classes (Orbit 5 = Transports/Buses/Libraries, Orbit 6 = Databases/Tables)
        LinkClassResources(result, input.ResourceLinks, activeByName, inboundReferencers, ResolveSourceClass);

        // 5. Materialize behavioral Class modules on Orbits 1..4
        foreach (var c in behavioralCandidates.OrderBy(x => bfsDepth[x.Id]).ThenBy(x => x.Name, StringComparer.Ordinal))
        {
            var depth = bfsDepth[c.Id];
            var tier = depth switch
            {
                <= 1 => ProjectMapTier.Application,
                2 => ProjectMapTier.Core,
                3 => ProjectMapTier.Data,
                _ => ProjectMapTier.Foundation
            };

            var fanOut = classAdj[c.Id].Distinct(StringComparer.OrdinalIgnoreCase).Count();
            var fanIn = classInDegree.GetValueOrDefault(c.Id);
            var total = fanIn + fanOut;

            var mod = new ProjectMapModule
            {
                Id = c.Id,
                Name = c.Name,
                Path = c.PrimaryFile,
                NodeKindOverride = "Type",
                PmType = "class",
                SubKind = c.SubKind,
                StartLine = c.StartLine,
                BfsDepth = depth,
                LineCount = c.LineCount,
                EntryPointCount = c.EntryPointCount,
                FanIn = fanIn,
                FanOut = fanOut,
                Instability = total == 0 ? 0.5 : (double)fanOut / total,
                TouchesData = c.TouchesData,
                DataFileCount = c.DataFileCount,
                Tier = tier,
                TierReason = bfsReason[c.Id]
            };
            mod.Files.AddRange(c.Files.OrderBy(f => f, StringComparer.OrdinalIgnoreCase));
            mod.Methods.AddRange(c.Methods.Values.OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase));
            mod.ImplementedInterfaces.AddRange(c.ImplementedInterfaces.OrderBy(i => i, StringComparer.OrdinalIgnoreCase));
            result.Modules.Add(mod);
        }

        DetectCycles(result);
        return result;
    }

    private static void LinkClassResources(
        ProjectMapResult result,
        IReadOnlyList<ProjectMapResourceLink> links,
        Dictionary<string, ClassCandidate> activeByName,
        Dictionary<string, HashSet<string>> inboundReferencers,
        Func<string?, string?, string?> resolveSourceClass)
    {
        var resolvedLinks = new List<(ClassCandidate Class, ProjectMapResourceLink Link)>();
        foreach (var link in links)
        {
            if (string.IsNullOrWhiteSpace(link.ResourceId)) continue;
            var ownerName = resolveSourceClass(link.Symbol, link.File);
            if (ownerName == null) continue;
            if (activeByName.TryGetValue(ownerName, out var cand))
            {
                resolvedLinks.Add((cand, link));
            }
            else if (inboundReferencers.TryGetValue(ownerName, out var refs))
            {
                foreach (var refName in refs)
                {
                    if (activeByName.TryGetValue(refName, out var refCand))
                    {
                        resolvedLinks.Add((refCand, link));
                    }
                }
            }
        }

        var topResources = resolvedLinks
            .GroupBy(x => x.Link.ResourceId, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key, StringComparer.Ordinal)
            .Take(MaxResources)
            .ToList();

        var edges = new Dictionary<(string, string), ProjectMapEdge>();
        var dataFiles = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var group in topResources)
        {
            var first = group.First().Link;
            var isDataResource = DataResourceKinds.Contains(first.ResourceKind);
            var resourceTier = isDataResource ? ProjectMapTier.Database : ProjectMapTier.Resource;

            result.Resources.Add(new ProjectMapResource
            {
                Id = first.ResourceId,
                Kind = first.ResourceKind,
                Name = first.ResourceName,
                Tier = resourceTier
            });

            foreach (var (cand, link) in group)
            {
                if (!edges.TryGetValue((cand.Id, link.ResourceId), out var edge))
                {
                    edge = new ProjectMapEdge { SourceId = cand.Id, TargetId = link.ResourceId, IsResourceLink = true };
                    edges[(cand.Id, link.ResourceId)] = edge;
                }

                Accumulate(edge, link.Kind, link.File, null);
                if (edge.Samples.Count < MaxSamplesPerEdge)
                {
                    edge.Samples.Add(new ProjectMapEvidenceSample(link.Symbol ?? cand.Name, link.ResourceName, link.File, link.Line, link.Kind));
                }

                if (isDataResource)
                {
                    cand.TouchesData = true;
                    if (!dataFiles.TryGetValue(cand.Id, out var set))
                    {
                        set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        dataFiles[cand.Id] = set;
                    }
                    set.Add(link.File);
                    cand.DataFileCount = set.Count;
                }
            }
        }

        result.Edges.AddRange(edges.Values.OrderByDescending(e => e.Count).ThenBy(e => e.TargetId, StringComparer.Ordinal));
    }

    private static string StripGenericArity(string typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName)) return "";
        var angleIdx = typeName.IndexOf('<');
        if (angleIdx > 0) typeName = typeName[..angleIdx];
        var tickIdx = typeName.IndexOf('`');
        if (tickIdx > 0) typeName = typeName[..tickIdx];
        return typeName.Trim();
    }

    private static bool LooksLikeInterfaceName(string name) =>
        name.Length >= 3 && name[0] == 'I' && char.IsUpper(name[1]) && char.IsLower(name[2]);

    private static bool IsConstructorOrNoiseMethod(string? methodName)
    {
        if (string.IsNullOrWhiteSpace(methodName)) return true;
        var m = methodName.Trim();
        return m is ".ctor" or ".cctor" or "constructor" or "__init__" || m.StartsWith('<');
    }

    private static string DeriveFileModuleName(string filePath, Func<string, bool> exists)
    {
        var norm = filePath.Replace('\\', '/').Trim('/');
        var parts = norm.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var fileName = parts.LastOrDefault() ?? "module";
        var dotIdx = fileName.IndexOf('.');
        var stem = dotIdx > 0 ? fileName[..dotIdx] : fileName;

        if (stem.Equals("index", StringComparison.OrdinalIgnoreCase) && parts.Length >= 2)
        {
            stem = parts[^2];
        }

        if (!exists(stem)) return stem;
        if (parts.Length >= 2)
        {
            var qualified = $"{parts[^2]}/{stem}";
            if (!exists(qualified)) return qualified;
        }
        return norm;
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
