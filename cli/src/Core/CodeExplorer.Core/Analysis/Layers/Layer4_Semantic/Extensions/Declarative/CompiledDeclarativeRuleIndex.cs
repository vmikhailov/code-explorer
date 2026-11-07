using System.Collections.Concurrent;
using System.Collections.Frozen;
using CodeExplorer.Core.Parser.Syntax;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Core.Parser;

internal sealed class CompiledDeclarativeRuleIndex
{
    private const string FusedIdPrefix = "fused:";
    private const string FusedNameLabel = "Fused Declarative Extensions";
    private const char KeySeparator = '|';

    private static readonly ConcurrentDictionary<string, DeclarativeSemanticExtension> FusedCache = new(StringComparer.Ordinal);

    internal required FrozenSet<string> AllTrackedCallMethods { get; init; }
    internal required FrozenDictionary<string, InboundRouteCallRule[]> InboundRoutesByMethod { get; init; }
    internal required FrozenDictionary<string, DataStoreAccessRule[]> DataStoresByMethod { get; init; }
    internal required FrozenDictionary<string, ServiceCallSinkRule[]> ServiceSinksByMethod { get; init; }
    internal required FrozenDictionary<string, MessagingBrokerRule[]> PublishByMethod { get; init; }
    internal required FrozenDictionary<string, MessagingBrokerRule[]> SubscribeByMethod { get; init; }
    internal required FrozenDictionary<string, SqlSinkRule[]> SqlCallSinksByMethod { get; init; }
    internal required SqlSinkRule[] AnnotationSqlSinkRules { get; init; }
    internal required OrmTableRule[] OrmTableRules { get; init; }
    internal required MessagingBrokerRule[] ConsumerInterfaceRules { get; init; }
    internal required MessagingBrokerRule[] AnnotationMessagingRules { get; init; }
    internal required AnnotationSinkRule[] AnnotationSinkRules { get; init; }
    internal required ClassHierarchySinkRule[] ClassHierarchySinkRules { get; init; }
    internal required FrozenDictionary<string, CustomAstRule[]> CustomRulesByNodeType { get; init; }
    internal required CustomAstRule[] UnfilteredCustomRules { get; init; }

    internal static DeclarativeSemanticExtension? GetOrCreateFused(IReadOnlyList<DeclarativeSemanticExtension> extensions)
    {
        if (extensions.Count == 0) return null;
        if (extensions.Count == 1) return extensions[0];

        var key = BuildCacheKey(extensions);
        return FusedCache.GetOrAdd(key, _ => CreateFusedExtension(extensions, key));
    }

    private static string BuildCacheKey(IReadOnlyList<DeclarativeSemanticExtension> extensions)
    {
        var ids = new string[extensions.Count];
        for (var i = 0; i < extensions.Count; i++)
        {
            ids[i] = extensions[i].Id;
        }
        return string.Join(KeySeparator, ids);
    }

    private static DeclarativeSemanticExtension CreateFusedExtension(IReadOnlyList<DeclarativeSemanticExtension> extensions, string key)
    {
        var annotEndpoints = new List<AnnotationEndpointRule>();
        var outboundHttp = new List<OutboundHttpRule>();
        var sqlSinks = new List<SqlSinkRule>();
        var ormTables = new List<OrmTableRule>();
        var messaging = new List<MessagingBrokerRule>();
        var inboundRoutes = new List<InboundRouteCallRule>();
        var dataStores = new List<DataStoreAccessRule>();
        var serviceSinks = new List<ServiceCallSinkRule>();
        var annotSinks = new List<AnnotationSinkRule>();
        var classHierarchySinks = new List<ClassHierarchySinkRule>();
        var customAstRules = new List<CustomAstRule>();

        for (var i = 0; i < extensions.Count; i++)
        {
            var ext = extensions[i];
            annotEndpoints.AddRange(ext.AnnotationEndpointRules);
            outboundHttp.AddRange(ext.OutboundHttpRules);
            sqlSinks.AddRange(ext.SqlSinkRules);
            ormTables.AddRange(ext.OrmTableRules);
            messaging.AddRange(ext.MessagingBrokerRules);
            inboundRoutes.AddRange(ext.InboundRouteCallRules);
            dataStores.AddRange(ext.DataStoreAccessRules);
            serviceSinks.AddRange(ext.ServiceCallSinkRules);
            annotSinks.AddRange(ext.AnnotationSinkRules);
            classHierarchySinks.AddRange(ext.ClassHierarchySinkRules);
            customAstRules.AddRange(ext.CustomAstRules);
        }

        return new DeclarativeSemanticExtension
        {
            Id = FusedIdPrefix + key,
            Name = FusedNameLabel,
            AnnotationEndpointRules = annotEndpoints,
            OutboundHttpRules = outboundHttp,
            SqlSinkRules = sqlSinks,
            OrmTableRules = ormTables,
            MessagingBrokerRules = messaging,
            InboundRouteCallRules = inboundRoutes,
            DataStoreAccessRules = dataStores,
            ServiceCallSinkRules = serviceSinks,
            AnnotationSinkRules = annotSinks,
            ClassHierarchySinkRules = classHierarchySinks,
            CustomAstRules = customAstRules
        };
    }

    internal static CompiledDeclarativeRuleIndex Build(DeclarativeSemanticExtension ext)
    {
        var allMethods = new HashSet<string>(StringComparer.Ordinal);
        var inboundMap = new Dictionary<string, List<InboundRouteCallRule>>(StringComparer.Ordinal);
        var dataStoreMap = new Dictionary<string, List<DataStoreAccessRule>>(StringComparer.Ordinal);
        var serviceSinkMap = new Dictionary<string, List<ServiceCallSinkRule>>(StringComparer.Ordinal);
        var publishMap = new Dictionary<string, List<MessagingBrokerRule>>(StringComparer.Ordinal);
        var subscribeMap = new Dictionary<string, List<MessagingBrokerRule>>(StringComparer.Ordinal);
        var sqlCallMap = new Dictionary<string, List<SqlSinkRule>>(StringComparer.Ordinal);
        var customByType = new Dictionary<string, List<CustomAstRule>>(StringComparer.Ordinal);
        var unfilteredCustom = new List<CustomAstRule>();

        for (var i = 0; i < ext.CustomAstRules.Count; i++)
        {
            var cr = ext.CustomAstRules[i];
            if (cr.NodeTypes.Count == 0)
            {
                unfilteredCustom.Add(cr);
            }
            else
            {
                foreach (var nt in cr.NodeTypes)
                {
                    AddRuleUnique(customByType, nt, cr);
                    var canonicalKinds = SyntaxKindResolver.GetCanonicalNodeTypes(nt);
                    for (var k = 0; k < canonicalKinds.Length; k++)
                    {
                        AddRuleUnique(customByType, canonicalKinds[k], cr);
                    }
                }
            }
        }

        for (var i = 0; i < ext.InboundRouteCallRules.Count; i++)
        {
            var r = ext.InboundRouteCallRules[i];
            foreach (var kvp in r.MethodToHttpVerb)
            {
                allMethods.Add(kvp.Key);
                AddRule(inboundMap, kvp.Key, r);
            }
        }

        for (var i = 0; i < ext.DataStoreAccessRules.Count; i++)
        {
            var r = ext.DataStoreAccessRules[i];
            foreach (var m in r.CallMethodNames)
            {
                allMethods.Add(m);
                AddRule(dataStoreMap, m, r);
            }
        }

        for (var i = 0; i < ext.ServiceCallSinkRules.Count; i++)
        {
            var r = ext.ServiceCallSinkRules[i];
            foreach (var m in r.CallMethodNames)
            {
                allMethods.Add(m);
                AddRule(serviceSinkMap, m, r);
            }
        }

        var consumerRules = new List<MessagingBrokerRule>();
        var annotMsgRules = new List<MessagingBrokerRule>();
        for (var i = 0; i < ext.MessagingBrokerRules.Count; i++)
        {
            var r = ext.MessagingBrokerRules[i];
            foreach (var m in r.PublishMethodNames)
            {
                allMethods.Add(m);
                AddRule(publishMap, m, r);
            }
            foreach (var m in r.SubscribeMethodNames)
            {
                allMethods.Add(m);
                AddRule(subscribeMap, m, r);
            }
            if (r.ConsumerInterfaceSubstrings.Count > 0)
            {
                consumerRules.Add(r);
            }
            if (r.ListenerAnnotationNames.Count > 0 || r.ProducerAnnotationNames.Count > 0 ||
                r.ChannelAnnotationNames.Count > 0 || r.MethodLevelEventAnnotations.Count > 0)
            {
                annotMsgRules.Add(r);
            }
        }

        var annotSqlRules = new List<SqlSinkRule>();
        for (var i = 0; i < ext.SqlSinkRules.Count; i++)
        {
            var r = ext.SqlSinkRules[i];
            foreach (var m in r.CallMethodNames)
            {
                allMethods.Add(m);
                AddRule(sqlCallMap, m, r);
            }
            if (r.QueryAnnotationSuffixes.Count > 0)
            {
                annotSqlRules.Add(r);
            }
        }

        return new CompiledDeclarativeRuleIndex
        {
            AllTrackedCallMethods = allMethods.ToFrozenSet(StringComparer.Ordinal),
            InboundRoutesByMethod = ToFrozenArrayMap(inboundMap),
            DataStoresByMethod = ToFrozenArrayMap(dataStoreMap),
            ServiceSinksByMethod = ToFrozenArrayMap(serviceSinkMap),
            PublishByMethod = ToFrozenArrayMap(publishMap),
            SubscribeByMethod = ToFrozenArrayMap(subscribeMap),
            SqlCallSinksByMethod = ToFrozenArrayMap(sqlCallMap),
            AnnotationSqlSinkRules = [.. annotSqlRules],
            OrmTableRules = [.. ext.OrmTableRules],
            ConsumerInterfaceRules = [.. consumerRules],
            AnnotationMessagingRules = [.. annotMsgRules],
            AnnotationSinkRules = [.. ext.AnnotationSinkRules],
            ClassHierarchySinkRules = [.. ext.ClassHierarchySinkRules],
            CustomRulesByNodeType = ToFrozenArrayMap(customByType),
            UnfilteredCustomRules = [.. unfilteredCustom]
        };
    }

    private static void AddRule<TRule>(Dictionary<string, List<TRule>> map, string key, TRule rule)
    {
        if (!map.TryGetValue(key, out var list))
        {
            list = [];
            map[key] = list;
        }
        list.Add(rule);
    }

    private static void AddRuleUnique<TRule>(Dictionary<string, List<TRule>> map, string key, TRule rule)
    {
        if (!map.TryGetValue(key, out var list))
        {
            list = [];
            map[key] = list;
        }
        if (!list.Contains(rule))
        {
            list.Add(rule);
        }
    }

    private static FrozenDictionary<string, TRule[]> ToFrozenArrayMap<TRule>(Dictionary<string, List<TRule>> map) =>
        map.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToArray(), StringComparer.Ordinal)
           .ToFrozenDictionary(StringComparer.Ordinal);

    internal string? EvaluateCustomMapNodeType(Node node, ParsingContext ctx) =>
        DeclarativeExtendedRuleEvaluator.EvaluateCustomMapNodeType(node, ctx, CustomRulesByNodeType, UnfilteredCustomRules);

    internal string? EvaluateCustomExtractIdentifier(Node node, ParsingContext ctx, string? projectName, string? relativePath) =>
        DeclarativeExtendedRuleEvaluator.EvaluateCustomExtractIdentifier(node, ctx, projectName, relativePath, CustomRulesByNodeType, UnfilteredCustomRules);

    internal void EvaluateCustomEnrichSymbol(Node node, SyntacticSymbol symbol, ParsingContext ctx) =>
        DeclarativeExtendedRuleEvaluator.EvaluateCustomEnrichSymbol(node, symbol, ctx, CustomRulesByNodeType, UnfilteredCustomRules);

    internal void EvaluateCollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (CustomRulesByNodeType.Count > 0 || UnfilteredCustomRules.Length > 0)
        {
            DeclarativeExtendedRuleEvaluator.EvaluateCustomCollectReferences(node, scopeSymbolId, references, ctx, CustomRulesByNodeType, UnfilteredCustomRules);
        }

        if (node is CallExprSyntax { CallKind: CallSyntaxKind.Method or CallSyntaxKind.Function or CallSyntaxKind.Constructor } ||
            node.SyntaxKind == SyntaxKind.CallExpression)
        {
            EvaluateCallNodeReferences(node, scopeSymbolId, references);
            return;
        }

        if (node is TypeDeclSyntax { Kind: TypeSyntaxKind.Class } || node.SyntaxKind == SyntaxKind.TypeDeclaration)
        {
            for (var i = 0; i < ConsumerInterfaceRules.Length; i++)
            {
                var rule = ConsumerInterfaceRules[i];
                if (node is TypeDeclSyntax { Kind: TypeSyntaxKind.Class } || rule.ClassDeclarationNodeTypes.Contains(node.Type))
                {
                    var msgType = DeclarativeSemanticExtension.ExtractGenericTypeFromInterfaces(node, rule.ConsumerInterfaceSubstrings);
                    if (!string.IsNullOrEmpty(msgType))
                    {
                        references.Add(new Reference(scopeSymbolId, DeclarativeUniversalRuleEvaluator.FormatBrokerTopic(rule.Protocol, msgType), OntologyConstants.Relationships.SubscribesTo));
                    }
                }
            }

            for (var i = 0; i < ClassHierarchySinkRules.Length; i++)
            {
                var rule = ClassHierarchySinkRules[i];
                if (DeclarativeExtendedRuleEvaluator.MatchesClassHierarchyRule(node, rule, out var targetName))
                {
                    references.Add(new Reference(scopeSymbolId, targetName, rule.TargetRelation));
                }
            }
            return;
        }

        if (node.SyntaxKind is not (SyntaxKind.Annotation or SyntaxKind.FunctionDeclaration or SyntaxKind.SyntaxList))
        {
            return;
        }

        EvaluateDeclarationAndAnnotationReferences(node, scopeSymbolId, references);
    }

    private void EvaluateCallNodeReferences(Node callNode, string scopeSymbolId, List<Reference> references)
    {
        if (AllTrackedCallMethods.Count == 0) return;

        var methodSpan = DeclarativeSemanticExtension.GetCallMethodSpan(callNode);
        if (methodSpan.IsEmpty || !AllTrackedCallMethods.GetAlternateLookup<ReadOnlySpan<char>>().Contains(methodSpan))
        {
            return;
        }

        if (SqlCallSinksByMethod.Count > 0 &&
            SqlCallSinksByMethod.GetAlternateLookup<ReadOnlySpan<char>>().TryGetValue(methodSpan, out var sqlRules))
        {
            for (var i = 0; i < sqlRules.Length; i++)
            {
                if (DeclarativeUniversalRuleEvaluator.IsCallNode(callNode, sqlRules[i].CallNodeTypes))
                {
                    var sqlText = DeclarativeSemanticExtension.ExtractSqlText(callNode);
                    if (!string.IsNullOrEmpty(sqlText))
                    {
                        NestedSqlParser.TryDetectSqlDependencies(sqlText, scopeSymbolId, references);
                    }
                }
            }
        }

        if (PublishByMethod.Count > 0 &&
            PublishByMethod.GetAlternateLookup<ReadOnlySpan<char>>().TryGetValue(methodSpan, out var pubRules))
        {
            for (var i = 0; i < pubRules.Length; i++)
            {
                if (DeclarativeUniversalRuleEvaluator.IsCallNode(callNode, pubRules[i].CallNodeTypes))
                {
                    DeclarativeSemanticExtension.TryCollectMessagingPublishReferences(callNode, methodSpan, scopeSymbolId, references, pubRules[i]);
                }
            }
        }

        if (SubscribeByMethod.Count > 0 &&
            SubscribeByMethod.GetAlternateLookup<ReadOnlySpan<char>>().TryGetValue(methodSpan, out var subRules))
        {
            for (var i = 0; i < subRules.Length; i++)
            {
                if (DeclarativeUniversalRuleEvaluator.IsCallNode(callNode, subRules[i].CallNodeTypes))
                {
                    DeclarativeSemanticExtension.TryCollectMessagingSubscribeReferences(callNode, methodSpan, scopeSymbolId, references, subRules[i]);
                }
            }
        }

        var methodResolved = true;
        if (InboundRoutesByMethod.Count > 0 &&
            InboundRoutesByMethod.GetAlternateLookup<ReadOnlySpan<char>>().TryGetValue(methodSpan, out var routeRules))
        {
            for (var i = 0; i < routeRules.Length; i++)
            {
                DeclarativeUniversalRuleEvaluator.TryCollectInboundRouteCallReferences(callNode, ref methodSpan, ref methodResolved, scopeSymbolId, references, routeRules[i]);
            }
        }

        if (DataStoresByMethod.Count > 0 &&
            DataStoresByMethod.GetAlternateLookup<ReadOnlySpan<char>>().TryGetValue(methodSpan, out var dbRules))
        {
            for (var i = 0; i < dbRules.Length; i++)
            {
                DeclarativeUniversalRuleEvaluator.TryCollectDataStoreAccessReferences(callNode, ref methodSpan, ref methodResolved, scopeSymbolId, references, dbRules[i]);
            }
        }

        if (ServiceSinksByMethod.Count > 0 &&
            ServiceSinksByMethod.GetAlternateLookup<ReadOnlySpan<char>>().TryGetValue(methodSpan, out var sinkRules))
        {
            for (var i = 0; i < sinkRules.Length; i++)
            {
                DeclarativeUniversalRuleEvaluator.TryCollectServiceCallSinkReferences(callNode, ref methodSpan, ref methodResolved, scopeSymbolId, references, sinkRules[i]);
            }
        }
    }

    private void EvaluateDeclarationAndAnnotationReferences(Node node, string scopeSymbolId, List<Reference> references)
    {
        for (var i = 0; i < AnnotationSqlSinkRules.Length; i++)
        {
            var rule = AnnotationSqlSinkRules[i];
            if (DeclarativeSemanticExtension.MatchesSqlSink(node, rule))
            {
                var sqlText = DeclarativeSemanticExtension.ExtractSqlText(node);
                if (!string.IsNullOrEmpty(sqlText))
                {
                    NestedSqlParser.TryDetectSqlDependencies(sqlText, scopeSymbolId, references);
                }
            }
        }

        for (var i = 0; i < OrmTableRules.Length; i++)
        {
            var rule = OrmTableRules[i];
            if (DeclarativeSemanticExtension.MatchesOrmTableAnnotation(node, rule))
            {
                var tableName = DeclarativeSemanticExtension.ExtractOrmTableName(node, rule);
                if (!string.IsNullOrEmpty(tableName))
                {
                    references.Add(new Reference(scopeSymbolId, tableName, OntologyConstants.Relationships.PersistedIn));
                }
            }
        }

        var emptySpan = ReadOnlySpan<char>.Empty;
        var resolved = true;
        for (var i = 0; i < AnnotationMessagingRules.Length; i++)
        {
            DeclarativeUniversalRuleEvaluator.CollectMessagingRuleReferences(node, ref emptySpan, ref resolved, scopeSymbolId, references, AnnotationMessagingRules[i]);
        }

        for (var i = 0; i < AnnotationSinkRules.Length; i++)
        {
            DeclarativeExtendedRuleEvaluator.EvaluateAnnotationSinkRule(node, scopeSymbolId, references, AnnotationSinkRules[i]);
        }
    }
}
