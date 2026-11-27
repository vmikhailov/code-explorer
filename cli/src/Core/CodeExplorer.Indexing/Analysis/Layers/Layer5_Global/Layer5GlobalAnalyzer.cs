using System.Diagnostics;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Common.Nodes.Layer5_Logical;
using CodeExplorer.Core.Common.Relationships;
using CodeExplorer.Core.Parser.Canonical;

namespace CodeExplorer.Core.Parser.Layers;

public partial class Layer5AnalysisParser
{
    private const string Layer5GlobalStageName = "Layer 5: Global";
    private const string MaterializeLogicalProgressMsg = "Materializing logical architecture structure...";
    private const string LogicalStructureName = "LogicalStructure";
    private const string DefaultCoreDomainKind = "Core";
    private const string RootWorkspacePath = ".";

    private Layer4Result? _currentL4Result;

    public Task<Layer5Result> ParseAsync(Layer4Result l4Result, Layer5Context ctx) =>
        ParseAsync(ctx);

    public async Task<Layer5Result> ParseAsync(Layer5Context ctx)
    {
        var l4Result = ctx.Layer4;
        var sw = Stopwatch.StartNew();
        _currentL4Result = l4Result;
        ctx.ReportProgress(68, "Layer 5: Global", "Starting late binding, cross-references & post-indexing analysis...");
        ctx.Layer5Log.ScanStarted();

        // 1. Upload the early enqueued relationships after all nodes are created
        var belongsToRels = new List<Relationship>();

        // Syntactic BelongsTo
        if (ctx.SyntaxStructure != null)
        {
            foreach (var pSyntax in ctx.SyntaxStructure.Children)
            {
                var matchedProj = ctx.Projects.FirstOrDefault(p => p.Path == pSyntax.Path);
                if (matchedProj != null)
                {
                    belongsToRels.Add(Relationship.FromRelationship(new BelongsToRelationship(pSyntax.Id, matchedProj.Id)));
                }
            }
        }

        // Semantic BelongsTo
        foreach (var pSem in l4Result.SemanticStructure.Children)
        {
            var matchedProj = ctx.Projects.FirstOrDefault(p => p.Path == pSem.Path);
            if (matchedProj != null)
            {
                belongsToRels.Add(Relationship.FromRelationship(new BelongsToRelationship(pSem.Id, matchedProj.Id)));
            }
        }

        if (belongsToRels.Count > 0)
        {
            ctx.Layer5Log.UploadingBelongsToRelationships(belongsToRels.Count);
            await ctx.EnqueueUploadRelationshipsAsync(belongsToRels);
            ctx.TotalRelsCount += belongsToRels.Count;
        }

        // 2. Upload project dependencies
        await UploadProjectDependenciesAsync(ctx);

        // 3. Resolve and upload global cross-references (like function CALLS)
        var referenceRelationships = await ResolveAndUploadGlobalReferencesAsync(ctx);
        ctx.EvictLayer3ResolutionBuffers();
        PipelineMemoryEvictor.EvictPostResolutionLeafNodes(ctx);

        // 4. Perform Late Binding
        var lateBoundRels = await PerformLateBindingAsync(l4Result, ctx, referenceRelationships);

        // 4b. Propagate Semantic Arguments & Ground Downward Target Relationships
        await SemanticArgumentPropagator.PropagateAndGroundAsync(l4Result, ctx, referenceRelationships, lateBoundRels);
        PipelineMemoryEvictor.EvictNonPostIndexLayersBeforePostIndex(ctx);

        // 5. Run PostIndexAnalyzer
        await ctx.FlushScanIssuesAsync();
        var postAnalyzer = new PostIndexAnalyzer(ctx.DbClient);
        if (ctx.IsSubtreeScan)
        {
            await ctx.WaitForQueueDrainedAsync();
            ctx.ReportProgress(84, "Layer 5: Global", "Running post-indexing analysis...");
            ctx.Layer5Log.RunningGlobalPostIndexAnalysis();
            await postAnalyzer.RunAsync(ctx.WorkspaceId, ctx);
        }
        else if (ctx.IncrementalChangedFiles == null)
        {
            await ctx.WaitForQueueDrainedAsync();
            ctx.ReportProgress(84, "Layer 5: Global", "Running in-memory post-indexing analysis...");
            ctx.Layer5Log.RunningInMemoryPostIndexAnalysis();
            await postAnalyzer.RunInMemoryAsync(ctx, l4Result, referenceRelationships, lateBoundRels);
        }
        else await ctx.WaitForQueueDrainedAsync();
        // 6. Materialize Layer 5 Logical Structure (LogicalStructure, Domain, BoundedContext)
        if (ctx.IncrementalChangedFiles == null) await MaterializeLogicalStructureAsync(l4Result, ctx);

        ctx.EvictPostIndexBuffers();
        ctx.Layer5Log.ScanCompleted(sw.ElapsedMilliseconds);
        return new Layer5Result(lateBoundRels);
    }

    private async Task UploadProjectDependenciesAsync(Layer5Context ctx)
    {
        if (ctx.GlobalProjectDependencies.Count > 0)
        {
            ctx.Layer5Log.UploadingProjectDependencies(ctx.GlobalProjectDependencies.Count);
            await ctx.EnqueueUploadRelationshipsAsync(ctx.GlobalProjectDependencies);
            ctx.TotalRelsCount += ctx.GlobalProjectDependencies.Count;
        }
    }

    private async Task<List<Relationship>> ResolveAndUploadGlobalReferencesAsync(Layer5Context ctx)
    {
        var sw = Stopwatch.StartNew();
        var totalReferences = ctx.GlobalReferences.Count;
        ctx.ReportProgress(71, "Layer 5: Global", $"Resolving {totalReferences} global cross-references...");
        ctx.Layer5Log.ResolvingCrossReferences(totalReferences);
        var referenceRelationships = new List<Relationship>(totalReferences > 0 ? Math.Min(totalReferences, 2000000) : 0);
        var inheritanceRels = new HashSet<(string From, string To)>();
        var interfaceToImplementors = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        await PreloadTargetedSymbolsAsync(ctx, interfaceToImplementors);

        var symbols = BuildSymbolLookups(ctx, _currentL4Result);
        ResolveInheritanceReferences(ctx, symbols, referenceRelationships, inheritanceRels, interfaceToImplementors);
        Layer5SymbolVisibilityResolver.ResolveMemberOfTypeRelationships(ctx, symbols, referenceRelationships);
        var bindings = BuildBindingLookups(ctx, symbols);
        var imports = BuildImportLookups(ctx, _currentL4Result);

        var createdTopicIds = new HashSet<string>(StringComparer.Ordinal);
        var newTopicNodes = new List<TopicNode>();
        var existingRefRels = new HashSet<(string From, string To, string Kind)>();
        var deferredParamMessagingRefs = new List<Reference>();

        ResolveGlobalReferencesBatch(
            ctx,
            symbols,
            bindings,
            imports,
            interfaceToImplementors,
            referenceRelationships,
            newTopicNodes,
            createdTopicIds,
            existingRefRels,
            deferredParamMessagingRefs);

        if (deferredParamMessagingRefs.Count > 0)
        {
            ResolveDeferredParamMessagingReferences(deferredParamMessagingRefs, ctx, symbols, bindings, newTopicNodes, createdTopicIds, referenceRelationships);
        }

        Layer5SymbolVisibilityResolver.ResolveSqlTriggerRelationships(_currentL4Result, ctx.FilesStructure, symbols, referenceRelationships, existingRefRels);

        await RunSemanticLayerEnrichersAsync(ctx, symbols, bindings, referenceRelationships, existingRefRels);
        await Layer5ComplexityPropagator.PropagateAndUploadAsync(ctx, referenceRelationships);

        ctx.Layer5Log.CrossReferencesResolved(totalReferences, totalReferences, referenceRelationships.Count, sw.ElapsedMilliseconds);

        if (newTopicNodes.Count > 0)
        {
            var nodes = newTopicNodes.Select(Node.FromNode).ToList();
            await ctx.EnqueueUploadNodesAsync(nodes);
            ctx.AddNodesCount(nodes.Count);
        }

        if (referenceRelationships.Count > 0)
        {
            ctx.Layer5Log.UploadingReferenceRelationships(referenceRelationships.Count);
            await ctx.EnqueueUploadRelationshipsAsync(referenceRelationships);
            ctx.TotalRelsCount += referenceRelationships.Count;
        }

        return referenceRelationships;
    }

    private async Task<List<Relationship>> PerformLateBindingAsync(
        Layer4Result l4Result,
        Layer5Context ctx,
        List<Relationship>? referenceRelationships = null)
    {
        var sw = Stopwatch.StartNew();
        var workspaceNode = ctx.Workspace;
        var allProjects = ctx.Projects.ToList();

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
            foreach (var childFn in ep.Children.OfType<FunctionNode>())
            {
                RegisterEndpointHandler(ep.Id, childFn.Id);
            }
        }

        foreach (var rel in ctx.TreeRelationships)
        {
            if (rel.Kind is RelationshipKind.Triggers or RelationshipKind.ExposedBy &&
                (rel.To.Contains(":fn:", StringComparison.Ordinal) || rel.To.Contains(":Function:", StringComparison.Ordinal)))
            {
                RegisterEndpointHandler(rel.From, rel.To);
            }
        }

        if (referenceRelationships != null)
        {
            foreach (var rel in referenceRelationships)
            {
                if (rel.Kind is RelationshipKind.Triggers or RelationshipKind.ExposedBy &&
                    (rel.To.Contains(":fn:", StringComparison.Ordinal) || rel.To.Contains(":Function:", StringComparison.Ordinal)))
                {
                    RegisterEndpointHandler(rel.From, rel.To);
                }
            }
        }

        var localExtIds = new HashSet<string>(externalServices.Select(e => e.Id));
        var localEndpointIds = new HashSet<string>(endpoints.Select(e => e.Id));
        var localEntryPointIds = new HashSet<string>(entryPoints.Select(e => e.Id));

        if (ctx.IsSubtreeScan || ctx.IncrementalChangedFiles != null)
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
        var projectsByPath = new Dictionary<string, ProjectNode>(allProjects.Count, StringComparer.Ordinal);
        foreach (var proj in allProjects)
        {
            if (!string.IsNullOrEmpty(proj.Path))
            {
                projectsByPath.TryAdd(proj.Path, proj);
            }
        }
        foreach (var pSem in l4Result.SemanticStructure.Children)
        {
            if (!string.IsNullOrEmpty(pSem.Path) && projectsByPath.TryGetValue(pSem.Path, out var matchedProj))
            {
                MapProjectNodes(pSem, matchedProj, nodeToProject);
            }
        }

        // Fallback mapping via file_path or path for all external services, endpoints, and entry points
        foreach (var es in externalServices)
        {
            if (!nodeToProject.ContainsKey(es.Id) && !string.IsNullOrEmpty(es.FilePath))
            {
                var proj = Layer2ProjectParser.FindProjectForFilePath(es.FilePath, allProjects);
                if (proj != null) nodeToProject[es.Id] = proj;
            }
        }
        foreach (var ep in endpoints)
        {
            if (!nodeToProject.ContainsKey(ep.Id))
            {
                var p = !string.IsNullOrEmpty(ep.Path) ? ep.Path : ep.FilePath;
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
                var p = !string.IsNullOrEmpty(entry.FilePath) ? entry.FilePath : entry.Path;
                if (!string.IsNullOrEmpty(p))
                {
                    var proj = Layer2ProjectParser.FindProjectForFilePath(p, allProjects);
                    if (proj != null) nodeToProject[entry.Id] = proj;
                }
            }
        }

        // Prepare and filter endpoints and entry points
        var prepEndpoints = new List<PreparedEndpoint>(endpoints.Count);
        var endpointsByShape = new Dictionary<string, List<PreparedEndpoint>>(StringComparer.OrdinalIgnoreCase);
        foreach (var ep in endpoints)
        {
            nodeToProject.TryGetValue(ep.Id, out var targetProj);
            var prep = PrepareEndpoint(ep, targetProj);
            if (!string.IsNullOrEmpty(prep.RouteNorm) && !prep.IsApiPrefixOnly)
            {
                prepEndpoints.Add(prep);
                if (prep.HasStaticSegment)
                {
                    if (!endpointsByShape.TryGetValue(prep.Canonical.CanonicalShape, out var shapeList))
                    {
                        shapeList = new List<PreparedEndpoint>(2);
                        endpointsByShape[prep.Canonical.CanonicalShape] = shapeList;
                    }
                    shapeList.Add(prep);
                }
            }
        }

        var projectsWithEndpoints = prepEndpoints
            .Select(e => e.TargetProj?.Id)
            .OfType<string>()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var prepEntryPoints = new List<PreparedEntryPoint>(entryPoints.Count);
        foreach (var entry in entryPoints)
        {
            if (IsNonServiceEntryPoint(entry.EntryType.ToCanonicalString()))
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

        ctx.ReportProgress(77, Layer5GlobalStageName, $"Late-binding {prepExtServices.Count} external service calls...");
        ctx.Layer5Log.LateBindingStarted(prepExtServices.Count, prepEndpoints.Count, prepEntryPoints.Count);

        var lateBoundRels = new List<Relationship>();
        var serviceCallEdgesByPair = new Dictionary<(string From, string To), Relationship>();
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

        var callableProjectIds = new HashSet<string>(projectsWithEndpoints, StringComparer.OrdinalIgnoreCase);
        foreach (var ep in prepEntryPoints)
        {
            if (ep.TargetProj != null)
            {
                callableProjectIds.Add(ep.TargetProj.Id);
            }
        }
        var callableHostCandidates = Layer5RuntimeCallBinder.BuildCallableHostCandidateProjects(allProjects, callableProjectIds);

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
                if (!Layer5RuntimeCallBinder.HasSufficientRouteOrHostSpecificity(
                        extService, entryPoint.Segments, extService.CallerProj, entryPoint.TargetProj, CheckProjectMatch,
                        entryPoint.EntryPoint.EntryType.ToCanonicalString()))
                {
                    continue;
                }

                ctx.Layer5Log.BindingToEntryPoint(extService.Service.Id, entryPoint.EntryPoint.Id);
                var rel = Relationship.FromRelationship(new CallsRelationship(extService.Service.Id, entryPoint.EntryPoint.Id));
                lateBoundRels.Add(rel);
                matchedEndpoint = true;
                boundServicesCount++;

                Layer5RuntimeCallBinder.EmitServiceCallViaEntryPoint(
                    ctx,
                    extService.CallerProj,
                    entryPoint.TargetProj,
                    entryPoint.EntryPoint.Name,
                    extService.Service.DomainOrService,
                    serviceCallEdgesByPair,
                    lateBoundRels);
            }

            var candidateEndpoints = new List<PreparedEndpoint>();
            var isRelOrPathSeg = extService.Service.IsRelativePath == true || extService.IsPathSegment;

            // O(1) CanonicalShape hash-join lookup first
            if (extService.HasStaticSegment &&
                endpointsByShape.TryGetValue(extService.Canonical.CanonicalShape, out var exactShapeCandidates))
            {
                foreach (var endpoint in exactShapeCandidates)
                {
                    if (ctx.IsSubtreeScan && !localExtIds.Contains(extService.Service.Id) && !localEndpointIds.Contains(endpoint.Endpoint.Id))
                    {
                        continue;
                    }

                    if (isRelOrPathSeg &&
                        IsRelativeFrontendTargetMismatch(extService.CallerProj, endpoint.TargetProj, projectsWithEndpoints))
                    {
                        continue;
                    }

                    if (IsMatch(extService, endpoint, CheckProjectMatch))
                    {
                        candidateEndpoints.Add(endpoint);
                    }
                }
            }

            if (candidateEndpoints.Count == 0)
            {
                foreach (var endpoint in prepEndpoints)
                {
                    if (ctx.IsSubtreeScan && !localExtIds.Contains(extService.Service.Id) && !localEndpointIds.Contains(endpoint.Endpoint.Id))
                    {
                        continue;
                    }

                    if (isRelOrPathSeg &&
                        IsRelativeFrontendTargetMismatch(extService.CallerProj, endpoint.TargetProj, projectsWithEndpoints))
                    {
                        continue;
                    }

                    if (IsMatch(extService, endpoint, CheckProjectMatch))
                    {
                        candidateEndpoints.Add(endpoint);
                    }
                }
            }

            candidateEndpoints = FilterAmbiguousCandidates(extService, candidateEndpoints, e => e.TargetProj, e => e.RouteNorm);

            foreach (var endpoint in candidateEndpoints)
            {
                if (extService.CallerProj != null &&
                    endpoint.TargetProj != null &&
                    extService.CallerProj.Id != endpoint.TargetProj.Id &&
                    !Layer5RuntimeCallBinder.HasSufficientRouteOrHostSpecificity(
                        extService, endpoint.Segments, extService.CallerProj, endpoint.TargetProj, CheckProjectMatch,
                        endpoint.Endpoint.Protocol))
                {
                    continue;
                }

                ctx.Layer5Log.BindingToEndpoint(extService.Service.Id, endpoint.Endpoint.Id);
                var epProtocol = endpoint.Endpoint.Protocol ?? "REST";
                var evKind = epProtocol.Equals("gRPC", StringComparison.OrdinalIgnoreCase) ? OntologyConstants.EvidenceKinds.GrpcCall
                           : epProtocol.Equals("WCF", StringComparison.OrdinalIgnoreCase) ? OntologyConstants.EvidenceKinds.WcfCall
                           : OntologyConstants.EvidenceKinds.HttpCall;
                var opVerb = !string.IsNullOrEmpty(endpoint.Endpoint.HttpMethod) &&
                             !endpoint.Endpoint.HttpMethod.Equals("ALL", StringComparison.OrdinalIgnoreCase)
                    ? endpoint.Endpoint.HttpMethod.ToUpperInvariant()
                    : "CALL";
                var extFp = !string.IsNullOrEmpty(extService.Service.FilePath) ? extService.Service.FilePath : null;
                var extLine = extService.Service.StartLine ?? 0;

                var rel = Relationship.FromRelationship(new CallsEndpointRelationship(extService.Service.Id, endpoint.Endpoint.Id)
                {
                    Operation = opVerb,
                    EvidenceKind = evKind,
                    FilePath = extFp,
                    Line = extLine
                });
                rel.CallContext = extService.Service.CallContext;
                lateBoundRels.Add(rel);
                matchedEndpoint = true;
                boundServicesCount++;

                // Bridge CALLS from caller function to endpoint handler/resolver FunctionNodes (L3 -> L3)
                var callerId = extService.Service.CallerSymbolId;
                if (!string.IsNullOrEmpty(callerId))
                {
                    var callerEpRel = Relationship.FromRelationship(new CallsEndpointRelationship(callerId, endpoint.Endpoint.Id)
                    {
                        Operation = opVerb,
                        EvidenceKind = evKind,
                        FilePath = extFp,
                        Line = extLine
                    });
                    callerEpRel.CallContext = extService.Service.CallContext;
                    lateBoundRels.Add(callerEpRel);

                    if (handlersByEndpointId.TryGetValue(endpoint.Endpoint.Id, out var handlerFnIds))
                    {
                        foreach (var handlerFnId in handlerFnIds)
                        {
                            if (callerId != handlerFnId && addedFunctionCalls.Add((callerId, handlerFnId)))
                            {
                                var fnCallRel = Relationship.FromRelationship(new CallsRelationship(callerId, handlerFnId)
                                {
                                    Operation = opVerb,
                                    EvidenceKind = evKind,
                                    FilePath = extFp,
                                    Line = extLine
                                });
                                fnCallRel.CallContext = extService.Service.CallContext;
                                lateBoundRels.Add(fnCallRel);
                            }
                        }
                    }
                }

                // Synthesize runtime SERVICE_CALL relationship (never conflated with build-time DEPENDS_ON)
                Layer5RuntimeCallBinder.EmitServiceCallViaEndpoint(
                    ctx,
                    extService.CallerProj,
                    endpoint.TargetProj,
                    endpoint.Endpoint.RouteTemplate,
                    extService.Service.DomainOrService,
                    rel,
                    serviceCallEdgesByPair,
                    lateBoundRels);
            }

            // Also match by domain/service name ONLY if no endpoint was matched and a single runnable service with endpoints matches
            if (!matchedEndpoint)
            {
                Layer5RuntimeCallBinder.TryInferServiceCallViaHost(
                    ctx,
                    extService,
                    callableHostCandidates,
                    CheckProjectMatch,
                    serviceCallEdgesByPair,
                    lateBoundRels);
            }

            processedExt++;
            if (processedExt % 100 == 0 || Stopwatch.GetElapsedTime(lastReport).TotalSeconds >= 3)
            {
                lastReport = Stopwatch.GetTimestamp();
                var pct = (double)processedExt / totalExt * 100.0;
                var pctInt = (int)Math.Round(pct);
                var globalPct = totalExt > 0 ? 77 + (int)(7.0 * processedExt / totalExt) : 84;
                ctx.Layer5Log.LateBindingProgress(processedExt, totalExt, pctInt);
                ctx.ReportProgress(globalPct, Layer5GlobalStageName, $"Late-binding external services: {processedExt}/{totalExt} ({pctInt}%)...");
            }
        }

        if (boundServicesCount > 0)
        {
            ctx.Layer5Log.LateBindingCompleted(boundServicesCount, sw.ElapsedMilliseconds);
        }

        if (lateBoundRels.Count > 0)
        {
            ctx.Layer5Log.UploadingLateBoundRelationships(lateBoundRels.Count);
            await ctx.EnqueueUploadRelationshipsAsync(lateBoundRels);
            ctx.TotalRelsCount += lateBoundRels.Count;
        }

        return lateBoundRels;
    }

    private static bool IsRelativeFrontendTargetMismatch(
        ProjectNode? callerProj,
        ProjectNode? targetProj,
        HashSet<string> projectsWithEndpoints)
    {
        if (callerProj == null || targetProj == null || callerProj.Id == targetProj.Id)
        {
            return false;
        }

        if (!IsFrontendAppProject(targetProj))
        {
            return false;
        }

        // A shared library or a fullstack FrontendApp with its own BFF endpoints never targets a different FrontendApp via a relative browser path
        return callerProj.IsLibrary ||
               (IsFrontendAppProject(callerProj) && projectsWithEndpoints.Contains(callerProj.Id));
    }

    private static bool IsFrontendAppProject(ProjectNode proj)
    {
        return proj.EntityKind == ProjectEntityKind.App ||
               string.Equals(proj.Role, OntologyConstants.ProjectRoles.FrontendApp, StringComparison.OrdinalIgnoreCase);
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
        public required CanonicalRoute Canonical { get; init; }
        public required bool HasStaticSegment { get; init; }
        public required bool HasTypedParameter { get; init; }
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
        public required CanonicalRoute Canonical { get; init; }
        public required bool HasStaticSegment { get; init; }
        public required bool HasTypedParameter { get; init; }
    }

    internal static PreparedEndpoint PrepareEndpoint(EndpointNode endpoint, ProjectNode? targetProj)
    {
        var routeNorm = NormalizePath(endpoint.RouteTemplate);
        var segs = routeNorm.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var last = segs.Length > 0 ? segs[^1] : string.Empty;
        var isParam = last == "*" || last.StartsWith(':') || (last.StartsWith('{') && last.EndsWith('}'));
        var isPrefixOnly = IsApiPrefixOnly(segs, null);
        var clean = "/" + routeNorm.Trim('/') + "/";
        var canonical = RouteCanonicalParser.Parse(
            !string.IsNullOrEmpty(endpoint.CanonicalRoute) ? endpoint.CanonicalRoute : routeNorm,
            RouteDialect.Auto);
        var hasStatic = false;
        var hasTyped = false;
        for (var i = 0; i < canonical.Segments.Count; i++)
        {
            var s = canonical.Segments[i];
            if (!s.IsParameter) hasStatic = true;
            else if (s.ParamType != RouteParamType.Any) hasTyped = true;
        }

        return new PreparedEndpoint
        {
            Endpoint = endpoint,
            TargetProj = targetProj,
            RouteNorm = routeNorm,
            CleanPath = clean,
            Segments = segs,
            LastSegment = last,
            IsLastSegmentParam = isParam,
            IsApiPrefixOnly = isPrefixOnly,
            Canonical = canonical,
            HasStaticSegment = hasStatic,
            HasTypedParameter = hasTyped
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
        var canonical = RouteCanonicalParser.Parse(
            !string.IsNullOrEmpty(extService.CanonicalRoute) ? extService.CanonicalRoute : pathNorm,
            RouteDialect.OutboundClient);
        var hasStatic = false;
        var hasTyped = false;
        for (var i = 0; i < canonical.Segments.Count; i++)
        {
            var s = canonical.Segments[i];
            if (!s.IsParameter) hasStatic = true;
            else if (s.ParamType != RouteParamType.Any) hasTyped = true;
        }

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
            IsRootOrGenericPath = isRootOrGeneric,
            Canonical = canonical,
            HasStaticSegment = hasStatic,
            HasTypedParameter = hasTyped
        };
    }

    private bool IsMatch(ExternalServiceNode extService, EndpointNode endpoint) => IsMatch(extService, endpoint, null);

    internal bool IsMatch(ExternalServiceNode extService, EndpointNode endpoint, ProjectNode? targetProj)
    {
        var prepExt = PrepareExternalService(extService, null);
        var prepEp = PrepareEndpoint(endpoint, targetProj);
        return IsMatch(prepExt, prepEp, (p, d) => DoesProjectMatchServiceDomain(p, d));
    }

    private async Task MaterializeLogicalStructureAsync(Layer4Result l4Result, Layer5Context ctx)
    {
        if (!WorkspaceConventions.DomainDetectionEnabled || l4Result.SemanticStructure == null) return;
        ctx.ReportProgress(86, Layer5GlobalStageName, MaterializeLogicalProgressMsg);

        var workspace = ctx.Workspace;
        var logicalNodeId = $"{ctx.WorkspaceId}:{OntologyConstants.IdPrefixes.LogicalStructure}";
        var logicalStructureNode = new LogicalStructureNode(logicalNodeId, LogicalStructureName, workspace.Path);

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
            var rawName = (wl as CompositeNode)?.RawName ?? wl.GetName();
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

            var domainNode = new DomainNode(domainId, domainKey, domainKey, DefaultCoreDomainKind, Path: RootWorkspacePath);
            var bcNode = new BoundedContextNode(bcId, $"{domainKey} Context", $"{domainKey} Context", domainId, Path: RootWorkspacePath);

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

        if (_tableOwnerModuleIds is { Count: > 0 })
        {
            foreach (var (tableId, ownerModId) in _tableOwnerModuleIds)
            {
                if (workloadToDomainKey.TryGetValue(ownerModId, out var ownerDomain) &&
                    bcIdByKey.TryGetValue(ownerDomain, out var ownerBcId))
                {
                    relsToUpload.Add(Relationship.FromRelationship(new OwnsRelationship(ownerBcId, tableId)));
                }
            }
        }

        var seenLogicalDeps = new HashSet<(string From, string To)>();
        if (l4Result.SemanticRelationships != null)
        {
            foreach (var rel in l4Result.SemanticRelationships)
            {
                if (rel.Kind is RelationshipKind.DependsOn or RelationshipKind.ServiceCall &&
                    workloadToDomainKey.TryGetValue(rel.From, out var fromDomain) &&
                    workloadToDomainKey.TryGetValue(rel.To, out var toDomain) &&
                    !string.Equals(fromDomain, toDomain, StringComparison.OrdinalIgnoreCase))
                {
                    var fromBc = bcIdByKey[fromDomain];
                    var toBc = bcIdByKey[toDomain];
                    if (seenLogicalDeps.Add((fromBc, toBc)))
                    {
                        relsToUpload.Add(Relationship.FromRelationship(new DependsOnRelationship(fromBc, toBc)
                        {
                            DependencyType = "bounded_context"
                        }));
                    }

                    var fromDom = domainIdByKey[fromDomain];
                    var toDom = domainIdByKey[toDomain];
                    if (seenLogicalDeps.Add((fromDom, toDom)))
                    {
                        relsToUpload.Add(Relationship.FromRelationship(new DependsOnRelationship(fromDom, toDom)
                        {
                            DependencyType = "domain"
                        }));
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
