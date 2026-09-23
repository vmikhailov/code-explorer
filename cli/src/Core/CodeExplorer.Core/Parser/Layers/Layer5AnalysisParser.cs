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
            await postAnalyzer.RunAsync(ctx.WorkspaceId);
        }
        else
        {
            ctx.Log("[Layer5] Running in-memory post-indexing analysis via PostIndexAnalyzer...");
            await postAnalyzer.RunInMemoryAsync(ctx, l4Result, referenceRelationships, lateBoundRels);
        }

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

        public IndexedBinding(string typeName, string? scopeId)
        {
            TypeName = typeName;
            ScopeMarker = string.IsNullOrEmpty(scopeId) ? null : $":{scopeId}:";
        }
    }

    private static string? ExtractFilePathFromSymbolId(string scopeSymbolId)
    {
        const string marker = ":symbol:";
        var markerIdx = scopeSymbolId.IndexOf(marker, StringComparison.Ordinal);
        if (markerIdx < 0) return null;

        var start = markerIdx + marker.Length;
        var end = scopeSymbolId.IndexOf(':', start);
        if (end <= start) return null;

        return scopeSymbolId[start..end];
    }

    private static string? ExtractSymbolNameFromId(string symbolId)
    {
        if (Urn.TryParse(symbolId, out var urn) && !string.IsNullOrEmpty(urn.Name))
        {
            return urn.Name;
        }
        // Format: {workspaceId}:symbol:{relativePath}:{mappedKind}:{name}:{row}
        var parts = symbolId.Split(':');
        return parts.Length >= 2 ? parts[^2] : null;
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

    private async Task<List<Relationship>> ResolveAndUploadGlobalReferencesAsync(ParsingContext ctx)
    {
        var totalReferences = ctx.GlobalReferences.Count;
        ctx.Log($"[Layer5] Resolving {totalReferences} global cross-references...");
        var referenceRelationships = new List<Relationship>(totalReferences > 0 ? Math.Min(totalReferences, 2000000) : 0);
        var inheritanceRels = new HashSet<(string From, string To)>();
        var interfaceToImplementors = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        await PreloadTargetedSymbolsAsync(ctx, interfaceToImplementors);

        // Pre-split GlobalSymbols into single-string dictionaries for O(1) single-hash lookups
        var typeSymbols = new Dictionary<string, string>(StringComparer.Ordinal);
        var functionSymbols = new Dictionary<string, string>(StringComparer.Ordinal);
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

        // Pass 1: Resolve all inheritance (Implements / InheritsFrom) relationships first and cache them in a HashSet.
        foreach (var refItem in ctx.GlobalReferences)
        {
            ctx.CancellationToken.ThrowIfCancellationRequested();

            if (refItem.Kind == OntologyConstants.Relationships.Implements ||
                refItem.Kind == OntologyConstants.Relationships.InheritsFrom)
            {
                if (typeSymbols.TryGetValue(refItem.TargetName, out var targetNodeId) && refItem.ScopeSymbolId != targetNodeId)
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
                else if (refItem.Kind == OntologyConstants.Relationships.Implements)
                {
                    if (endpointSymbols.TryGetValue(refItem.TargetName, out var targetEndpointId))
                    {
                        referenceRelationships.Add(
                            Relationship.FromRelationship(new ExposedByRelationship(targetEndpointId,
                                refItem.ScopeSymbolId)));
                    }
                    else if (entryPointSymbols.TryGetValue(refItem.TargetName, out var targetEpId))
                    {
                        referenceRelationships.Add(
                            Relationship.FromRelationship(new ImplementedByRelationship(targetEpId,
                                refItem.ScopeSymbolId)));
                        inheritanceRels.Add((targetEpId, refItem.ScopeSymbolId));
                    }
                }
            }
        }

        // Index RawTypeBindings by (FilePath, VariableName) for O(1) fast member-call lookup
        var bindingsLookup = new Dictionary<(string FilePath, string VarName), List<IndexedBinding>>(ctx.RawTypeBindings.Count);
        foreach (var b in ctx.RawTypeBindings)
        {
            var key = (b.FilePath, b.VariableName);
            if (!bindingsLookup.TryGetValue(key, out var list))
            {
                list = new List<IndexedBinding>(1);
                bindingsLookup[key] = list;
            }
            list.Add(new IndexedBinding(b.TypeName, b.ScopeId));
        }

        // Index RawVariables constants for resolving constant topic/queue names across files
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

        // Pass 2: Resolve all other relationships using the cached inheritance relationships and bindings index.
        var resolvedCount = 0;
        var createdTopicIds = new HashSet<string>(StringComparer.Ordinal);
        var newTopicNodes = new List<TopicNode>();

        foreach (var refItem in ctx.GlobalReferences)
        {
            ctx.CancellationToken.ThrowIfCancellationRequested();
            resolvedCount++;

            if (resolvedCount % 100000 == 0)
            {
                ctx.Log($"[Layer5] Resolving global cross-references: {resolvedCount}/{totalReferences}...");
            }

            if (refItem.Kind == OntologyConstants.Relationships.Implements ||
                refItem.Kind == OntologyConstants.Relationships.InheritsFrom)
            {
                continue;
            }

            if (refItem.Kind == OntologyConstants.Relationships.Calls)
            {
                var targetName = refItem.TargetName;
                string? targetTypeName = null;
                string? methodName = null;

                if (targetName.Contains('.'))
                {
                    var dotIdx = targetName.LastIndexOf('.');
                    var varName = targetName[..dotIdx];
                    methodName = targetName[(dotIdx + 1)..];

                    var filePath = ExtractFilePathFromSymbolId(refItem.ScopeSymbolId);

                    if (filePath != null)
                    {
                        if (bindingsLookup.TryGetValue((filePath, varName), out var candidates))
                        {
                            // Priority 1: Match by scope name
                            for (int i = 0; i < candidates.Count; i++)
                            {
                                var candidate = candidates[i];
                                if (candidate.ScopeMarker != null &&
                                    refItem.ScopeSymbolId.Contains(candidate.ScopeMarker, StringComparison.Ordinal))
                                {
                                    targetTypeName = candidate.TypeName;
                                    break;
                                }
                            }

                            // Priority 2: Fallback to any binding in the same file
                            targetTypeName ??= candidates[0].TypeName;
                        }

                        if (targetTypeName != null)
                        {
                            targetName = $"{targetTypeName}.{methodName}";
                        }
                        else
                        {
                            targetName = methodName;
                        }
                    }
                    else
                    {
                        targetName = methodName;
                    }
                }

                var callAdded = false;
                if (functionSymbols.TryGetValue(targetName, out var targetNodeId))
                {
                    referenceRelationships.Add(
                        Relationship.FromRelationship(new CallsRelationship(refItem.ScopeSymbolId, targetNodeId)));
                    callAdded = true;
                }
                else if (procedureSymbols.TryGetValue(targetName, out var targetProcId))
                {
                    referenceRelationships.Add(
                        Relationship.FromRelationship(new CalledByRelationship(targetProcId, refItem.ScopeSymbolId)));
                    callAdded = true;
                }

                // If targetTypeName is an interface or base class with implementations, link to the concrete implementations
                if (targetTypeName != null && !string.IsNullOrEmpty(methodName) && interfaceToImplementors.TryGetValue(targetTypeName, out var implementors))
                {
                    foreach (var implClass in implementors)
                    {
                        if (functionSymbols.TryGetValue($"{implClass}.{methodName}", out var implNodeId) && implNodeId != targetNodeId)
                        {
                            referenceRelationships.Add(
                                Relationship.FromRelationship(new CallsRelationship(refItem.ScopeSymbolId, implNodeId)));
                            callAdded = true;
                        }
                    }
                }

                if (!callAdded && methodName != null && functionSymbols.TryGetValue(methodName, out var fallbackNodeId))
                {
                    referenceRelationships.Add(
                        Relationship.FromRelationship(new CallsRelationship(refItem.ScopeSymbolId, fallbackNodeId)));
                }
            }
            else if (refItem.Kind == OntologyConstants.Relationships.DependsOn)
            {
                if (tableSymbols.TryGetValue(refItem.TargetName, out var targetTableId))
                {
                    referenceRelationships.Add(
                        Relationship.FromRelationship(new QueriedByRelationship(targetTableId, refItem.ScopeSymbolId)));
                }
            }
            else if (refItem.Kind == OntologyConstants.Relationships.UsesType)
            {
                if (typeSymbols.TryGetValue(refItem.TargetName, out var targetNodeId))
                {
                    referenceRelationships.Add(
                        Relationship.FromRelationship(new UsesTypeRelationship(refItem.ScopeSymbolId, targetNodeId)));
                }
            }
            else if (refItem.Kind == OntologyConstants.Relationships.PotentialType)
            {
                if (typeSymbols.TryGetValue(refItem.TargetName, out var targetNodeId))
                {
                    if (refItem.ScopeSymbolId != targetNodeId)
                    {
                        if (!inheritanceRels.Contains((refItem.ScopeSymbolId, targetNodeId)))
                        {
                            referenceRelationships.Add(
                                Relationship.FromRelationship(new UsesTypeRelationship(refItem.ScopeSymbolId,
                                    targetNodeId)));
                        }
                    }
                }
            }
            else if (refItem.Kind == OntologyConstants.Relationships.Triggers)
            {
                var targetId = functionSymbols.GetValueOrDefault(refItem.TargetName);
                if (targetId == null && refItem.TargetName.Contains('.'))
                {
                    var shortName = refItem.TargetName[(refItem.TargetName.LastIndexOf('.') + 1)..];
                    targetId = functionSymbols.GetValueOrDefault(shortName);
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
            else if (refItem.Kind == OntologyConstants.Relationships.PublishesTo ||
                     refItem.Kind == OntologyConstants.Relationships.SubscribesTo)
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
                        else
                        {
                            topicName = "EVENT_JOURNAL_TOPIC";
                        }
                    }
                    else
                    {
                        continue;
                    }
                }

                if (topicName.Equals("topic", StringComparison.OrdinalIgnoreCase) ||
                    topicName.Equals("networkTopic", StringComparison.OrdinalIgnoreCase))
                {
                    topicName = "EVENT_JOURNAL_TOPIC";
                }

                if (constantLookup.TryGetValue(topicName, out var resolvedConst) && !string.IsNullOrEmpty(resolvedConst))
                {
                    topicName = resolvedConst;
                }

                var topicId = $"{ctx.WorkspaceId}:topic:{brokerType}:{topicName}";

                if (!createdTopicIds.Contains(topicId))
                {
                    createdTopicIds.Add(topicId);
                    var topicNode = new TopicNode(topicId, topicName, "", brokerType);
                    newTopicNodes.Add(topicNode);
                    ctx.AddGlobalSymbol(OntologyConstants.NodeLabels.Topic, refItem.TargetName, topicId);
                }

                var relKind = refItem.Kind == OntologyConstants.Relationships.PublishesTo
                    ? OntologyConstants.Relationships.PublishedBy
                    : OntologyConstants.Relationships.SubscribedBy;

                referenceRelationships.Add(new Relationship(topicId, refItem.ScopeSymbolId, relKind, new()));
            }
            else if (refItem.Kind == OntologyConstants.Relationships.PersistedIn)
            {
                if (typeSymbols.TryGetValue(refItem.ScopeSymbolId, out var tid))
                {
                    var targetTableId = tableSymbols.TryGetValue(refItem.TargetName, out var tblId) ? tblId : $"{ctx.WorkspaceId}:table:{refItem.TargetName.ToLowerInvariant()}";
                    if (tid != targetTableId && !referenceRelationships.Any(r => r.From == tid && r.To == targetTableId && r.Kind == OntologyConstants.Relationships.PersistedIn))
                    {
                        referenceRelationships.Add(Relationship.FromRelationship(new PersistedInRelationship(tid, targetTableId)));
                    }
                }
            }
        }

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
    private async Task<List<Relationship>> PerformLateBindingAsync(Layer4Result l4Result, ParsingContext ctx)
    {
        var workspaceNode = l4Result.Prev.Prev.Prev.Workspace;
        var projects = l4Result.Prev.Prev.Projects;

        var entryPoints = new List<EntryPointNode>();
        var endpoints = new List<EndpointNode>();
        var externalServices = new List<ExternalServiceNode>();

        CollectPublicSymbols(workspaceNode, entryPoints, endpoints, externalServices);

        // Build mapping from symbol/node ID to containing ProjectNode
        var nodeToProject = new Dictionary<string, ProjectNode>();
        foreach (var pSem in l4Result.SemanticStructure.Children)
        {
            var matchedProj = projects.FirstOrDefault(p => p.Path == pSem.Path);
            if (matchedProj != null)
            {
                MapProjectNodes(pSem, matchedProj, nodeToProject);
            }
        }

        // Fallback mapping via file_path
        foreach (var es in externalServices)
        {
            if (!nodeToProject.ContainsKey(es.Id) && es.Extensions?.TryGetValue("file_path", out var fp) == true)
            {
                var proj = Layer2ProjectParser.FindProjectForFilePath(fp, projects);
                if (proj != null) nodeToProject[es.Id] = proj;
            }
        }
        foreach (var ep in endpoints)
        {
            if (!nodeToProject.ContainsKey(ep.Id) && !string.IsNullOrEmpty(ep.Path))
            {
                var proj = Layer2ProjectParser.FindProjectForFilePath(ep.Path, projects);
                if (proj != null) nodeToProject[ep.Id] = proj;
            }
        }

        var localExtIds = new HashSet<string>(externalServices.Select(e => e.Id));
        var localEndpointIds = new HashSet<string>(endpoints.Select(e => e.Id));
        var localEntryPointIds = new HashSet<string>(entryPoints.Select(e => e.Id));

        if (ctx.IsSubtreeScan)
        {
            var needEndpoints = externalServices.Count > 0;
            var needExternalServices = endpoints.Count > 0 || entryPoints.Count > 0;
            if (needEndpoints || needExternalServices)
            {
                var (dbEndpoints, dbEntryPoints, dbExtServices) = await ctx.DbClient.LoadLateBindingCandidatesAsync(
                    ctx.WorkspaceId,
                    needEndpoints: needEndpoints,
                    needExternalServices: needExternalServices,
                    ctx.CancellationToken
                );

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

        ctx.Log($"[Layer5] [LateBinding] Found {entryPoints.Count} EntryPoints, {endpoints.Count} Endpoints, and {externalServices.Count} ExternalServices.");

        var lateBoundRels = new List<Relationship>();
        var addedProjectDeps = new HashSet<(string From, string To)>();

        foreach (var extService in externalServices)
        {
            var matchedEndpoint = false;

            foreach (var entryPoint in entryPoints)
            {
                if (ctx.IsSubtreeScan && !localExtIds.Contains(extService.Id) && !localEntryPointIds.Contains(entryPoint.Id))
                {
                    continue;
                }

                if (IsMatch(extService, entryPoint))
                {
                    ctx.Log($"[Layer5] [LateBinding] Binding ExternalService '{extService.Id}' to EntryPoint '{entryPoint.Id}'");
                    var rel = Relationship.FromRelationship(new CallsRelationship(extService.Id, entryPoint.Id));
                    lateBoundRels.Add(rel);
                    matchedEndpoint = true;
                }
            }

            foreach (var endpoint in endpoints)
            {
                if (ctx.IsSubtreeScan && !localExtIds.Contains(extService.Id) && !localEndpointIds.Contains(endpoint.Id))
                {
                    continue;
                }

                if (IsMatch(extService, endpoint))
                {
                    ctx.Log($"[Layer5] [LateBinding] Binding ExternalService '{extService.Id}' to Endpoint '{endpoint.Id}'");
                    var rel = Relationship.FromRelationship(new CallsEndpointRelationship(extService.Id, endpoint.Id));
                    lateBoundRels.Add(rel);
                    matchedEndpoint = true;

                    // Synthesize Project -> Project DEPENDS_ON relationship
                    nodeToProject.TryGetValue(extService.Id, out var callerProj);
                    nodeToProject.TryGetValue(endpoint.Id, out var targetProj);

                    if (callerProj != null && targetProj != null && callerProj.Id != targetProj.Id)
                    {
                        if (addedProjectDeps.Add((callerProj.Id, targetProj.Id)))
                        {
                            ctx.Log($"[Layer5] [LateBinding] Synthesized dependency: Project '{callerProj.Name}' -> Project '{targetProj.Name}' via endpoint '{endpoint.RouteTemplate}'");
                            lateBoundRels.Add(Relationship.FromRelationship(new DependsOnRelationship(callerProj.Id, targetProj.Id, new() { ["dependency_type"] = "service_call" })));
                        }
                    }
                }
            }

            // Also match by domain/service name ONLY if no endpoint was matched and a known project exists
            if (!matchedEndpoint &&
                !string.IsNullOrWhiteSpace(extService.DomainOrService) &&
                extService.DomainOrService is not ("*" or "unknown-service"))
            {
                nodeToProject.TryGetValue(extService.Id, out var callerProj);
                if (callerProj != null)
                {
                    var domain = extService.DomainOrService.Trim().ToLowerInvariant();
                    foreach (var proj in projects)
                    {
                        if (proj.Id == callerProj.Id) continue;
                        var pName = proj.Name.ToLowerInvariant();

                        // Avoid matching internal class libraries or test suites
                        if (pName.EndsWith(".tests") || pName.EndsWith(".test") ||
                            pName.EndsWith(".data") || pName.EndsWith(".logic") ||
                            pName.EndsWith(".contracts") || pName.EndsWith(".client") ||
                            pName.EndsWith(".common") || pName.EndsWith(".shared"))
                        {
                            continue;
                        }

                        if (pName == domain ||
                            pName.TrimEnd('s') == domain.TrimEnd('s') ||
                            pName.Replace("-", "") == domain.Replace("-", "") ||
                            pName.EndsWith("." + domain, StringComparison.OrdinalIgnoreCase) ||
                            pName.EndsWith("." + domain + "s", StringComparison.OrdinalIgnoreCase) ||
                            pName.EndsWith("." + domain + ".api", StringComparison.OrdinalIgnoreCase) ||
                            pName.EndsWith("." + domain + ".service", StringComparison.OrdinalIgnoreCase) ||
                            pName.EndsWith("." + domain + "service", StringComparison.OrdinalIgnoreCase))
                        {
                            if (addedProjectDeps.Add((callerProj.Id, proj.Id)))
                            {
                                ctx.Log($"[Layer5] [LateBinding] Inferred project dependency: Project '{callerProj.Name}' -> Project '{proj.Name}' via service domain '{extService.DomainOrService}'");
                                lateBoundRels.Add(Relationship.FromRelationship(new DependsOnRelationship(callerProj.Id, proj.Id, new() { ["dependency_type"] = "service_call" })));
                            }
                        }
                    }
                }
            }
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

    private bool MatchPaths(string pathA, string pathB)
    {
        var partsA = pathA.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var partsB = pathB.Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (partsA.Length == 0 || partsB.Length == 0)
        {
            return false;
        }

        var len = Math.Min(partsA.Length, partsB.Length);
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

        return hasExactMatch;
    }

    private bool IsMatch(ExternalServiceNode extService, EntryPointNode entryPoint)
    {
        var servicePathNorm = NormalizePath(extService.Path);
        var serviceDomainNorm = NormalizePath(extService.DomainOrService);
        var entryNorm = NormalizePath(entryPoint.Name);

        if (string.IsNullOrEmpty(entryNorm))
        {
            return false;
        }

        if (serviceDomainNorm is "*" or "unknown-service" && (string.IsNullOrEmpty(servicePathNorm) || servicePathNorm is "/" or "*"))
        {
            return false;
        }

        if (string.Equals(servicePathNorm, entryNorm, StringComparison.OrdinalIgnoreCase) ||
            MatchPaths(servicePathNorm, entryNorm))
        {
            return true;
        }

        if (!string.IsNullOrEmpty(serviceDomainNorm) && serviceDomainNorm != "*" && serviceDomainNorm != "unknown-service")
        {
            if (string.Equals(serviceDomainNorm, entryNorm, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (serviceDomainNorm.StartsWith('/') && MatchPaths(serviceDomainNorm, entryNorm))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsMatch(ExternalServiceNode extService, EndpointNode endpoint)
    {
        var servicePathNorm = NormalizePath(extService.Path);
        var serviceDomainNorm = NormalizePath(extService.DomainOrService);
        var routeNorm = NormalizePath(endpoint.RouteTemplate);

        if (string.IsNullOrEmpty(routeNorm))
        {
            return false;
        }

        if (serviceDomainNorm is "*" or "unknown-service" && (string.IsNullOrEmpty(servicePathNorm) || servicePathNorm is "/" or "*"))
        {
            return false;
        }

        if (string.Equals(servicePathNorm, routeNorm, StringComparison.OrdinalIgnoreCase) ||
            MatchPaths(servicePathNorm, routeNorm))
        {
            return true;
        }

        var cleanPathA = "/" + servicePathNorm.Trim('/') + "/";
        var cleanPathB = "/" + routeNorm.Trim('/') + "/";

        if (cleanPathA != "//" && cleanPathB != "//")
        {
            if (cleanPathA.EndsWith(cleanPathB, StringComparison.OrdinalIgnoreCase) ||
                cleanPathB.EndsWith(cleanPathA, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var segsA = cleanPathA.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            var segsB = cleanPathB.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            if ((cleanPathB.StartsWith(cleanPathA, StringComparison.OrdinalIgnoreCase) && segsA.Length >= 2) ||
                (cleanPathA.StartsWith(cleanPathB, StringComparison.OrdinalIgnoreCase) && segsB.Length >= 2))
            {
                return true;
            }
        }

        if (!string.IsNullOrEmpty(serviceDomainNorm) && serviceDomainNorm != "*" && serviceDomainNorm != "unknown-service" && serviceDomainNorm.StartsWith('/'))
        {
            if (string.Equals(serviceDomainNorm, routeNorm, StringComparison.OrdinalIgnoreCase) ||
                MatchPaths(serviceDomainNorm, routeNorm))
            {
                return true;
            }
        }

        return false;
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

        return normalized.Trim('/');
    }
}
