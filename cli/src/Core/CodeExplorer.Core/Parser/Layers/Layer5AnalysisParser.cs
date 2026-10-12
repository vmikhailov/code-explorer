using System.Diagnostics;
using CodeExplorer.Common;
using CodeExplorer.Core.Analysis;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Common.Relationships;
using CodeExplorer.Core.Database;

namespace CodeExplorer.Core.Parser.Layers;

public class Layer5AnalysisParser
{
    public async Task<Layer5Result> ParseAsync(Layer4Result l4Result, ParsingContext ctx)
    {
        ctx.Log("[Layer5] Starting Layer 5 (Late Binding, Cross-References & Post-Indexing Analysis)...");

        // 1. Upload the early enqueued relationships after all nodes are created
        var belongsToRels = new List<Relationship>();
        
        // Syntactic BelongsTo
        foreach (var pSyntax in l4Result.Prev.SyntaxStructure.Children)
        {
            var matchedProj = l4Result.Prev.Prev.Projects.FirstOrDefault(p => p.Path == pSyntax.Path);
            if (matchedProj != null)
            {
                belongsToRels.Add(Relationship.FromRelationship(new BelongsToRelationship(pSyntax.Id, matchedProj.Id)));
            }
        }
        
        // Semantic BelongsTo
        foreach (var pSem in l4Result.SemanticStructure.Children)
        {
            var matchedProj = l4Result.Prev.Prev.Projects.FirstOrDefault(p => p.Path == pSem.Path);
            if (matchedProj != null)
            {
                belongsToRels.Add(Relationship.FromRelationship(new BelongsToRelationship(pSem.Id, matchedProj.Id)));
            }
        }

        if (belongsToRels.Count > 0)
        {
            ctx.Log($"[Layer5] Uploading {belongsToRels.Count} project containment (BelongsTo) relationships...");
            await ctx.DbClient.UploadRelationshipsAsync(belongsToRels);
            ctx.TotalRelsCount += belongsToRels.Count;
        }

        // 2. Upload project dependencies
        await UploadProjectDependenciesAsync(ctx);

        // 3. Resolve and upload global cross-references (like function CALLS)
        var referenceRelationships = await ResolveAndUploadGlobalReferencesAsync(ctx);

        // 4. Perform Late Binding
        var lateBoundRels = await PerformLateBindingAsync(l4Result, ctx);

        // 5. Run PostIndexAnalyzer
        var postAnalyzer = new PostIndexAnalyzer(ctx.DbClient);
        if (ctx.IsSubtreeScan)
        {
            await ctx.WaitForQueueDrainedAsync();
            ctx.Log("[Layer5] Running global post-indexing analysis via PostIndexAnalyzer across workspace...");
            await postAnalyzer.RunAsync(ctx.WorkspaceId, ctx);
        }
        else
        {
            await ctx.WaitForQueueDrainedAsync();
            ctx.Log("[Layer5] Running in-memory post-indexing analysis via PostIndexAnalyzer...");
            await postAnalyzer.RunInMemoryAsync(ctx, l4Result, referenceRelationships, lateBoundRels);
        }

        // 6. Run CodeIntentAnalyzer (if model / code-intent-distill is available)
        await CodeIntentAnalyzer.EnrichAsync(ctx);

        ctx.Log("[Layer5] Late binding and post-indexing analysis pass complete.");
        return new Layer5Result(l4Result, lateBoundRels);
    }

    private async Task UploadProjectDependenciesAsync(ParsingContext ctx)
    {
        if (ctx.GlobalProjectDependencies.Count > 0)
        {
            ctx.Log(
                $"[Layer5] Uploading {ctx.GlobalProjectDependencies.Count} local project dependency relationships...");
            await ctx.DbClient.UploadRelationshipsAsync(ctx.GlobalProjectDependencies);
            ctx.TotalRelsCount += ctx.GlobalProjectDependencies.Count;
        }
    }

    private readonly struct IndexedBinding
    {
        public readonly string TypeName;
        public readonly string? ScopeMarker;
        public readonly string? ScopeId;

        public IndexedBinding(string typeName, string? scopeId)
        {
            TypeName = typeName.TrimEnd('?');
            ScopeId = string.IsNullOrWhiteSpace(scopeId) ? null : scopeId;
            ScopeMarker = ScopeId != null ? $":{ScopeId}:" : null;
        }
    }

    private static string? ExtractFilePathFromSymbolId(string scopeSymbolId)
    {
        if (Urn.TryParse(scopeSymbolId, out var urn) && !string.IsNullOrEmpty(urn.Path))
        {
            var p = urn.Path;
            var colonIdx = p.IndexOf(':');
            if (colonIdx > 1) // preserve Windows drive letters like C:/
            {
                p = p[..colonIdx];
            }
            return p.Replace('\\', '/');
        }
        string[] markers =
        [
            $":{OntologyConstants.IdPrefixes.Symbol}:", ":symbol:", ":sym:",
            ":function:", ":fn:",
            $":{OntologyConstants.IdPrefixes.Procedure}:", ":procedure:", ":proc:",
            ":m:", ":method:", ":type:"
        ];
        foreach (var marker in markers)
        {
            var markerIdx = scopeSymbolId.IndexOf(marker, StringComparison.Ordinal);
            if (markerIdx >= 0)
            {
                var start = markerIdx + marker.Length;
                var end = scopeSymbolId.IndexOf(':', start);
                if (end > start)
                {
                    return scopeSymbolId[start..end].Replace('\\', '/');
                }
            }
        }
        return null;
    }

    private static string? ExtractSymbolNameFromId(string symbolId)
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
                if (r.TargetName.Contains('.'))
                {
                    var dotIdx = r.TargetName.LastIndexOf('.');
                    var typePart = r.TargetName[..dotIdx];
                    var memberPart = r.TargetName[(dotIdx + 1)..];
                    neededNames.Add(typePart);
                    neededNames.Add(memberPart);
                    candidateTypeNames.Add(typePart);
                }
                else
                {
                    candidateTypeNames.Add(r.TargetName);
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
        ctx.Log($"[Layer5] Targeted preloading: querying SQLite for {neededNames.Count} referenced symbol names across workspace...");
        var preloaded = await ctx.DbClient.LoadSymbolsByNamesAsync(neededNames, ctx.WorkspaceId, ctx.CancellationToken);

        var addedCount = 0;
        foreach (var ((kind, name), id) in preloaded)
        {
            if (ctx.GlobalSymbols.TryAdd((kind, name), id))
            {
                addedCount++;
            }
        }

        ctx.Log($"[Layer5] Targeted preloading complete: preloaded {addedCount} external symbols into memory.");
    }

    private record SymbolLookups(
        Dictionary<string, string> TypeSymbols,
        Dictionary<string, string> FunctionSymbols,
        Dictionary<(string FilePath, string FuncName), string> FunctionSymbolsByFile,
        Dictionary<string, string> ProcedureSymbols,
        Dictionary<string, string> TableSymbols,
        Dictionary<string, string> EndpointSymbols,
        Dictionary<string, string> EntryPointSymbols);

    private record BindingLookups(
        Dictionary<(string FilePath, string VarName), List<IndexedBinding>> BindingsLookup,
        Dictionary<string, List<IndexedBinding>> GlobalBindingsLookup,
        Dictionary<string, string> ConstantLookup);

    private static SymbolLookups BuildSymbolLookups(ParsingContext ctx)
    {
        var typeSymbols = new Dictionary<string, string>(StringComparer.Ordinal);
        var functionSymbols = new Dictionary<string, string>(StringComparer.Ordinal);
        var functionSymbolsByFile = new Dictionary<(string FilePath, string FuncName), string>();
        var procedureSymbols = new Dictionary<string, string>(StringComparer.Ordinal);
        var tableSymbols = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var endpointSymbols = new Dictionary<string, string>(StringComparer.Ordinal);
        var entryPointSymbols = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var (key, id) in ctx.GlobalSymbols)
        {
            switch (key.Kind)
            {
                case OntologyConstants.NodeLabels.Type:
                    typeSymbols[key.Name] = id;
                    break;
                case OntologyConstants.NodeLabels.Function:
                    functionSymbols[key.Name] = id;
                    var funcFile = ExtractFilePathFromSymbolId(id);
                    if (funcFile != null)
                    {
                        functionSymbolsByFile[(funcFile, key.Name)] = id;
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

        return new SymbolLookups(
            typeSymbols,
            functionSymbols,
            functionSymbolsByFile,
            procedureSymbols,
            tableSymbols,
            endpointSymbols,
            entryPointSymbols);
    }

    private static void ResolveInheritanceReferences(
        ParsingContext ctx,
        SymbolLookups symbols,
        List<Relationship> referenceRelationships,
        HashSet<(string From, string To)> inheritanceRels,
        Dictionary<string, List<string>> interfaceToImplementors)
    {
        foreach (var refItem in ctx.GlobalReferences)
        {
            ctx.CancellationToken.ThrowIfCancellationRequested();

            try
            {
                if (refItem.Kind == OntologyConstants.Relationships.Implements ||
                    refItem.Kind == OntologyConstants.Relationships.InheritsFrom)
                {
                    if (symbols.TypeSymbols.TryGetValue(refItem.TargetName, out var targetNodeId) && refItem.ScopeSymbolId != targetNodeId)
                    {
                        if (refItem.Kind == OntologyConstants.Relationships.Implements)
                        {
                            IOntologyRelationship rel = new ImplementsRelationship(refItem.ScopeSymbolId, targetNodeId);
                            referenceRelationships.Add(Relationship.FromRelationship(rel));
                        }
                        else
                        {
                            IOntologyRelationship rel = new InheritsFromRelationship(refItem.ScopeSymbolId, targetNodeId);
                            referenceRelationships.Add(Relationship.FromRelationship(rel));
                        }

                        inheritanceRels.Add((refItem.ScopeSymbolId, targetNodeId));
                    }
                    else if (refItem.Kind == OntologyConstants.Relationships.Implements)
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
                ctx.LogDebug($"[Layer5] Failed to resolve inheritance reference '{refItem.TargetName}': {ex.Message}");
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
            var ib = new IndexedBinding(b.TypeName, b.ScopeId);
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

    private async Task<List<Relationship>> ResolveAndUploadGlobalReferencesAsync(ParsingContext ctx)
    {
        var totalReferences = ctx.GlobalReferences.Count;
        ctx.Log($"[Layer5] Resolving {totalReferences} global cross-references...");
        var referenceRelationships = new List<Relationship>(totalReferences > 0 ? Math.Min(totalReferences, 2000000) : 0);
        var inheritanceRels = new HashSet<(string From, string To)>();
        var interfaceToImplementors = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        await PreloadTargetedSymbolsAsync(ctx, interfaceToImplementors);

        var symbols = BuildSymbolLookups(ctx);
        ResolveInheritanceReferences(ctx, symbols, referenceRelationships, inheritanceRels, interfaceToImplementors);
        var bindings = BuildBindingLookups(ctx);

        var resolvedCount = 0;
        var createdTopicIds = new HashSet<string>(StringComparer.Ordinal);
        var newTopicNodes = new List<TopicNode>();

        var lastRefReport = Stopwatch.GetTimestamp();
        foreach (var refItem in ctx.GlobalReferences)
        {
            ctx.CancellationToken.ThrowIfCancellationRequested();
            resolvedCount++;

            if (resolvedCount % 25000 == 0 || Stopwatch.GetElapsedTime(lastRefReport).TotalSeconds >= 3)
            {
                lastRefReport = Stopwatch.GetTimestamp();
                var pct = totalReferences > 0 ? (double)resolvedCount / totalReferences * 100.0 : 100.0;
                ctx.Log($"[Layer5] Resolving global cross-references: {resolvedCount}/{totalReferences} ({pct:F0}%)...");
            }

            if (refItem.Kind == OntologyConstants.Relationships.Implements ||
                refItem.Kind == OntologyConstants.Relationships.InheritsFrom)
            {
                continue;
            }

            try
            {
                switch (refItem.Kind)
                {
                    case OntologyConstants.Relationships.Calls:
                        ResolveCallsReference(refItem, ctx, symbols, bindings, interfaceToImplementors, referenceRelationships);
                        break;
                    case OntologyConstants.Relationships.DependsOn:
                        ResolveDependsOnReference(refItem, symbols, referenceRelationships);
                        break;
                    case OntologyConstants.Relationships.Triggers:
                        ResolveTriggersReference(refItem, symbols, referenceRelationships);
                        break;
                    case OntologyConstants.Relationships.PublishesTo:
                    case OntologyConstants.Relationships.SubscribesTo:
                        ResolveMessagingReference(refItem, ctx, symbols, bindings, newTopicNodes, createdTopicIds, referenceRelationships);
                        break;
                    case OntologyConstants.Relationships.PersistedIn:
                        ResolvePersistedInReference(refItem, ctx, symbols, referenceRelationships);
                        break;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                ctx.LogDebug($"[Layer5] Failed to resolve reference '{refItem.TargetName}': {ex.Message}");
            }
        }
        
        ctx.Log($"[Layer5] Resolved {resolvedCount}/{totalReferences} global cross-references (generated {referenceRelationships.Count} relationships).");

        if (newTopicNodes.Count > 0)
        {
            var nodes = newTopicNodes.Select(Node.FromNode).ToList();
            await ctx.DbClient.UploadNodesAsync(nodes);
            ctx.AddNodesCount(nodes.Count);
        }

        if (referenceRelationships.Count > 0)
        {
            ctx.Log($"[Layer5] Uploading {referenceRelationships.Count} resolved reference relationships...");
            await ctx.DbClient.UploadRelationshipsAsync(referenceRelationships);
            ctx.TotalRelsCount += referenceRelationships.Count;
        }

        return referenceRelationships;
    }

    private static void ResolveCallsReference(
        Reference refItem,
        ParsingContext ctx,
        SymbolLookups symbols,
        BindingLookups bindings,
        Dictionary<string, List<string>> interfaceToImplementors,
        List<Relationship> referenceRelationships)
    {
        var targetName = refItem.TargetName;
        string? targetTypeName = null;
        string? methodName = null;
        var isExternalStaticClass = false;
        var callerFile = ExtractFilePathFromSymbolId(refItem.ScopeSymbolId);

        if (targetName.Contains('.'))
        {
            var dotIdx = targetName.LastIndexOf('.');
            var varName = targetName[..dotIdx];
            methodName = targetName[(dotIdx + 1)..];

            if (callerFile != null)
            {
                if (bindings.BindingsLookup.TryGetValue((callerFile, varName), out var candidates))
                {
                    for (int i = 0; i < candidates.Count; i++)
                    {
                        var candidate = candidates[i];
                        if (!string.IsNullOrEmpty(candidate.ScopeMarker) && !string.IsNullOrEmpty(candidate.ScopeId) &&
                            (refItem.ScopeSymbolId.Contains(candidate.ScopeMarker, StringComparison.Ordinal) ||
                             refItem.ScopeSymbolId.Contains($":{candidate.ScopeId}:", StringComparison.OrdinalIgnoreCase) ||
                             refItem.ScopeSymbolId.Contains($"/{candidate.ScopeId}.", StringComparison.OrdinalIgnoreCase) ||
                             refItem.ScopeSymbolId.Contains($"\\{candidate.ScopeId}.", StringComparison.OrdinalIgnoreCase)))
                        {
                            targetTypeName = candidate.TypeName;
                            break;
                        }
                    }
                    targetTypeName ??= candidates[0].TypeName;
                }
                else if (bindings.GlobalBindingsLookup.TryGetValue(varName, out var globalCandidates))
                {
                    foreach (var candidate in globalCandidates)
                    {
                        if (!string.IsNullOrEmpty(candidate.ScopeMarker) && !string.IsNullOrEmpty(candidate.ScopeId) &&
                            (refItem.ScopeSymbolId.Contains(candidate.ScopeMarker, StringComparison.Ordinal) ||
                             refItem.ScopeSymbolId.Contains($":{candidate.ScopeId}:", StringComparison.OrdinalIgnoreCase) ||
                             refItem.ScopeSymbolId.Contains($"/{candidate.ScopeId}.", StringComparison.OrdinalIgnoreCase) ||
                             refItem.ScopeSymbolId.Contains($"\\{candidate.ScopeId}.", StringComparison.OrdinalIgnoreCase)))
                        {
                            targetTypeName = candidate.TypeName;
                            break;
                        }
                    }

                    targetTypeName ??= globalCandidates[0].TypeName;
                }

                if (targetTypeName != null)
                {
                    targetName = $"{targetTypeName}.{methodName}";
                }
                else if (symbols.TypeSymbols.ContainsKey(varName))
                {
                    targetName = $"{varName}.{methodName}";
                }
                else if (!string.IsNullOrEmpty(varName) && char.IsUpper(varName[0]))
                {
                    targetName = $"{varName}.{methodName}";
                    isExternalStaticClass = true;
                }
                else
                {
                    targetName = methodName;
                }
            }
            else
            {
                if (symbols.TypeSymbols.ContainsKey(varName))
                {
                    targetName = $"{varName}.{methodName}";
                }
                else if (!string.IsNullOrEmpty(varName) && char.IsUpper(varName[0]))
                {
                    targetName = $"{varName}.{methodName}";
                    isExternalStaticClass = true;
                }
                else
                {
                    targetName = methodName;
                }
            }
        }

        var callAdded = false;
        string? targetNodeId = null;
        if (callerFile != null && ctx.FileScopedSymbols.TryGetValue((callerFile, OntologyConstants.NodeLabels.Function, targetName), out var localTargetId))
        {
            referenceRelationships.Add(Relationship.FromRelationship(new CallsRelationship(refItem.ScopeSymbolId, localTargetId)));
            callAdded = true;
            targetNodeId = localTargetId;
        }
        else if (callerFile != null && symbols.FunctionSymbolsByFile.TryGetValue((callerFile, targetName), out localTargetId))
        {
            referenceRelationships.Add(Relationship.FromRelationship(new CallsRelationship(refItem.ScopeSymbolId, localTargetId)));
            callAdded = true;
            targetNodeId = localTargetId;
        }
        else if (symbols.FunctionSymbols.TryGetValue(targetName, out targetNodeId))
        {
            referenceRelationships.Add(Relationship.FromRelationship(new CallsRelationship(refItem.ScopeSymbolId, targetNodeId)));
            callAdded = true;
        }
        else if (symbols.ProcedureSymbols.TryGetValue(targetName, out var targetProcId))
        {
            referenceRelationships.Add(Relationship.FromRelationship(new CalledByRelationship(targetProcId, refItem.ScopeSymbolId)));
            callAdded = true;
        }

        if (targetTypeName != null && !string.IsNullOrEmpty(methodName) && interfaceToImplementors.TryGetValue(targetTypeName, out var implementors))
        {
            foreach (var implClass in implementors)
            {
                if (symbols.FunctionSymbols.TryGetValue($"{implClass}.{methodName}", out var implNodeId) && implNodeId != targetNodeId)
                {
                    referenceRelationships.Add(Relationship.FromRelationship(new CallsRelationship(refItem.ScopeSymbolId, implNodeId)));
                    callAdded = true;
                }
            }
        }

        if (!callAdded && !isExternalStaticClass && methodName != null)
        {
            if (callerFile != null && ctx.FileScopedSymbols.TryGetValue((callerFile, OntologyConstants.NodeLabels.Function, methodName), out var localFallbackId))
            {
                referenceRelationships.Add(Relationship.FromRelationship(new CallsRelationship(refItem.ScopeSymbolId, localFallbackId)));
            }
            else if (callerFile != null && symbols.FunctionSymbolsByFile.TryGetValue((callerFile, methodName), out localFallbackId))
            {
                referenceRelationships.Add(Relationship.FromRelationship(new CallsRelationship(refItem.ScopeSymbolId, localFallbackId)));
            }
            else if (symbols.FunctionSymbols.TryGetValue(methodName, out var fallbackNodeId))
            {
                var targetFile = ExtractFilePathFromSymbolId(fallbackNodeId);
                var isSameProjectOrFile = callerFile != null && targetFile != null &&
                    (callerFile.Equals(targetFile, StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(ConstantRegistry.ExtractProjectName(callerFile), ConstantRegistry.ExtractProjectName(targetFile), StringComparison.OrdinalIgnoreCase));

                if (isSameProjectOrFile || (callerFile == null && targetFile == null))
                {
                    referenceRelationships.Add(Relationship.FromRelationship(new CallsRelationship(refItem.ScopeSymbolId, fallbackNodeId)));
                }
            }
        }
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
            referenceRelationships.Add(
                Relationship.FromRelationship(new QueriedByRelationship(targetTableId, refItem.ScopeSymbolId)));
            if (refItem.ScopeSymbolId.Contains($":{OntologyConstants.IdPrefixes.Query}:") || refItem.ScopeSymbolId.Contains(":query:"))
            {
                referenceRelationships.Add(Relationship.FromRelationship(new DependsOnRelationship(refItem.ScopeSymbolId, targetTableId)));
            }
        }
    }

    private static void ResolveTriggersReference(
        Reference refItem,
        SymbolLookups symbols,
        List<Relationship> referenceRelationships)
    {
        var targetId = symbols.FunctionSymbols.GetValueOrDefault(refItem.TargetName);
        if (targetId == null && refItem.TargetName.Contains('.'))
        {
            var shortName = refItem.TargetName[(refItem.TargetName.LastIndexOf('.') + 1)..];
            targetId = symbols.FunctionSymbols.GetValueOrDefault(shortName);
        }

        if (targetId != null)
        {
            if (!referenceRelationships.Any(r => r.From == refItem.ScopeSymbolId && r.To == targetId && r.Kind == OntologyConstants.Relationships.Triggers))
            {
                referenceRelationships.Add(
                    Relationship.FromRelationship(new TriggersRelationship(refItem.ScopeSymbolId, targetId)));
            }
        }
    }

    private static void ResolveMessagingReference(
        Reference refItem,
        ParsingContext ctx,
        SymbolLookups symbols,
        BindingLookups bindings,
        List<TopicNode> newTopicNodes,
        HashSet<string> createdTopicIds,
        List<Relationship> referenceRelationships)
    {
        var topicName = refItem.TargetName;
        var brokerType = "event";
        (brokerType, topicName) = topicName switch
        {
            _ when topicName.StartsWith("rabbitmq:", StringComparison.OrdinalIgnoreCase) => ("rabbitmq", topicName["rabbitmq:".Length..]),
            _ when topicName.StartsWith("kafka:", StringComparison.OrdinalIgnoreCase) => ("kafka", topicName["kafka:".Length..]),
            _ when topicName.StartsWith("gcp:", StringComparison.OrdinalIgnoreCase) => ("gcp", topicName["gcp:".Length..]),
            _ when topicName.StartsWith("pubsub:", StringComparison.OrdinalIgnoreCase) => ("gcp", topicName["pubsub:".Length..]),
            _ when topicName.StartsWith("mediatr:", StringComparison.OrdinalIgnoreCase) => ("mediatr", topicName["mediatr:".Length..]),
            _ when topicName.StartsWith("masstransit:", StringComparison.OrdinalIgnoreCase) => ("masstransit", topicName["masstransit:".Length..]),
            _ when topicName.StartsWith("spring:", StringComparison.OrdinalIgnoreCase) => ("spring", topicName["spring:".Length..]),
            _ when topicName.StartsWith("cqrs:", StringComparison.OrdinalIgnoreCase) => ("cqrs", topicName["cqrs:".Length..]),
            _ when topicName.StartsWith("event:", StringComparison.OrdinalIgnoreCase) => ("event", topicName["event:".Length..]),
            _ => (brokerType, topicName)
        };

        if (brokerType == "gcp")
        {
            if (topicName.EndsWith("-SUB", StringComparison.OrdinalIgnoreCase))
            {
                topicName = topicName[..^4];
            }
            else if (topicName.EndsWith("_SUBSCRIPTION_NAME", StringComparison.OrdinalIgnoreCase))
            {
                topicName = topicName[..^"_SUBSCRIPTION_NAME".Length] + "_TOPIC_NAME";
            }
            else if (topicName.EndsWith("_SUBSCRIPTION", StringComparison.OrdinalIgnoreCase))
            {
                topicName = topicName[..^"_SUBSCRIPTION".Length] + "_TOPIC";
            }
            else if (topicName.EndsWith("_SUB", StringComparison.OrdinalIgnoreCase))
            {
                topicName = topicName[..^"_SUB".Length];
            }
        }

        if (topicName.Contains("await", StringComparison.OrdinalIgnoreCase) || topicName.Contains('('))
        {
            var innerMatch = System.Text.RegularExpressions.Regex.Match(topicName, @"\(\s*([^,\)]+)\s*\)");
            if (innerMatch.Success)
            {
                var inner = innerMatch.Groups[1].Value.Trim().Trim('\'', '"', '`');
                if (!string.IsNullOrEmpty(inner) && !inner.Equals("topic", StringComparison.OrdinalIgnoreCase))
                {
                    topicName = inner;
                }
            }
            else
            {
                return;
            }
        }

        if (topicName.StartsWith("this.config.", StringComparison.OrdinalIgnoreCase))
        {
            var prop = topicName["this.config.".Length..];
            if (ConstantRegistry.TryResolve(null, prop, out var resolved) && !string.IsNullOrEmpty(resolved))
            {
                topicName = resolved;
            }
            else
            {
                topicName = prop;
            }
        }
        else if (topicName.StartsWith("config.", StringComparison.OrdinalIgnoreCase))
        {
            var prop = topicName["config.".Length..];
            if (ConstantRegistry.TryResolve(null, prop, out var resolved) && !string.IsNullOrEmpty(resolved))
            {
                topicName = resolved;
            }
            else
            {
                topicName = prop;
            }
        }
        else if (topicName.StartsWith("this.", StringComparison.OrdinalIgnoreCase))
        {
            var prop = topicName["this.".Length..];
            if (ConstantRegistry.TryResolve(null, prop, out var resolved) && !string.IsNullOrEmpty(resolved))
            {
                topicName = resolved;
            }
        }

        var scopeFilePath = ExtractFilePathFromSymbolId(refItem.ScopeSymbolId);
        var scopeProject = ConstantRegistry.ExtractProjectName(scopeFilePath);
        string? globalConst = null;

        if (bindings.ConstantLookup.TryGetValue(topicName, out var resolvedConst) &&
            WorkspaceConventions.IsValidTopicOrQueueName(resolvedConst))
        {
            topicName = resolvedConst;
        }
        else if (ConstantRegistry.TryResolve(scopeProject ?? scopeFilePath, topicName, out var regConst) &&
                 WorkspaceConventions.IsValidTopicOrQueueName(regConst))
        {
            topicName = regConst;
        }
        else if (System.Text.RegularExpressions.Regex.IsMatch(topicName, @"^[A-Z0-9_]{3,}$") &&
                 ConstantRegistry.TryResolve(null, topicName, out globalConst) &&
                 WorkspaceConventions.IsValidTopicOrQueueName(globalConst))
        {
            topicName = globalConst;
        }

        topicName = topicName.Trim('\'', '"', '`');

        var isInternal = brokerType is "mediatr" or "cqrs";
        var topicId = $"{ctx.WorkspaceId}:{OntologyConstants.IdPrefixes.Topic}:{brokerType}:{topicName}";

        if (!createdTopicIds.Contains(topicId))
        {
            createdTopicIds.Add(topicId);
            var topicExt = new Dictionary<string, string>
            {
                ["is_internal"] = isInternal ? "true" : "false",
                ["scope"] = isInternal ? "internal" : "external",
                ["message_type"] = isInternal ? "internal" : "external",
                ["is_semantic_entity"] = isInternal ? "false" : "true"
            };
            var topicNode = new TopicNode(topicId, topicName, "", brokerType, topicExt);
            newTopicNodes.Add(topicNode);
            ctx.AddGlobalSymbol(OntologyConstants.NodeLabels.Topic, refItem.TargetName, topicId);
        }

        var relKind = refItem.Kind == OntologyConstants.Relationships.PublishesTo
            ? OntologyConstants.Relationships.PublishedBy
            : OntologyConstants.Relationships.SubscribedBy;

        var relProps = new Dictionary<string, object>
        {
            ["is_internal"] = isInternal ? "true" : "false",
            ["scope"] = isInternal ? "internal" : "external",
            ["category"] = "messaging",
            ["dependency_type"] = "messaging",
            ["broker_type"] = brokerType
        };
        if (isInternal)
        {
            relProps["is_semantic"] = "false";
        }

        referenceRelationships.Add(new Relationship(topicId, refItem.ScopeSymbolId, relKind, relProps));
        referenceRelationships.Add(new Relationship(refItem.ScopeSymbolId, topicId, refItem.Kind, relProps));
    }

    private static void ResolvePersistedInReference(
        Reference refItem,
        ParsingContext ctx,
        SymbolLookups symbols,
        List<Relationship> referenceRelationships)
    {
        if (symbols.TypeSymbols.TryGetValue(refItem.ScopeSymbolId, out var tid))
        {
            var targetTableId = symbols.TableSymbols.TryGetValue(refItem.TargetName, out var tblId)
                ? tblId
                : $"{ctx.WorkspaceId}:{OntologyConstants.IdPrefixes.Table}:{refItem.TargetName.ToLowerInvariant()}";
            if (tid != targetTableId && !referenceRelationships.Any(r => r.From == tid && r.To == targetTableId && r.Kind == OntologyConstants.Relationships.PersistedIn))
            {
                referenceRelationships.Add(Relationship.FromRelationship(new PersistedInRelationship(tid, targetTableId)));
            }
        }
    }
    private async Task<List<Relationship>> PerformLateBindingAsync(Layer4Result l4Result, ParsingContext ctx)
    {
        var workspaceNode = l4Result.Prev.Prev.Prev.Workspace;
        var allProjects = l4Result.Prev.Prev.Projects.ToList();

        var entryPoints = new List<EntryPointNode>();
        var endpoints = new List<EndpointNode>();
        var externalServices = new List<ExternalServiceNode>();

        CollectPublicSymbols(workspaceNode, entryPoints, endpoints, externalServices);

        var localExtIds = new HashSet<string>(externalServices.Select(e => e.Id));
        var localEndpointIds = new HashSet<string>(endpoints.Select(e => e.Id));
        var localEntryPointIds = new HashSet<string>(entryPoints.Select(e => e.Id));

        if (ctx.IsSubtreeScan)
        {
            var needEndpoints = externalServices.Count > 0;
            var needExternalServices = endpoints.Count > 0 || entryPoints.Count > 0;
            if (needEndpoints || needExternalServices)
            {
                var (dbEndpoints, dbEntryPoints, dbExtServices, dbProjects) = await ctx.DbClient.LoadLateBindingCandidatesAsync(
                    ctx.WorkspaceId,
                    needEndpoints: needEndpoints,
                    needExternalServices: needExternalServices,
                    ctx.CancellationToken
                );

                if (dbProjects.Count > 0)
                {
                    var curProjIds = new HashSet<string>(allProjects.Select(p => p.Id));
                    allProjects.AddRange(dbProjects.Where(p => curProjIds.Add(p.Id)));
                }

                if (needEndpoints)
                {
                    var curEpIds = new HashSet<string>(endpoints.Select(e => e.Id));
                    endpoints.AddRange(dbEndpoints.Where(e => curEpIds.Add(e.Id)));

                    var curEntryIds = new HashSet<string>(entryPoints.Select(e => e.Id));
                    entryPoints.AddRange(dbEntryPoints.Where(e => curEntryIds.Add(e.Id)));
                }

                if (needExternalServices)
                {
                    var curExtIds = new HashSet<string>(externalServices.Select(e => e.Id));
                    externalServices.AddRange(dbExtServices.Where(e => curExtIds.Add(e.Id)));
                }
            }
        }

        // Build mapping from symbol/node ID to containing ProjectNode (including DB-loaded candidates)
        var nodeToProject = new Dictionary<string, ProjectNode>();
        foreach (var pSem in l4Result.SemanticStructure.Children)
        {
            var matchedProj = allProjects.FirstOrDefault(p => p.Path == pSem.Path);
            if (matchedProj != null)
            {
                MapProjectNodes(pSem, matchedProj, nodeToProject);
            }
        }

        // Fallback mapping via file_path or path for all external services, endpoints, and entry points
        foreach (var es in externalServices)
        {
            if (!nodeToProject.ContainsKey(es.Id) && es.Extensions?.TryGetValue("file_path", out var fp) == true)
            {
                var proj = Layer2ProjectParser.FindProjectForFilePath(fp, allProjects);
                if (proj != null) nodeToProject[es.Id] = proj;
            }
        }
        foreach (var ep in endpoints)
        {
            if (!nodeToProject.ContainsKey(ep.Id))
            {
                var p = !string.IsNullOrEmpty(ep.Path) ? ep.Path : (ep.Extensions?.TryGetValue("file_path", out var efp) == true ? efp : null);
                if (!string.IsNullOrEmpty(p))
                {
                    var proj = Layer2ProjectParser.FindProjectForFilePath(p, allProjects);
                    if (proj != null) nodeToProject[ep.Id] = proj;
                }
            }
        }
        foreach (var entry in entryPoints)
        {
            if (!nodeToProject.ContainsKey(entry.Id))
            {
                var p = entry.Extensions?.TryGetValue("file_path", out var efp) == true ? efp : null;
                if (!string.IsNullOrEmpty(p))
                {
                    var proj = Layer2ProjectParser.FindProjectForFilePath(p, allProjects);
                    if (proj != null) nodeToProject[entry.Id] = proj;
                }
            }
        }

        // Prepare and filter endpoints and entry points
        var prepEndpoints = new List<PreparedEndpoint>(endpoints.Count);
        foreach (var ep in endpoints)
        {
            nodeToProject.TryGetValue(ep.Id, out var targetProj);
            var prep = PrepareEndpoint(ep, targetProj);
            if (!string.IsNullOrEmpty(prep.RouteNorm) && !prep.IsApiPrefixOnly)
            {
                prepEndpoints.Add(prep);
            }
        }

        var prepEntryPoints = new List<PreparedEntryPoint>(entryPoints.Count);
        foreach (var entry in entryPoints)
        {
            nodeToProject.TryGetValue(entry.Id, out var targetProj);
            var prep = PrepareEntryPoint(entry, targetProj);
            if (!string.IsNullOrEmpty(prep.EntryNorm) && !prep.IsApiPrefixOnly)
            {
                prepEntryPoints.Add(prep);
            }
        }

        var prepExtServices = new List<PreparedExternalService>(externalServices.Count);
        foreach (var es in externalServices)
        {
            nodeToProject.TryGetValue(es.Id, out var callerProj);
            prepExtServices.Add(PrepareExternalService(es, callerProj));
        }

        ctx.Log($"[Layer5] Late-binding {prepExtServices.Count} external service calls against {prepEndpoints.Count} endpoints and {prepEntryPoints.Count} entry points...");

        var lateBoundRels = new List<Relationship>();
        var addedProjectDeps = new HashSet<(string From, string To)>();
        var boundServicesCount = 0;

        var projectMatchCache = new Dictionary<(string ProjectId, string Domain), bool>();
        bool CheckProjectMatch(ProjectNode proj, string domain)
        {
            if (string.IsNullOrWhiteSpace(domain)) return false;
            var key = (proj.Id, domain);
            if (!projectMatchCache.TryGetValue(key, out var matches))
            {
                matches = DoesProjectMatchServiceDomain(proj, domain);
                projectMatchCache[key] = matches;
            }
            return matches;
        }

        var lastReport = Stopwatch.GetTimestamp();
        var processedExt = 0;
        var totalExt = prepExtServices.Count;

        foreach (var extService in prepExtServices)
        {
            var matchedEndpoint = false;

            var candidateEntryPoints = new List<PreparedEntryPoint>();
            foreach (var entryPoint in prepEntryPoints)
            {
                if (ctx.IsSubtreeScan && !localExtIds.Contains(extService.Service.Id) && !localEntryPointIds.Contains(entryPoint.EntryPoint.Id))
                {
                    continue;
                }

                if (IsMatch(extService, entryPoint, CheckProjectMatch))
                {
                    candidateEntryPoints.Add(entryPoint);
                }
            }

            if (extService.IsGenericHostOrService && candidateEntryPoints.Count > 1)
            {
                var distinctTargetProjects = candidateEntryPoints
                    .Select(e => e.TargetProj)
                    .Where(p => p != null)
                    .DistinctBy(p => p!.Id)
                    .ToList();

                if (distinctTargetProjects.Count > 1)
                {
                    if (extService.CallerProj != null && distinctTargetProjects.Any(p => p!.Id == extService.CallerProj.Id))
                    {
                        candidateEntryPoints.RemoveAll(e => e.TargetProj?.Id != extService.CallerProj.Id);
                    }
                    else
                    {
                        var exactMatches = candidateEntryPoints
                            .Where(e => string.Equals(extService.ServicePathNorm, e.EntryNorm, StringComparison.OrdinalIgnoreCase))
                            .ToList();
                        var exactProjects = exactMatches
                            .Select(e => e.TargetProj)
                            .Where(p => p != null)
                            .DistinctBy(p => p!.Id)
                            .ToList();

                        if (exactProjects.Count == 1)
                        {
                            candidateEntryPoints = exactMatches;
                        }
                        else
                        {
                            candidateEntryPoints.Clear();
                        }
                    }
                }
            }

            foreach (var entryPoint in candidateEntryPoints)
            {
                ctx.LogDebug($"[Layer5] [LateBinding] Binding ExternalService '{extService.Service.Id}' to EntryPoint '{entryPoint.EntryPoint.Id}'");
                var rel = Relationship.FromRelationship(new CallsRelationship(extService.Service.Id, entryPoint.EntryPoint.Id));
                lateBoundRels.Add(rel);
                matchedEndpoint = true;
                boundServicesCount++;

                var callerProj = extService.CallerProj;
                var targetProj = entryPoint.TargetProj;
                if (callerProj != null && targetProj != null && callerProj.Id != targetProj.Id)
                {
                    if (addedProjectDeps.Add((callerProj.Id, targetProj.Id)))
                    {
                        ctx.LogDebug($"[Layer5] [LateBinding] Synthesized dependency: Project '{callerProj.Name}' -> Project '{targetProj.Name}' via entrypoint '{entryPoint.EntryPoint.Name}'");
                        lateBoundRels.Add(Relationship.FromRelationship(new DependsOnRelationship(callerProj.Id, targetProj.Id, new() { ["dependency_type"] = "service_call" })));
                    }
                }
            }

            var candidateEndpoints = new List<PreparedEndpoint>();
            foreach (var endpoint in prepEndpoints)
            {
                if (ctx.IsSubtreeScan && !localExtIds.Contains(extService.Service.Id) && !localEndpointIds.Contains(endpoint.Endpoint.Id))
                {
                    continue;
                }

                if (IsMatch(extService, endpoint, CheckProjectMatch))
                {
                    candidateEndpoints.Add(endpoint);
                }
            }

            if (extService.IsGenericHostOrService && candidateEndpoints.Count > 1)
            {
                var distinctTargetProjects = candidateEndpoints
                    .Select(e => e.TargetProj)
                    .Where(p => p != null)
                    .DistinctBy(p => p!.Id)
                    .ToList();

                if (distinctTargetProjects.Count > 1)
                {
                    if (extService.CallerProj != null && distinctTargetProjects.Any(p => p!.Id == extService.CallerProj.Id))
                    {
                        candidateEndpoints.RemoveAll(e => e.TargetProj?.Id != extService.CallerProj.Id);
                    }
                    else
                    {
                        var exactMatches = candidateEndpoints
                            .Where(e => string.Equals(extService.ServicePathNorm, e.RouteNorm, StringComparison.OrdinalIgnoreCase))
                            .ToList();
                        var exactProjects = exactMatches
                            .Select(e => e.TargetProj)
                            .Where(p => p != null)
                            .DistinctBy(p => p!.Id)
                            .ToList();

                        if (exactProjects.Count == 1)
                        {
                            candidateEndpoints = exactMatches;
                        }
                        else
                        {
                            candidateEndpoints.Clear();
                        }
                    }
                }
            }

            foreach (var endpoint in candidateEndpoints)
            {
                ctx.LogDebug($"[Layer5] [LateBinding] Binding ExternalService '{extService.Service.Id}' to Endpoint '{endpoint.Endpoint.Id}'");
                var rel = Relationship.FromRelationship(new CallsEndpointRelationship(extService.Service.Id, endpoint.Endpoint.Id));
                lateBoundRels.Add(rel);
                matchedEndpoint = true;
                boundServicesCount++;

                // Also bind directly from caller function if known
                if (extService.Service.Extensions != null &&
                    extService.Service.Extensions.TryGetValue("caller_symbol_id", out var callerId) &&
                    !string.IsNullOrEmpty(callerId))
                {
                    ctx.LogDebug($"[Layer5] [LateBinding] Direct Binding Function '{callerId}' to Endpoint '{endpoint.Endpoint.Id}'");
                    lateBoundRels.Add(Relationship.FromRelationship(new CallsEndpointRelationship(callerId, endpoint.Endpoint.Id)));
                }

                // Synthesize Project -> Project DEPENDS_ON relationship
                var callerProj = extService.CallerProj;
                var targetProj = endpoint.TargetProj;

                if (callerProj != null && targetProj != null && callerProj.Id != targetProj.Id)
                {
                    if (addedProjectDeps.Add((callerProj.Id, targetProj.Id)))
                    {
                        ctx.LogDebug($"[Layer5] [LateBinding] Synthesized dependency: Project '{callerProj.Name}' -> Project '{targetProj.Name}' via endpoint '{endpoint.Endpoint.RouteTemplate}'");
                        lateBoundRels.Add(Relationship.FromRelationship(new DependsOnRelationship(callerProj.Id, targetProj.Id, new() { ["dependency_type"] = "service_call" })));
                    }
                }
            }

            // Also match by domain/service name ONLY if no endpoint was matched and a known project exists
            if (!matchedEndpoint &&
                !string.IsNullOrWhiteSpace(extService.Service.DomainOrService) &&
                extService.Service.DomainOrService is not ("*" or "unknown-service"))
            {
                if (extService.Service.Path.StartsWith('/') && !extService.Service.Path.Contains("://"))
                {
                    // Relative endpoint call, not remote microservice host
                }
                else
                {
                    var callerProj = extService.CallerProj;
                    if (callerProj != null)
                    {
                        if (!CheckProjectMatch(callerProj, extService.Service.DomainOrService))
                        {
                            foreach (var proj in allProjects)
                            {
                                if (proj.Id == callerProj.Id) continue;
                                var pName = proj.Name.ToLowerInvariant();

                                if (pName.EndsWith(".tests") || pName.EndsWith(".test") ||
                                    pName.EndsWith(".data") || pName.EndsWith(".logic") ||
                                    pName.EndsWith(".contracts") || pName.EndsWith(".client") ||
                                    pName.EndsWith(".common") || pName.EndsWith(".shared"))
                                {
                                    continue;
                                }

                                if (CheckProjectMatch(proj, extService.Service.DomainOrService))
                                {
                                    if (addedProjectDeps.Add((callerProj.Id, proj.Id)))
                                    {
                                        ctx.LogDebug($"[Layer5] [LateBinding] Inferred project dependency: Project '{callerProj.Name}' -> Project '{proj.Name}' via service domain '{extService.Service.DomainOrService}'");
                                        lateBoundRels.Add(Relationship.FromRelationship(new DependsOnRelationship(callerProj.Id, proj.Id, new() { ["dependency_type"] = "service_call" })));
                                    }
                                }
                            }
                        }
                    }
                }
            }

            processedExt++;
            if (processedExt % 100 == 0 || Stopwatch.GetElapsedTime(lastReport).TotalSeconds >= 3)
            {
                lastReport = Stopwatch.GetTimestamp();
                var pct = (double)processedExt / totalExt * 100.0;
                ctx.Log($"[Layer5] Late-binding progress: {processedExt}/{totalExt} external services ({pct:F0}%)...");
            }
        }

        if (boundServicesCount > 0)
        {
            ctx.Log($"[Layer5] [LateBinding] Successfully bound {boundServicesCount} ExternalService call(s) to endpoints/entrypoints.");
        }

        if (lateBoundRels.Count > 0)
        {
            ctx.Log($"[Layer5] Uploading {lateBoundRels.Count} late-bound relationships...");
            await ctx.DbClient.UploadRelationshipsAsync(lateBoundRels);
            ctx.TotalRelsCount += lateBoundRels.Count;
        }

        return lateBoundRels;
    }

    private static void MapProjectNodes(IOntologyNode node, ProjectNode proj, Dictionary<string, ProjectNode> map)
    {
        map[node.Id] = proj;
        foreach (var child in node.Children)
        {
            MapProjectNodes(child, proj, map);
        }
    }

    private void CollectPublicSymbols(
        IOntologyNode node,
        List<EntryPointNode> entryPoints,
        List<EndpointNode> endpoints,
        List<ExternalServiceNode> externalServices)
    {
        if (node is EntryPointNode ep)
        {
            entryPoints.Add(ep);
        }
        else if (node is EndpointNode endp)
        {
            endpoints.Add(endp);
        }
        else if (node is ExternalServiceNode es)
        {
            externalServices.Add(es);
        }

        foreach (var child in node.Children)
        {
            CollectPublicSymbols(child, entryPoints, endpoints, externalServices);
        }
    }

    private static readonly HashSet<string> AllowedApiPrefixSegments = new(StringComparer.OrdinalIgnoreCase)
    {
        "api", "v1", "v2", "v3", "v4", "v5", "v6", "rest", "internal", "public", "private"
    };

    private static readonly System.Text.RegularExpressions.Regex ServiceDomainEnvRegex = new(
        @"^(environment\.|env\.|config\.|base_url_)",
        System.Text.RegularExpressions.RegexOptions.Compiled);

    private static readonly System.Text.RegularExpressions.Regex ServiceSuffixRegex = new(
        @"(_service|service)$",
        System.Text.RegularExpressions.RegexOptions.Compiled);

    private static string CleanServiceDomain(string serviceDomain)
    {
        return ServiceDomainEnvRegex.Replace(serviceDomain, "")
            .Replace("-", "").Replace("_", "").ToLowerInvariant();
    }

    private static bool IsApiPrefixOnly(string[] segs, string? serviceDomain = null)
    {
        if (segs.Length == 0) return false;
        foreach (var seg in segs)
        {
            if (AllowedApiPrefixSegments.Contains(seg)) continue;
            if (!string.IsNullOrEmpty(serviceDomain))
            {
                var cleanDomain = CleanServiceDomain(serviceDomain);
                var cleanSeg = seg.Replace("-", "").Replace("_", "").ToLowerInvariant();
                if (cleanDomain == cleanSeg || cleanDomain.TrimEnd('s') == cleanSeg.TrimEnd('s')) continue;
            }
            return false;
        }
        return true;
    }

    private static bool IsApiPrefixOnly(string prefix, string? serviceDomain = null)
    {
        if (string.IsNullOrWhiteSpace(prefix)) return false;
        var segs = prefix.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return IsApiPrefixOnly(segs, serviceDomain);
    }

    private static readonly HashSet<string> GenericHostOrEnvNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "*", "unknown-service", "localhost", "127.0.0.1", "0.0.0.0",
        "prod", "production", "stage", "staging", "dev", "development", "test", "testing", "local", "environment", "env",
        "url", "http-call", "v1", "v2", "event", "response", "service", "api", "request", "httprequest", "pageurl"
    };

    private static readonly HashSet<string> GenericProbeOrHealthCheckPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "", "/", "*",
        "ping", "/ping",
        "health", "/health",
        "healthz", "/healthz",
        "live", "/live",
        "livez", "/livez",
        "ready", "/ready",
        "readyz", "/readyz",
        "status", "/status",
        "metrics", "/metrics",
        "version", "/version",
        "info", "/info",
        "check-deploy", "/check-deploy",
        "favicon.ico", "/favicon.ico"
    };

    private static bool IsRootOrGenericPath(string? pathNorm)
    {
        if (string.IsNullOrWhiteSpace(pathNorm)) return true;
        var p = pathNorm.Trim();
        var clean = p.Trim('/');
        return p is "/" or "*" ||
               string.IsNullOrEmpty(clean) ||
               clean is "*" ||
               GenericProbeOrHealthCheckPaths.Contains(p) ||
               GenericProbeOrHealthCheckPaths.Contains(clean);
    }

    private static bool IsGenericHostOrService(string? domainOrService)
    {
        if (string.IsNullOrWhiteSpace(domainOrService))
            return true;

        var d = domainOrService.Trim().ToLowerInvariant();
        var protoIdx = d.IndexOf("://", StringComparison.Ordinal);
        if (protoIdx >= 0) d = d[(protoIdx + 3)..];
        var slashIdx = d.IndexOf('/');
        if (slashIdx >= 0) d = d[..slashIdx];
        var colonIdx = d.IndexOf(':');
        if (colonIdx >= 0) d = d[..colonIdx];

        return string.IsNullOrWhiteSpace(d) || GenericHostOrEnvNames.Contains(d);
    }

    private static bool IsInferredPathSegmentDomain(ExternalServiceNode extService)
    {
        if (string.IsNullOrEmpty(extService.DomainOrService) || extService.DomainOrService.Contains('.'))
            return false;

        if (string.IsNullOrEmpty(extService.Path) || !extService.Path.StartsWith('/'))
            return false;

        var firstSeg = extService.Path.TrimStart('/').Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return string.Equals(firstSeg, extService.DomainOrService, StringComparison.OrdinalIgnoreCase);
    }

    private static bool DoesProjectMatchServiceDomain(ProjectNode proj, string domainOrService)
    {
        if (string.IsNullOrWhiteSpace(domainOrService))
            return false;

        var d = domainOrService.Trim().ToLowerInvariant();
        var protoIdx = d.IndexOf("://", StringComparison.Ordinal);
        if (protoIdx >= 0) d = d[(protoIdx + 3)..];
        var slashIdx = d.IndexOf('/');
        if (slashIdx >= 0) d = d[..slashIdx];
        var colonIdx = d.IndexOf(':');
        if (colonIdx >= 0) d = d[..colonIdx];

        if (string.IsNullOrWhiteSpace(d) || GenericHostOrEnvNames.Contains(d))
            return false;

        // 1. Direct matching: check if project name, directory name, or repo matches full host or subdomain
        var pName = proj.Name.ToLowerInvariant();
        var pFolder = Path.GetFileName(proj.Path.TrimEnd('/', '\\')).ToLowerInvariant();
        var gitRepo = proj.Extensions?.TryGetValue("git_repo", out var gr) == true ? gr.ToLowerInvariant() : "";

        var cand = new ProjectMatchCandidate(proj.Id, proj.Name, proj.Path, gitRepo, proj.IsLibrary);
        var score = EndpointScoringEngine.ScoreProjectMatch(d, cand);
        if (score >= 40.0)
        {
            return true;
        }

        // Check full domain (e.g. "rule-configurator")
        if (MatchesProject(d, pName, pFolder, gitRepo))
        {
            return true;
        }

        // Check subdomain / first host segment (e.g. "rule-configurator" in "rule-configurator.at-systems.biz")
        if (d.Contains('.'))
        {
            var subdomain = d.Split('.', 2)[0];
            if (!string.IsNullOrEmpty(subdomain) && (EndpointScoringEngine.ScoreProjectMatch(subdomain, cand) >= 40.0 || MatchesProject(subdomain, pName, pFolder, gitRepo)))
            {
                return true;
            }
        }

        // Third-party external domains (e.g. google.com, telegram.org, cloudflare.com) should NEVER match internal projects
        // ONLY reject if the domain is not an internal service match tested above.
        if (d.EndsWith(".com") || d.EndsWith(".org") || d.EndsWith(".net") || d.EndsWith(".biz") ||
            d.EndsWith(".io") || d.EndsWith(".pro") || d.EndsWith(".ru") || d.EndsWith(".dev"))
        {
            return false;
        }

        var normD = WorkspaceConventions.NormalizeServiceName(d);
        var cleanDomain = ServiceDomainEnvRegex.Replace(normD, "")
            .Replace("-", "").Replace("_", "");
        cleanDomain = ServiceSuffixRegex.Replace(cleanDomain, "");

        var normP = WorkspaceConventions.NormalizeServiceName(pName);
        var cleanPName = normP.Replace("-", "").Replace("_", "");
        cleanPName = ServiceSuffixRegex.Replace(cleanPName, "");

        if (cleanDomain.Length >= 4 && (cleanDomain == cleanPName || cleanDomain.TrimEnd('s') == cleanPName.TrimEnd('s')))
            return true;

        if (pName == d || pName.Replace("-", "") == d.Replace("-", "") || normP == normD)
            return true;

        if (d.Length >= 4 && (pName.EndsWith("." + d) || pName.EndsWith("." + d + "s")))
            return true;

        // Common namespace/service prefix stripped match (e.g. id-helper-nest or id_helper_nest vs helper-nest)
        var dWithoutId = d.StartsWith("id-") || d.StartsWith("id_") ? d[3..] : null;
        if (dWithoutId != null)
        {
            var cleanDWithoutId = dWithoutId.Replace("-", "").Replace("_", "");
            if (pName == dWithoutId || normP == dWithoutId || pFolder == dWithoutId ||
                cleanPName == cleanDWithoutId || MatchesProject(dWithoutId, pName, pFolder, gitRepo))
            {
                return true;
            }
        }
        var pWithoutId = pName.StartsWith("id-") || pName.StartsWith("id_") ? pName[3..] : null;
        if (pWithoutId != null)
        {
            var cleanPWithoutId = pWithoutId.Replace("-", "").Replace("_", "");
            if (d == pWithoutId || normD == pWithoutId || cleanDomain == cleanPWithoutId || MatchesProject(pWithoutId, d, d, ""))
            {
                return true;
            }
        }

        return false;
    }

    private static readonly HashSet<string> GenericProjectFolderNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "app", "src", "lib", "libs", "main", "core", "api", "client", "service"
    };

    private static bool MatchesProject(string token, string pName, string pFolder, string gitRepo)
    {
        if (string.IsNullOrWhiteSpace(token)) return false;

        var cleanToken = token.Replace("-", "").Replace("_", "");
        var canMatchFolder = !GenericProjectFolderNames.Contains(pFolder);

        if (pName == token || (canMatchFolder && pFolder == token) || gitRepo == token)
            return true;

        var cleanPName = pName.Replace("-", "").Replace("_", "");
        var cleanPFolder = pFolder.Replace("-", "").Replace("_", "");
        var cleanGitRepo = gitRepo.Replace("-", "").Replace("_", "");

        if (cleanPName == cleanToken || (canMatchFolder && cleanPFolder == cleanToken) || (!string.IsNullOrEmpty(cleanGitRepo) && cleanGitRepo == cleanToken))
            return true;

        if (cleanPName.TrimEnd('s') == cleanToken.TrimEnd('s') || (canMatchFolder && cleanPFolder.TrimEnd('s') == cleanToken.TrimEnd('s')))
            return true;

        if (pName.EndsWith("." + token, StringComparison.OrdinalIgnoreCase) ||
            pName.EndsWith("." + token + "s", StringComparison.OrdinalIgnoreCase) ||
            pName.EndsWith($".services.{token}", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private sealed class PreparedEndpoint
    {
        public required EndpointNode Endpoint { get; init; }
        public ProjectNode? TargetProj { get; init; }
        public required string RouteNorm { get; init; }
        public required string CleanPath { get; init; }
        public required string[] Segments { get; init; }
        public required string LastSegment { get; init; }
        public required bool IsLastSegmentParam { get; init; }
        public required bool IsApiPrefixOnly { get; init; }
    }

    private sealed class PreparedEntryPoint
    {
        public required EntryPointNode EntryPoint { get; init; }
        public ProjectNode? TargetProj { get; init; }
        public required string EntryNorm { get; init; }
        public required string CleanPath { get; init; }
        public required string[] Segments { get; init; }
        public required string LastSegment { get; init; }
        public required bool IsLastSegmentParam { get; init; }
        public required bool IsApiPrefixOnly { get; init; }
    }

    private sealed class PreparedExternalService
    {
        public required ExternalServiceNode Service { get; init; }
        public ProjectNode? CallerProj { get; init; }
        public required string ServicePathNorm { get; init; }
        public required string ServiceDomainNorm { get; init; }
        public required string RawDomain { get; init; }
        public required bool IsGenericHostOrService { get; init; }
        public required bool IsPathSegment { get; init; }
        public required string CleanPath { get; init; }
        public required string[] Segments { get; init; }
        public required string LastSegment { get; init; }
        public required bool IsLastSegmentParam { get; init; }
        public required bool IsRootOrGenericPath { get; init; }
    }

    private PreparedEndpoint PrepareEndpoint(EndpointNode endpoint, ProjectNode? targetProj)
    {
        var routeNorm = NormalizePath(endpoint.RouteTemplate);
        var segs = routeNorm.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var last = segs.Length > 0 ? segs[^1] : string.Empty;
        var isParam = last == "*" || last.StartsWith(':') || (last.StartsWith('{') && last.EndsWith('}'));
        var isPrefixOnly = IsApiPrefixOnly(segs, null);
        var clean = "/" + routeNorm.Trim('/') + "/";

        return new PreparedEndpoint
        {
            Endpoint = endpoint,
            TargetProj = targetProj,
            RouteNorm = routeNorm,
            CleanPath = clean,
            Segments = segs,
            LastSegment = last,
            IsLastSegmentParam = isParam,
            IsApiPrefixOnly = isPrefixOnly
        };
    }

    private PreparedEntryPoint PrepareEntryPoint(EntryPointNode entryPoint, ProjectNode? targetProj)
    {
        var entryNorm = NormalizePath(entryPoint.Name);
        var segs = entryNorm.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var last = segs.Length > 0 ? segs[^1] : string.Empty;
        var isParam = last == "*" || last.StartsWith(':') || (last.StartsWith('{') && last.EndsWith('}'));
        var isPrefixOnly = IsApiPrefixOnly(segs, null);
        var clean = "/" + entryNorm.Trim('/') + "/";

        return new PreparedEntryPoint
        {
            EntryPoint = entryPoint,
            TargetProj = targetProj,
            EntryNorm = entryNorm,
            CleanPath = clean,
            Segments = segs,
            LastSegment = last,
            IsLastSegmentParam = isParam,
            IsApiPrefixOnly = isPrefixOnly
        };
    }

    private PreparedExternalService PrepareExternalService(ExternalServiceNode extService, ProjectNode? callerProj)
    {
        var pathNorm = NormalizePath(extService.Path);
        var domainNorm = NormalizePath(extService.DomainOrService);
        var segs = pathNorm.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var last = segs.Length > 0 ? segs[^1] : string.Empty;
        var isParam = last == "*" || last.StartsWith(':') || (last.StartsWith('{') && last.EndsWith('}'));
        var rawDomain = (extService.DomainOrService ?? "").Trim('/').ToLowerInvariant();
        var isGeneric = IsGenericHostOrService(extService.DomainOrService);
        var isPathSeg = IsInferredPathSegmentDomain(extService);
        var clean = "/" + pathNorm.Trim('/') + "/";
        var isRootOrGeneric = IsRootOrGenericPath(pathNorm);

        return new PreparedExternalService
        {
            Service = extService,
            CallerProj = callerProj,
            ServicePathNorm = pathNorm,
            ServiceDomainNorm = domainNorm,
            RawDomain = rawDomain,
            IsGenericHostOrService = isGeneric,
            IsPathSegment = isPathSeg,
            CleanPath = clean,
            Segments = segs,
            LastSegment = last,
            IsLastSegmentParam = isParam,
            IsRootOrGenericPath = isRootOrGeneric
        };
    }

    private static bool MatchParts(string[] partsA, string[] partsB, string? serviceDomain)
    {
        if (partsA.Length == 0 || partsB.Length == 0)
        {
            return false;
        }

        var len = Math.Min(partsA.Length, partsB.Length);
        var lastA = partsA[^1];
        var lastB = partsB[^1];
        var isParamLastA = lastA == "*" || lastA.StartsWith(':') || (lastA.StartsWith('{') && lastA.EndsWith('}'));
        var isParamLastB = lastB == "*" || lastB.StartsWith(':') || (lastB.StartsWith('{') && lastB.EndsWith('}'));

        // Fast early rejection on last segment
        if (!isParamLastA && !isParamLastB && !string.Equals(lastA, lastB, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var hasExactMatch = false;

        for (int i = 1; i <= len; i++)
        {
            var a = partsA[^i];
            var b = partsB[^i];

            var isParamA = a == "*" || a.StartsWith(':') || (a.StartsWith('{') && a.EndsWith('}'));
            var isParamB = b == "*" || b.StartsWith(':') || (b.StartsWith('{') && b.EndsWith('}'));

            if (isParamA && isParamB)
            {
                continue;
            }

            if (isParamA != isParamB)
            {
                if (a == "*" || b == "*") return false;
                continue;
            }

            if (!string.Equals(a, b, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            hasExactMatch = true;
        }

        if (!hasExactMatch) return false;

        var longerParts = partsA.Length > partsB.Length ? partsA : partsB;
        var prefixCount = longerParts.Length - len;
        if (len == 1 && prefixCount > 0)
        {
            for (int i = 0; i < prefixCount; i++)
            {
                var seg = longerParts[i];
                if (AllowedApiPrefixSegments.Contains(seg)) continue;
                if (!string.IsNullOrEmpty(serviceDomain))
                {
                    var cleanDomain = CleanServiceDomain(serviceDomain);
                    var cleanSeg = seg.Replace("-", "").Replace("_", "").ToLowerInvariant();
                    if (cleanDomain == cleanSeg || cleanDomain.TrimEnd('s') == cleanSeg.TrimEnd('s')) continue;
                }
                return false;
            }
        }

        return true;
    }

    private bool MatchPaths(string pathA, string pathB) => MatchPaths(pathA, pathB, null);

    private bool MatchPaths(string pathA, string pathB, string? serviceDomain)
    {
        var partsA = pathA.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var partsB = pathB.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return MatchParts(partsA, partsB, serviceDomain);
    }

    private bool IsMatch(PreparedExternalService ext, PreparedEntryPoint ep, Func<ProjectNode, string, bool> projectMatcher)
    {
        if (ep.TargetProj != null && !ext.IsPathSegment && !ext.IsGenericHostOrService)
        {
            if (!projectMatcher(ep.TargetProj, ext.Service.DomainOrService))
            {
                return false;
            }
        }

        if (string.IsNullOrEmpty(ep.EntryNorm))
        {
            return false;
        }

        if (ep.IsApiPrefixOnly)
        {
            return false;
        }

        if ((ext.RawDomain is "*" or "unknown-service" or "" || ext.IsGenericHostOrService) && ext.IsRootOrGenericPath)
        {
            return false;
        }

        if (ext.IsRootOrGenericPath)
        {
            if (ep.TargetProj == null || !projectMatcher(ep.TargetProj, ext.Service.DomainOrService ?? ""))
            {
                return false;
            }
        }

        if (string.Equals(ext.ServicePathNorm, ep.EntryNorm, StringComparison.OrdinalIgnoreCase) ||
            MatchParts(ext.Segments, ep.Segments, ext.Service.DomainOrService))
        {
            return true;
        }

        if (ext.CleanPath != "//" && ep.CleanPath != "//")
        {
            if (ep.CleanPath.EndsWith(ext.CleanPath, StringComparison.OrdinalIgnoreCase))
            {
                var prefix = ep.CleanPath[..^ext.CleanPath.Length].Trim('/');
                if (IsApiPrefixOnly(prefix, ext.Service.DomainOrService))
                {
                    return true;
                }
            }
            else if (ext.CleanPath.EndsWith(ep.CleanPath, StringComparison.OrdinalIgnoreCase))
            {
                var prefix = ext.CleanPath[..^ep.CleanPath.Length].Trim('/');
                if (IsApiPrefixOnly(prefix, ext.Service.DomainOrService))
                {
                    return true;
                }
            }
        }

        if (!string.IsNullOrEmpty(ext.ServiceDomainNorm) && ext.ServiceDomainNorm != "*" && ext.ServiceDomainNorm != "unknown-service")
        {
            if (string.Equals(ext.ServiceDomainNorm, ep.EntryNorm, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (ext.ServiceDomainNorm.StartsWith('/') && MatchPaths(ext.ServiceDomainNorm, ep.EntryNorm, ext.Service.DomainOrService))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsMatch(PreparedExternalService ext, PreparedEndpoint ep, Func<ProjectNode, string, bool> projectMatcher)
    {
        if (ep.TargetProj != null && !ext.IsPathSegment && !ext.IsGenericHostOrService)
        {
            if (!projectMatcher(ep.TargetProj, ext.Service.DomainOrService))
            {
                return false;
            }
        }

        if (string.IsNullOrEmpty(ep.RouteNorm))
        {
            return false;
        }

        if (ep.IsApiPrefixOnly)
        {
            return false;
        }

        if ((ext.RawDomain is "*" or "unknown-service" or "" || ext.IsGenericHostOrService) && ext.IsRootOrGenericPath)
        {
            return false;
        }

        if (ext.IsRootOrGenericPath)
        {
            if (ep.TargetProj == null || !projectMatcher(ep.TargetProj, ext.Service.DomainOrService ?? ""))
            {
                return false;
            }
        }

        if (string.Equals(ext.ServicePathNorm, ep.RouteNorm, StringComparison.OrdinalIgnoreCase) ||
            MatchParts(ext.Segments, ep.Segments, ext.Service.DomainOrService))
        {
            return true;
        }

        if (ext.CleanPath != "//" && ep.CleanPath != "//")
        {
            if (ep.CleanPath.EndsWith(ext.CleanPath, StringComparison.OrdinalIgnoreCase))
            {
                var prefix = ep.CleanPath[..^ext.CleanPath.Length].Trim('/');
                if (IsApiPrefixOnly(prefix, ext.Service.DomainOrService))
                {
                    return true;
                }
            }
            else if (ext.CleanPath.EndsWith(ep.CleanPath, StringComparison.OrdinalIgnoreCase))
            {
                var prefix = ext.CleanPath[..^ep.CleanPath.Length].Trim('/');
                if (IsApiPrefixOnly(prefix, ext.Service.DomainOrService))
                {
                    return true;
                }
            }

            if ((ep.CleanPath.StartsWith(ext.CleanPath, StringComparison.OrdinalIgnoreCase) && ext.Segments.Length >= 2 && !ep.IsApiPrefixOnly) ||
                (ext.CleanPath.StartsWith(ep.CleanPath, StringComparison.OrdinalIgnoreCase) && ep.Segments.Length >= 2 && !ep.IsApiPrefixOnly))
            {
                return true;
            }
        }

        if (!string.IsNullOrEmpty(ext.ServiceDomainNorm) && ext.ServiceDomainNorm != "*" && ext.ServiceDomainNorm != "unknown-service" && ext.ServiceDomainNorm.StartsWith('/'))
        {
            if (string.Equals(ext.ServiceDomainNorm, ep.RouteNorm, StringComparison.OrdinalIgnoreCase) ||
                MatchPaths(ext.ServiceDomainNorm, ep.RouteNorm, ext.Service.DomainOrService))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsMatch(ExternalServiceNode extService, EndpointNode endpoint) => IsMatch(extService, endpoint, null);

    private bool IsMatch(ExternalServiceNode extService, EndpointNode endpoint, ProjectNode? targetProj)
    {
        var prepExt = PrepareExternalService(extService, null);
        var prepEp = PrepareEndpoint(endpoint, targetProj);
        return IsMatch(prepExt, prepEp, (p, d) => DoesProjectMatchServiceDomain(p, d));
    }

    private bool IsMatch(ExternalServiceNode extService, EntryPointNode entryPoint) => IsMatch(extService, entryPoint, null);

    private bool IsMatch(ExternalServiceNode extService, EntryPointNode entryPoint, ProjectNode? targetProj)
    {
        var prepExt = PrepareExternalService(extService, null);
        var prepEp = PrepareEntryPoint(entryPoint, targetProj);
        return IsMatch(prepExt, prepEp, (p, d) => DoesProjectMatchServiceDomain(p, d));
    }

    private string NormalizePath(string path)
    {
        if (string.IsNullOrEmpty(path)) return string.Empty;

        var normalized = path.Replace('\\', '/').ToLowerInvariant();

        var protocolIdx = normalized.IndexOf("://");

        if (protocolIdx != -1)
        {
            normalized = normalized[(protocolIdx + 3)..];
        }

        var colonIdx = normalized.IndexOf(':');
        var slashIdx = normalized.IndexOf('/');
        if (colonIdx != -1 && (slashIdx == -1 || colonIdx < slashIdx))
        {
            var prefix = normalized[..colonIdx];
            if (prefix is "get" or "post" or "put" or "delete" or "patch" or "options" or "head" or "all" or "ws" or "rpc" or "grpc" or "graphql")
            {
                normalized = normalized[(colonIdx + 1)..];
            }
        }

        return normalized.Trim('/');
    }

    private static string NormalizeGcpTopicName(string raw) => WorkspaceConventions.NormalizeTopicName(raw);
}
