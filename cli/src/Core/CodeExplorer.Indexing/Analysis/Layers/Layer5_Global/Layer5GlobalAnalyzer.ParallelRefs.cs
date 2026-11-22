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
                case RelationshipKind.References:
                    Layer5SymbolVisibilityResolver.ResolveForeignKeyReference(refItem, symbols, localRels);
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

    private static ImportLookups BuildImportLookups(ParsingContext ctx, Layer4Result? l4Result)
    {
        const string wildcardImportSymbol = "*";
        var fileImports = new Dictionary<(string FilePath, string LocalName), ImportedSymbolEntry>();
        var starImportsByFile = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var imp in ctx.RawImports)
        {
            if (string.IsNullOrEmpty(imp.FilePath) || string.IsNullOrEmpty(imp.Path)) continue;

            var normFile = imp.FilePath.Replace('\\', '/');

            if (imp.Specifiers != null && imp.Specifiers.Count > 0)
            {
                foreach (var spec in imp.Specifiers)
                {
                    if (spec.IsNamespace || spec.LocalName == wildcardImportSymbol)
                    {
                        if (!starImportsByFile.TryGetValue(normFile, out var starList))
                        {
                            starList = [];
                            starImportsByFile[normFile] = starList;
                        }
                        starList.Add(imp.Path);

                        if (!string.IsNullOrEmpty(spec.LocalName) && spec.LocalName != wildcardImportSymbol)
                        {
                            fileImports[(normFile, spec.LocalName)] = new ImportedSymbolEntry(
                                imp.Path,
                                wildcardImportSymbol,
                                IsDefault: false,
                                IsNamespace: true
                            );
                        }
                    }
                    else
                    {
                        fileImports[(normFile, spec.LocalName)] = new ImportedSymbolEntry(
                            imp.Path,
                            spec.ImportedName ?? spec.LocalName,
                            spec.IsDefault,
                            IsNamespace: false
                        );
                    }
                }
            }
        }

        var workspacePackages = new Dictionary<string, PackageTargetInfo>(StringComparer.OrdinalIgnoreCase);
        var inMemoryProjects = l4Result?.Prev.Prev.Projects ?? ctx.ProjectsStructure?.Children.OfType<CodeExplorer.Core.Common.Nodes.Layer2_Boundaries.ProjectNode>().ToList();
        if (inMemoryProjects != null && inMemoryProjects.Count > 0)
        {
            foreach (var proj in inMemoryProjects)
            {
                var pkgName = proj.PackageName;
                var mainFile = proj.MainFile ?? proj.ModuleFile;
                var normProjPath = proj.Path.Replace('\\', '/');
                if (!string.IsNullOrEmpty(pkgName))
                {
                    workspacePackages[pkgName] = new PackageTargetInfo(normProjPath, mainFile);
                    if (pkgName.StartsWith('@') && pkgName.Contains('/'))
                    {
                        var unqualified = pkgName[(pkgName.IndexOf('/') + 1)..];
                        workspacePackages.TryAdd(unqualified, new PackageTargetInfo(normProjPath, mainFile));
                    }
                }
                if (!string.IsNullOrEmpty(proj.Name))
                {
                    workspacePackages.TryAdd(proj.Name, new PackageTargetInfo(normProjPath, mainFile));
                }
            }
        }

        var tsConfigPaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (k, v) in ctx.TsConfigPaths) tsConfigPaths[k] = v;

        return new ImportLookups(fileImports, starImportsByFile, workspacePackages, tsConfigPaths);
    }

    private static int ScoreScopeAffinity(in SymbolScopeInfo a, in SymbolScopeInfo b)
    {
        var score = 0;
        if (!string.IsNullOrEmpty(a.Namespace) && !string.IsNullOrEmpty(b.Namespace))
        {
            var minLen = Math.Min(a.Namespace.Length, b.Namespace.Length);
            var common = 0;
            while (common < minLen && a.Namespace[common] == b.Namespace[common]) common++;
            score += common * 2;
        }
        if (!string.IsNullOrEmpty(a.FilePath) && !string.IsNullOrEmpty(b.FilePath))
        {
            var minPath = Math.Min(a.FilePath.Length, b.FilePath.Length);
            var commonPath = 0;
            while (commonPath < minPath && char.ToLowerInvariant(a.FilePath[commonPath]) == char.ToLowerInvariant(b.FilePath[commonPath])) commonPath++;
            score += commonPath;
        }
        return score;
    }

    private static readonly HashSet<string> MediatorDispatchMethodNames = new(StringComparer.Ordinal)
    {
        "Send", "SendAsync", "Publish", "PublishAsync",
        "ExecuteCommandAsync", "ExecuteQueryAsync",
        "EnqueueAsync", "Dispatch", "DispatchAsync", "Execute", "ExecuteAsync",
        "execute", "send", "publish", "dispatch", "ask", "query"
    };

    private static readonly HashSet<string> AmbiguousExecutionMethodNames = new(StringComparer.Ordinal)
    {
        "Execute", "ExecuteAsync", "execute", "query"
    };

    private static readonly HashSet<string> NonMediatorReceiverNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "db", "_db", "conn", "_conn", "connection", "_connection", "sqlConnection", "_sqlConnection",
        "cmd", "_cmd", "command", "_command", "jdbcTemplate", "namedParameterJdbcTemplate",
        "entityManager", "session", "_session", "prisma", "knex", "pdo"
    };

    private static readonly string[] NonMediatorTypeTokens =
    [
        "DbConnection", "SqlConnection", "NpgsqlConnection", "SqliteConnection", "MySqlConnection",
        "DbCommand", "SqlCommand", "JdbcTemplate", "EntityManager", "DataSource"
    ];

    private static bool IsNonMediatorExecutionReceiver(string? receiverName, string? normCallerFile, BindingLookups bindings)
    {
        if (string.IsNullOrEmpty(receiverName)) return false;
        var shortRecv = receiverName.Contains('.') ? receiverName[(receiverName.LastIndexOf('.') + 1)..] : receiverName;
        if (NonMediatorReceiverNames.Contains(shortRecv)) return true;
        if (normCallerFile != null && bindings.BindingsLookup.TryGetValue((normCallerFile, shortRecv), out var bList))
        {
            for (var i = 0; i < bList.Count; i++)
            {
                var tName = bList[i].TypeName;
                if (string.IsNullOrEmpty(tName)) continue;
                for (var k = 0; k < NonMediatorTypeTokens.Length; k++)
                {
                    if (tName.Contains(NonMediatorTypeTokens[k], StringComparison.OrdinalIgnoreCase)) return true;
                }
            }
        }
        return false;
    }

    private static void TryResolveMediatorMessageDispatch(
        Reference refItem,
        string targetName,
        string? methodName,
        string? normCallerFile,
        SymbolScopeInfo callerScope,
        ParsingContext ctx,
        SymbolLookups symbols,
        BindingLookups bindings,
        List<Relationship> referenceRelationships)
    {
        void DispatchMessageType(string rawMsgType)
        {
            if (string.IsNullOrWhiteSpace(rawMsgType)) return;
            var clean = rawMsgType.Trim();
            if (clean.StartsWith("new ", StringComparison.Ordinal)) clean = clean[4..].Trim();
            var lt = clean.IndexOf('<');
            if (lt > 0)
            {
                var gt = clean.LastIndexOf('>');
                if (gt > lt + 1 && clean.StartsWith("IdentifiedCommand<", StringComparison.Ordinal))
                {
                    var inner = clean[(lt + 1)..gt];
                    var comma = inner.IndexOf(',');
                    clean = (comma > 0 ? inner[..comma] : inner).Trim();
                }
                else
                {
                    clean = clean[..lt].Trim();
                }
            }
            if (clean.Contains('.')) clean = clean[(clean.LastIndexOf('.') + 1)..];
            if (!bindings.MessageHandlersByType.TryGetValue(clean, out var handlerIds) || handlerIds.Count == 0)
            {
                return;
            }

            if (handlerIds.Count == 1)
            {
                var onlyHandler = handlerIds[0];
                if (onlyHandler != refItem.ScopeSymbolId &&
                    Layer5SymbolVisibilityResolver.IsImplementorVisibleToCaller(callerScope, onlyHandler, symbols, ctx))
                {
                    referenceRelationships.Add(CreateCallRel(refItem, onlyHandler));
                }
                return;
            }

            var bestScore = -1;
            for (var i = 0; i < handlerIds.Count; i++)
            {
                var hid = handlerIds[i];
                if (hid == refItem.ScopeSymbolId ||
                    !Layer5SymbolVisibilityResolver.IsImplementorVisibleToCaller(callerScope, hid, symbols, ctx))
                {
                    continue;
                }
                var sc = ScoreScopeAffinity(callerScope, Layer5SymbolVisibilityResolver.GetOrInferSymbolScope(hid, null, ctx));
                if (sc > bestScore) bestScore = sc;
            }
            for (var i = 0; i < handlerIds.Count; i++)
            {
                var hid = handlerIds[i];
                if (hid == refItem.ScopeSymbolId ||
                    !Layer5SymbolVisibilityResolver.IsImplementorVisibleToCaller(callerScope, hid, symbols, ctx))
                {
                    continue;
                }
                var hScope = Layer5SymbolVisibilityResolver.GetOrInferSymbolScope(hid, null, ctx);
                if (bestScore <= 0 || ScoreScopeAffinity(callerScope, hScope) == bestScore)
                {
                    referenceRelationships.Add(CreateCallRel(refItem, hid));
                }
            }
        }

        if (refItem.TargetName.StartsWith("new ", StringComparison.Ordinal))
        {
            DispatchMessageType(refItem.TargetName);
            return;
        }

        if (bindings.MessageHandlersByType.ContainsKey(targetName))
        {
            DispatchMessageType(targetName);
            return;
        }

        var checkMethod = methodName ?? targetName;
        if (checkMethod != null && MediatorDispatchMethodNames.Contains(checkMethod) && normCallerFile != null)
        {
            if (AmbiguousExecutionMethodNames.Contains(checkMethod) &&
                IsNonMediatorExecutionReceiver(refItem.ReceiverName, normCallerFile, bindings))
            {
                return;
            }
            var callerFnName = ExtractSymbolNameFromId(refItem.ScopeSymbolId);
            foreach (var ((fp, _), bList) in bindings.BindingsLookup)
            {
                if (!string.Equals(fp, normCallerFile, StringComparison.OrdinalIgnoreCase)) continue;
                for (var i = 0; i < bList.Count; i++)
                {
                    var ib = bList[i];
                    if (!string.IsNullOrEmpty(callerFnName) &&
                        !string.IsNullOrEmpty(ib.ScopeId) &&
                        !string.Equals(ib.ScopeId, callerFnName, StringComparison.Ordinal))
                    {
                        continue;
                    }
                    DispatchMessageType(ib.TypeName);
                }
            }
        }
    }

    private static void ResolveDeferredParamMessagingReferences(
        List<Reference> deferredRefs,
        ParsingContext ctx,
        SymbolLookups symbols,
        BindingLookups bindings,
        List<TopicNode> newTopicNodes,
        HashSet<string> createdTopicIds,
        List<Relationship> referenceRelationships)
    {
        var callsByTarget = new Dictionary<string, List<Relationship>>(StringComparer.Ordinal);
        foreach (var rel in referenceRelationships)
        {
            if (rel.Kind == RelationshipKind.Calls && !string.IsNullOrEmpty(rel.ArgFlow))
            {
                if (!callsByTarget.TryGetValue(rel.To, out var list))
                {
                    list = [];
                    callsByTarget[rel.To] = list;
                }
                list.Add(rel);
            }
        }

        if (callsByTarget.Count == 0) return;

        foreach (var defRef in deferredRefs)
        {
            var paramIdxPos = defRef.TargetName.IndexOf("$param:", StringComparison.Ordinal);
            if (paramIdxPos < 0) continue;

            var prefix = defRef.TargetName[..paramIdxPos];
            var paramStr = defRef.TargetName[(paramIdxPos + 7)..];
            if (!int.TryParse(paramStr, out var paramIdx)) continue;

            if (!callsByTarget.TryGetValue(defRef.ScopeSymbolId, out var incomingCalls)) continue;

            var matchPrefix = $"{paramIdx}<-";
            foreach (var callRel in incomingCalls)
            {
                var argFlow = callRel.ArgFlow!;
                foreach (var segment in argFlow.Split(';', StringSplitOptions.RemoveEmptyEntries))
                {
                    if (!segment.StartsWith(matchPrefix, StringComparison.Ordinal)) continue;
                    var originsPart = segment[matchPrefix.Length..];
                    foreach (var rawOrigin in originsPart.Split(',', StringSplitOptions.RemoveEmptyEntries))
                    {
                        var origin = rawOrigin.Trim();
                        if (string.IsNullOrEmpty(origin) ||
                            (origin.Length >= 2 && origin[0] == 'p' && char.IsDigit(origin[1])))
                        {
                            continue;
                        }

                        var concreteRef = defRef with
                        {
                            ScopeSymbolId = callRel.From,
                            TargetName = $"{prefix}{origin}",
                            Line = callRel.Line > 0 ? callRel.Line : defRef.Line,
                            Column = callRel.Column > 0 ? callRel.Column : defRef.Column
                        };
                        ResolveMessagingReference(concreteRef, ctx, symbols, bindings, newTopicNodes, createdTopicIds, referenceRelationships);
                    }
                }
            }
        }
    }
}
