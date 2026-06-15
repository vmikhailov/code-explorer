using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CodeExplorer.Common;
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
        ctx.Log("[Layer5AnalysisParser] Starting Layer 5 (Late Binding, Cross-References & Post-Indexing Analysis)...");

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
            ctx.Log($"[Layer5AnalysisParser] Uploading {belongsToRels.Count} project containment (BelongsTo) relationships...");
            await ctx.DbClient.UploadRelationshipsAsync(belongsToRels);
            ctx.TotalRelsCount += belongsToRels.Count;
        }

        // 2. Upload project dependencies
        await UploadProjectDependenciesAsync(ctx);

        // 3. Resolve and upload global cross-references (like function CALLS)
        await ResolveAndUploadGlobalReferencesAsync(ctx);

        // 4. Perform Late Binding
        var workspaceNode = l4Result.Prev.Prev.Prev.Workspace;
        var lateBoundRels = await PerformLateBindingAsync(workspaceNode, ctx);

        // 5. Run PostIndexAnalyzer
        ctx.Log("[Layer5AnalysisParser] Running post-indexing analysis via PostIndexAnalyzer...");
        var postAnalyzer = new PostIndexAnalyzer(ctx.DbClient);
        await postAnalyzer.RunAsync(ctx.WorkspaceId);

        ctx.Log("[Layer5AnalysisParser] Late binding and post-indexing analysis pass complete.");
        return new Layer5Result(l4Result, lateBoundRels);
    }

    private async Task UploadProjectDependenciesAsync(ParsingContext ctx)
    {
        if (ctx.GlobalProjectDependencies.Count > 0)
        {
            ctx.Log(
                $"[Layer5AnalysisParser] Uploading {ctx.GlobalProjectDependencies.Count} local project dependency relationships...");
            await ctx.DbClient.UploadRelationshipsAsync(ctx.GlobalProjectDependencies);
            ctx.TotalRelsCount += ctx.GlobalProjectDependencies.Count;
        }
    }

    private async Task ResolveAndUploadGlobalReferencesAsync(ParsingContext ctx)
    {
        var totalReferences = ctx.GlobalReferences.Count;
        ctx.Log($"[Layer5AnalysisParser] Resolving {totalReferences} global cross-references...");
        var referenceRelationships = new List<Relationship>();
        var inheritanceRels = new HashSet<(string From, string To)>();

        // Pass 1: Resolve all inheritance (Implements / InheritsFrom) relationships first and cache them in a HashSet.
        foreach (var refItem in ctx.GlobalReferences)
        {
            ctx.CancellationToken.ThrowIfCancellationRequested();

            if (refItem.Kind == OntologyConstants.Relationships.Implements ||
                refItem.Kind == OntologyConstants.Relationships.InheritsFrom)
            {
                if (ctx.GlobalSymbols.TryGetValue((OntologyConstants.NodeLabels.Type, refItem.TargetName),
                        out var targetNodeId))
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
                    if (ctx.GlobalSymbols.TryGetValue((OntologyConstants.NodeLabels.Endpoint, refItem.TargetName),
                            out var targetEndpointId))
                    {
                        referenceRelationships.Add(
                            Relationship.FromRelationship(new ExposedByRelationship(targetEndpointId,
                                refItem.ScopeSymbolId)));
                    }
                    else if (ctx.GlobalSymbols.TryGetValue(
                                 (OntologyConstants.NodeLabels.EntryPoint, refItem.TargetName), out var targetEpId))
                    {
                        referenceRelationships.Add(
                            Relationship.FromRelationship(new ImplementedByRelationship(targetEpId,
                                refItem.ScopeSymbolId)));
                        inheritanceRels.Add((targetEpId, refItem.ScopeSymbolId));
                    }
                }
            }
        }

        // Pass 2: Resolve all other relationships using the cached inheritance relationships.
        var resolvedCount = 0;

        foreach (var refItem in ctx.GlobalReferences)
        {
            ctx.CancellationToken.ThrowIfCancellationRequested();
            resolvedCount++;

            if (resolvedCount % 100000 == 0)
            {
                ctx.Log($"[Layer5AnalysisParser] Resolving global cross-references: {resolvedCount}/{totalReferences}...");
            }

            if (refItem.Kind == OntologyConstants.Relationships.Calls)
            {
                var targetName = refItem.TargetName;

                if (targetName.Contains('.'))
                {
                    var dotIdx = targetName.LastIndexOf('.');
                    var varName = targetName.Substring(0, dotIdx);
                    var methodName = targetName.Substring(dotIdx + 1);

                    string? filePath = null;
                    var scopeParts = refItem.ScopeSymbolId.Split(':');

                    if (scopeParts.Length > 2 && scopeParts[1] == "symbol")
                    {
                        filePath = scopeParts[2];
                    }

                    if (filePath != null)
                    {
                        RawTypeBinding? binding = null;

                        // Priority 1: Match by scope name
                        binding = ctx.RawTypeBindings.FirstOrDefault(b =>
                            b.FilePath == filePath && b.VariableName == varName &&
                            refItem.ScopeSymbolId.Contains($":{b.ScopeId}:"));

                        // Priority 2: Fallback to any binding in the same file
                        if (binding == null)
                        {
                            binding = ctx.RawTypeBindings.FirstOrDefault(b =>
                                b.FilePath == filePath && b.VariableName == varName);
                        }

                        if (binding != null)
                        {
                            targetName = $"{binding.TypeName}.{methodName}";
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

                if (ctx.GlobalSymbols.TryGetValue((OntologyConstants.NodeLabels.Function, targetName),
                        out var targetNodeId))
                {
                    referenceRelationships.Add(
                        Relationship.FromRelationship(new CallsRelationship(refItem.ScopeSymbolId, targetNodeId)));
                }
                else if (ctx.GlobalSymbols.TryGetValue((OntologyConstants.NodeLabels.Procedure, targetName),
                             out var targetProcId))
                {
                    referenceRelationships.Add(
                        Relationship.FromRelationship(new CalledByRelationship(targetProcId, refItem.ScopeSymbolId)));
                }
            }
            else if (refItem.Kind == OntologyConstants.Relationships.DependsOn)
            {
                if (ctx.GlobalSymbols.TryGetValue((OntologyConstants.NodeLabels.Table, refItem.TargetName),
                        out var targetTableId))
                {
                    referenceRelationships.Add(
                        Relationship.FromRelationship(new QueriedByRelationship(targetTableId, refItem.ScopeSymbolId)));
                }
            }
            else if (refItem.Kind == OntologyConstants.Relationships.UsesType)
            {
                if (ctx.GlobalSymbols.TryGetValue((OntologyConstants.NodeLabels.Type, refItem.TargetName),
                        out var targetNodeId))
                {
                    referenceRelationships.Add(
                        Relationship.FromRelationship(new UsesTypeRelationship(refItem.ScopeSymbolId, targetNodeId)));
                }
            }
            else if (refItem.Kind == OntologyConstants.Relationships.PotentialType)
            {
                if (ctx.GlobalSymbols.TryGetValue((OntologyConstants.NodeLabels.Type, refItem.TargetName),
                        out var targetNodeId))
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
                if (ctx.GlobalSymbols.TryGetValue((OntologyConstants.NodeLabels.Function, refItem.TargetName),
                        out var targetNodeId))
                {
                    referenceRelationships.Add(
                        Relationship.FromRelationship(new TriggersRelationship(refItem.ScopeSymbolId, targetNodeId)));
                }
            }
        }

        if (referenceRelationships.Count > 0)
        {
            ctx.Log($"[Layer5AnalysisParser] Uploading {referenceRelationships.Count} resolved reference relationships...");
            await ctx.DbClient.UploadRelationshipsAsync(referenceRelationships);
            ctx.TotalRelsCount += referenceRelationships.Count;
        }
    }

    private async Task<List<Relationship>> PerformLateBindingAsync(IOntologyNode rootNode, ParsingContext ctx)
    {
        var entryPoints = new List<EntryPointNode>();
        var endpoints = new List<EndpointNode>();
        var externalServices = new List<ExternalServiceNode>();

        CollectPublicSymbols(rootNode, entryPoints, endpoints, externalServices);

        ctx.Log($"[Layer5AnalysisParser] [LateBinding] Found {entryPoints.Count} EntryPoints, {endpoints.Count} Endpoints, and {externalServices.Count} ExternalServices.");

        var lateBoundRels = new List<Relationship>();

        foreach (var extService in externalServices)
        {
            foreach (var entryPoint in entryPoints)
            {
                if (IsMatch(extService, entryPoint))
                {
                    ctx.Log($"[Layer5AnalysisParser] [LateBinding] Binding ExternalService '{extService.Id}' to EntryPoint '{entryPoint.Id}'");
                    var rel = Relationship.FromRelationship(new CallsRelationship(extService.Id, entryPoint.Id));
                    lateBoundRels.Add(rel);
                }
            }

            foreach (var endpoint in endpoints)
            {
                if (IsMatch(extService, endpoint))
                {
                    ctx.Log($"[Layer5AnalysisParser] [LateBinding] Binding ExternalService '{extService.Id}' to Endpoint '{endpoint.Id}'");
                    var rel = Relationship.FromRelationship(new CallsEndpointRelationship(extService.Id, endpoint.Id));
                    lateBoundRels.Add(rel);
                }
            }
        }

        if (lateBoundRels.Count > 0)
        {
            ctx.Log($"[Layer5AnalysisParser] Uploading {lateBoundRels.Count} late-bound relationships...");
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

    private bool IsMatch(ExternalServiceNode extService, EntryPointNode entryPoint)
    {
        var servicePathNorm = NormalizePath(extService.Path);
        var serviceDomainNorm = NormalizePath(extService.DomainOrService);
        var entryNorm = NormalizePath(entryPoint.Name);

        if (string.IsNullOrEmpty(entryNorm))
        {
            return false;
        }

        if (string.Equals(servicePathNorm, entryNorm, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(serviceDomainNorm, entryNorm, StringComparison.OrdinalIgnoreCase))
        {
            return true;
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

        if (string.Equals(servicePathNorm, routeNorm, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!string.IsNullOrEmpty(servicePathNorm) &&
            (servicePathNorm.EndsWith("/" + routeNorm, StringComparison.OrdinalIgnoreCase) ||
             servicePathNorm.EndsWith(routeNorm, StringComparison.OrdinalIgnoreCase) ||
             routeNorm.EndsWith("/" + servicePathNorm, StringComparison.OrdinalIgnoreCase) ||
             routeNorm.EndsWith(servicePathNorm, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        if (string.Equals(serviceDomainNorm, routeNorm, StringComparison.OrdinalIgnoreCase))
        {
            return true;
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
            normalized = normalized.Substring(protocolIdx + 3);
        }

        return normalized.Trim('/');
    }
}
