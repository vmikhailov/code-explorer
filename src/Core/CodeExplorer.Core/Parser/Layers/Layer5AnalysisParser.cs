using CodeExplorer.Core.Common;
using CodeExplorer.Core.Common.Nodes;
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
        var workspaceNode = l4Result.Prev.Prev.Prev.Workspace;
        var lateBoundRels = await PerformLateBindingAsync(workspaceNode, ctx);

        // 5. Run PostIndexAnalyzer
        ctx.Log("[Layer5] Running in-memory post-indexing analysis via PostIndexAnalyzer...");
        var postAnalyzer = new PostIndexAnalyzer(ctx.DbClient);
        await postAnalyzer.RunInMemoryAsync(ctx, l4Result, referenceRelationships, lateBoundRels);

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
        // Format: {workspaceId}:symbol:{relativePath}:{mappedKind}:{name}:{row}
        var parts = symbolId.Split(':');
        return parts.Length >= 2 ? parts[^2] : null;
    }

    private async Task<List<Relationship>> ResolveAndUploadGlobalReferencesAsync(ParsingContext ctx)
    {
        var totalReferences = ctx.GlobalReferences.Count;
        ctx.Log($"[Layer5] Resolving {totalReferences} global cross-references...");
        var referenceRelationships = new List<Relationship>(totalReferences > 0 ? Math.Min(totalReferences, 2000000) : 0);
        var inheritanceRels = new HashSet<(string From, string To)>();
        var interfaceToImplementors = new Dictionary<string, List<string>>(StringComparer.Ordinal);

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
                if (typeSymbols.TryGetValue(refItem.TargetName, out var targetNodeId))
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
                if (functionSymbols.TryGetValue(refItem.TargetName, out var targetNodeId))
                {
                    referenceRelationships.Add(
                        Relationship.FromRelationship(new TriggersRelationship(refItem.ScopeSymbolId, targetNodeId)));
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
                    _ when topicName.StartsWith("mediatr:", StringComparison.OrdinalIgnoreCase) => ("mediatr", topicName["mediatr:".Length..]),
                    _ when topicName.StartsWith("masstransit:", StringComparison.OrdinalIgnoreCase) => ("masstransit", topicName["masstransit:".Length..]),
                    _ when topicName.StartsWith("spring:", StringComparison.OrdinalIgnoreCase) => ("spring", topicName["spring:".Length..]),
                    _ when topicName.StartsWith("cqrs:", StringComparison.OrdinalIgnoreCase) => ("cqrs", topicName["cqrs:".Length..]),
                    _ when topicName.StartsWith("event:", StringComparison.OrdinalIgnoreCase) => ("event", topicName["event:".Length..]),
                    _ => (brokerType, topicName)
                };

                var topicId = $"{ctx.WorkspaceId}:topic:{brokerType}:{topicName}";

                if (!createdTopicIds.Contains(topicId))
                {
                    createdTopicIds.Add(topicId);
                    var topicNode = new TopicNode(topicId, topicName, "", brokerType);
                    newTopicNodes.Add(topicNode);
                    ctx.AddGlobalSymbol(OntologyConstants.NodeLabels.Topic, refItem.TargetName, topicId);
                }

                if (refItem.Kind == OntologyConstants.Relationships.PublishesTo)
                {
                    referenceRelationships.Add(
                        Relationship.FromRelationship(new PublishedByRelationship(topicId, refItem.ScopeSymbolId)));
                }
                else
                {
                    referenceRelationships.Add(
                        Relationship.FromRelationship(new SubscribedByRelationship(topicId, refItem.ScopeSymbolId)));
                }
            }
            else if (refItem.Kind == OntologyConstants.Relationships.PersistedIn)
            {
                var fromTypeId = typeSymbols.TryGetValue(refItem.ScopeSymbolId, out var tid) ? tid : refItem.ScopeSymbolId;
                var targetTableId = tableSymbols.TryGetValue(refItem.TargetName, out var tblId) ? tblId : $"{ctx.WorkspaceId}:table:{refItem.TargetName.ToLowerInvariant()}";
                referenceRelationships.Add(Relationship.FromRelationship(new PersistedInRelationship(fromTypeId, targetTableId)));
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
    private async Task<List<Relationship>> PerformLateBindingAsync(IOntologyNode rootNode, ParsingContext ctx)
    {
        var entryPoints = new List<EntryPointNode>();
        var endpoints = new List<EndpointNode>();
        var externalServices = new List<ExternalServiceNode>();

        CollectPublicSymbols(rootNode, entryPoints, endpoints, externalServices);

        ctx.Log($"[Layer5] [LateBinding] Found {entryPoints.Count} EntryPoints, {endpoints.Count} Endpoints, and {externalServices.Count} ExternalServices.");

        var lateBoundRels = new List<Relationship>();

        foreach (var extService in externalServices)
        {
            foreach (var entryPoint in entryPoints)
            {
                if (IsMatch(extService, entryPoint))
                {
                    ctx.Log($"[Layer5] [LateBinding] Binding ExternalService '{extService.Id}' to EntryPoint '{entryPoint.Id}'");
                    var rel = Relationship.FromRelationship(new CallsRelationship(extService.Id, entryPoint.Id));
                    lateBoundRels.Add(rel);
                }
            }

            foreach (var endpoint in endpoints)
            {
                if (IsMatch(extService, endpoint))
                {
                    ctx.Log($"[Layer5] [LateBinding] Binding ExternalService '{extService.Id}' to Endpoint '{endpoint.Id}'");
                    var rel = Relationship.FromRelationship(new CallsEndpointRelationship(extService.Id, endpoint.Id));
                    lateBoundRels.Add(rel);
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

        if (partsA.Length != partsB.Length || partsA.Length == 0)
        {
            return false;
        }

        var hasExactMatch = false;

        for (int i = 0; i < partsA.Length; i++)
        {
            var a = partsA[i];
            var b = partsB[i];

            var isParamA = a == "*" || a.StartsWith(':') || (a.StartsWith('{') && a.EndsWith('}'));
            var isParamB = b == "*" || b.StartsWith(':') || (b.StartsWith('{') && b.EndsWith('}'));

            if (!isParamA && !isParamB)
            {
                if (!string.Equals(a, b, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
                hasExactMatch = true;
            }
        }

        return hasExactMatch || (partsA.Length == 1 && string.Equals(partsA[0], partsB[0], StringComparison.OrdinalIgnoreCase));
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

        if (cleanPathA != "//" && cleanPathB != "//" &&
            (cleanPathA.EndsWith(cleanPathB, StringComparison.OrdinalIgnoreCase) ||
             cleanPathB.EndsWith(cleanPathA, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
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
