namespace CodeExplorer.Core.Parser.Layers;

public partial class Layer5AnalysisParser
{
    private Dictionary<string, string>? _tableOwnerModuleIds;

    private async Task RunSemanticLayerEnrichersAsync(
        Layer5Context ctx,
        SymbolLookups symbols,
        BindingLookups bindings,
        List<Relationship> referenceRelationships,
        HashSet<(string From, string To, string Kind)> existingRefRels)
    {
        var enrichedOrmTables = OrmSchemaEnricher.EnrichOrmTablesAndForeignKeys(
            _currentL4Result, ctx, referenceRelationships, existingRefRels);
        if (enrichedOrmTables.Count > 0)
        {
            await ctx.EnqueueUploadNodesAsync(enrichedOrmTables);
        }

        var sensitivityTables = DataSensitivityEnricher.EnrichTableSensitivity(_currentL4Result, ctx.Workspace);
        if (sensitivityTables.Count > 0)
        {
            await ctx.EnqueueUploadNodesAsync(sensitivityTables);
        }

        var cqrs = CqrsMessageEnricher.EnrichCqrsMessages(
            _currentL4Result, ctx, symbols, bindings.MessageHandlersByType, existingRefRels);
        if (cqrs.Relationships.Count > 0) referenceRelationships.AddRange(cqrs.Relationships);

        var refsByScopeId = SemanticEnrichmentHelpers.BuildRefsByScopeId(ctx.GlobalReferences);

        var caches = CacheLineageEnricher.EnrichCaches(_currentL4Result, ctx, refsByScopeId, existingRefRels);
        if (caches.Relationships.Count > 0) referenceRelationships.AddRange(caches.Relationships);

        var txs = TransactionBoundaryEnricher.EnrichTransactions(
            _currentL4Result, ctx, refsByScopeId, referenceRelationships, existingRefRels);
        if (txs.Relationships.Count > 0) referenceRelationships.AddRange(txs.Relationships);

        var policies = PolicyAuthorizationEnricher.EnrichPolicies(
            _currentL4Result, ctx, cqrs.Messages, refsByScopeId, referenceRelationships, existingRefRels);
        if (policies.Relationships.Count > 0) referenceRelationships.AddRange(policies.Relationships);

        var jobs = ScheduledJobEnricher.EnrichJobs(_currentL4Result, ctx, refsByScopeId, existingRefRels);
        if (jobs.Relationships.Count > 0) referenceRelationships.AddRange(jobs.Relationships);

        _tableOwnerModuleIds = TableOwnershipEnricher.EnrichTableOwnership(
            _currentL4Result, ctx, referenceRelationships, existingRefRels);

        var semanticNodesToUpload = new List<Node>(
            cqrs.Messages.Count + caches.Caches.Count + txs.Transactions.Count + policies.Policies.Count + jobs.Jobs.Count);
        for (var i = 0; i < cqrs.Messages.Count; i++) semanticNodesToUpload.Add(Node.FromNode(cqrs.Messages[i]));
        for (var i = 0; i < caches.Caches.Count; i++) semanticNodesToUpload.Add(Node.FromNode(caches.Caches[i]));
        for (var i = 0; i < txs.Transactions.Count; i++) semanticNodesToUpload.Add(Node.FromNode(txs.Transactions[i]));
        for (var i = 0; i < policies.Policies.Count; i++) semanticNodesToUpload.Add(Node.FromNode(policies.Policies[i]));
        for (var i = 0; i < jobs.Jobs.Count; i++) semanticNodesToUpload.Add(Node.FromNode(jobs.Jobs[i]));

        if (semanticNodesToUpload.Count > 0)
        {
            await ctx.EnqueueUploadNodesAsync(semanticNodesToUpload);
            ctx.AddNodesCount(semanticNodesToUpload.Count);
        }
    }
}
