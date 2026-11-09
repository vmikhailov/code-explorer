using System.Text.Json;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Common.Relationships;

namespace CodeExplorer.Core.Parser.Layers;

public partial class Layer5AnalysisParser
{
    private readonly struct IndexedBinding
    {
        public readonly string TypeName;
        public readonly string? ScopeMarker;
        public readonly string? ScopeId;
        public readonly string? FilePath;

        public IndexedBinding(string typeName, string? scopeId, string? filePath = null)
        {
            TypeName = typeName.TrimEnd('?');
            ScopeId = string.IsNullOrWhiteSpace(scopeId) ? null : scopeId;
            ScopeMarker = ScopeId != null ? $":{ScopeId}:" : null;
            FilePath = string.IsNullOrWhiteSpace(filePath) ? null : filePath;
        }
    }

    internal static string? ExtractSymbolNameFromId(string symbolId)
    {
        if (Urn.TryParse(symbolId, out var urn) && !string.IsNullOrEmpty(urn.Name))
        {
            var n = urn.Name;
            var hashIdx = n.IndexOf('#');
            return hashIdx > 0 ? n[..hashIdx] : n;
        }
        // Format: {workspaceId}:symbol:{relativePath}:{mappedKind}:{name} OR with :{row}
        var parts = symbolId.Split(':');
        if (parts.Length >= 2)
        {
            var raw = int.TryParse(parts[^1], out _) ? parts[^2] : parts[^1];
            var hashIdx = raw.IndexOf('#');
            return hashIdx > 0 ? raw[..hashIdx] : raw;
        }
        return null;
    }

    private async Task PreloadTargetedSymbolsAsync(ParsingContext ctx, Dictionary<string, List<string>> interfaceToImplementors)
    {
        if (!ctx.IsSubtreeScan) return;

        var neededNames = new HashSet<string>(StringComparer.Ordinal);
        var candidateTypeNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (var r in ctx.GlobalReferences)
        {
            if (!string.IsNullOrWhiteSpace(r.TargetName))
            {
                neededNames.Add(r.TargetName);
                if (r.HasExplicitReceiver)
                {
                    var typePart = r.ReceiverName!;
                    var memberPart = r.MemberName;
                    neededNames.Add(typePart);
                    neededNames.Add(memberPart);
                    candidateTypeNames.Add(typePart);
                }
                else
                {
                    candidateTypeNames.Add(r.MemberName);
                }
            }
        }

        foreach (var b in ctx.RawTypeBindings)
        {
            if (!string.IsNullOrWhiteSpace(b.TypeName))
            {
                neededNames.Add(b.TypeName);
                candidateTypeNames.Add(b.TypeName);
                if (b.TypeName.Contains('.'))
                {
                    var parts = b.TypeName.Split('.', StringSplitOptions.RemoveEmptyEntries);
                    foreach (var part in parts)
                    {
                        neededNames.Add(part);
                    }
                }
            }
        }

        if (neededNames.Count == 0) return;

        // 1. Query interface implementations from SQLite for candidate types
        if (candidateTypeNames.Count > 0)
        {
            var dbImplementors = await ctx.DbClient.LoadImplementationsForTypesAsync(candidateTypeNames, ctx.WorkspaceId, ctx.CancellationToken);
            foreach (var (iface, impls) in dbImplementors)
            {
                if (!interfaceToImplementors.TryGetValue(iface, out var list))
                {
                    list = [];
                    interfaceToImplementors[iface] = list;
                }
                foreach (var impl in impls)
                {
                    if (!list.Contains(impl)) list.Add(impl);
                    neededNames.Add(impl);
                }
            }
        }

        // 2. Query matching symbols from SQLite
        ctx.Layer5Log.PreloadingTargetedSymbols(neededNames.Count);
        var preloaded = await ctx.DbClient.LoadSymbolsByNamesAsync(neededNames, ctx.WorkspaceId, ctx.CancellationToken);

        var addedCount = 0;
        foreach (var ((kind, name), id) in preloaded)
        {
            if (ctx.GlobalSymbols.TryAdd((kind, name), id))
            {
                addedCount++;
            }
        }

        ctx.Layer5Log.PreloadedTargetedSymbols(addedCount);
    }

    internal record SymbolLookups(
        Dictionary<string, string> TypeSymbols,
        Dictionary<string, string> FunctionSymbols,
        Dictionary<(string FilePath, string FuncName), string> FunctionSymbolsByFile,
        Dictionary<string, List<string>> FunctionsByName,
        HashSet<string> KnownFiles,
        Dictionary<string, List<string>> FilesByProject,
        Dictionary<string, string> ProcedureSymbols,
        Dictionary<string, string> TableSymbols,
        Dictionary<string, string> EndpointSymbols,
        Dictionary<string, string> EntryPointSymbols,
        Dictionary<string, Dictionary<string, List<string>>>? FunctionsByProject = null,
        List<string>? ProjectsSortedByLengthDesc = null,
        Dictionary<string, List<string>>? TypesByName = null,
        Dictionary<string, HashSet<string>>? ReachableProjects = null,
        HashSet<string>? ProjectsInDepGraph = null,
        HashSet<(string From, string To)>? InheritanceRels = null)
    {
        public Dictionary<string, Dictionary<string, List<string>>> FunctionsByProject { get; init; } = FunctionsByProject ?? new(StringComparer.OrdinalIgnoreCase);
        public List<string> ProjectsSortedByLengthDesc { get; init; } = ProjectsSortedByLengthDesc ?? [];
        public Dictionary<string, List<string>> TypesByName { get; init; } = TypesByName ?? new(StringComparer.Ordinal);
        public Dictionary<string, HashSet<string>> ReachableProjects { get; init; } = ReachableProjects ?? new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> ProjectsInDepGraph { get; init; } = ProjectsInDepGraph ?? new(StringComparer.OrdinalIgnoreCase);
        public HashSet<(string From, string To)> InheritanceRels { get; init; } = InheritanceRels ?? [];
    }

    private record ImportedSymbolEntry(
        string ImportPath,
        string ImportedName,
        bool IsDefault,
        bool IsNamespace);

    private record PackageTargetInfo(
        string ProjectPath,
        string? MainFile);

    private record ImportLookups(
        Dictionary<(string FilePath, string LocalName), ImportedSymbolEntry> FileImports,
        Dictionary<string, List<string>> StarImportsByFile,
        Dictionary<string, PackageTargetInfo> WorkspacePackages,
        Dictionary<string, string> TsConfigPaths,
        Dictionary<(string CallerFile, string ImportPath), List<string>>? CandidateCache = null)
    {
        public Dictionary<(string CallerFile, string ImportPath), List<string>> CandidateCache { get; init; } = CandidateCache ?? [];
    }

    private record BindingLookups(
        Dictionary<(string FilePath, string VarName), List<IndexedBinding>> BindingsLookup,
        Dictionary<string, List<IndexedBinding>> GlobalBindingsLookup,
        Dictionary<string, string> ConstantLookup);

    private static SymbolLookups BuildSymbolLookups(ParsingContext ctx, Layer4Result? l4Result = null)
    {
        var typeSymbols = new Dictionary<string, string>(StringComparer.Ordinal);
        var typesByName = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var functionSymbols = new Dictionary<string, string>(StringComparer.Ordinal);
        var functionSymbolsByFile = new Dictionary<(string FilePath, string FuncName), string>();
        var functionsInFile = new Dictionary<string, List<(string FuncName, string FuncId)>>(StringComparer.OrdinalIgnoreCase);
        var functionsByName = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var functionsByProject = new Dictionary<string, Dictionary<string, List<string>>>(StringComparer.OrdinalIgnoreCase);
        var knownFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var filesByProject = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var procedureSymbols = new Dictionary<string, string>(StringComparer.Ordinal);
        var tableSymbols = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var endpointSymbols = new Dictionary<string, string>(StringComparer.Ordinal);
        var entryPointSymbols = new Dictionary<string, string>(StringComparer.Ordinal);

        void RegisterFileInProject(string? projKey, string normFile)
        {
            if (string.IsNullOrEmpty(projKey)) return;
            if (!filesByProject.TryGetValue(projKey, out var pFiles))
            {
                pFiles = [];
                filesByProject[projKey] = pFiles;
            }
            if (!pFiles.Contains(normFile)) pFiles.Add(normFile);
        }

        void RegisterFunctionInFile(string normFile, string fnName, string fnId)
        {
            functionSymbolsByFile[(normFile, fnName)] = fnId;
            if (!functionsInFile.TryGetValue(normFile, out var list))
            {
                list = [];
                functionsInFile[normFile] = list;
            }
            list.Add((fnName, fnId));
        }

        void RegisterFunctionInProject(string? projKey, string fnName, string fnId)
        {
            if (string.IsNullOrEmpty(projKey)) return;
            if (!functionsByProject.TryGetValue(projKey, out var projFns))
            {
                projFns = new Dictionary<string, List<string>>(StringComparer.Ordinal);
                functionsByProject[projKey] = projFns;
            }
            if (!projFns.TryGetValue(fnName, out var pfnList))
            {
                pfnList = [];
                projFns[fnName] = pfnList;
            }
            if (!pfnList.Contains(fnId))
            {
                pfnList.Add(fnId);
            }
        }

        foreach (var ((filePath, kind, name), id) in ctx.FileScopedSymbols)
        {
            var normFuncFile = filePath.Replace('\\', '/');
            knownFiles.Add(normFuncFile);

            var proj = ConstantRegistry.ExtractProjectName(normFuncFile);
            RegisterFileInProject(proj, normFuncFile);
            string? scopeProj = null;
            if (ctx.SymbolScopes.TryGetValue(id, out var scopeInfo) &&
                !string.IsNullOrEmpty(scopeInfo.ProjectName) &&
                !string.Equals(scopeInfo.ProjectName, proj, StringComparison.OrdinalIgnoreCase))
            {
                scopeProj = scopeInfo.ProjectName;
                RegisterFileInProject(scopeProj, normFuncFile);
            }

            if (kind == OntologyConstants.NodeLabels.Type || kind is "Class" or "Interface" or "Struct" or "Enum" or "Record")
            {
                typeSymbols[name] = id;
                if (!typesByName.TryGetValue(name, out var tList))
                {
                    tList = [];
                    typesByName[name] = tList;
                }
                if (!tList.Contains(id)) tList.Add(id);
            }
            else if (kind == OntologyConstants.NodeLabels.Function)
            {
                RegisterFunctionInFile(normFuncFile, name, id);
                if (!string.Equals(name, "<module_init>", StringComparison.Ordinal))
                {
                    RegisterFunctionInProject(proj, name, id);
                    RegisterFunctionInProject(scopeProj, name, id);

                    if (!functionsByName.TryGetValue(name, out var fnList))
                    {
                        fnList = [];
                        functionsByName[name] = fnList;
                    }
                    if (!fnList.Contains(id))
                    {
                        fnList.Add(id);
                    }
                }
            }
        }

        foreach (var (key, id) in ctx.GlobalSymbols)
        {
            switch (key.Kind)
            {
                case OntologyConstants.NodeLabels.Type:
                    typeSymbols[key.Name] = id;
                    if (!typesByName.TryGetValue(key.Name, out var tList))
                    {
                        tList = [];
                        typesByName[key.Name] = tList;
                    }
                    if (!tList.Contains(id)) tList.Add(id);
                    break;
                case OntologyConstants.NodeLabels.Function:
                    if (!string.Equals(key.Name, "<module_init>", StringComparison.Ordinal))
                    {
                        functionSymbols[key.Name] = id;
                        if (!functionsByName.TryGetValue(key.Name, out var fnList))
                        {
                            fnList = [];
                            functionsByName[key.Name] = fnList;
                        }
                        if (!fnList.Contains(id))
                        {
                            fnList.Add(id);
                        }
                    }

                    var funcFile = ExtractFilePathFromSymbolId(id);
                    if (funcFile != null)
                    {
                        var normFuncFile = funcFile.Replace('\\', '/');
                        RegisterFunctionInFile(normFuncFile, key.Name, id);
                        knownFiles.Add(normFuncFile);

                        var proj = ConstantRegistry.ExtractProjectName(normFuncFile);
                        RegisterFileInProject(proj, normFuncFile);
                        string? scopeProj = null;
                        if (ctx.SymbolScopes.TryGetValue(id, out var scopeInfo) &&
                            !string.IsNullOrEmpty(scopeInfo.ProjectName) &&
                            !string.Equals(scopeInfo.ProjectName, proj, StringComparison.OrdinalIgnoreCase))
                        {
                            scopeProj = scopeInfo.ProjectName;
                            RegisterFileInProject(scopeProj, normFuncFile);
                        }

                        if (!string.Equals(key.Name, "<module_init>", StringComparison.Ordinal))
                        {
                            RegisterFunctionInProject(proj, key.Name, id);
                            RegisterFunctionInProject(scopeProj, key.Name, id);
                        }
                    }
                    break;
                case OntologyConstants.NodeLabels.Procedure:
                    procedureSymbols[key.Name] = id;
                    break;
                case OntologyConstants.NodeLabels.Table:
                    tableSymbols[key.Name] = id;
                    break;
                case OntologyConstants.NodeLabels.Endpoint:
                    endpointSymbols[key.Name] = id;
                    break;
                case OntologyConstants.NodeLabels.EntryPoint:
                    entryPointSymbols[key.Name] = id;
                    break;
            }
        }

        foreach (var (k, _) in ctx.FileScopedSymbols)
        {
            knownFiles.Add(k.FilePath.Replace('\\', '/'));
        }
        foreach (var imp in ctx.RawImports)
        {
            if (!string.IsNullOrEmpty(imp.FilePath))
            {
                knownFiles.Add(imp.FilePath.Replace('\\', '/'));
            }
        }

        if (l4Result != null)
        {
            CollectTableSymbols(l4Result.SemanticStructure, tableSymbols);
        }

        Layer5SymbolVisibilityResolver.UnifyPartialTypeDeclarations(typesByName, typeSymbols, functionSymbolsByFile, functionsInFile, ctx);

        var (reachableProjects, projectsInDepGraph) = Layer5SymbolVisibilityResolver.BuildProjectReachabilityMatrix(ctx, l4Result);
        var projectsSortedByLengthDesc = filesByProject.Keys.OrderByDescending(k => k.Length).ToList();

        return new SymbolLookups(
            typeSymbols,
            functionSymbols,
            functionSymbolsByFile,
            functionsByName,
            knownFiles,
            filesByProject,
            procedureSymbols,
            tableSymbols,
            endpointSymbols,
            entryPointSymbols,
            functionsByProject,
            projectsSortedByLengthDesc,
            typesByName,
            reachableProjects,
            projectsInDepGraph);
    }

    private static void CollectTableSymbols(IOntologyNode node, Dictionary<string, string> tableSymbols)
    {
        if (node is TableNode tbl && !string.IsNullOrEmpty(tbl.Name) && !tableSymbols.ContainsKey(tbl.Name))
        {
            tableSymbols[tbl.Name] = tbl.Id;
        }
        foreach (var child in node.Children)
        {
            CollectTableSymbols(child, tableSymbols);
        }
    }

    private static void ResolveInheritanceReferences(
        ParsingContext ctx,
        SymbolLookups symbols,
        List<Relationship> referenceRelationships,
        HashSet<(string From, string To)> inheritanceRels,
        Dictionary<string, List<string>> interfaceToImplementors)
    {
        var directInherits = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var directImplements = new Dictionary<string, List<(string TargetId, string TargetName)>>(StringComparer.Ordinal);

        foreach (var refItem in ctx.GlobalReferences)
        {
            ctx.CancellationToken.ThrowIfCancellationRequested();

            try
            {
                if (refItem.Kind is RelationshipKind.Implements or RelationshipKind.InheritsFrom)
                {
                    var targetNodeId = Layer5SymbolVisibilityResolver.ResolveVisibleTypeSymbol(refItem.TargetName, refItem.ScopeSymbolId, ctx, symbols);
                    if (targetNodeId == null && refItem.Kind == RelationshipKind.InheritsFrom)
                    {
                        symbols.FunctionSymbols.TryGetValue(refItem.TargetName, out targetNodeId);
                    }
                    if (targetNodeId != null && refItem.ScopeSymbolId != targetNodeId)
                    {
                        if (refItem.Kind == RelationshipKind.Implements)
                        {
                            IOntologyRelationship rel = new ImplementsRelationship(refItem.ScopeSymbolId, targetNodeId);
                            referenceRelationships.Add(Relationship.FromRelationship(rel));
                            if (!directImplements.TryGetValue(refItem.ScopeSymbolId, out var impTargets))
                            {
                                impTargets = [];
                                directImplements[refItem.ScopeSymbolId] = impTargets;
                            }
                            impTargets.Add((targetNodeId, refItem.TargetName));
                        }
                        else
                        {
                            IOntologyRelationship rel = new InheritsFromRelationship(refItem.ScopeSymbolId, targetNodeId);
                            referenceRelationships.Add(Relationship.FromRelationship(rel));
                            if (!directInherits.TryGetValue(refItem.ScopeSymbolId, out var baseTargets))
                            {
                                baseTargets = [];
                                directInherits[refItem.ScopeSymbolId] = baseTargets;
                            }
                            baseTargets.Add(targetNodeId);
                        }

                        inheritanceRels.Add((refItem.ScopeSymbolId, targetNodeId));
                        symbols.InheritanceRels.Add((refItem.ScopeSymbolId, targetNodeId));
                    }
                    else if (refItem.Kind == RelationshipKind.Implements)
                    {
                        if (symbols.EndpointSymbols.TryGetValue(refItem.TargetName, out var targetEndpointId))
                        {
                            referenceRelationships.Add(
                                Relationship.FromRelationship(new ExposedByRelationship(targetEndpointId, refItem.ScopeSymbolId)));
                        }
                        else if (symbols.EntryPointSymbols.TryGetValue(refItem.TargetName, out var targetEpId))
                        {
                            referenceRelationships.Add(
                                Relationship.FromRelationship(new ImplementedByRelationship(targetEpId, refItem.ScopeSymbolId)));
                            inheritanceRels.Add((targetEpId, refItem.ScopeSymbolId));
                            symbols.InheritanceRels.Add((targetEpId, refItem.ScopeSymbolId));
                        }
                    }

                    var className = ExtractSymbolNameFromId(refItem.ScopeSymbolId);
                    if (!string.IsNullOrEmpty(className))
                    {
                        if (!interfaceToImplementors.TryGetValue(refItem.TargetName, out var implList))
                        {
                            implList = [];
                            interfaceToImplementors[refItem.TargetName] = implList;
                        }
                        if (!implList.Contains(className))
                        {
                            implList.Add(className);
                        }
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                ctx.Layer5Log.InheritanceResolutionFailed(refItem.TargetName, ex.Message);
            }
        }

        PropagateTransitiveImplements(directInherits, directImplements, referenceRelationships, inheritanceRels, symbols, interfaceToImplementors);
    }

    private static void PropagateTransitiveImplements(
        Dictionary<string, List<string>> directInherits,
        Dictionary<string, List<(string TargetId, string TargetName)>> directImplements,
        List<Relationship> referenceRelationships,
        HashSet<(string From, string To)> inheritanceRels,
        SymbolLookups symbols,
        Dictionary<string, List<string>> interfaceToImplementors)
    {
        if (directInherits.Count == 0 || directImplements.Count == 0) return;

        const int maxInheritanceDepth = 5;
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var queue = new Queue<(string BaseId, int Depth)>();

        foreach (var (derivedId, immediateBases) in directInherits)
        {
            visited.Clear();
            queue.Clear();
            visited.Add(derivedId);
            for (var i = 0; i < immediateBases.Count; i++)
            {
                var b = immediateBases[i];
                if (visited.Add(b)) queue.Enqueue((b, 1));
            }

            var derivedClassName = ExtractSymbolNameFromId(derivedId);
            while (queue.Count > 0)
            {
                var (currBaseId, depth) = queue.Dequeue();
                if (directImplements.TryGetValue(currBaseId, out var implementedIfaces))
                {
                    for (var i = 0; i < implementedIfaces.Count; i++)
                    {
                        var (ifaceId, ifaceName) = implementedIfaces[i];
                        if (derivedId != ifaceId && inheritanceRels.Add((derivedId, ifaceId)))
                        {
                            referenceRelationships.Add(Relationship.FromRelationship(new ImplementsRelationship(derivedId, ifaceId)));
                            symbols.InheritanceRels.Add((derivedId, ifaceId));
                        }
                        if (!string.IsNullOrEmpty(derivedClassName))
                        {
                            if (!interfaceToImplementors.TryGetValue(ifaceName, out var implList))
                            {
                                implList = [];
                                interfaceToImplementors[ifaceName] = implList;
                            }
                            if (!implList.Contains(derivedClassName))
                            {
                                implList.Add(derivedClassName);
                            }
                        }
                    }
                }

                if (depth < maxInheritanceDepth && directInherits.TryGetValue(currBaseId, out var parentBases))
                {
                    for (var i = 0; i < parentBases.Count; i++)
                    {
                        var nextBase = parentBases[i];
                        if (visited.Add(nextBase)) queue.Enqueue((nextBase, depth + 1));
                    }
                }
            }
        }
    }

    private static BindingLookups BuildBindingLookups(ParsingContext ctx)
    {
        var bindingsLookup = new Dictionary<(string FilePath, string VarName), List<IndexedBinding>>(ctx.RawTypeBindings.Count);
        var globalBindingsLookup = new Dictionary<string, List<IndexedBinding>>(StringComparer.Ordinal);
        foreach (var b in ctx.RawTypeBindings)
        {
            var normPath = b.FilePath.Replace('\\', '/');
            var key = (normPath, b.VariableName);
            if (!bindingsLookup.TryGetValue(key, out var list))
            {
                list = new List<IndexedBinding>(1);
                bindingsLookup[key] = list;
            }
            var ib = new IndexedBinding(b.TypeName, b.ScopeId, normPath);
            list.Add(ib);

            if (!globalBindingsLookup.TryGetValue(b.VariableName, out var gList))
            {
                gList = new List<IndexedBinding>(1);
                globalBindingsLookup[b.VariableName] = gList;
            }
            gList.Add(ib);
        }

        var constantLookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var v in ctx.RawVariables)
        {
            if (v.IsConstant && !string.IsNullOrEmpty(v.InitializerText) && !constantLookup.ContainsKey(v.Name))
            {
                var rawInit = v.InitializerText.Trim();
                string? cleanVal = null;
                if ((rawInit.StartsWith('"') && rawInit.EndsWith('"')) ||
                    (rawInit.StartsWith('\'') && rawInit.EndsWith('\'')) ||
                    (rawInit.StartsWith('`') && rawInit.EndsWith('`')))
                {
                    cleanVal = rawInit.Trim('\'', '"', '`');
                }
                else if (rawInit.Contains("??"))
                {
                    var fallbackMatch = System.Text.RegularExpressions.Regex.Match(rawInit, @"\?\?\s*['""]([^'""]+)['""]");
                    if (fallbackMatch.Success)
                    {
                        cleanVal = fallbackMatch.Groups[1].Value;
                    }
                }

                if (!string.IsNullOrEmpty(cleanVal) &&
                    !cleanVal.Contains('\n') &&
                    !cleanVal.Contains('(') &&
                    !cleanVal.Contains(')') &&
                    !cleanVal.Contains('{') &&
                    !cleanVal.Contains('}') &&
                    !cleanVal.Contains("await", StringComparison.OrdinalIgnoreCase) &&
                    cleanVal.Length <= 200)
                {
                    constantLookup[v.Name] = cleanVal;
                }
            }
        }

        return new BindingLookups(bindingsLookup, globalBindingsLookup, constantLookup);
    }

    private static List<string> ResolveImportCandidateFiles(
        string callerFile,
        string importPath,
        ImportLookups imports,
        ParsingContext ctx,
        SymbolLookups symbols)
    {
        if (imports.CandidateCache != null && imports.CandidateCache.TryGetValue((callerFile, importPath), out var cached))
        {
            return cached;
        }

        var results = new List<string>();

        // Case A: Relative import (./ or ../ or /)
        if (importPath.StartsWith('.') || importPath.StartsWith('/'))
        {
            var callerDir = Path.GetDirectoryName(callerFile)?.Replace('\\', '/') ?? "";
            var combined = NormalizeRelativePath(string.IsNullOrEmpty(callerDir)
                ? importPath.TrimStart('.', '/')
                : $"{callerDir}/{importPath}");

            AddFileCandidates(combined, results, symbols);
            imports.CandidateCache?[(callerFile, importPath)] = results;
            return results;
        }

        // Case B: tsconfig paths alias (e.g. @/* or exact)
        foreach (var (pattern, target) in imports.TsConfigPaths)
        {
            if (pattern.EndsWith("/*") && target.EndsWith("/*"))
            {
                var prefix = pattern[..^2];
                if (importPath.StartWithIC(prefix))
                {
                    var remainder = importPath[prefix.Length..];
                    var mapped = target[..^2] + remainder;
                    AddFileCandidates(NormalizeRelativePath(mapped), results, symbols);
                }
            }
            else if (string.Equals(pattern, importPath, StringComparison.OrdinalIgnoreCase))
            {
                AddFileCandidates(NormalizeRelativePath(target), results, symbols);
            }
        }

        if (results.Count > 0)
        {
            imports.CandidateCache?[(callerFile, importPath)] = results;
            return results;
        }

        // Case C: Workspace packages (monorepos)
        // Check exact package name
        if (imports.WorkspacePackages.TryGetValue(importPath, out var pkgInfo))
        {
            AddPackageEntryCandidates(pkgInfo, results, symbols);
            imports.CandidateCache?[(callerFile, importPath)] = results;
            return results;
        }

        // Check package prefix (subpath import: @repro/text/sub)
        var slashIdx = importPath.IndexOf('/');
        if (slashIdx > 0)
        {
            var firstSlash = slashIdx;
            if (importPath.StartsWith('@'))
            {
                var secondSlash = importPath.IndexOf('/', firstSlash + 1);
                if (secondSlash > 0) firstSlash = secondSlash;
            }

            var pkgName = importPath[..firstSlash];
            var subPath = importPath[(firstSlash + 1)..];

            if (imports.WorkspacePackages.TryGetValue(pkgName, out var subPkgInfo))
            {
                var subCombined = NormalizeRelativePath($"{subPkgInfo.ProjectPath}/{subPath}");
                AddFileCandidates(subCombined, results, symbols);
                var srcCombined = NormalizeRelativePath($"{subPkgInfo.ProjectPath}/src/{subPath}");
                AddFileCandidates(srcCombined, results, symbols);
                imports.CandidateCache?[(callerFile, importPath)] = results;
                return results;
            }
        }

        imports.CandidateCache?[(callerFile, importPath)] = results;
        return results;
    }

    private static void AddPackageEntryCandidates(PackageTargetInfo pkgInfo, List<string> results, SymbolLookups symbols)
    {
        if (!string.IsNullOrEmpty(pkgInfo.MainFile))
        {
            AddFileCandidates(NormalizeRelativePath($"{pkgInfo.ProjectPath}/{pkgInfo.MainFile}"), results, symbols);
        }

        AddFileCandidates(NormalizeRelativePath($"{pkgInfo.ProjectPath}/src/index"), results, symbols);
        AddFileCandidates(NormalizeRelativePath($"{pkgInfo.ProjectPath}/index"), results, symbols);
    }

    private static void AddFileCandidates(string basePath, List<string> results, SymbolLookups symbols)
    {
        var candidates = new List<string>
        {
            basePath,
            basePath + ".ts",
            basePath + ".tsx",
            basePath + ".js",
            basePath + ".jsx",
            basePath + ".mjs",
            basePath + ".cjs",
            basePath + "/index.ts",
            basePath + "/index.tsx",
            basePath + "/index.js",
            basePath + "/index.jsx"
        };

        if (basePath.EndsWith(".js", StringComparison.OrdinalIgnoreCase))
        {
            var noExt = basePath[..^3];
            candidates.Add(noExt + ".ts");
            candidates.Add(noExt + ".tsx");
        }

        foreach (var cand in candidates)
        {
            if (symbols.KnownFiles.Contains(cand) && !results.Contains(cand))
            {
                results.Add(cand);
            }
        }
    }

    private static readonly JsonDocumentOptions PkgJsonOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private static ImportLookups BuildImportLookups(ParsingContext ctx, Layer4Result? l4Result)
    {
        var fileImports = new Dictionary<(string FilePath, string LocalName), ImportedSymbolEntry>();
        var starImportsByFile = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var imp in ctx.RawImports)
        {
            if (string.IsNullOrEmpty(imp.FilePath) || string.IsNullOrEmpty(imp.Path)) continue;

            var normFile = imp.FilePath.Replace('\\', '/');

            if (imp.Specifiers != null && imp.Specifiers.Count > 0)
            {
                foreach (var spec in imp.Specifiers)
                {
                    if (spec.IsNamespace || spec.LocalName == "*")
                    {
                        if (!starImportsByFile.TryGetValue(normFile, out var starList))
                        {
                            starList = [];
                            starImportsByFile[normFile] = starList;
                        }
                        starList.Add(imp.Path);

                        if (!string.IsNullOrEmpty(spec.LocalName) && spec.LocalName != "*")
                        {
                            fileImports[(normFile, spec.LocalName)] = new ImportedSymbolEntry(
                                imp.Path,
                                "*",
                                IsDefault: false,
                                IsNamespace: true
                            );
                        }
                    }
                    else
                    {
                        fileImports[(normFile, spec.LocalName)] = new ImportedSymbolEntry(
                            imp.Path,
                            spec.ImportedName ?? spec.LocalName,
                            spec.IsDefault,
                            IsNamespace: false
                        );
                    }
                }
            }
        }

        var workspacePackages = new Dictionary<string, PackageTargetInfo>(StringComparer.OrdinalIgnoreCase);
        var inMemoryProjects = l4Result?.Prev.Prev.Projects ?? ctx.ProjectsStructure?.Children.OfType<CodeExplorer.Core.Common.Nodes.Layer2_Boundaries.ProjectNode>().ToList();
        if (inMemoryProjects != null && inMemoryProjects.Count > 0)
        {
            foreach (var proj in inMemoryProjects)
            {
                var pkgName = proj.PackageName;
                var mainFile = proj.MainFile ?? proj.ModuleFile;
                var normProjPath = proj.Path.Replace('\\', '/');
                if (!string.IsNullOrEmpty(pkgName))
                {
                    workspacePackages[pkgName] = new PackageTargetInfo(normProjPath, mainFile);
                    if (pkgName.StartsWith('@') && pkgName.Contains('/'))
                    {
                        var unqualified = pkgName[(pkgName.IndexOf('/') + 1)..];
                        workspacePackages.TryAdd(unqualified, new PackageTargetInfo(normProjPath, mainFile));
                    }
                }
                if (!string.IsNullOrEmpty(proj.Name))
                {
                    workspacePackages.TryAdd(proj.Name, new PackageTargetInfo(normProjPath, mainFile));
                }
            }
        }

        var tsConfigPaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (k, v) in ctx.TsConfigPaths) tsConfigPaths[k] = v;

        return new ImportLookups(fileImports, starImportsByFile, workspacePackages, tsConfigPaths);
    }

    private static void ResolveDependsOnReference(
        Reference refItem,
        SymbolLookups symbols,
        List<Relationship> referenceRelationships)
    {
        var found = symbols.TableSymbols.TryGetValue(refItem.TargetName, out var targetTableId);
        if (!found && refItem.TargetName.Contains('.'))
        {
            var dotIdx = refItem.TargetName.LastIndexOf('.');
            found = symbols.TableSymbols.TryGetValue(refItem.TargetName[(dotIdx + 1)..], out targetTableId);
        }

        if (found && targetTableId != null)
        {
            var filePath = ExtractFilePathFromSymbolId(refItem.ScopeSymbolId);
            var cleanFilePath = !string.IsNullOrEmpty(filePath) ? filePath : null;

            referenceRelationships.Add(
                Relationship.FromRelationship(new AccessedByRelationship(targetTableId, refItem.ScopeSymbolId)
                {
                    Operation = "SELECT",
                    EvidenceKind = OntologyConstants.EvidenceKinds.Query,
                    FilePath = cleanFilePath
                }));
            if (refItem.ScopeSymbolId.Contains($":{OntologyConstants.IdPrefixes.Query}:") || refItem.ScopeSymbolId.Contains(":query:"))
            {
                referenceRelationships.Add(Relationship.FromRelationship(new DependsOnRelationship(refItem.ScopeSymbolId, targetTableId)
                {
                    Operation = "SELECT",
                    EvidenceKind = OntologyConstants.EvidenceKinds.Query,
                    FilePath = cleanFilePath
                }));
            }
        }
    }

    private static void ResolvePersistedInReference(
        Reference refItem,
        ParsingContext ctx,
        SymbolLookups symbols,
        List<Relationship> referenceRelationships,
        HashSet<(string From, string To, string Kind)> existingRefRels)
    {
        if (!symbols.TypeSymbols.TryGetValue(refItem.ScopeSymbolId, out var tid) &&
            refItem.ScopeSymbolId.Contains(":Type:", StringComparison.Ordinal))
        {
            tid = refItem.ScopeSymbolId;
        }

        if (!string.IsNullOrEmpty(tid))
        {
            var targetTableId = symbols.TableSymbols.TryGetValue(refItem.TargetName, out var tblId)
                ? tblId
                : $"{ctx.WorkspaceId}:{OntologyConstants.IdPrefixes.Table}:{refItem.TargetName.ToLowerInvariant()}";
            if (tid != targetTableId)
            {
                var filePath = ExtractFilePathFromSymbolId(tid);
                var cleanFilePath = !string.IsNullOrEmpty(filePath) ? filePath : null;
                if (existingRefRels.Add((targetTableId, tid, OntologyConstants.Relationships.ImplementedBy)))
                {
                    referenceRelationships.Add(Relationship.FromRelationship(new ImplementedByRelationship(targetTableId, tid)
                    {
                        Operation = "ORM",
                        EvidenceKind = OntologyConstants.EvidenceKinds.Orm,
                        FilePath = cleanFilePath
                    }));
                }
                if (existingRefRels.Add((targetTableId, tid, OntologyConstants.Relationships.AccessedBy)))
                {
                    referenceRelationships.Add(Relationship.FromRelationship(new AccessedByRelationship(targetTableId, tid)
                    {
                        Operation = "ORM",
                        EvidenceKind = OntologyConstants.EvidenceKinds.Orm,
                        FilePath = cleanFilePath
                    }));
                }
            }
        }
    }

    private static void ResolveUsesTypeReference(Reference refItem, ParsingContext ctx, SymbolLookups symbols, List<Relationship> referenceRelationships) =>
        Layer5SymbolVisibilityResolver.ResolveUsesTypeReference(refItem, ctx, symbols, referenceRelationships);

    private static void ResolveAccessedByOrImplementedByReference(Reference refItem, ParsingContext ctx, SymbolLookups symbols, List<Relationship> referenceRelationships) =>
        Layer5SymbolVisibilityResolver.ResolveAccessedByOrImplementedByReference(refItem, symbols, referenceRelationships);

    private static void ResolveReadsOrWritesReference(Reference refItem, SymbolLookups symbols, List<Relationship> referenceRelationships) =>
        Layer5SymbolVisibilityResolver.ResolveReadsOrWritesReference(refItem, symbols, referenceRelationships);
}

internal static partial class Layer5SymbolVisibilityResolver
{
    private const string PluralSuffixS = "s";
    private const string PluralSuffixEs = "es";
    private const string SnakeCaseRegexPattern = "([a-z0-9])([A-Z])";
    private const string SnakeCaseRegexReplacement = "$1_$2";

    internal static void ResolveUsesTypeReference(
        Reference refItem,
        ParsingContext ctx,
        Layer5AnalysisParser.SymbolLookups symbols,
        List<Relationship> referenceRelationships)
    {
        var targetName = refItem.TargetName;
        var targetTypeId = ResolveVisibleTypeSymbol(targetName, refItem.ScopeSymbolId, ctx, symbols);
        if (targetTypeId != null && refItem.ScopeSymbolId != targetTypeId)
        {
            referenceRelationships.Add(Relationship.CreateWithCallSite(
                refItem.ScopeSymbolId,
                targetTypeId,
                CodeExplorer.Cypher.Common.RelationshipKind.UsesType,
                refItem.Line,
                refItem.Column,
                refItem.Context,
                refItem.ArgFlow));
            return;
        }

        if (refItem.HasExplicitReceiver)
        {
            var shortTypeId = ResolveVisibleTypeSymbol(refItem.MemberName, refItem.ScopeSymbolId, ctx, symbols);
            if (shortTypeId != null && refItem.ScopeSymbolId != shortTypeId)
            {
                referenceRelationships.Add(Relationship.CreateWithCallSite(
                    refItem.ScopeSymbolId,
                    shortTypeId,
                    CodeExplorer.Cypher.Common.RelationshipKind.UsesType,
                    refItem.Line,
                    refItem.Column,
                    refItem.Context,
                    refItem.ArgFlow));
            }
        }
    }

    internal static void ResolveAccessedByOrImplementedByReference(
        Reference refItem,
        Layer5AnalysisParser.SymbolLookups symbols,
        List<Relationship> referenceRelationships)
    {
        var targetName = refItem.TargetName;
        if (string.IsNullOrEmpty(targetName)) return;

        var isImpl = refItem.Kind == RelationshipKind.ImplementedBy;

        if (symbols.TypeSymbols.TryGetValue(targetName, out var directTypeId) && refItem.ScopeSymbolId != directTypeId)
        {
            var rel = isImpl
                ? Relationship.FromRelationship(new ImplementedByRelationship(refItem.ScopeSymbolId, directTypeId))
                : Relationship.FromRelationship(new AccessedByRelationship(refItem.ScopeSymbolId, directTypeId));
            referenceRelationships.Add(rel);
            return;
        }

        if (refItem.HasExplicitReceiver)
        {
            var methodOrMember = refItem.MemberName;
            var qualifier = refItem.ReceiverName!;
            var shortTypeName = qualifier.Contains('.') ? qualifier[(qualifier.LastIndexOf('.') + 1)..] : qualifier;

            var typeMethodKey = $"{shortTypeName}.{methodOrMember}";
            if (symbols.FunctionSymbols.TryGetValue(typeMethodKey, out var fnId) && refItem.ScopeSymbolId != fnId)
            {
                var rel = isImpl
                    ? Relationship.FromRelationship(new ImplementedByRelationship(refItem.ScopeSymbolId, fnId))
                    : Relationship.FromRelationship(new AccessedByRelationship(refItem.ScopeSymbolId, fnId));
                referenceRelationships.Add(rel);
            }
            else if (symbols.FunctionsByName.TryGetValue(methodOrMember, out var candidateFns))
            {
                var matched = candidateFns.FirstOrDefault(id => id.Contains(shortTypeName, StringComparison.OrdinalIgnoreCase))
                              ?? (candidateFns.Count == 1 ? candidateFns[0] : null);
                if (matched != null && refItem.ScopeSymbolId != matched)
                {
                    var rel = isImpl
                        ? Relationship.FromRelationship(new ImplementedByRelationship(refItem.ScopeSymbolId, matched))
                        : Relationship.FromRelationship(new AccessedByRelationship(refItem.ScopeSymbolId, matched));
                    referenceRelationships.Add(rel);
                }
            }

            if (symbols.TypeSymbols.TryGetValue(shortTypeName, out var typeId) && refItem.ScopeSymbolId != typeId)
            {
                var rel = isImpl
                    ? Relationship.FromRelationship(new ImplementedByRelationship(refItem.ScopeSymbolId, typeId))
                    : Relationship.FromRelationship(new AccessedByRelationship(refItem.ScopeSymbolId, typeId));
                referenceRelationships.Add(rel);
            }
            return;
        }

        if (symbols.FunctionsByName.TryGetValue(targetName, out var singleFns) && singleFns.Count == 1 && refItem.ScopeSymbolId != singleFns[0])
        {
            var rel = isImpl
                ? Relationship.FromRelationship(new ImplementedByRelationship(refItem.ScopeSymbolId, singleFns[0]))
                : Relationship.FromRelationship(new AccessedByRelationship(refItem.ScopeSymbolId, singleFns[0]));
            referenceRelationships.Add(rel);
        }
    }

    internal static void ResolveReadsOrWritesReference(
        Reference refItem,
        Layer5AnalysisParser.SymbolLookups symbols,
        List<Relationship> referenceRelationships)
    {
        var targetName = refItem.TargetName;
        if (string.IsNullOrEmpty(targetName)) return;

        void AddTableAccessRel(string resolvedTableId)
        {
            var rel = refItem.Kind == RelationshipKind.WritesTo
                ? Relationship.FromRelationship(new WritesToRelationship(refItem.ScopeSymbolId, resolvedTableId))
                : Relationship.FromRelationship(new ReadsFromRelationship(refItem.ScopeSymbolId, resolvedTableId));
            rel.Line = refItem.Line;
            rel.Column = refItem.Column;
            rel.CallContext = refItem.Context;
            referenceRelationships.Add(rel);
        }

        if (symbols.TableSymbols.TryGetValue(targetName, out var tableId) && refItem.ScopeSymbolId != tableId)
        {
            AddTableAccessRel(tableId);
            return;
        }

        if (refItem.HasExplicitReceiver)
        {
            var shortTableName = refItem.MemberName;
            if (symbols.TableSymbols.TryGetValue(shortTableName, out var shortTableId) && refItem.ScopeSymbolId != shortTableId)
            {
                AddTableAccessRel(shortTableId);
                return;
            }
        }

        string? candidateTableId = null;
        if (targetName.EndsWith(PluralSuffixS, StringComparison.OrdinalIgnoreCase))
        {
            symbols.TableSymbols.TryGetValue(targetName[..^1], out candidateTableId);
            if (candidateTableId == null && targetName.EndsWith(PluralSuffixEs, StringComparison.OrdinalIgnoreCase))
                symbols.TableSymbols.TryGetValue(targetName[..^2], out candidateTableId);
        }
        else
        {
            symbols.TableSymbols.TryGetValue(targetName + PluralSuffixS, out candidateTableId);
            if (candidateTableId == null)
                symbols.TableSymbols.TryGetValue(targetName + PluralSuffixEs, out candidateTableId);
        }
        if (candidateTableId == null)
        {
            var snakeName = System.Text.RegularExpressions.Regex.Replace(targetName, SnakeCaseRegexPattern, SnakeCaseRegexReplacement).ToLowerInvariant();
            if (snakeName != targetName)
                symbols.TableSymbols.TryGetValue(snakeName, out candidateTableId);
        }

        if (!string.IsNullOrEmpty(candidateTableId) && refItem.ScopeSymbolId != candidateTableId)
        {
            AddTableAccessRel(candidateTableId);
        }
    }
    internal static string? ResolveVisibleTypeSymbol(
        string typeName,
        string callerSymbolId,
        ParsingContext ctx,
        Layer5AnalysisParser.SymbolLookups symbols)
    {
        if (string.IsNullOrEmpty(typeName)) return null;

        var callerFile = Layer5AnalysisParser.ExtractFilePathFromSymbolId(callerSymbolId);

        if (symbols.TypesByName.TryGetValue(typeName, out var candidates) && candidates.Count > 0)
        {
            return SelectVisibleSymbolCandidate(
                candidates,
                callerSymbolId,
                callerFile,
                hasExplicitReceiver: false,
                receiverTypeName: null,
                symbols,
                ctx);
        }

        if (symbols.TypeSymbols.TryGetValue(typeName, out var singleId))
        {
            if (IsSymbolVisibleToCaller(callerSymbolId, callerFile, singleId, hasExplicitReceiver: false, receiverTypeName: null, symbols, ctx))
            {
                return singleId;
            }
        }

        var lastDot = typeName.LastIndexOf('.');
        if (lastDot > 0 && lastDot < typeName.Length - 1)
        {
            var nsQualifier = typeName[..lastDot];
            var shortTypeName = typeName[(lastDot + 1)..];
            if (symbols.TypesByName.TryGetValue(shortTypeName, out var shortCandidates) && shortCandidates.Count > 0)
            {
                var nsMatched = new List<string>();
                for (var i = 0; i < shortCandidates.Count; i++)
                {
                    var cid = shortCandidates[i];
                    if (string.Equals(cid, callerSymbolId, StringComparison.Ordinal)) continue;
                    var cScope = GetOrInferSymbolScope(cid, null, ctx);
                    if (!string.IsNullOrEmpty(cScope.Namespace) &&
                        (string.Equals(cScope.Namespace, nsQualifier, StringComparison.Ordinal) ||
                         cScope.Namespace.EndsWith("." + nsQualifier, StringComparison.Ordinal)))
                    {
                        nsMatched.Add(cid);
                    }
                }
                var pool = nsMatched.Count > 0 ? nsMatched : shortCandidates;
                return SelectVisibleSymbolCandidate(
                    pool,
                    callerSymbolId,
                    callerFile,
                    hasExplicitReceiver: false,
                    receiverTypeName: null,
                    symbols,
                    ctx);
            }
        }

        return null;
    }

    internal static (Dictionary<string, HashSet<string>> Reachable, HashSet<string> ProjectsInDepGraph) BuildProjectReachabilityMatrix(
        ParsingContext ctx,
        Layer4Result? l4Result)
    {
        var reachable = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var projectsInDepGraph = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (ctx.GlobalProjectDependencies.Count == 0)
        {
            return (reachable, projectsInDepGraph);
        }

        var projectKeysById = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var inMemoryProjects = l4Result?.Prev.Prev.Projects ?? ctx.ProjectsStructure?.Children.OfType<CodeExplorer.Core.Common.Nodes.Layer2_Boundaries.ProjectNode>().ToList();
        if (inMemoryProjects != null)
        {
            foreach (var p in inMemoryProjects)
            {
                var keys = new List<string>(2);
                if (!string.IsNullOrWhiteSpace(p.Name)) keys.Add(p.Name);
                var extracted = ConstantRegistry.ExtractProjectName(p.Path);
                if (!string.IsNullOrWhiteSpace(extracted) && !keys.Contains(extracted, StringComparer.OrdinalIgnoreCase))
                {
                    keys.Add(extracted);
                }
                if (keys.Count > 0)
                {
                    projectKeysById[p.Id] = keys;
                    foreach (var k in keys)
                    {
                        projectsInDepGraph.Add(k);
                    }
                }
            }
        }

        static List<string>? ResolveProjectIdKeys(string nodeId, Dictionary<string, List<string>> map)
        {
            if (map.TryGetValue(nodeId, out var existing)) return existing;
            if (!Urn.TryParse(nodeId, out var urn) || !urn.IsProject)
            {
                return null;
            }

            var projName = !string.IsNullOrWhiteSpace(urn.Name) ? urn.Name : urn.Path;
            if (string.IsNullOrWhiteSpace(projName))
            {
                return null;
            }

            var list = new List<string> { projName };
            if (!string.IsNullOrWhiteSpace(urn.Path))
            {
                var ext = ConstantRegistry.ExtractProjectName(urn.Path);
                if (!string.IsNullOrWhiteSpace(ext) && !list.Contains(ext, StringComparer.OrdinalIgnoreCase))
                {
                    list.Add(ext);
                }
            }
            map[nodeId] = list;
            return list;
        }

        var directEdges = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        void AddDirectEdge(string fromKey, string toKey)
        {
            if (!directEdges.TryGetValue(fromKey, out var set))
            {
                set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                directEdges[fromKey] = set;
            }
            set.Add(toKey);
        }

        var hasAnyProjectToProjectEdge = false;
        foreach (var rel in ctx.GlobalProjectDependencies)
        {
            if (rel.Kind != RelationshipKind.DependsOn) continue;
            var fromKeys = ResolveProjectIdKeys(rel.From, projectKeysById);
            var toKeys = ResolveProjectIdKeys(rel.To, projectKeysById);
            if (fromKeys == null || toKeys == null) continue;

            hasAnyProjectToProjectEdge = true;
            foreach (var fk in fromKeys)
            {
                projectsInDepGraph.Add(fk);
                foreach (var tk in toKeys)
                {
                    projectsInDepGraph.Add(tk);
                    AddDirectEdge(fk, tk);
                }
            }
        }

        if (!hasAnyProjectToProjectEdge)
        {
            projectsInDepGraph.Clear();
            return (reachable, projectsInDepGraph);
        }

        foreach (var startProj in projectsInDepGraph)
        {
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { startProj };
            var queue = new Queue<string>();
            queue.Enqueue(startProj);
            while (queue.Count > 0)
            {
                var curr = queue.Dequeue();
                if (directEdges.TryGetValue(curr, out var neighbors))
                {
                    foreach (var next in neighbors)
                    {
                        if (visited.Add(next))
                        {
                            queue.Enqueue(next);
                        }
                    }
                }
            }
            reachable[startProj] = visited;
        }

        return (reachable, projectsInDepGraph);
    }
}
