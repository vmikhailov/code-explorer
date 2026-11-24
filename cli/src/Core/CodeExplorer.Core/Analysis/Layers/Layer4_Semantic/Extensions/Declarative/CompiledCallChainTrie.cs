using System.Collections.Frozen;
using CodeExplorer.Core.Common.Relationships;
using CodeExplorer.Core.Parser.Syntax;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Core.Parser;

/// <summary>
/// Pre-compiled reverse call-chain Trie (tree automaton) for a declarative library extension or project library bundle.
/// Evaluates arbitrary-depth fluent call chains, variable-split chains, nested route groups, outbound HTTP builders,
/// and messaging builders in a single <c>O(L)</c> inward pass from the outermost call node with zero intermediate collection allocations.
/// </summary>
internal sealed class CompiledCallChainTrie
{
    private const int MaxChainDepth = 12;
    private const int MaxParentBlockWalk = 16;
    private const int MaxBitmaskRuleIndex = 64;
    private const int MaxRouteGroupSegments = 4;
    private const int FirstGroupIndex = 0;
    private const int SecondGroupIndex = 1;
    private const int ThirdGroupIndex = 2;
    private const ulong SingleBitMask = 1UL;
    private const string SlashPrefix = "/";
    private const string HttpPrefix = "http";
    private const char DollarChar = '$';
    private const char SlashChar = '/';

    internal enum TrieTerminalCategory : byte
    {
        InboundRoute,
        DataStoreRead,
        DataStoreWrite,
        DataStoreDefault,
        MessagingPublish,
        MessagingSubscribe,
        ServiceSink,
        SqlSink
    }

    internal readonly record struct TrieTerminalAction(
        TrieTerminalCategory Category,
        string VerbOrRelation,
        int RuleIndex);

    private readonly InboundRouteCallRule[] _inboundRules;
    private readonly DataStoreAccessRule[] _dataStoreRules;
    private readonly MessagingBrokerRule[] _messagingRules;
    private readonly ServiceCallSinkRule[] _serviceSinkRules;
    private readonly SqlSinkRule[] _sqlSinkRules;

    private readonly FrozenDictionary<string, TrieTerminalAction[]> _rootTerminals;
    private readonly FrozenDictionary<string, TrieTerminalAction[]>.AlternateLookup<ReadOnlySpan<char>> _rootLookup;

    private readonly FrozenDictionary<string, ulong> _dbSelectorMaskByMethod;
    private readonly FrozenDictionary<string, ulong>.AlternateLookup<ReadOnlySpan<char>> _dbSelectorLookup;

    private readonly FrozenSet<string>.AlternateLookup<ReadOnlySpan<char>> _msgSelectorLookup;

    private CompiledCallChainTrie(
        InboundRouteCallRule[] inboundRules,
        DataStoreAccessRule[] dataStoreRules,
        MessagingBrokerRule[] messagingRules,
        ServiceCallSinkRule[] serviceSinkRules,
        SqlSinkRule[] sqlSinkRules,
        FrozenDictionary<string, TrieTerminalAction[]> rootTerminals,
        FrozenDictionary<string, ulong> dbSelectorMaskByMethod)
    {
        _inboundRules = inboundRules;
        _dataStoreRules = dataStoreRules;
        _messagingRules = messagingRules;
        _serviceSinkRules = serviceSinkRules;
        _sqlSinkRules = sqlSinkRules;
        _rootTerminals = rootTerminals;
        _rootLookup = rootTerminals.GetAlternateLookup<ReadOnlySpan<char>>();
        _dbSelectorMaskByMethod = dbSelectorMaskByMethod;
        _dbSelectorLookup = dbSelectorMaskByMethod.GetAlternateLookup<ReadOnlySpan<char>>();
        _msgSelectorLookup = DeclarativeCallChainCombinators.DefaultMessagingSelectorMethods.GetAlternateLookup<ReadOnlySpan<char>>();
    }

    internal static CompiledCallChainTrie Build(DeclarativeSemanticExtension ext)
    {
        var inboundRules = ext.InboundRouteCallRules.ToArray();
        var dataStoreRules = ext.DataStoreAccessRules.ToArray();
        var messagingRules = ext.MessagingBrokerRules.ToArray();
        var serviceSinkRules = ext.ServiceCallSinkRules.ToArray();
        var sqlSinkRules = ext.SqlSinkRules.ToArray();

        var rootMap = new Dictionary<string, List<TrieTerminalAction>>(StringComparer.Ordinal);
        var dbSelectorMap = new Dictionary<string, ulong>(StringComparer.Ordinal);

        for (var i = 0; i < inboundRules.Length; i++)
        {
            var r = inboundRules[i];
            foreach (var kvp in r.MethodToHttpVerb)
            {
                AddAction(rootMap, kvp.Key, new TrieTerminalAction(TrieTerminalCategory.InboundRoute, kvp.Value, i));
            }
        }

        for (var i = 0; i < dataStoreRules.Length; i++)
        {
            var r = dataStoreRules[i];
            foreach (var m in r.WriteMethodNames)
            {
                AddAction(rootMap, m, new TrieTerminalAction(TrieTerminalCategory.DataStoreWrite, OntologyConstants.Relationships.WritesTo, i));
            }
            foreach (var m in r.ReadMethodNames)
            {
                AddAction(rootMap, m, new TrieTerminalAction(TrieTerminalCategory.DataStoreRead, OntologyConstants.Relationships.ReadsFrom, i));
            }
            foreach (var m in r.CallMethodNames)
            {
                if (!r.WriteMethodNames.Contains(m) && !r.ReadMethodNames.Contains(m))
                {
                    AddAction(rootMap, m, new TrieTerminalAction(TrieTerminalCategory.DataStoreDefault, r.DefaultTargetRelation, i));
                }
            }
            foreach (var m in r.TargetSelectorMethods)
            {
                if (!r.CallMethodNames.Contains(m) && !r.WriteMethodNames.Contains(m) && !r.ReadMethodNames.Contains(m))
                {
                    AddAction(rootMap, m, new TrieTerminalAction(TrieTerminalCategory.DataStoreDefault, r.DefaultTargetRelation, i));
                }
            }

            if (i < MaxBitmaskRuleIndex)
            {
                var bit = SingleBitMask << i;
                var selectors = r.TargetSelectorMethods.Count > 0 ? r.TargetSelectorMethods : r.CallMethodNames;
                foreach (var s in selectors)
                {
                    dbSelectorMap[s] = dbSelectorMap.TryGetValue(s, out var existing) ? existing | bit : bit;
                }
            }
        }

        for (var i = 0; i < messagingRules.Length; i++)
        {
            var r = messagingRules[i];
            foreach (var m in r.PublishMethodNames)
            {
                AddAction(rootMap, m, new TrieTerminalAction(TrieTerminalCategory.MessagingPublish, OntologyConstants.Relationships.PublishesTo, i));
            }
            foreach (var m in r.SubscribeMethodNames)
            {
                AddAction(rootMap, m, new TrieTerminalAction(TrieTerminalCategory.MessagingSubscribe, OntologyConstants.Relationships.SubscribesTo, i));
            }
        }

        for (var i = 0; i < serviceSinkRules.Length; i++)
        {
            var r = serviceSinkRules[i];
            foreach (var m in r.CallMethodNames)
            {
                AddAction(rootMap, m, new TrieTerminalAction(TrieTerminalCategory.ServiceSink, r.TargetRelation, i));
            }
        }

        for (var i = 0; i < sqlSinkRules.Length; i++)
        {
            var r = sqlSinkRules[i];
            foreach (var m in r.CallMethodNames)
            {
                AddAction(rootMap, m, new TrieTerminalAction(TrieTerminalCategory.SqlSink, string.Empty, i));
            }
        }

        var frozenRoot = rootMap.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToArray(), StringComparer.Ordinal)
                                .ToFrozenDictionary(StringComparer.Ordinal);

        return new CompiledCallChainTrie(
            inboundRules,
            dataStoreRules,
            messagingRules,
            serviceSinkRules,
            sqlSinkRules,
            frozenRoot,
            dbSelectorMap.ToFrozenDictionary(StringComparer.Ordinal));
    }

    private static void AddAction(Dictionary<string, List<TrieTerminalAction>> map, string method, TrieTerminalAction action)
    {
        if (!map.TryGetValue(method, out var list))
        {
            list = [];
            map[method] = list;
        }
        if (!list.Contains(action))
        {
            list.Add(action);
        }
    }

    /// <summary>
    /// Evaluates a call node against the compiled reverse Trie in a single <c>O(L)</c> inward pass.
    /// </summary>
    internal void EvaluateCallNode(Node callNode, string scopeSymbolId, List<Reference> references)
    {
        if (_rootTerminals.Count == 0) return;

        var methodSpan = DeclarativeSemanticExtension.GetCallMethodSpan(callNode);
        if (methodSpan.IsEmpty || !_rootLookup.TryGetValue(methodSpan, out var actions))
        {
            return;
        }

        for (var i = 0; i < actions.Length; i++)
        {
            var act = actions[i];
            switch (act.Category)
            {
                case TrieTerminalCategory.InboundRoute:
                    EvaluateInboundRouteTriePass(callNode, scopeSymbolId, references, _inboundRules[act.RuleIndex], act.VerbOrRelation);
                    break;
                case TrieTerminalCategory.DataStoreRead:
                case TrieTerminalCategory.DataStoreWrite:
                case TrieTerminalCategory.DataStoreDefault:
                    EvaluateDataStoreTriePass(callNode, scopeSymbolId, references, _dataStoreRules[act.RuleIndex], act.RuleIndex, act.VerbOrRelation);
                    break;
                case TrieTerminalCategory.MessagingPublish:
                    EvaluateMessagingPublishTriePass(callNode, methodSpan, scopeSymbolId, references, _messagingRules[act.RuleIndex]);
                    break;
                case TrieTerminalCategory.MessagingSubscribe:
                    DeclarativeSemanticExtension.TryCollectMessagingSubscribeReferences(callNode, methodSpan, scopeSymbolId, references, _messagingRules[act.RuleIndex]);
                    break;
                case TrieTerminalCategory.ServiceSink:
                    var resolved = true;
                    DeclarativeUniversalRuleEvaluator.TryCollectServiceCallSinkReferences(callNode, ref methodSpan, ref resolved, scopeSymbolId, references, _serviceSinkRules[act.RuleIndex]);
                    break;
                case TrieTerminalCategory.SqlSink:
                    if (DeclarativeUniversalRuleEvaluator.IsCallNode(callNode, _sqlSinkRules[act.RuleIndex].CallNodeTypes))
                    {
                        var sqlText = DeclarativeSemanticExtension.ExtractSqlText(callNode);
                        if (!string.IsNullOrEmpty(sqlText))
                        {
                            NestedSqlParser.TryDetectSqlDependencies(sqlText, scopeSymbolId, references);
                        }
                    }
                    break;
            }
        }
    }

    private void EvaluateDataStoreTriePass(
        Node callNode,
        string scopeSymbolId,
        List<Reference> references,
        DataStoreAccessRule rule,
        int ruleIndex,
        string terminalRelation)
    {
        if (!DeclarativeUniversalRuleEvaluator.IsCallNode(callNode, rule.CallNodeTypes)) return;

        // O(1) Outer-chain deferral: if this node is an inner step (e.g. db.table("orders")) of an outer call
        // whose terminal method is also in the Trie for this rule, skip the inner node immediately.
        if (rule.ResolveFluentChains && DeclarativeCallChainCombinators.ShouldDeferToOutermostCallInChain(callNode, rule))
        {
            return;
        }

        var directObjSpan = DeclarativeSemanticExtension.GetCallReceiverSpan(callNode);
        var receiverMatched = rule.ReceiverSubstrings.Count == 0 ||
                              DeclarativeUniversalRuleEvaluator.SpanContainsAny(directObjSpan, rule.ReceiverSubstrings);

        if (!receiverMatched && (!rule.ResolveFluentChains || directObjSpan.IsEmpty))
        {
            return;
        }

        if (!string.IsNullOrEmpty(rule.StaticTargetName))
        {
            if (!receiverMatched && !WalkChainForReceiverOnly(callNode, rule.ReceiverSubstrings))
            {
                return;
            }
            references.Add(new Reference(scopeSymbolId, rule.StaticTargetName, rule.DefaultTargetRelation));
            if (rule.ParseSqlFromTarget)
            {
                var sql = DeclarativeUniversalRuleEvaluator.ExtractStringArgAtOrFirst(callNode, 0);
                if (!string.IsNullOrEmpty(sql))
                {
                    NestedSqlParser.TryDetectSqlDependencies(sql, scopeSymbolId, references);
                }
            }
            return;
        }

        // Single-pass Reverse-Trie walk from terminal call inward along receivers & variable initializers
        string? target = null;
        if (terminalRelation == rule.DefaultTargetRelation)
        {
            target = rule.TableArgumentIndex > 0
                ? DeclarativeUniversalRuleEvaluator.ExtractStringArgAtOrFirst(callNode, rule.TableArgumentIndex)
                : DeclarativeCallChainAstHelper.ExtractStepTargetArgument(callNode);
        }
        if (rule.ResolveFluentChains && (string.IsNullOrEmpty(target) || !receiverMatched))
        {
            var ruleBit = ruleIndex < MaxBitmaskRuleIndex ? (SingleBitMask << ruleIndex) : 0UL;
            ReadOnlySpan<char> visitedVar0 = default;
            ReadOnlySpan<char> visitedVar1 = default;
            var curr = DeclarativeCallChainAstHelper.UnwrapTransparentReceiver(DeclarativeCallChainAstHelper.GetReceiverNode(callNode));
            var steps = 0;

            while (curr != null && steps++ < MaxChainDepth)
            {
                if (!receiverMatched && DeclarativeUniversalRuleEvaluator.SpanContainsAny(curr.AsSpan(), rule.ReceiverSubstrings))
                {
                    receiverMatched = true;
                }

                if (DeclarativeCallChainAstHelper.IsCallLikeNode(curr))
                {
                    if (string.IsNullOrEmpty(target))
                    {
                        var stepMethod = DeclarativeSemanticExtension.GetCallMethodSpan(curr);
                        if (!stepMethod.IsEmpty &&
                            ((_dbSelectorLookup.TryGetValue(stepMethod, out var mask) && (mask & ruleBit) != 0) ||
                             DeclarativeCallChainCombinators.DefaultTargetSelectorMethods.GetAlternateLookup<ReadOnlySpan<char>>().Contains(stepMethod)))
                        {
                            target = rule.TableArgumentIndex > 0
                                ? DeclarativeUniversalRuleEvaluator.ExtractStringArgAtOrFirst(curr, rule.TableArgumentIndex)
                                : DeclarativeCallChainAstHelper.ExtractStepTargetArgument(curr);
                        }
                    }

                    var nextRecv = DeclarativeCallChainAstHelper.UnwrapTransparentReceiver(DeclarativeCallChainAstHelper.GetReceiverNode(curr));
                    if (!receiverMatched && nextRecv != null && DeclarativeUniversalRuleEvaluator.SpanContainsAny(nextRecv.AsSpan(), rule.ReceiverSubstrings))
                    {
                        receiverMatched = true;
                    }

                    // Only short-circuit early if we are NOT inside a variable hop (otherwise an inner call
                    // like .where("overdue", true) would capture "overdue" before reaching db.table("invoices"))
                    if (receiverMatched && !string.IsNullOrEmpty(target) && nextRecv is not CallExprSyntax)
                    {
                        break;
                    }

                    curr = nextRecv;
                    continue;
                }

                if (curr is IdentifierExprSyntax)
                {
                    var varSpan = curr.AsSpan().TrimStart(DollarChar);
                    if (!varSpan.IsEmpty && !varSpan.SequenceEqual(visitedVar0) && !varSpan.SequenceEqual(visitedVar1))
                    {
                        var initNode = DeclarativeCallChainAstHelper.TryFindVariableInitializerCallNodeSpan(callNode, varSpan);
                        if (initNode != null)
                        {
                            if (visitedVar0.IsEmpty) visitedVar0 = varSpan;
                            else visitedVar1 = varSpan;
                            curr = initNode;
                            continue;
                        }
                    }
                }
                break;
            }
        }

        // Fallback: single-call read/write terminal that directly carries the table/entity argument (e.g. mongo.find(q, Invoice.class))
        if (string.IsNullOrEmpty(target))
        {
            target = rule.TableArgumentIndex > 0
                ? DeclarativeUniversalRuleEvaluator.ExtractStringArgAtOrFirst(callNode, rule.TableArgumentIndex)
                : DeclarativeCallChainAstHelper.ExtractStepTargetArgument(callNode);
        }

        if (!receiverMatched || string.IsNullOrEmpty(target)) return;

        var effectiveRel = (rule.ReadMethodNames.Count == 0 && rule.WriteMethodNames.Count == 0)
            ? rule.DefaultTargetRelation
            : terminalRelation;

        DeclarativeUniversalRuleEvaluator.AddDataStoreReferenceDeduplicated(references, scopeSymbolId, target, effectiveRel);
    }

    private void EvaluateInboundRouteTriePass(
        Node callNode,
        string scopeSymbolId,
        List<Reference> references,
        InboundRouteCallRule rule,
        string verb)
    {
        if (!DeclarativeUniversalRuleEvaluator.IsCallNode(callNode, rule.CallNodeTypes)) return;

        var directObjSpan = DeclarativeSemanticExtension.GetCallReceiverSpan(callNode);
        if (rule.ExcludedReceivers.Count > 0 && !directObjSpan.IsEmpty &&
            rule.ExcludedReceivers.GetAlternateLookup<ReadOnlySpan<char>>().Contains(directObjSpan))
        {
            return;
        }

        var receiverMatched = rule.ReceiverSubstrings.Count == 0 ||
                              DeclarativeUniversalRuleEvaluator.SpanContainsAny(directObjSpan, rule.ReceiverSubstrings);
        if (!receiverMatched && (!rule.ResolveChainedRouteGroups || directObjSpan.IsEmpty))
        {
            return;
        }

        // Fast early exit on non-route calls (e.g. staleInvoices.delete() with 0 args or webClient.post() with 0 args)
        if (string.IsNullOrEmpty(rule.StaticRoute) &&
            rule.RequireLeadingSlashOnPath &&
            DeclarativeUniversalRuleEvaluator.CountCallArguments(callNode) < 2)
        {
            return;
        }

        var route = !string.IsNullOrEmpty(rule.StaticRoute)
            ? rule.StaticRoute
            : DeclarativeUniversalRuleEvaluator.ExtractStringArgAtOrFirst(callNode, rule.PathArgumentIndex);

        if (string.IsNullOrEmpty(rule.StaticRoute) &&
            rule.RequireLeadingSlashOnPath &&
            (string.IsNullOrEmpty(route) || !route.StartsWith(SlashPrefix, StringComparison.Ordinal)))
        {
            return;
        }

        if (string.IsNullOrEmpty(route))
        {
            if (!rule.EmitEntryPointNode) return;
            route = SlashPrefix;
        }

        // Fast stack-based route group accumulator (up to 4 group prefixes, zero heap allocation)
        string? g0 = null, g1 = null, g2 = null, g3 = null;
        var gCount = 0;

        if (rule.ResolveChainedRouteGroups)
        {
            var groupLookup = (rule.GroupMethodNames.Count > 0 ? rule.GroupMethodNames : DeclarativeCallChainCombinators.DefaultRouteGroupMethods)
                .GetAlternateLookup<ReadOnlySpan<char>>();
            ReadOnlySpan<char> visitedVar0 = default;
            ReadOnlySpan<char> visitedVar1 = default;

            var curr = DeclarativeCallChainAstHelper.UnwrapTransparentReceiver(DeclarativeCallChainAstHelper.GetReceiverNode(callNode));
            var steps = 0;
            while (curr != null && steps++ < MaxChainDepth)
            {
                if (!receiverMatched && DeclarativeUniversalRuleEvaluator.SpanContainsAny(curr.AsSpan(), rule.ReceiverSubstrings))
                {
                    receiverMatched = true;
                }

                if (DeclarativeCallChainAstHelper.IsCallLikeNode(curr))
                {
                    var mSpan = DeclarativeSemanticExtension.GetCallMethodSpan(curr);
                    if (!mSpan.IsEmpty && groupLookup.Contains(mSpan))
                    {
                        var seg = DeclarativeUniversalRuleEvaluator.ExtractStringArgAtOrFirst(curr, 0);
                        if (!string.IsNullOrEmpty(seg))
                        {
                            switch (gCount)
                            {
                                case 0: g0 = seg; break;
                                case 1: g1 = seg; break;
                                case 2: g2 = seg; break;
                                default: g3 = seg; break;
                            }
                            gCount++;
                        }
                    }
                    curr = DeclarativeCallChainAstHelper.UnwrapTransparentReceiver(DeclarativeCallChainAstHelper.GetReceiverNode(curr));
                    continue;
                }

                if (curr is IdentifierExprSyntax)
                {
                    var varSpan = curr.AsSpan().TrimStart(DollarChar);
                    if (!varSpan.IsEmpty && !varSpan.SequenceEqual(visitedVar0) && !varSpan.SequenceEqual(visitedVar1))
                    {
                        var initCall = DeclarativeCallChainAstHelper.TryFindVariableInitializerCallNodeSpan(callNode, varSpan);
                        if (initCall != null)
                        {
                            if (visitedVar0.IsEmpty) visitedVar0 = varSpan;
                            else visitedVar1 = varSpan;
                            curr = initCall;
                            continue;
                        }
                    }
                }
                break;
            }

            // Walk enclosing callback blocks for nested DSL route groups (only when callNode is inside a lambda/callback argument)
            var parent = callNode.Parent;
            var pDepth = 0;
            var insideCallback = false;
            while (parent != null && pDepth++ < MaxParentBlockWalk)
            {
                if (parent is LambdaExprSyntax or FunctionDeclSyntax)
                {
                    insideCallback = true;
                }
                else if (insideCallback && DeclarativeCallChainAstHelper.IsCallLikeNode(parent) && parent != callNode)
                {
                    var pMethod = DeclarativeSemanticExtension.GetCallMethodSpan(parent);
                    if (!pMethod.IsEmpty && groupLookup.Contains(pMethod))
                    {
                        if (!receiverMatched)
                        {
                            var pRecv = DeclarativeSemanticExtension.GetCallReceiverSpan(parent);
                            if (DeclarativeUniversalRuleEvaluator.SpanContainsAny(pRecv, rule.ReceiverSubstrings))
                            {
                                receiverMatched = true;
                            }
                        }
                        var seg = DeclarativeUniversalRuleEvaluator.ExtractStringArgAtOrFirst(parent, 0);
                        if (!string.IsNullOrEmpty(seg))
                        {
                            switch (gCount)
                            {
                                case 0: g0 = seg; break;
                                case 1: g1 = seg; break;
                                case 2: g2 = seg; break;
                                default: g3 = seg; break;
                            }
                            gCount++;
                        }
                    }
                }
                parent = parent.Parent;
            }
        }

        if (!receiverMatched) return;

        var effectivePrefix = rule.RoutePrefix;
        for (var i = Math.Min(gCount, MaxRouteGroupSegments) - 1; i >= 0; i--)
        {
            var seg = i switch { FirstGroupIndex => g0, SecondGroupIndex => g1, ThirdGroupIndex => g2, _ => g3 };
            if (!string.IsNullOrEmpty(seg))
            {
                effectivePrefix = DeclarativeSemanticExtension.CombineDeclarativeRoutes(
                    effectivePrefix,
                    DeclarativeSemanticExtension.NormalizeDeclarativeRoute(seg));
            }
        }

        var combined = string.IsNullOrEmpty(verb) && !string.IsNullOrEmpty(rule.StaticRoute)
            ? rule.StaticRoute
            : DeclarativeSemanticExtension.CombineDeclarativeRoutes(effectivePrefix, DeclarativeSemanticExtension.NormalizeDeclarativeRoute(route));
        var routeId = string.IsNullOrEmpty(verb) ? combined : $"{verb}:{combined}";

        if (rule.EmitEntryPointNode)
        {
            if (rule.EmitHandlerTriggers)
            {
                var handlerName = DeclarativeUniversalRuleEvaluator.ExtractLastIdentifierArg(callNode);
                if (!string.IsNullOrEmpty(handlerName))
                {
                    references.Add(new Reference(scopeSymbolId, handlerName, OntologyConstants.Relationships.Triggers));
                }
            }
            return;
        }

        references.Add(new Reference(scopeSymbolId, routeId, OntologyConstants.Relationships.Exposes));
    }

    private void EvaluateMessagingPublishTriePass(
        Node callNode,
        ReadOnlySpan<char> methodSpan,
        string scopeSymbolId,
        List<Reference> references,
        MessagingBrokerRule rule)
    {
        if (!DeclarativeUniversalRuleEvaluator.IsCallNode(callNode, rule.CallNodeTypes)) return;

        if (!rule.ResolveFluentChains)
        {
            DeclarativeSemanticExtension.TryCollectMessagingPublishReferences(callNode, methodSpan, scopeSymbolId, references, rule);
            return;
        }

        // Fast inline reverse-Trie walk for fluent builder chains (pulsar.newMessage().topic("x").sendAsync())
        // or nested builder argument (sns.publish(PublishRequest.builder().topicArn("y").build()))
        var recv = DeclarativeCallChainAstHelper.UnwrapTransparentReceiver(DeclarativeCallChainAstHelper.GetReceiverNode(callNode));
        if (DeclarativeCallChainAstHelper.IsCallLikeNode(recv))
        {
            var topic = WalkBuilderChainForTarget(recv, _msgSelectorLookup);
            if (!string.IsNullOrEmpty(topic))
            {
                if (rule.PublishReceiverSubstrings.Count == 0 ||
                    DeclarativeUniversalRuleEvaluator.SpanContainsAny(DeclarativeSemanticExtension.GetCallReceiverSpan(callNode), rule.PublishReceiverSubstrings) ||
                    WalkChainForReceiverOnly(callNode, rule.PublishReceiverSubstrings))
                {
                    references.Add(new Reference(scopeSymbolId, DeclarativeUniversalRuleEvaluator.FormatBrokerTopic(rule.Protocol, topic), OntologyConstants.Relationships.PublishesTo));
                }
                return;
            }
        }

        if (callNode is CallExprSyntax { Arguments.Count: > 0 } callExpr)
        {
            var firstArg = callExpr.Arguments[0].Value ?? (Node)callExpr.Arguments[0];
            if (firstArg is CallExprSyntax { CallKind: not CallSyntaxKind.Constructor })
            {
                var nestedTopic = WalkBuilderChainForTarget(firstArg, _msgSelectorLookup);
                if (!string.IsNullOrEmpty(nestedTopic))
                {
                    if (rule.PublishReceiverSubstrings.Count == 0 ||
                        DeclarativeUniversalRuleEvaluator.SpanContainsAny(DeclarativeSemanticExtension.GetCallReceiverSpan(callNode), rule.PublishReceiverSubstrings) ||
                        WalkChainForReceiverOnly(callNode, rule.PublishReceiverSubstrings))
                    {
                        references.Add(new Reference(scopeSymbolId, DeclarativeUniversalRuleEvaluator.FormatBrokerTopic(rule.Protocol, nestedTopic), OntologyConstants.Relationships.PublishesTo));
                    }
                    return;
                }
            }
        }

        DeclarativeSemanticExtension.TryCollectMessagingPublishReferences(callNode, methodSpan, scopeSymbolId, references, rule);
    }

    internal static string? TryExtractOutboundHttpTargetViaTrie(Node callNode, OutboundHttpRule rule)
    {
        if (!rule.ResolveFluentChains) return null;
        var recv = DeclarativeCallChainAstHelper.UnwrapTransparentReceiver(DeclarativeCallChainAstHelper.GetReceiverNode(callNode));
        if (!DeclarativeCallChainAstHelper.IsCallLikeNode(recv)) return null;

        var rawUrl = WalkBuilderChainForTarget(recv, DeclarativeCallChainCombinators.DefaultUriSelectorMethods.GetAlternateLookup<ReadOnlySpan<char>>());
        if (string.IsNullOrEmpty(rawUrl)) return null;
        if (!rawUrl.Contains(SlashChar) && !rawUrl.StartsWith(HttpPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return RouteDictionaryRegistry.NormalizeResolvedUrl(rawUrl);
    }

    private static string? WalkBuilderChainForTarget(
        Node? startCall,
        FrozenSet<string>.AlternateLookup<ReadOnlySpan<char>> selectorLookup)
    {
        ReadOnlySpan<char> visitedVar0 = default;
        ReadOnlySpan<char> visitedVar1 = default;
        var curr = startCall;
        var steps = 0;
        while (curr != null && steps++ < MaxChainDepth)
        {
            if (DeclarativeCallChainAstHelper.IsCallLikeNode(curr))
            {
                var mSpan = DeclarativeSemanticExtension.GetCallMethodSpan(curr);
                if (!mSpan.IsEmpty && selectorLookup.Contains(mSpan))
                {
                    var arg = DeclarativeCallChainAstHelper.ExtractStepTargetArgument(curr);
                    if (!string.IsNullOrEmpty(arg)) return arg;
                }
                curr = DeclarativeCallChainAstHelper.UnwrapTransparentReceiver(DeclarativeCallChainAstHelper.GetReceiverNode(curr));
                continue;
            }

            if (curr is IdentifierExprSyntax)
            {
                var varSpan = curr.AsSpan().TrimStart(DollarChar);
                if (!varSpan.IsEmpty && !varSpan.SequenceEqual(visitedVar0) && !varSpan.SequenceEqual(visitedVar1))
                {
                    var initCall = DeclarativeCallChainAstHelper.TryFindVariableInitializerCallNodeSpan(curr, varSpan);
                    if (initCall != null)
                    {
                        if (visitedVar0.IsEmpty) visitedVar0 = varSpan;
                        else visitedVar1 = varSpan;
                        curr = initCall;
                        continue;
                    }
                }
            }
            break;
        }
        return null;
    }

    private static bool WalkChainForReceiverOnly(Node callNode, FrozenSet<string> receiverSubstrings)
    {
        var curr = DeclarativeCallChainAstHelper.UnwrapTransparentReceiver(DeclarativeCallChainAstHelper.GetReceiverNode(callNode));
        var steps = 0;
        while (curr != null && steps++ < MaxChainDepth)
        {
            if (DeclarativeUniversalRuleEvaluator.SpanContainsAny(curr.AsSpan(), receiverSubstrings)) return true;
            if (DeclarativeCallChainAstHelper.IsCallLikeNode(curr))
            {
                curr = DeclarativeCallChainAstHelper.UnwrapTransparentReceiver(DeclarativeCallChainAstHelper.GetReceiverNode(curr));
                continue;
            }
            break;
        }
        return false;
    }
}
