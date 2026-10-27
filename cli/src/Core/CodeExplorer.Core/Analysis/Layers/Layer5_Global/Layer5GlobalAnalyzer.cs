using System.Diagnostics;
using CodeExplorer.Core.Analysis;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Common.Nodes.Layer5_Logical;
using CodeExplorer.Core.Common.Relationships;
using CodeExplorer.Core.Database;

namespace CodeExplorer.Core.Parser.Layers;

public partial class Layer5AnalysisParser
{
    private Layer4Result? _currentL4Result;

    public async Task<Layer5Result> ParseAsync(Layer4Result l4Result, ParsingContext ctx)
    {
        _currentL4Result = l4Result;
        ctx.ReportProgress(68, "Layer 5: Global", "Starting late binding, cross-references & post-indexing analysis...");
        ctx.Log("[Layer 5    ] Starting Layer 5 (Late Binding, Cross-References & Post-Indexing Analysis)...");

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
            ctx.LogDebug($"[Layer 5    ] Uploading {belongsToRels.Count} project containment (BelongsTo) relationships...");
            await ctx.EnqueueUploadRelationshipsAsync(belongsToRels);
            ctx.TotalRelsCount += belongsToRels.Count;
        }

        // 2. Upload project dependencies
        await UploadProjectDependenciesAsync(ctx);

        // 3. Resolve and upload global cross-references (like function CALLS)
        var referenceRelationships = await ResolveAndUploadGlobalReferencesAsync(ctx);

        // 4. Perform Late Binding
        var lateBoundRels = await PerformLateBindingAsync(l4Result, ctx, referenceRelationships);

        // 5. Run PostIndexAnalyzer
        await ctx.FlushScanIssuesAsync();
        var postAnalyzer = new PostIndexAnalyzer(ctx.DbClient);
        if (ctx.IsSubtreeScan)
        {
            await ctx.WaitForQueueDrainedAsync();
            ctx.ReportProgress(84, "Layer 5: Global", "Running post-indexing analysis...");
            ctx.Log("[Layer 5    ] Running global post-indexing analysis via PostIndexAnalyzer across workspace...");
            await postAnalyzer.RunAsync(ctx.WorkspaceId, ctx);
        }
        else
        {
            await ctx.WaitForQueueDrainedAsync();
            ctx.ReportProgress(84, "Layer 5: Global", "Running in-memory post-indexing analysis...");
            ctx.Log("[Layer 5    ] Running in-memory post-indexing analysis via PostIndexAnalyzer...");
            await postAnalyzer.RunInMemoryAsync(ctx, l4Result, referenceRelationships, lateBoundRels);
        }

        // 6. Materialize Layer 5 Logical Structure (LogicalStructure, Domain, BoundedContext)
        await MaterializeLogicalStructureAsync(l4Result, ctx);

        // 7. Run CodeIntentAnalyzer (if model / code-intent-distill is available)
        await CodeIntentAnalyzer.EnrichAsync(ctx);

        ctx.Log("[Layer 5    ] Late binding and post-indexing analysis pass complete.");
        return new Layer5Result(l4Result, lateBoundRels);
    }

    private async Task UploadProjectDependenciesAsync(ParsingContext ctx)
    {
        if (ctx.GlobalProjectDependencies.Count > 0)
        {
            ctx.Log(
                $"[Layer 5    ] Uploading {ctx.GlobalProjectDependencies.Count} local project dependency relationships...");
            await ctx.EnqueueUploadRelationshipsAsync(ctx.GlobalProjectDependencies);
            ctx.TotalRelsCount += ctx.GlobalProjectDependencies.Count;
        }
    }

    private async Task<List<Relationship>> ResolveAndUploadGlobalReferencesAsync(ParsingContext ctx)
    {
        var totalReferences = ctx.GlobalReferences.Count;
        ctx.ReportProgress(71, "Layer 5: Global", $"Resolving {totalReferences} global cross-references...");
        ctx.Log($"[Layer 5    ] Resolving {totalReferences} global cross-references...");
        var referenceRelationships = new List<Relationship>(totalReferences > 0 ? Math.Min(totalReferences, 2000000) : 0);
        var inheritanceRels = new HashSet<(string From, string To)>();
        var interfaceToImplementors = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        await PreloadTargetedSymbolsAsync(ctx, interfaceToImplementors);

        var symbols = BuildSymbolLookups(ctx);
        ResolveInheritanceReferences(ctx, symbols, referenceRelationships, inheritanceRels, interfaceToImplementors);
        var bindings = BuildBindingLookups(ctx);
        var imports = BuildImportLookups(ctx, _currentL4Result);

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
                var globalPct = totalReferences > 0 ? 71 + (int)(6.0 * resolvedCount / totalReferences) : 77;
                ctx.Log($"[Layer 5    ] Resolving global cross-references: {resolvedCount}/{totalReferences} ({pct:F0}%)...");
                ctx.ReportProgress(globalPct, "Layer 5: Global", $"Resolving cross-references: {resolvedCount}/{totalReferences} ({pct:F0}%)...");
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
                        ResolveCallsReference(refItem, ctx, symbols, bindings, imports, interfaceToImplementors, referenceRelationships);
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
                ctx.RecordScanIssue("Layer5_Global", "reference_error", refItem.ScopeSymbolId, $"Failed to resolve reference '{refItem.TargetName}': {ex.Message}", name: "ReferenceResolutionError", exception: ex, isFileParent: false);
            }
        }
        
        ctx.Log($"[Layer 5    ] Resolved {resolvedCount}/{totalReferences} global cross-references ({referenceRelationships.Count} edges).");

        if (newTopicNodes.Count > 0)
        {
            var nodes = newTopicNodes.Select(Node.FromNode).ToList();
            await ctx.EnqueueUploadNodesAsync(nodes);
            ctx.AddNodesCount(nodes.Count);
        }

        if (referenceRelationships.Count > 0)
        {
            ctx.LogDebug($"[Layer 5    ] Uploading {referenceRelationships.Count} resolved reference relationships...");
            await ctx.EnqueueUploadRelationshipsAsync(referenceRelationships);
            ctx.TotalRelsCount += referenceRelationships.Count;
        }

        return referenceRelationships;
    }

    private async Task<List<Relationship>> PerformLateBindingAsync(
        Layer4Result l4Result,
        ParsingContext ctx,
        List<Relationship>? referenceRelationships = null)
    {
        var workspaceNode = l4Result.Prev.Prev.Prev.Workspace;
        var allProjects = l4Result.Prev.Prev.Projects.ToList();

        var entryPoints = new List<EntryPointNode>();
        var endpoints = new List<EndpointNode>();
        var externalServices = new List<ExternalServiceNode>();

        CollectPublicSymbols(workspaceNode, entryPoints, endpoints, externalServices);

        // Build lookup of endpoint ID -> handler FunctionNode IDs (from child functions, TreeRelationships, and referenceRelationships)
        var handlersByEndpointId = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        void RegisterEndpointHandler(string epId, string fnId)
        {
            if (string.IsNullOrEmpty(epId) || string.IsNullOrEmpty(fnId) || epId == fnId) return;
            if (!handlersByEndpointId.TryGetValue(epId, out var set))
            {
                set = new HashSet<string>(StringComparer.Ordinal);
                handlersByEndpointId[epId] = set;
            }
            set.Add(fnId);
        }

        foreach (var ep in endpoints)
        {
            foreach (var childFn in ep.Children.OfType<Common.Nodes.Layer3_Syntactic.FunctionNode>())
            {
                RegisterEndpointHandler(ep.Id, childFn.Id);
            }
        }

        foreach (var rel in ctx.TreeRelationships)
        {
            if (rel.Kind is OntologyConstants.Relationships.Triggers or OntologyConstants.Relationships.ExposedBy &&
                (rel.To.Contains(":fn:", StringComparison.Ordinal) || rel.To.Contains(":Function:", StringComparison.Ordinal)))
            {
                RegisterEndpointHandler(rel.From, rel.To);
            }
        }

        if (referenceRelationships != null)
        {
            foreach (var rel in referenceRelationships)
            {
                if (rel.Kind is OntologyConstants.Relationships.Triggers or OntologyConstants.Relationships.ExposedBy &&
                    (rel.To.Contains(":fn:", StringComparison.Ordinal) || rel.To.Contains(":Function:", StringComparison.Ordinal)))
                {
                    RegisterEndpointHandler(rel.From, rel.To);
                }
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
            if (IsNonServiceEntryPoint(entry.EntryType))
            {
                continue;
            }

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

        ctx.ReportProgress(77, "Layer 5: Global", $"Late-binding {prepExtServices.Count} external service calls...");
        ctx.Log($"[Layer 5    ] Late-binding {prepExtServices.Count} external service calls against {prepEndpoints.Count} endpoints and {prepEntryPoints.Count} entry points...");

        var lateBoundRels = new List<Relationship>();
        var addedProjectDeps = new HashSet<(string From, string To)>();
        var addedFunctionCalls = new HashSet<(string From, string To)>();
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

            candidateEntryPoints = FilterAmbiguousCandidates(extService, candidateEntryPoints, e => e.TargetProj, e => e.EntryNorm);

            foreach (var entryPoint in candidateEntryPoints)
            {
                ctx.LogDebug($"[Layer 5    ] Binding ExternalService '{extService.Service.Id}' to EntryPoint '{entryPoint.EntryPoint.Id}'");
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
                        ctx.LogDebug($"[Layer 5    ] Synthesized dependency: Project '{callerProj.Name}' -> Project '{targetProj.Name}' via entrypoint '{entryPoint.EntryPoint.Name}'");
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

            candidateEndpoints = FilterAmbiguousCandidates(extService, candidateEndpoints, e => e.TargetProj, e => e.RouteNorm);

            foreach (var endpoint in candidateEndpoints)
            {
                ctx.LogDebug($"[Layer 5    ] Binding ExternalService '{extService.Service.Id}' to Endpoint '{endpoint.Endpoint.Id}'");
                var epProtocol = endpoint.Endpoint.Protocol ?? "REST";
                var evKind = epProtocol.Equals("gRPC", StringComparison.OrdinalIgnoreCase) ? OntologyConstants.EvidenceKinds.GrpcCall
                           : epProtocol.Equals("WCF", StringComparison.OrdinalIgnoreCase) ? OntologyConstants.EvidenceKinds.WcfCall
                           : OntologyConstants.EvidenceKinds.HttpCall;
                var opVerb = !string.IsNullOrEmpty(endpoint.Endpoint.HttpMethod) &&
                             !endpoint.Endpoint.HttpMethod.Equals("ALL", StringComparison.OrdinalIgnoreCase)
                    ? endpoint.Endpoint.HttpMethod.ToUpperInvariant()
                    : "CALL";
                var callEpExt = new Dictionary<string, string>
                {
                    [OntologyConstants.PropertyNames.Operation] = opVerb,
                    [OntologyConstants.PropertyNames.EvidenceKind] = evKind
                };
                if (extService.Service.Extensions != null)
                {
                    if (extService.Service.Extensions.TryGetValue("file_path", out var fp) && !string.IsNullOrEmpty(fp))
                        callEpExt[OntologyConstants.PropertyNames.FilePath] = fp;
                    if (extService.Service.Extensions.TryGetValue("start_line", out var sl) && !string.IsNullOrEmpty(sl))
                        callEpExt[OntologyConstants.PropertyNames.Line] = sl;
                }

                var rel = Relationship.FromRelationship(new CallsEndpointRelationship(extService.Service.Id, endpoint.Endpoint.Id, callEpExt));
                lateBoundRels.Add(rel);
                matchedEndpoint = true;
                boundServicesCount++;

                // Bridge CALLS from caller function to endpoint handler/resolver FunctionNodes (L3 -> L3)
                if (extService.Service.Extensions != null &&
                    extService.Service.Extensions.TryGetValue("caller_symbol_id", out var callerId) &&
                    !string.IsNullOrEmpty(callerId))
                {
                    if (handlersByEndpointId.TryGetValue(endpoint.Endpoint.Id, out var handlerFnIds))
                    {
                        foreach (var handlerFnId in handlerFnIds)
                        {
                            if (callerId != handlerFnId && addedFunctionCalls.Add((callerId, handlerFnId)))
                            {
                                lateBoundRels.Add(Relationship.FromRelationship(new CallsRelationship(callerId, handlerFnId, callEpExt)));
                            }
                        }
                    }
                }

                // Synthesize Project -> Project DEPENDS_ON relationship
                var callerProj = extService.CallerProj;
                var targetProj = endpoint.TargetProj;

                if (callerProj != null && targetProj != null && callerProj.Id != targetProj.Id)
                {
                    var contribRel = rel;
                    if (addedProjectDeps.Add((callerProj.Id, targetProj.Id)))
                    {
                        ctx.LogDebug($"[Layer 5    ] Synthesized dependency: Project '{callerProj.Name}' -> Project '{targetProj.Name}' via endpoint '{endpoint.Endpoint.RouteTemplate}'");
                        var projDep = Relationship.FromRelationship(new DependsOnRelationship(callerProj.Id, targetProj.Id, new() { ["dependency_type"] = "service_call" }));
                        EvidenceProvenanceHelper.RecordContributingEdge(projDep.Properties, contribRel);
                        lateBoundRels.Add(projDep);
                    }
                    else
                    {
                        var existingProjDep = lateBoundRels.FirstOrDefault(r =>
                            r.From == callerProj.Id && r.To == targetProj.Id && r.Kind == OntologyConstants.Relationships.DependsOn);
                        if (existingProjDep != null)
                        {
                            EvidenceProvenanceHelper.RecordContributingEdge(existingProjDep.Properties, contribRel);
                        }
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
                                        ctx.LogDebug($"[Layer 5    ] Inferred project dependency: Project '{callerProj.Name}' -> Project '{proj.Name}' via service domain '{extService.Service.DomainOrService}'");
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
                var globalPct = totalExt > 0 ? 77 + (int)(7.0 * processedExt / totalExt) : 84;
                ctx.Log($"[Layer 5    ] Late-binding progress: {processedExt}/{totalExt} external services ({pct:F0}%)...");
                ctx.ReportProgress(globalPct, "Layer 5: Global", $"Late-binding external services: {processedExt}/{totalExt} ({pct:F0}%)...");
            }
        }

        if (boundServicesCount > 0)
        {
            ctx.Log($"[Layer 5    ] Successfully bound {boundServicesCount} ExternalService call(s) to endpoints/entrypoints.");
        }

        if (lateBoundRels.Count > 0)
        {
            ctx.LogDebug($"[Layer 5    ] Uploading {lateBoundRels.Count} late-bound relationships...");
            await ctx.EnqueueUploadRelationshipsAsync(lateBoundRels);
            ctx.TotalRelsCount += lateBoundRels.Count;
        }

        return lateBoundRels;
    }

    private static bool IsNonServiceEntryPoint(string? entryType)
    {
        if (string.IsNullOrWhiteSpace(entryType)) return false;
        return entryType.Equals("React", StringComparison.OrdinalIgnoreCase) ||
               entryType.Equals("Angular", StringComparison.OrdinalIgnoreCase) ||
               entryType.Equals("Vue", StringComparison.OrdinalIgnoreCase) ||
               entryType.Equals("Svelte", StringComparison.OrdinalIgnoreCase) ||
               entryType.Equals("Vite", StringComparison.OrdinalIgnoreCase) ||
               entryType.Equals("Next", StringComparison.OrdinalIgnoreCase) ||
               entryType.Equals("Nuxt", StringComparison.OrdinalIgnoreCase) ||
               entryType.Equals("Jest", StringComparison.OrdinalIgnoreCase) ||
               entryType.Equals("Mocha", StringComparison.OrdinalIgnoreCase) ||
               entryType.Equals("Cypress", StringComparison.OrdinalIgnoreCase) ||
               entryType.Equals("Playwright", StringComparison.OrdinalIgnoreCase) ||
               entryType.Equals("xUnit", StringComparison.OrdinalIgnoreCase) ||
               entryType.Equals("NUnit", StringComparison.OrdinalIgnoreCase) ||
               entryType.Equals("MSTest", StringComparison.OrdinalIgnoreCase) ||
               entryType.Equals("CLI", StringComparison.OrdinalIgnoreCase) ||
               entryType.Equals("cron", StringComparison.OrdinalIgnoreCase) ||
               entryType.Equals("queue-listener", StringComparison.OrdinalIgnoreCase) ||
               entryType.Equals("Consume", StringComparison.OrdinalIgnoreCase) ||
               entryType.Equals("Consumer", StringComparison.OrdinalIgnoreCase);
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

    internal sealed class PreparedEndpoint
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

    internal sealed class PreparedEntryPoint
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

    internal sealed class PreparedExternalService
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

    internal static PreparedEndpoint PrepareEndpoint(EndpointNode endpoint, ProjectNode? targetProj)
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

    internal static PreparedEntryPoint PrepareEntryPoint(EntryPointNode entryPoint, ProjectNode? targetProj)
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

    internal static PreparedExternalService PrepareExternalService(ExternalServiceNode extService, ProjectNode? callerProj)
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

    private bool IsMatch(ExternalServiceNode extService, EndpointNode endpoint) => IsMatch(extService, endpoint, null);

    internal bool IsMatch(ExternalServiceNode extService, EndpointNode endpoint, ProjectNode? targetProj)
    {
        var prepExt = PrepareExternalService(extService, null);
        var prepEp = PrepareEndpoint(endpoint, targetProj);
        return IsMatch(prepExt, prepEp, (p, d) => DoesProjectMatchServiceDomain(p, d));
    }

    private async Task MaterializeLogicalStructureAsync(Layer4Result l4Result, ParsingContext ctx)
    {
        if (l4Result.SemanticStructure == null) return;
        ctx.ReportProgress(86, "Layer 5: Global", "Materializing logical architecture structure...");

        var workspace = l4Result.Prev.Prev.Prev.Workspace;
        var logicalNodeId = $"{ctx.WorkspaceId}:{OntologyConstants.IdPrefixes.LogicalStructure}";
        var logicalStructureNode = new LogicalStructureNode(logicalNodeId, "LogicalStructure", workspace.Path);

        var nodesToUpload = new List<Node> { Node.FromNode(logicalStructureNode) };
        var relsToUpload = new List<Relationship>
        {
            Relationship.FromRelationship(new ContainsRelationship(workspace.Id, logicalStructureNode.Id))
        };

        var workloads = l4Result.SemanticStructure.Children
            .Where(c => c is ServiceNode or AppNode or WorkerNode or CliToolNode or LibraryNode)
            .ToList();

        var domainGroups = new Dictionary<string, List<IOntologyNode>>(StringComparer.OrdinalIgnoreCase);
        var workloadToDomainKey = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var wl in workloads)
        {
            var rawName = wl.Extensions?.GetValueOrDefault("raw_name") ?? wl.GetName();
            var domainKey = ResourceSemanticAnalyzer.CleanProjectNameToDomain(rawName);
            if (string.IsNullOrWhiteSpace(domainKey))
            {
                domainKey = "Core";
            }
            else
            {
                domainKey = char.ToUpperInvariant(domainKey[0]) + domainKey[1..];
            }

            if (!domainGroups.TryGetValue(domainKey, out var list))
            {
                list = [];
                domainGroups[domainKey] = list;
            }
            list.Add(wl);
            workloadToDomainKey[wl.Id] = domainKey;
        }

        var domainIdByKey = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var bcIdByKey = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (domainKey, memberWorkloads) in domainGroups)
        {
            var slug = domainKey.ToLowerInvariant();
            var domainId = $"{ctx.WorkspaceId}:{OntologyConstants.IdPrefixes.Domain}:{slug}";
            var bcId = $"{ctx.WorkspaceId}:{OntologyConstants.IdPrefixes.BoundedContext}:{slug}";

            domainIdByKey[domainKey] = domainId;
            bcIdByKey[domainKey] = bcId;

            var domainNode = new DomainNode(domainId, domainKey, domainKey, "Core", Path: ".", Extensions: new Dictionary<string, string>
            {
                ["workload_count"] = memberWorkloads.Count.ToString()
            });
            var bcNode = new BoundedContextNode(bcId, $"{domainKey} Context", $"{domainKey} Context", domainId, Path: ".", Extensions: new Dictionary<string, string>
            {
                ["domain"] = domainKey,
                ["workload_count"] = memberWorkloads.Count.ToString()
            });

            nodesToUpload.Add(Node.FromNode(domainNode));
            nodesToUpload.Add(Node.FromNode(bcNode));

            relsToUpload.Add(Relationship.FromRelationship(new ContainsRelationship(logicalStructureNode.Id, domainId)));
            relsToUpload.Add(Relationship.FromRelationship(new ContainsRelationship(domainId, bcId)));

            foreach (var wl in memberWorkloads)
            {
                relsToUpload.Add(Relationship.FromRelationship(new ContainsRelationship(bcId, wl.Id)));
                relsToUpload.Add(Relationship.FromRelationship(new ContainsRelationship(domainId, wl.Id)));
            }
        }

        var seenLogicalDeps = new HashSet<(string From, string To)>();
        if (l4Result.SemanticRelationships != null)
        {
            foreach (var rel in l4Result.SemanticRelationships)
            {
                if (rel.Kind is OntologyConstants.Relationships.DependsOn or OntologyConstants.Relationships.ServiceCall &&
                    workloadToDomainKey.TryGetValue(rel.From, out var fromDomain) &&
                    workloadToDomainKey.TryGetValue(rel.To, out var toDomain) &&
                    !string.Equals(fromDomain, toDomain, StringComparison.OrdinalIgnoreCase))
                {
                    var fromBc = bcIdByKey[fromDomain];
                    var toBc = bcIdByKey[toDomain];
                    if (seenLogicalDeps.Add((fromBc, toBc)))
                    {
                        relsToUpload.Add(Relationship.FromRelationship(new DependsOnRelationship(fromBc, toBc, new Dictionary<string, string>
                        {
                            ["dependency_type"] = "bounded_context"
                        })));
                    }

                    var fromDom = domainIdByKey[fromDomain];
                    var toDom = domainIdByKey[toDomain];
                    if (seenLogicalDeps.Add((fromDom, toDom)))
                    {
                        relsToUpload.Add(Relationship.FromRelationship(new DependsOnRelationship(fromDom, toDom, new Dictionary<string, string>
                        {
                            ["dependency_type"] = "domain"
                        })));
                    }
                }
            }
        }

        await ctx.DbClient.UploadNodesAsync(nodesToUpload);
        await ctx.DbClient.UploadRelationshipsAsync(relsToUpload);
        ctx.AddNodesCount(nodesToUpload.Count);
        ctx.AddRelsCount(relsToUpload.Count);
    }
}
