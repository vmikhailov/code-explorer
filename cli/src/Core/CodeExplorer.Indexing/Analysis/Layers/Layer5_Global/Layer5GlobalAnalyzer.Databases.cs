using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Common.Relationships;

namespace CodeExplorer.Core.Parser.Layers;

public partial class Layer5AnalysisParser
{
    internal readonly struct IndexedBinding
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

    private async Task PreloadTargetedSymbolsAsync(Layer5Context ctx, Dictionary<string, List<string>> interfaceToImplementors)
    {
        if (!ctx.IsSubtreeScan && ctx.IncrementalChangedFiles == null) return; await ctx.WaitForQueueDrainedAsync();

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
        HashSet<(string From, string To)>? InheritanceRels = null,
        Dictionary<(string FilePath, string TypeName, string MemberName), string>? MembersByType = null,
        List<(string MemberId, string TypeId, string DataType)>? MemberTypeCandidates = null,
        HashSet<(string DerivedName, string BaseName)>? InheritedTypeNames = null)
    {
        public Dictionary<string, Dictionary<string, List<string>>> FunctionsByProject { get; init; } = FunctionsByProject ?? new(StringComparer.OrdinalIgnoreCase);
        public List<string> ProjectsSortedByLengthDesc { get; init; } = ProjectsSortedByLengthDesc ?? [];
        public Dictionary<string, List<string>> TypesByName { get; init; } = TypesByName ?? new(StringComparer.Ordinal);
        public Dictionary<string, HashSet<string>> ReachableProjects { get; init; } = ReachableProjects ?? new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> ProjectsInDepGraph { get; init; } = ProjectsInDepGraph ?? new(StringComparer.OrdinalIgnoreCase);
        public HashSet<(string From, string To)> InheritanceRels { get; init; } = InheritanceRels ?? [];
        public Dictionary<(string FilePath, string TypeName, string MemberName), string> MembersByType { get; init; } = MembersByType ?? [];
        public List<(string MemberId, string TypeId, string DataType)> MemberTypeCandidates { get; init; } = MemberTypeCandidates ?? [];
        public HashSet<(string DerivedName, string BaseName)> InheritedTypeNames { get; init; } = InheritedTypeNames ?? [];
    }

    internal record ImportedSymbolEntry(
        string ImportPath,
        string ImportedName,
        bool IsDefault,
        bool IsNamespace);

    internal record PackageTargetInfo(
        string ProjectPath,
        string? MainFile);

    internal record ImportLookups(
        Dictionary<(string FilePath, string LocalName), ImportedSymbolEntry> FileImports,
        Dictionary<string, List<string>> StarImportsByFile,
        Dictionary<string, PackageTargetInfo> WorkspacePackages,
        Dictionary<string, string> TsConfigPaths,
        System.Collections.Concurrent.ConcurrentDictionary<(string CallerFile, string ImportPath), List<string>>? CandidateCache = null,
        System.Collections.Concurrent.ConcurrentDictionary<(string CandFile, string ExportedName), string?>? FunctionResolutionCache = null)
    {
        public System.Collections.Concurrent.ConcurrentDictionary<(string CallerFile, string ImportPath), List<string>> CandidateCache { get; init; } = CandidateCache ?? new();
        public System.Collections.Concurrent.ConcurrentDictionary<(string CandFile, string ExportedName), string?> FunctionResolutionCache { get; init; } = FunctionResolutionCache ?? new();
    }

    internal record BindingLookups(
        Dictionary<(string FilePath, string VarName), List<IndexedBinding>> BindingsLookup,
        Dictionary<string, List<IndexedBinding>> GlobalBindingsLookup,
        Dictionary<string, string> ConstantLookup,
        Dictionary<string, List<string>>? MessageHandlersByType = null)
    {
        public Dictionary<string, List<string>> MessageHandlersByType { get; init; } = MessageHandlersByType ?? new(StringComparer.Ordinal);
    }

    private static SymbolLookups BuildSymbolLookups(Layer5Context ctx, Layer4Result? l4Result = null)
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
        var procedureSymbols = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
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

        foreach (var (filePath, kind, name, id) in ctx.OverloadedFileScopedSymbols)
        {
            var normFuncFile = filePath.Replace('\\', '/');
            var proj = ConstantRegistry.ExtractProjectName(normFuncFile);
            string? scopeProj = null;
            if (ctx.SymbolScopes.TryGetValue(id, out var scopeInfo) &&
                !string.IsNullOrEmpty(scopeInfo.ProjectName) &&
                !string.Equals(scopeInfo.ProjectName, proj, StringComparison.OrdinalIgnoreCase))
            {
                scopeProj = scopeInfo.ProjectName;
            }

            if (kind == OntologyConstants.NodeLabels.Type || kind is "Class" or "Interface" or "Struct" or "Enum" or "Record")
            {
                if (!typesByName.TryGetValue(name, out var tList))
                {
                    tList = [];
                    typesByName[name] = tList;
                }
                if (!tList.Contains(id)) tList.Add(id);
            }
            else if (kind == OntologyConstants.NodeLabels.Function && !string.Equals(name, "<module_init>", StringComparison.Ordinal))
            {
                RegisterFunctionInProject(proj, name, id);
                RegisterFunctionInProject(scopeProj, name, id);
                if (!functionsByName.TryGetValue(name, out var fnList))
                {
                    fnList = [];
                    functionsByName[name] = fnList;
                }
                if (!fnList.Contains(id)) fnList.Add(id);
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
            CollectTableAndProcedureSymbols(l4Result.SemanticStructure, tableSymbols, procedureSymbols);
            if (ctx.FilesStructure != null)
            {
                CollectTableAndProcedureSymbols(ctx.FilesStructure, tableSymbols, procedureSymbols);
            }
        }

        var membersByType = new Dictionary<(string FilePath, string TypeName, string MemberName), string>();
        var memberTypeCandidates = new List<(string MemberId, string TypeId, string DataType)>();
        var syntaxStruct = ctx.SyntaxStructure;
        if (syntaxStruct != null)
        {
            CollectMemberSymbols(syntaxStruct, null, membersByType, memberTypeCandidates);
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
            projectsInDepGraph,
            null,
            membersByType,
            memberTypeCandidates);
    }

    private static void CollectMemberSymbols(
        IOntologyNode node,
        Common.Nodes.Layer3_Syntactic.TypeNode? enclosingType,
        Dictionary<(string FilePath, string TypeName, string MemberName), string> membersByType,
        List<(string MemberId, string TypeId, string DataType)> memberTypeCandidates)
    {
        if (node is Common.Nodes.Layer3_Syntactic.TypeNode tn)
        {
            enclosingType = tn;
        }
        else if (node is Common.Nodes.Layer3_Syntactic.MemberNode mn && enclosingType != null)
        {
            var normFile = (enclosingType.FilePath ?? "").Replace('\\', '/');
            membersByType.TryAdd((normFile, enclosingType.Name, mn.Name), mn.Id);
            membersByType.TryAdd(("", enclosingType.Name, mn.Name), mn.Id);
            if (!string.IsNullOrWhiteSpace(mn.DataType))
            {
                memberTypeCandidates.Add((mn.Id, enclosingType.Id, mn.DataType));
            }
        }

        foreach (var child in node.Children)
        {
            CollectMemberSymbols(child, enclosingType, membersByType, memberTypeCandidates);
        }
    }

    private static void CollectTableAndProcedureSymbols(
        IOntologyNode node,
        Dictionary<string, string> tableSymbols,
        Dictionary<string, string> procedureSymbols)
    {
        if (node is TableNode tbl && !string.IsNullOrEmpty(tbl.Name))
        {
            tableSymbols.TryAdd(tbl.Name, tbl.Id);
            if (!string.IsNullOrEmpty(tbl.Schema))
            {
                tableSymbols.TryAdd($"{tbl.Schema}.{tbl.Name}", tbl.Id);
            }
        }
        else if (node is ProcedureNode proc && !string.IsNullOrEmpty(proc.Name))
        {
            procedureSymbols.TryAdd(proc.Name, proc.Id);
            if (!string.IsNullOrEmpty(proc.Schema))
            {
                procedureSymbols.TryAdd($"{proc.Schema}.{proc.Name}", proc.Id);
            }
        }
        foreach (var child in node.Children)
        {
            CollectTableAndProcedureSymbols(child, tableSymbols, procedureSymbols);
        }
    }

    private static void ResolveInheritanceReferences(
        Layer5Context ctx,
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
                        symbols.InheritedTypeNames.Add((className, refItem.TargetName));
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
        Layer5GoSymbolHelper.ResolveImplicitGoInterfaces(ctx, symbols, referenceRelationships, inheritanceRels, interfaceToImplementors);
    }

    private static void PropagateTransitiveImplements(
        Dictionary<string, List<string>> directInherits,
        Dictionary<string, List<(string TargetId, string TargetName)>> directImplements,
        List<Relationship> referenceRelationships,
        HashSet<(string From, string To)> inheritanceRels,
        SymbolLookups symbols,
        Dictionary<string, List<string>> interfaceToImplementors)
    {
        if (directInherits.Count == 0) return;

        const int maxInheritanceDepth = 8;
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
                symbols.InheritanceRels.Add((derivedId, currBaseId));
                if (!string.IsNullOrEmpty(derivedClassName))
                {
                    var currBaseName = ExtractSymbolNameFromId(currBaseId);
                    if (!string.IsNullOrEmpty(currBaseName))
                    {
                        symbols.InheritedTypeNames.Add((derivedClassName, currBaseName));
                    }
                }
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
                            symbols.InheritedTypeNames.Add((derivedClassName, ifaceName));
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

    private static BindingLookups BuildBindingLookups(Layer5Context ctx, SymbolLookups? symbols = null)
    {
        var bindingsLookup = new Dictionary<(string FilePath, string VarName), List<IndexedBinding>>(ctx.RawTypeBindings.Count);
        var globalBindingsLookup = new Dictionary<string, List<IndexedBinding>>(StringComparer.Ordinal);
        var messageHandlersByType = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        static bool IsLikelyMessageTypeName(string typeName) =>
            typeName.Length > 7 &&
            typeName is not ("IntegrationEvent" or "DomainEvent" or "DomainEventBase" or "CommandBase" or "QueryBase" or "BaseCommand" or "BaseQuery" or "BaseEvent" or "BaseRequest") &&
            !typeName.EndsWith("IntegrationEvent", StringComparison.Ordinal) &&
            (typeName.EndsWith("Command", StringComparison.Ordinal) ||
             typeName.EndsWith("Query", StringComparison.Ordinal) ||
             typeName.EndsWith("DomainEvent", StringComparison.Ordinal) ||
             typeName.EndsWith("Request", StringComparison.Ordinal) ||
             typeName.EndsWith("Notification", StringComparison.Ordinal));

        void RegisterMessageHandler(string msgType, string handlerFnId, bool fromHandlerClassName = false)
        {
            if (string.IsNullOrWhiteSpace(msgType) || string.IsNullOrWhiteSpace(handlerFnId)) return;
            var cleanMsg = msgType.Trim().TrimEnd('?');
            var lt = cleanMsg.IndexOf('<');
            if (lt > 0)
            {
                var gt = cleanMsg.LastIndexOf('>');
                if (gt > lt + 1 && (cleanMsg.StartsWith("ConsumeContext<", StringComparison.Ordinal) ||
                                    cleanMsg.StartsWith("IRequest<", StringComparison.Ordinal) ||
                                    cleanMsg.StartsWith("ICommand<", StringComparison.Ordinal) ||
                                    cleanMsg.StartsWith("IQuery<", StringComparison.Ordinal) ||
                                    cleanMsg.StartsWith("IdentifiedCommand<", StringComparison.Ordinal)))
                {
                    var inner = cleanMsg[(lt + 1)..gt];
                    var comma = inner.IndexOf(',');
                    cleanMsg = (comma > 0 ? inner[..comma] : inner).Trim();
                }
                else
                {
                    cleanMsg = cleanMsg[..lt].Trim();
                }
            }
            if (cleanMsg.Contains('.'))
            {
                cleanMsg = cleanMsg[(cleanMsg.LastIndexOf('.') + 1)..];
            }
            if (string.IsNullOrEmpty(cleanMsg) ||
                cleanMsg is "CancellationToken" or "Object" or "object" or "String" or "string" or "Guid" or "Int32" or "int" ||
                (!fromHandlerClassName && !IsLikelyMessageTypeName(cleanMsg)))
            {
                return;
            }
            if (!messageHandlersByType.TryGetValue(cleanMsg, out var hList))
            {
                hList = [];
                messageHandlersByType[cleanMsg] = hList;
            }
            if (!hList.Contains(handlerFnId))
            {
                hList.Add(handlerFnId);
            }
        }

        static bool IsHandlerMethodName(string methodName) =>
            methodName is "Handle" or "HandleAsync" or "Execute" or "ExecuteAsync" or "Consume" or "ConsumeAsync"
                or "handle" or "execute" or "consume" or "__invoke" or "on" or "On";

        if (symbols != null)
        {
            foreach (var (fnName, fnIds) in symbols.FunctionsByName)
            {
                var dotIdx = fnName.LastIndexOf('.');
                var mName = dotIdx > 0 ? fnName[(dotIdx + 1)..] : fnName;
                if (!IsHandlerMethodName(mName))
                    continue;
                for (var i = 0; i < fnIds.Count; i++)
                {
                    var fnId = fnIds[i];
                    var clsName = dotIdx > 0
                        ? fnName[..dotIdx]
                        : (ctx.SymbolScopes.TryGetValue(fnId, out var fnScope) ? fnScope.EnclosingTypeName : null);
                    if (!string.IsNullOrEmpty(clsName) && clsName.EndsWith("Handler", StringComparison.Ordinal) && clsName.Length > 7)
                    {
                        var inferred = clsName[..^7];
                        if (IsLikelyMessageTypeName(inferred))
                        {
                            RegisterMessageHandler(inferred, fnId, fromHandlerClassName: true);
                        }
                    }
                }
            }
        }

        foreach (var b in ctx.RawTypeBindings)
        {
            if (string.Equals(b.VariableName, Layer5GoSymbolHelper.GoInterfaceMethodMarker, StringComparison.Ordinal))
            {
                continue;
            }
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

            if (symbols != null &&
                !string.IsNullOrEmpty(normPath) &&
                IsHandlerMethodName(b.ScopeId) &&
                (IsLikelyMessageTypeName(b.TypeName) ||
                 normPath.Contains("Handler", StringComparison.OrdinalIgnoreCase) ||
                 normPath.Contains("Consumer", StringComparison.OrdinalIgnoreCase) ||
                 normPath.Contains("/commands/", StringComparison.OrdinalIgnoreCase) ||
                 normPath.Contains("/queries/", StringComparison.OrdinalIgnoreCase) ||
                 normPath.Contains("/command/", StringComparison.OrdinalIgnoreCase) ||
                 normPath.Contains("/query/", StringComparison.OrdinalIgnoreCase)) &&
                symbols.FunctionSymbolsByFile.TryGetValue((normPath, b.ScopeId), out var hFnId))
            {
                RegisterMessageHandler(b.TypeName, hFnId);
            }
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
                    var fallbackMatch = NullCoalescingLiteralRegex.Match(rawInit);
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

        return new BindingLookups(bindingsLookup, globalBindingsLookup, constantLookup, messageHandlersByType);
    }

    private static List<string> ResolveImportCandidateFiles(
        string callerFile,
        string importPath,
        ImportLookups imports,
        Layer5Context ctx,
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
            var callerDir = Path.GetDirectoryName(callerFile)?.NormalizeSlashes() ?? string.Empty;
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

        // Case C: Workspace packages (monorepos & Go modules)
        if (imports.WorkspacePackages.TryGetValue(importPath, out var pkgInfo))
        {
            AddPackageEntryCandidates(pkgInfo, results, symbols);
            Layer5GoSymbolHelper.AddGoPackageDirectoryCandidates(NormalizeRelativePath(pkgInfo.ProjectPath), results, symbols);
            imports.CandidateCache?[(callerFile, importPath)] = results;
            return results;
        }

        var (matchedPkgKey, matchedPkgInfo) = Layer5GoSymbolHelper.FindLongestWorkspaceModuleMatch(importPath, imports.WorkspacePackages);
        if (matchedPkgKey != null && matchedPkgInfo != null)
        {
            var subPath = importPath[(matchedPkgKey.Length + 1)..];
            var subCombined = NormalizeRelativePath($"{matchedPkgInfo.ProjectPath}/{subPath}");
            AddFileCandidates(subCombined, results, symbols);
            Layer5GoSymbolHelper.AddGoPackageDirectoryCandidates(subCombined, results, symbols);
            AddFileCandidates(NormalizeRelativePath($"{matchedPkgInfo.ProjectPath}/src/{subPath}"), results, symbols);
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
                Layer5GoSymbolHelper.AddGoPackageDirectoryCandidates(subCombined, results, symbols);
                AddFileCandidates(NormalizeRelativePath($"{subPkgInfo.ProjectPath}/src/{subPath}"), results, symbols);
                imports.CandidateCache?[(callerFile, importPath)] = results;
                return results;
            }
        }

        if (results.Count == 0)
        {
            Layer3IncludePathResolver.AppendIncludePathCandidates(
                callerFile,
                importPath,
                symbols.KnownFiles,
                results,
                rawCand => AddFileCandidates(NormalizeRelativePath(rawCand), results, symbols));
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
            basePath + ".php",
            basePath + ".phtml",
            basePath + ".inc",
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
}

internal static partial class Layer5SymbolVisibilityResolver
{
    private const string PluralSuffixS = "s";
    private const string PluralSuffixEs = "es";
    private const string SnakeCaseRegexPattern = "([a-z0-9])([A-Z])";
    private const string SnakeCaseRegexReplacement = "$1_$2";

    internal static void ResolveMemberOfTypeRelationships(
        Layer5Context ctx,
        Layer5AnalysisParser.SymbolLookups symbols,
        List<Relationship> referenceRelationships)
    {
        if (symbols.MemberTypeCandidates.Count == 0) return;

        foreach (var (memberId, enclosingTypeId, rawDataType) in symbols.MemberTypeCandidates)
        {
            var cleanType = rawDataType.Trim().TrimEnd('?', '[', ']');
            var ltIdx = cleanType.IndexOf('<');
            if (ltIdx > 0) cleanType = cleanType[..ltIdx].Trim();
            if (string.IsNullOrEmpty(cleanType)) continue;

            var targetTypeId = ResolveVisibleTypeSymbol(cleanType, enclosingTypeId, ctx, symbols);
            if (!string.IsNullOrEmpty(targetTypeId))
            {
                referenceRelationships.Add(Relationship.FromRelationship(new OfTypeRelationship(memberId, targetTypeId)));
            }
        }
    }

    internal static void ResolveDependsOnReference(
        Reference refItem,
        Layer5Context ctx,
        Layer5AnalysisParser.SymbolLookups symbols,
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
            var filePath = Layer5AnalysisParser.ExtractFilePathFromSymbolId(refItem.ScopeSymbolId);
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
            return;
        }

        var targetTypeId = ResolveVisibleTypeSymbol(refItem.TargetName, refItem.ScopeSymbolId, ctx, symbols);
        if (targetTypeId != null && refItem.ScopeSymbolId != targetTypeId)
        {
            referenceRelationships.Add(Relationship.CreateWithCallSite(
                refItem.ScopeSymbolId,
                targetTypeId,
                RelationshipKind.DependsOn,
                refItem.Line,
                refItem.Column,
                refItem.Context,
                refItem.ArgFlow));
        }
    }

    internal static void ResolvePersistedInReference(
        Reference refItem,
        Layer5Context ctx,
        Layer5AnalysisParser.SymbolLookups symbols,
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
                var filePath = Layer5AnalysisParser.ExtractFilePathFromSymbolId(tid);
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

    internal static void ResolveUsesTypeReference(
        Reference refItem,
        Layer5Context ctx,
        Layer5AnalysisParser.SymbolLookups symbols,
        List<Relationship> referenceRelationships)
    {
        var targetName = ctx.NamedBeanTypes.TryGetValue(refItem.TargetName, out var mappedBeanType)
            ? mappedBeanType
            : refItem.TargetName;
        var targetTypeId = ResolveVisibleTypeSymbol(targetName, refItem.ScopeSymbolId, ctx, symbols);
        if (targetTypeId != null && refItem.ScopeSymbolId != targetTypeId)
        {
            referenceRelationships.Add(Relationship.CreateWithCallSite(
                refItem.ScopeSymbolId,
                targetTypeId,
                RelationshipKind.UsesType,
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
                    RelationshipKind.UsesType,
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

        Layer5GoSymbolHelper.ResolveUnqualifiedHandlerOrAccessorFallback(refItem, targetName, isImpl, symbols, referenceRelationships);
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

        if (targetName.Contains(":tbl:", StringComparison.OrdinalIgnoreCase) ||
            targetName.Contains(":table:", StringComparison.OrdinalIgnoreCase))
        {
            var lastColon = targetName.LastIndexOf(':');
            if (lastColon >= 0 && lastColon + 1 < targetName.Length)
            {
                var extractedTbl = targetName[(lastColon + 1)..];
                if (symbols.TableSymbols.TryGetValue(extractedTbl, out var urnTableId) && refItem.ScopeSymbolId != urnTableId)
                {
                    AddTableAccessRel(urnTableId);
                    return;
                }
            }
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

    internal static void ResolveForeignKeyReference(
        Reference refItem,
        Layer5AnalysisParser.SymbolLookups symbols,
        List<Relationship> referenceRelationships)
    {
        var targetName = refItem.TargetName;
        if (string.IsNullOrEmpty(targetName)) return;

        var found = symbols.TableSymbols.TryGetValue(targetName, out var targetTableId);
        if (!found && targetName.Contains('.'))
        {
            var dotIdx = targetName.LastIndexOf('.');
            found = symbols.TableSymbols.TryGetValue(targetName[(dotIdx + 1)..], out targetTableId);
        }

        if (found && !string.IsNullOrEmpty(targetTableId) && refItem.ScopeSymbolId != targetTableId)
        {
            var rel = Relationship.FromRelationship(new ReferencesRelationship(refItem.ScopeSymbolId, targetTableId));
            rel.Line = refItem.Line;
            rel.Column = refItem.Column;
            rel.CallContext = refItem.Context;
            referenceRelationships.Add(rel);
        }
    }

    internal static void ResolveSqlTriggerRelationships(
        Layer4Result? l4Result,
        FilesStructureNode? filesStructure,
        Layer5AnalysisParser.SymbolLookups symbols,
        List<Relationship> referenceRelationships,
        HashSet<(string From, string To, string Kind)> existingRefRels)
    {
        if (l4Result == null) return;

        void Walk(IOntologyNode node)
        {
            if (node is ProcedureNode proc && !string.IsNullOrWhiteSpace(proc.TriggerTable))
            {
                var targetName = proc.TriggerTable;
                var found = symbols.TableSymbols.TryGetValue(targetName, out var tableId);
                if (!found && targetName.Contains('.'))
                {
                    var dotIdx = targetName.LastIndexOf('.');
                    found = symbols.TableSymbols.TryGetValue(targetName[(dotIdx + 1)..], out tableId);
                }

                if (found && !string.IsNullOrEmpty(tableId) && existingRefRels.Add((tableId, proc.Id, OntologyConstants.Relationships.Triggers)))
                {
                    referenceRelationships.Add(Relationship.FromRelationship(new TriggersRelationship(tableId, proc.Id)
                    {
                        Operation = proc.TriggerEvent,
                        EvidenceKind = OntologyConstants.EvidenceKinds.Procedure
                    }));
                }
            }

            foreach (var child in node.Children)
            {
                Walk(child);
            }
        }

        Walk(l4Result.SemanticStructure);
        if (filesStructure != null)
        {
            Walk(filesStructure);
        }
    }
    internal static string? ResolveVisibleTypeSymbol(
        string typeName,
        string callerSymbolId,
        Layer5Context ctx,
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
        Layer5Context ctx,
        Layer4Result? l4Result)
    {
        var reachable = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var projectsInDepGraph = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (ctx.GlobalProjectDependencies.Count == 0)
        {
            return (reachable, projectsInDepGraph);
        }

        var projectKeysById = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var ambiguousKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var inMemoryProjects = ctx.Projects;
        if (inMemoryProjects != null)
        {
            var keyFrequency = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in inMemoryProjects)
            {
                if (!string.IsNullOrWhiteSpace(p.Name))
                {
                    keyFrequency[p.Name] = keyFrequency.TryGetValue(p.Name, out var c1) ? c1 + 1 : 1;
                }
                var extracted = ConstantRegistry.ExtractProjectName(p.Path);
                if (!string.IsNullOrWhiteSpace(extracted) && !string.Equals(extracted, p.Name, StringComparison.OrdinalIgnoreCase))
                {
                    keyFrequency[extracted] = keyFrequency.TryGetValue(extracted, out var c2) ? c2 + 1 : 1;
                }
            }
            foreach (var (k, count) in keyFrequency)
            {
                if (count > 1) ambiguousKeys.Add(k);
            }

            foreach (var p in inMemoryProjects)
            {
                var keys = new List<string>(3);
                var normPath = p.Path.Replace('\\', '/').Trim('/');
                if (!string.IsNullOrWhiteSpace(normPath)) keys.Add(normPath);
                if (!string.IsNullOrWhiteSpace(p.Name) && !ambiguousKeys.Contains(p.Name) && !keys.Contains(p.Name, StringComparer.OrdinalIgnoreCase))
                {
                    keys.Add(p.Name);
                }
                var extracted = ConstantRegistry.ExtractProjectName(p.Path);
                if (!string.IsNullOrWhiteSpace(extracted) && !ambiguousKeys.Contains(extracted) && !keys.Contains(extracted, StringComparer.OrdinalIgnoreCase))
                {
                    keys.Add(extracted);
                }
                if (keys.Count > 0)
                {
                    projectKeysById[p.Id] = keys;
                }
            }
        }

        List<string>? ResolveProjectIdKeys(string nodeId, Dictionary<string, List<string>> map)
        {
            if (map.TryGetValue(nodeId, out var existing)) return existing;
            if (!Urn.TryParse(nodeId, out var urn) || !urn.IsProject)
            {
                return null;
            }

            var list = new List<string>(3);
            if (!string.IsNullOrWhiteSpace(urn.Path))
            {
                var normUrnPath = urn.Path.Replace('\\', '/').Trim('/');
                if (!string.IsNullOrWhiteSpace(normUrnPath)) list.Add(normUrnPath);
                var ext = ConstantRegistry.ExtractProjectName(urn.Path);
                if (!string.IsNullOrWhiteSpace(ext) && !ambiguousKeys.Contains(ext) && !list.Contains(ext, StringComparer.OrdinalIgnoreCase))
                {
                    list.Add(ext);
                }
            }
            if (!string.IsNullOrWhiteSpace(urn.Name) && !ambiguousKeys.Contains(urn.Name) && !list.Contains(urn.Name, StringComparer.OrdinalIgnoreCase))
            {
                list.Add(urn.Name);
            }
            if (list.Count == 0) return null;
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
