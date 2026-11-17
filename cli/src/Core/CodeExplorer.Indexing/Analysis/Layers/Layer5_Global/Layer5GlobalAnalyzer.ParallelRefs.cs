using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;

namespace CodeExplorer.Core.Parser.Layers;

public partial class Layer5AnalysisParser
{
    private const string Layer5GlobalSource = "Layer5_Global";
    private const string ReferenceErrorCode = "reference_error";
    private const string ReferenceErrorName = "ReferenceResolutionError";
    private const string ParamTargetPrefix = "$param:";

    private void ResolveGlobalReferencesBatch(
        ParsingContext ctx,
        SymbolLookups symbols,
        BindingLookups bindings,
        ImportLookups imports,
        Dictionary<string, List<string>> interfaceToImplementors,
        List<Relationship> referenceRelationships,
        List<TopicNode> newTopicNodes,
        HashSet<string> createdTopicIds,
        HashSet<(string From, string To, string Kind)> existingRefRels,
        List<Reference> deferredParamMessagingRefs)
    {
        var globalRefs = ctx.GlobalReferences;
        var total = globalRefs.Count;
        if (total == 0) return;

        var workerCount = Math.Max(1, Math.Min(Environment.ProcessorCount, (total + 4095) / 4096));
        var chunkSize = (total + workerCount - 1) / workerCount;
        var chunkRels = new List<Relationship>[workerCount];
        var chunkStatefulRefs = new List<Reference>[workerCount];

        Parallel.For(0, workerCount, new ParallelOptions { CancellationToken = ctx.CancellationToken, MaxDegreeOfParallelism = workerCount }, chunkIdx =>
        {
            var start = chunkIdx * chunkSize;
            var end = Math.Min(total, start + chunkSize);
            if (start >= end) return;

            var localRels = new List<Relationship>((end - start) / 3 + 16);
            var localStateful = new List<Reference>();

            for (var i = start; i < end; i++)
            {
                ctx.CancellationToken.ThrowIfCancellationRequested();
                var refItem = globalRefs[i];
                if (refItem.Kind is RelationshipKind.Implements or RelationshipKind.InheritsFrom)
                {
                    continue;
                }

                if (refItem.Kind is RelationshipKind.Triggers or RelationshipKind.PublishesTo or RelationshipKind.SubscribesTo or RelationshipKind.PersistedIn)
                {
                    localStateful.Add(refItem);
                    continue;
                }

                ResolveStatelessReference(refItem, ctx, symbols, bindings, imports, interfaceToImplementors, localRels);
            }

            chunkRels[chunkIdx] = localRels;
            chunkStatefulRefs[chunkIdx] = localStateful;
        });

        for (var c = 0; c < workerCount; c++)
        {
            if (chunkRels[c] is { Count: > 0 } rels)
            {
                referenceRelationships.AddRange(rels);
            }
        }

        for (var c = 0; c < workerCount; c++)
        {
            if (chunkStatefulRefs[c] is not { Count: > 0 } stateful) continue;
            foreach (var refItem in stateful)
            {
                ResolveStatefulReference(refItem, ctx, symbols, bindings, newTopicNodes, createdTopicIds, existingRefRels, deferredParamMessagingRefs, referenceRelationships);
            }
        }
    }

    private void ResolveStatelessReference(
        Reference refItem,
        ParsingContext ctx,
        SymbolLookups symbols,
        BindingLookups bindings,
        ImportLookups imports,
        Dictionary<string, List<string>> interfaceToImplementors,
        List<Relationship> localRels)
    {
        try
        {
            switch (refItem.Kind)
            {
                case RelationshipKind.Calls:
                    ResolveCallsReference(refItem, ctx, symbols, bindings, imports, interfaceToImplementors, localRels);
                    break;
                case RelationshipKind.DependsOn:
                    Layer5SymbolVisibilityResolver.ResolveDependsOnReference(refItem, ctx, symbols, localRels);
                    break;
                case RelationshipKind.Instantiates:
                    ResolveInstantiatesReference(refItem, ctx, symbols, imports, localRels);
                    break;
                case RelationshipKind.UsesType:
                    Layer5SymbolVisibilityResolver.ResolveUsesTypeReference(refItem, ctx, symbols, localRels);
                    break;
                case RelationshipKind.AccessedBy:
                case RelationshipKind.ImplementedBy:
                    Layer5SymbolVisibilityResolver.ResolveAccessedByOrImplementedByReference(refItem, symbols, localRels);
                    break;
                case RelationshipKind.ReadsFrom:
                case RelationshipKind.WritesTo:
                    Layer5SymbolVisibilityResolver.ResolveReadsOrWritesReference(refItem, symbols, localRels);
                    break;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ctx.RecordScanIssue(Layer5GlobalSource, ReferenceErrorCode, refItem.ScopeSymbolId, $"Failed to resolve reference '{refItem.TargetName}': {ex.Message}", name: ReferenceErrorName, exception: ex, isFileParent: false);
        }
    }

    private void ResolveStatefulReference(
        Reference refItem,
        ParsingContext ctx,
        SymbolLookups symbols,
        BindingLookups bindings,
        List<TopicNode> newTopicNodes,
        HashSet<string> createdTopicIds,
        HashSet<(string From, string To, string Kind)> existingRefRels,
        List<Reference> deferredParamMessagingRefs,
        List<Relationship> referenceRelationships)
    {
        try
        {
            switch (refItem.Kind)
            {
                case RelationshipKind.Triggers:
                    ResolveTriggersReference(refItem, symbols, referenceRelationships, existingRefRels);
                    break;
                case RelationshipKind.PublishesTo:
                case RelationshipKind.SubscribesTo:
                    if (refItem.TargetName.Contains(ParamTargetPrefix, StringComparison.Ordinal))
                    {
                        deferredParamMessagingRefs.Add(refItem);
                    }
                    else
                    {
                        ResolveMessagingReference(refItem, ctx, symbols, bindings, newTopicNodes, createdTopicIds, referenceRelationships);
                    }
                    break;
                case RelationshipKind.PersistedIn:
                    Layer5SymbolVisibilityResolver.ResolvePersistedInReference(refItem, ctx, symbols, referenceRelationships, existingRefRels);
                    break;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ctx.RecordScanIssue(Layer5GlobalSource, ReferenceErrorCode, refItem.ScopeSymbolId, $"Failed to resolve reference '{refItem.TargetName}': {ex.Message}", name: ReferenceErrorName, exception: ex, isFileParent: false);
        }
    }
}
