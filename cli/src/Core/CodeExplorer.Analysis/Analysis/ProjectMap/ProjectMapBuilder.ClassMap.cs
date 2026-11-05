namespace CodeExplorer.Core.Analysis.ProjectMap;

public static partial class ProjectMapBuilder
{
    private sealed class ClassCandidate
    {
        public required string Id { get; init; }
        public required string Name { get; init; }
        public required TypeKind SubKind { get; set; }
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

    private static (
        Dictionary<string, ClassCandidate> Classes,
        Dictionary<string, string> FileToPrimaryClass,
        HashSet<string> Interfaces)?
    CollectClassCandidates(ProjectMapInput input, List<ProjectMapFile> sourceFiles)
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

            var subKind = t.SubKind;
            if (IgnoredTypeSubKinds.Contains(subKind)) continue;

            if (subKind == TypeKind.Interface)
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
        var fileToFunctions = input.Functions
            .Where(f => !f.IsTest && IsAllowedFile(f.FilePath))
            .GroupBy(f => f.FilePath, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Select(f => f.Name).ToHashSet(StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase);

        foreach (var (file, clsNames) in fileToClasses)
        {
            var baseFileName = Path.GetFileNameWithoutExtension(file);
            var matchingClass = clsNames.FirstOrDefault(c => string.Equals(c, baseFileName, StringComparison.OrdinalIgnoreCase));
            if (matchingClass != null)
            {
                fileToPrimaryClass[file] = matchingClass;
                continue;
            }

            if (fileToFunctions.TryGetValue(file, out var fnNames) && fnNames.Contains(baseFileName))
            {
                continue;
            }

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
                    SubKind = TypeKind.Module,
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

        // Ensure files with top-level entrypoints, resource links, or symbol calls get a module candidate
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
                    SubKind = TypeKind.Module,
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

        return (classes, fileToPrimaryClass, interfaces);
    }

    private static ProjectMapResult? TryBuildClassMap(ProjectMapInput input, List<ProjectMapFile> sourceFiles)
    {
        var collected = CollectClassCandidates(input, sourceFiles);
        if (collected == null) return null;

        var (classes, fileToPrimaryClass, interfaces) = collected.Value;

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
            if (te.Kind is not RelationshipKind.Implements and not RelationshipKind.InheritsFrom)
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
            if (se.Kind is not RelationshipKind.Implements and not RelationshipKind.InheritsFrom)
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

        IReadOnlyList<string> ResolveTargetClasses(
            string? symbol,
            string? file,
            string? srcClass = null,
            bool allowInterfaces = true,
            string? fromFile = null)
        {
            var clean = StripGenericArity(symbol ?? "");
            if (!string.IsNullOrEmpty(clean))
            {
                if (classes.TryGetValue(clean, out var c) && (string.IsNullOrEmpty(file) || c.Files.Contains(file))) return [c.Name];
                if (allowInterfaces && interfaceToImpls.TryGetValue(clean, out var impls) && impls.Count > 0)
                {
                    // An implementing class of this interface should never resolve references to this interface into dependencies on sibling implementations
                    if (srcClass != null && classes.TryGetValue(srcClass, out var sc) && sc.ImplementedInterfaces.Contains(clean))
                    {
                        return [];
                    }

                    if (srcClass == null) return impls;
                    return impls.Where(tgt => !string.Equals(tgt, srcClass, StringComparison.OrdinalIgnoreCase)).ToList();
                }
            }

            // Intra-file calls to unattached helper symbols must not fall back to an arbitrary class in the same file
            if (!string.IsNullOrEmpty(fromFile) && !string.IsNullOrEmpty(file) && fromFile.Equals(file, StringComparison.OrdinalIgnoreCase))
            {
                return [];
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
        var (rawClassEdges, inboundReferencers) = CollectClassEdges(input, classes, ResolveSourceClass, ResolveTargetClasses);

        // Map EntryPoints to classes
        var entryPointTargets = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var ep in input.EntryPointNodes)
        {
            var targets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrEmpty(ep.FilePath))
            {
                foreach (var resolved in ResolveTargetClasses(null, ep.FilePath))
                {
                    targets.Add(resolved);
                }
            }
            if (targets.Count == 0 && ep.TargetSymbols != null)
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
            (c.SubKind == TypeKind.Module && (c.ResourceLinkCount > 0 || c.IncomingCallCount > 0));

        // Count resource links per class (forwarding ORM entity table links to behavioral classes that use them)
        foreach (var link in input.ResourceLinks)
        {
            if (string.IsNullOrWhiteSpace(link.ResourceId)) continue;
            var owner = ResolveSourceClass(link.Symbol, link.File);
            if (owner != null && classes.TryGetValue(owner, out var c))
            {
                if (IsBehavioralCandidate(c) || c.SubKind == TypeKind.Module)
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
        var eligibleEntryPoints = input.EntryPointNodes
            .Where(ep => entryPointTargets.TryGetValue(ep.Id, out var tgts) && tgts.Any(activeByName.ContainsKey))
            .ToList();

        var byTarget = eligibleEntryPoints
            .GroupBy(ep => entryPointTargets[ep.Id].First(activeByName.ContainsKey), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.OrderBy(ep => ep.Kind.ToCypherLabel(), StringComparer.OrdinalIgnoreCase).ThenBy(ep => ep.Name, StringComparer.OrdinalIgnoreCase).ToList(), StringComparer.OrdinalIgnoreCase);

        var activeEntryPoints = new List<ProjectMapEntryPointNode>();
        var maxLimit = Math.Max(MaxEntryPointNodes, byTarget.Count * 4);
        maxLimit = Math.Min(maxLimit, 100);

        var round = 0;
        bool addedAny;
        do
        {
            addedAny = false;
            foreach (var targetList in byTarget.Values)
            {
                if (round < targetList.Count && activeEntryPoints.Count < maxLimit)
                {
                    activeEntryPoints.Add(targetList[round]);
                    addedAny = true;
                }
            }
            round++;
        } while (addedAny && activeEntryPoints.Count < maxLimit);

        foreach (var ep in activeEntryPoints)
        {
            var epModuleId = $"pm:ep:{ep.Id}";
            var epKind = ep.Kind == NodeKind.Unspecified ? NodeKind.EntryPoint : ep.Kind;
            var epMod = new ProjectMapModule
            {
                Id = epModuleId,
                Name = ep.Name,
                Path = ep.FilePath,
                NodeKindOverride = epKind,
                PmType = ProjectMapModuleType.EntryPoint,
                EntrySubKind = ep.SubKind,
                StartLine = ep.Line,
                BfsDepth = 0,
                LineCount = 1,
                EntryPointCount = 1,
                Tier = ProjectMapTier.Ingress,
                TierReason = $"{epKind.ToCypherLabel()} (Orbit 0)"
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
        var (bfsDepth, bfsReason) = AssignBfsDepths(behavioralCandidates, activeById, classAdj, classInDegree);

        // 4. Link resources to active classes
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
                NodeKindOverride = NodeKind.Type,
                PmType = ProjectMapModuleType.Class,
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

    private static (Dictionary<(string Src, string Tgt), ProjectMapEdge> Edges, Dictionary<string, HashSet<string>> InboundRefs) CollectClassEdges(
        ProjectMapInput input,
        Dictionary<string, ClassCandidate> classes,
        Func<string?, string?, string?> resolveSourceClass,
        Func<string?, string?, string?, bool, string?, IReadOnlyList<string>> resolveTargetClasses)
    {
        var rawClassEdges = new Dictionary<(string Src, string Tgt), ProjectMapEdge>();
        var inboundReferencers = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        void AddClassEdge(string srcName, string tgtName, RelationshipKind kind, string fromFile, string? toFile, string? fromSym, string? toSym, int? line)
        {
            if (string.Equals(srcName, tgtName, StringComparison.OrdinalIgnoreCase)) return;
            var srcCand = classes[srcName];
            var tgtCand = classes[tgtName];

            if (kind == RelationshipKind.UsesType && srcCand.Methods.Count == 0)
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
            if (kind != RelationshipKind.UsesType)
            {
                srcCand.OutgoingCallCount++;
                tgtCand.IncomingCallCount++;
            }
        }

        var seenRawEdges = new HashSet<(string FromFile, string? ToFile, string? FromSym, string? ToSym, RelationshipKind Kind, int? Line)>();

        foreach (var te in input.TypeEdges)
        {
            if (te.IsExternalCaller)
            {
                foreach (var tgt in resolveTargetClasses(te.ToSymbol, te.ToFile, null, true, null))
                {
                    classes[tgt].ExternalCallersCount++;
                }
                continue;
            }

            if (te.Kind == RelationshipKind.Implements)
            {
                continue;
            }

            seenRawEdges.Add((te.FromFile, te.ToFile, te.FromSymbol, te.ToSymbol, te.Kind, te.Line));

            var src = resolveSourceClass(te.FromSymbol, te.FromFile);
            if (src == null) continue;

            var isInheritance = te.Kind == RelationshipKind.InheritsFrom;
            foreach (var tgt in resolveTargetClasses(te.ToSymbol, te.ToFile, src, !isInheritance, te.FromFile))
            {
                AddClassEdge(src, tgt, te.Kind, te.FromFile, te.ToFile, te.FromSymbol, te.ToSymbol, te.Line);
            }
        }

        foreach (var se in input.SymbolEdges)
        {
            if (se.Kind == RelationshipKind.Implements)
            {
                continue;
            }

            if (!seenRawEdges.Add((se.FromFile, se.ToFile, se.FromSymbol, se.ToSymbol, se.Kind, se.FromLine)))
            {
                continue;
            }

            var src = resolveSourceClass(se.FromSymbol, se.FromFile);
            if (src == null) continue;

            var isInheritance = se.Kind == RelationshipKind.InheritsFrom;
            foreach (var tgt in resolveTargetClasses(se.ToSymbol, se.ToFile, src, !isInheritance, se.FromFile))
            {
                AddClassEdge(src, tgt, se.Kind, se.FromFile, se.ToFile, se.FromSymbol, se.ToSymbol, se.FromLine);
            }
        }

        return (rawClassEdges, inboundReferencers);
    }

    private static (Dictionary<string, int> Depths, Dictionary<string, string> Reasons) AssignBfsDepths(
        List<ClassCandidate> behavioralCandidates,
        Dictionary<string, ClassCandidate> activeById,
        Dictionary<string, List<string>> classAdj,
        Dictionary<string, int> classInDegree)
    {
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

        return (bfsDepth, bfsReason);
    }
}
