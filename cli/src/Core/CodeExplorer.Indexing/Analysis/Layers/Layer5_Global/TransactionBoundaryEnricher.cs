using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Common.Relationships;

namespace CodeExplorer.Core.Parser.Layers;

internal static class TransactionBoundaryEnricher
{
    private const string TransactionalToken = "Transactional";
    private const string TransactionAttrToken = "Transaction";
    private const string ReadOnlyToken = "readOnly";
    private const string ReadOnlyPascalToken = "ReadOnly";
    private const string TransactionScopeType = "TransactionScope";
    private const string PipelineBehaviorToken = "PipelineBehavior";
    private const string UnitOfWorkBehaviorToken = "UnitOfWorkBehavior";
    private const string TransactionBehaviorToken = "TransactionBehavior";
    private const string PrismaTransactionMethod = "$transaction";
    private const string DbFacadeTransaction = "DB::transaction";
    private const string FrameworkSpringTx = "spring-tx";
    private const string FrameworkDotNetScope = "transactionscope";
    private const string FrameworkPipeline = "mediator-pipeline";
    private const string FrameworkExplicit = "db-transaction";
    private const int MaxPropagationHops = 3;

    private static readonly HashSet<string> ExplicitTxMethodNames = new(StringComparer.Ordinal)
    {
        "BeginTransaction", "BeginTransactionAsync", "ExecuteInTransaction", "ExecuteInTransactionAsync",
        "startTransaction", "$transaction", "BeginTx", "beginTransaction", "wrapInTransaction", "atomic"
    };

    private static readonly HashSet<string>ContextualTxMethodNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "transaction", "begin", "execute"
    };

    private static readonly HashSet<string> ContextualTxReceivers = new(StringComparer.OrdinalIgnoreCase)
    {
        "db", "_db", "database", "dataSource", "_dataSource", "entityManager", "manager",
        "queryRunner", "prisma", "_prisma", "sequelize", "knex", "pool", "_pool",
        "session", "_session", "conn", "connection", "transactionTemplate", "DB"
    };

    internal sealed record TransactionEnrichmentOutput(List<TransactionNode> Transactions, List<Relationship> Relationships);

    public static TransactionEnrichmentOutput EnrichTransactions(
        Layer4Result? l4Result,
        Layer5Context ctx,
        Dictionary<string, List<Reference>> refsByScopeId,
        List<Relationship> referenceRelationships,
        HashSet<(string From, string To, string Kind)> existingRefRels)
    {
        var txById = new Dictionary<string, TransactionNode>(StringComparer.Ordinal);
        var rels = new List<Relationship>();
        if (l4Result == null) return new TransactionEnrichmentOutput([], rels);

        var syntaxStruct = ctx.SyntaxStructure;
        if (syntaxStruct == null) return new TransactionEnrichmentOutput([], rels);

        var filesByPath = SemanticEnrichmentHelpers.CollectFilesByPath(ctx.FilesStructure);
        var modules = SemanticEnrichmentHelpers.CollectModules(l4Result.SemanticStructure);
        var index = SemanticEnrichmentHelpers.CollectTypesAndMethods(syntaxStruct);

        var rootTxByFn = new Dictionary<string, List<TransactionNode>>(StringComparer.Ordinal);
        var pipelineTxNodes = new List<(TransactionNode Tx, string? ProjectName)>();

        foreach (var (typeId, tn) in index.TypesById)
        {
            if (tn.IsTest || !index.MethodsByTypeId.TryGetValue(typeId, out var methods)) continue;

            var classDeclarative = HasDeclarativeTxAttribute(tn.Attributes, out var classReadOnly);
            var isPipelineType = IsTransactionPipelineBehavior(tn);

            for (var i = 0; i < methods.Count; i++)
            {
                var fn = methods[i];
                if (fn.IsTest) continue;
                var scopeRefs = SemanticEnrichmentHelpers.GetScopeReferences(fn.Id, fn.References, refsByScopeId);

                if (isPipelineType && (HasExplicitOrAmbientTx(scopeRefs) || tn.Name.Contains(TransactionBehaviorToken, StringComparison.OrdinalIgnoreCase) || tn.Name.Contains(UnitOfWorkBehaviorToken, StringComparison.OrdinalIgnoreCase)))
                {
                    var pTx = GetOrCreateTx(ctx, fn, tn.Name, TransactionKind.PipelineBehavior, false, FrameworkPipeline, filesByPath, modules, txById, rels, existingRefRels);
                    AttachRootTx(fn.Id, pTx, rootTxByFn, rels, existingRefRels);
                    pipelineTxNodes.Add((pTx, tn.ProjectName ?? fn.ProjectName));
                    continue;
                }

                if (HasDeclarativeTxAttribute(fn.Attributes, out var fnReadOnly) || classDeclarative)
                {
                    var ro = HasDeclarativeTxAttribute(fn.Attributes, out _) ? fnReadOnly : classReadOnly;
                    var dTx = GetOrCreateTx(ctx, fn, $"{tn.Name}.{fn.Name}", TransactionKind.Declarative, ro, FrameworkSpringTx, filesByPath, modules, txById, rels, existingRefRels);
                    AttachRootTx(fn.Id, dTx, rootTxByFn, rels, existingRefRels);
                }
                else if (HasAmbientTxScope(scopeRefs))
                {
                    var aTx = GetOrCreateTx(ctx, fn, $"{tn.Name}.{fn.Name}", TransactionKind.Ambient, false, FrameworkDotNetScope, filesByPath, modules, txById, rels, existingRefRels);
                    AttachRootTx(fn.Id, aTx, rootTxByFn, rels, existingRefRels);
                }
                else if (HasExplicitTxCall(scopeRefs))
                {
                    var eTx = GetOrCreateTx(ctx, fn, $"{tn.Name}.{fn.Name}", TransactionKind.Explicit, false, FrameworkExplicit, filesByPath, modules, txById, rels, existingRefRels);
                    AttachRootTx(fn.Id, eTx, rootTxByFn, rels, existingRefRels);
                }
            }
        }

        for (var i = 0; i < index.TopLevelFunctions.Count; i++)
        {
            var fn = index.TopLevelFunctions[i];
            if (fn.IsTest) continue;
            var scopeRefs = SemanticEnrichmentHelpers.GetScopeReferences(fn.Id, fn.References, refsByScopeId);
            if (HasDeclarativeTxAttribute(fn.Attributes, out var ro))
            {
                var dTx = GetOrCreateTx(ctx, fn, fn.Name, TransactionKind.Declarative, ro, FrameworkSpringTx, filesByPath, modules, txById, rels, existingRefRels);
                AttachRootTx(fn.Id, dTx, rootTxByFn, rels, existingRefRels);
            }
            else if (HasAmbientTxScope(scopeRefs))
            {
                var aTx = GetOrCreateTx(ctx, fn, fn.Name, TransactionKind.Ambient, false, FrameworkDotNetScope, filesByPath, modules, txById, rels, existingRefRels);
                AttachRootTx(fn.Id, aTx, rootTxByFn, rels, existingRefRels);
            }
            else if (HasExplicitTxCall(scopeRefs))
            {
                var eTx = GetOrCreateTx(ctx, fn, fn.Name, TransactionKind.Explicit, false, FrameworkExplicit, filesByPath, modules, txById, rels, existingRefRels);
                AttachRootTx(fn.Id, eTx, rootTxByFn, rels, existingRefRels);
            }
        }

        if (pipelineTxNodes.Count > 0)
        {
            BindPipelineTransactionsToCommandHandlers(pipelineTxNodes, index.FunctionsById, referenceRelationships, rootTxByFn, rels, existingRefRels);
        }

        if (rootTxByFn.Count > 0)
        {
            PropagateDownstreamExecutesInTx(rootTxByFn, index.FunctionsById, referenceRelationships, rels, existingRefRels);
        }

        return new TransactionEnrichmentOutput(txById.Values.ToList(), rels);
    }

    private static bool IsTransactionPipelineBehavior(TypeNode tn)
    {
        var contracts = $"{tn.Implements} {tn.Extends}";
        return contracts.Contains(PipelineBehaviorToken, StringComparison.Ordinal) &&
               (tn.Name.Contains(TransactionAttrToken, StringComparison.OrdinalIgnoreCase) ||
                tn.Name.Contains(UnitOfWorkBehaviorToken, StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasDeclarativeTxAttribute(string? attrs, out bool isReadOnly)
    {
        isReadOnly = false;
        if (string.IsNullOrEmpty(attrs)) return false;
        if (!attrs.Contains(TransactionalToken, StringComparison.Ordinal) &&
            !string.Equals(attrs.Trim(), TransactionAttrToken, StringComparison.Ordinal))
        {
            return false;
        }
        isReadOnly = attrs.Contains(ReadOnlyToken, StringComparison.OrdinalIgnoreCase) ||
                     attrs.Contains(ReadOnlyPascalToken, StringComparison.Ordinal);
        return true;
    }

    private static bool HasExplicitOrAmbientTx(IReadOnlyList<Reference> refs) =>
        HasAmbientTxScope(refs) || HasExplicitTxCall(refs);

    private static bool HasAmbientTxScope(IReadOnlyList<Reference> refs)
    {
        for (var i = 0; i < refs.Count; i++)
        {
            var r = refs[i];
            if (r.Kind is RelationshipKind.Instantiates or RelationshipKind.UsesType &&
                r.TargetName.EndsWith(TransactionScopeType, StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }

    private static bool HasExplicitTxCall(IReadOnlyList<Reference> refs)
    {
        for (var i = 0; i < refs.Count; i++)
        {
            var r = refs[i];
            if (r.Kind != RelationshipKind.Calls) continue;
            var target = r.TargetName;
            if (string.IsNullOrEmpty(target)) continue;

            if (target.Contains(PrismaTransactionMethod, StringComparison.Ordinal) ||
                target.StartsWith(DbFacadeTransaction, StringComparison.Ordinal))
            {
                return true;
            }

            var tail = SemanticEnrichmentHelpers.ExtractMethodTail(r.MemberName);
            if (ExplicitTxMethodNames.Contains(tail)) return true;

            if (ContextualTxMethodNames.Contains(tail) &&
                SemanticEnrichmentHelpers.TryGetReceiverAndMethod(r, out var recv, out _) &&
                ContextualTxReceivers.Contains(recv))
            {
                return true;
            }
        }
        return false;
    }

    private static void BindPipelineTransactionsToCommandHandlers(
        List<(TransactionNode Tx, string? ProjectName)> pipelineTxNodes,
        Dictionary<string, FunctionNode> functionsById,
        List<Relationship> referenceRelationships,
        Dictionary<string, List<TransactionNode>> rootTxByFn,
        List<Relationship> rels,
        HashSet<(string From, string To, string Kind)> existingRefRels)
    {
        for (var i = 0; i < referenceRelationships.Count; i++)
        {
            var r = referenceRelationships[i];
            if (r.Kind != RelationshipKind.HandlesCommand) continue;
            if (!functionsById.TryGetValue(r.From, out var cmdFn) || cmdFn.IsTest) continue;

            var chosen = pipelineTxNodes[0].Tx;
            for (var p = 0; p < pipelineTxNodes.Count; p++)
            {
                if (string.Equals(pipelineTxNodes[p].ProjectName, cmdFn.ProjectName, StringComparison.OrdinalIgnoreCase))
                {
                    chosen = pipelineTxNodes[p].Tx;
                    break;
                }
            }
            AttachRootTx(cmdFn.Id, chosen, rootTxByFn, rels, existingRefRels);
        }
    }

    private static void PropagateDownstreamExecutesInTx(
        Dictionary<string, List<TransactionNode>> rootTxByFn,
        Dictionary<string, FunctionNode> functionsById,
        List<Relationship> referenceRelationships,
        List<Relationship> rels,
        HashSet<(string From, string To, string Kind)> existingRefRels)
    {
        var calleesByCaller = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        for (var i = 0; i < referenceRelationships.Count; i++)
        {
            var r = referenceRelationships[i];
            if (r.Kind != RelationshipKind.Calls) continue;
            if (!functionsById.ContainsKey(r.From) || !functionsById.ContainsKey(r.To)) continue;
            if (!calleesByCaller.TryGetValue(r.From, out var list))
            {
                list = [];
                calleesByCaller[r.From] = list;
            }
            list.Add(r.To);
        }

        var queue = new Queue<(string FnId, TransactionNode Tx, int Depth)>();
        var visited = new HashSet<(string FnId, string TxId)>();

        foreach (var (fnId, txList) in rootTxByFn)
        {
            for (var i = 0; i < txList.Count; i++)
            {
                var tx = txList[i];
                if (visited.Add((fnId, tx.Id)))
                {
                    queue.Enqueue((fnId, tx, 0));
                }
            }
        }

        while (queue.Count > 0)
        {
            var (currFn, tx, depth) = queue.Dequeue();
            if (depth >= MaxPropagationHops) continue;
            if (!calleesByCaller.TryGetValue(currFn, out var nextFns)) continue;

            for (var i = 0; i < nextFns.Count; i++)
            {
                var nextId = nextFns[i];
                if (!functionsById.TryGetValue(nextId, out var nextNode) || nextNode.IsTest) continue;
                if (!visited.Add((nextId, tx.Id))) continue;

                SemanticEnrichmentHelpers.AddUniqueRel(new ExecutesInTxRelationship(nextId, tx.Id), rels, existingRefRels);
                queue.Enqueue((nextId, tx, depth + 1));
            }
        }
    }

    private static void AttachRootTx(
        string fnId,
        TransactionNode tx,
        Dictionary<string, List<TransactionNode>> rootTxByFn,
        List<Relationship> rels,
        HashSet<(string From, string To, string Kind)> existingRefRels)
    {
        SemanticEnrichmentHelpers.AddUniqueRel(new WrapsRelationship(tx.Id, fnId), rels, existingRefRels);
        SemanticEnrichmentHelpers.AddUniqueRel(new ExecutesInTxRelationship(fnId, tx.Id), rels, existingRefRels);
        if (!rootTxByFn.TryGetValue(fnId, out var list))
        {
            list = [];
            rootTxByFn[fnId] = list;
        }
        if (!list.Contains(tx)) list.Add(tx);
    }

    private static TransactionNode GetOrCreateTx(
        Layer5Context ctx,
        FunctionNode fn,
        string scopeName,
        TransactionKind subKind,
        bool isReadOnly,
        string framework,
        Dictionary<string, FileNode> filesByPath,
        List<ModuleNode> modules,
        Dictionary<string, TransactionNode> txById,
        List<Relationship> rels,
        HashSet<(string From, string To, string Kind)> existingRefRels)
    {
        var kindKey = subKind.ToCanonicalString();
        var fileNorm = fn.FilePath.Replace('\\', '/');
        var txId = $"{ctx.WorkspaceId}:{OntologyConstants.IdPrefixes.Transaction}:{kindKey}:{fileNorm}:{scopeName}";

        if (!txById.TryGetValue(txId, out var node))
        {
            node = new TransactionNode(txId, scopeName, fileNorm, subKind)
            {
                IsReadOnly = isReadOnly,
                Framework = framework,
                FilePath = fileNorm,
                StartLine = fn.StartLine
            };
            txById[txId] = node;

            if (filesByPath.TryGetValue(fileNorm, out var fNode))
            {
                SemanticEnrichmentHelpers.AddUniqueRel(new DeclaredInRelationship(txId, fNode.Id), rels, existingRefRels);
            }
            var ownerMod = SemanticEnrichmentHelpers.FindOwningModule(fileNorm, modules);
            if (ownerMod != null)
            {
                SemanticEnrichmentHelpers.AddUniqueRel(new ContainsRelationship(ownerMod.Id, txId), rels, existingRefRels);
            }
        }
        return node;
    }
}
