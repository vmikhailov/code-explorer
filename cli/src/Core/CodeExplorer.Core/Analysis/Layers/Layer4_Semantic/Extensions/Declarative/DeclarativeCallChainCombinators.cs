using System.Collections.Frozen;
using CodeExplorer.Core.Parser.Syntax;
using Superpower;
using Superpower.Model;
using Superpower.Parsers;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Core.Parser;

internal enum CallChainTokenKind
{
    Receiver,
    TargetSelector,
    RouteGroup,
    VerbSelector,
    IntermediateStep,
    TerminalRead,
    TerminalWrite,
    TerminalAction
}

internal readonly record struct CallChainStep(
    CallChainTokenKind Kind,
    Node? StepNode);

internal readonly record struct DataStoreChainMatch(
    string TargetTable,
    string TargetRelation,
    bool IsSqlQuery);

internal readonly record struct OutboundHttpChainMatch(
    string? HttpVerb,
    string TargetUrl);

/// <summary>
/// Island-grammar combinator engine built on Superpower (<see cref="TokenListParser{TTokenKind,TResult}"/>)
/// that linearizes fluent AST call chains (including local variable receiver hops and enclosing route-group blocks)
/// into a <see cref="TokenList{TTokenKind}"/> and extracts multi-hop targets, route prefixes, and read/write semantics.
/// </summary>
internal static class DeclarativeCallChainCombinators
{
    private const int MaxChainDepth = 12;
    private const int MaxVariableHops = 4;
    private const int MaxParentBlockWalk = 16;
    private const int SingleLineNumber = 1;
    private const int SingleCharLength = 1;
    private const string DummyTokenSource = "_";
    private const string SlashStr = "/";
    private const string HttpPrefix = "http";
    private const string ClassSuffix = ".class";
    private const string DoubleColonClassSuffix = "::class";
    private const string PropertyElementType = "property_element";
    private const string ClassBodyType = "class_body";
    private const string StatementBlockType = "statement_block";
    private const string CompoundStatementType = "compound_statement";
    private const string ShortVarDeclType = "short_var_declaration";
    private const string AssignmentExprType = "assignment_expression";
    private const string LexicalDeclType = "lexical_declaration";
    private const string VariableDeclaratorType = "variable_declarator";
    private const string LocalVarDeclType = "local_variable_declaration";
    private const string LetDeclType = "let_declaration";
    private const string GetVerb = "get";
    private const string PostVerb = "post";
    private const string PutVerb = "put";
    private const string DeleteVerb = "delete";
    private const string PatchVerb = "patch";
    private const string GetUpper = "GET";
    private const string PostUpper = "POST";
    private const string PutUpper = "PUT";
    private const string DeleteUpper = "DELETE";
    private const string PatchUpper = "PATCH";
    private const char DotChar = '.';
    private const char OpenAngleChar = '<';
    private const char CloseAngleChar = '>';
    private const char OpenParenChar = '(';
    private const char CommaChar = ',';
    private const char SpaceChar = ' ';
    private const char SlashChar = '/';
    private const char DollarChar = '$';
    private static readonly char[] QuoteChars = ['"', '\'', '`'];

    internal static readonly FrozenSet<string> DefaultTargetSelectorMethods = new[]
    {
        "table", "Table", "from", "From", "into", "Into", "collection", "Collection",
        "query", "Query", "entity", "Entity", "model", "Model", "bucket", "Bucket",
        "index", "Index", "key", "Key", "stream", "Stream"
    }.ToFrozenSet(StringComparer.Ordinal);

    internal static readonly FrozenSet<string> DefaultUriSelectorMethods = new[]
    {
        "uri", "Uri", "url", "Url", "path", "Path", "request", "Request",
        "endpoint", "Endpoint", "baseUrl", "BaseUrl", "target", "Target"
    }.ToFrozenSet(StringComparer.Ordinal);

    internal static readonly FrozenSet<string> DefaultMessagingSelectorMethods = new[]
    {
        "topic", "Topic", "withTopic", "WithTopic", "topicArn", "TopicArn",
        "queue", "Queue", "withQueue", "WithQueue", "queueUrl", "QueueUrl",
        "subject", "Subject", "channel", "Channel", "exchange", "Exchange",
        "stream", "Stream", "routingKey", "RoutingKey", "destination", "Destination"
    }.ToFrozenSet(StringComparer.Ordinal);

    internal static readonly FrozenSet<string> DefaultRouteGroupMethods = new[]
    {
        "group", "Group", "route", "Route", "MapGroup", "mapGroup",
        "nest", "Nest", "scope", "Scope", "prefix", "Prefix",
        "namespace", "Namespace", "subrouter", "Subrouter", "PathPrefix"
    }.ToFrozenSet(StringComparer.Ordinal);

    internal static readonly FrozenSet<string> DefaultHttpVerbMethods = new[]
    {
        "get", "GET", "Get", "GetAsync", "MapGet",
        "post", "POST", "Post", "PostAsync", "MapPost",
        "put", "PUT", "Put", "PutAsync", "MapPut",
        "delete", "DELETE", "Delete", "DeleteAsync", "MapDelete",
        "patch", "PATCH", "Patch", "PatchAsync", "MapPatch",
        "head", "HEAD", "Head", "options", "OPTIONS", "Options"
    }.ToFrozenSet(StringComparer.Ordinal);

    internal static readonly FrozenSet<string> DefaultWriteTerminalMethods = new[]
    {
        "insert", "Insert", "InsertAsync", "insertOne", "insertMany", "InsertAll", "InsertAllAsync",
        "update", "Update", "UpdateAsync", "updateOne", "updateMany", "UpdateAll", "UpdateAllAsync",
        "upsert", "Upsert", "UpsertAsync", "merge", "Merge", "MergeAsync",
        "delete", "Delete", "DeleteAsync", "deleteOne", "deleteMany", "DeleteAll", "DeleteAllAsync",
        "remove", "Remove", "RemoveAsync", "destroy", "Destroy",
        "save", "Save", "SaveAsync", "create", "Create", "CreateAsync",
        "set", "Set", "SetAsync", "hset", "HSet", "lpush", "rpush", "sadd", "zadd",
        "increment", "decrement", "truncate", "Truncate",
        "execute", "Execute", "ExecuteAsync", "exec", "Exec", "ExecuteNonQuery", "ExecuteNonQueryAsync"
    }.ToFrozenSet(StringComparer.Ordinal);

    internal static readonly FrozenSet<string> DefaultReadTerminalMethods = new[]
    {
        "select", "Select", "SelectAsync", "first", "First", "FirstAsync",
        "firstOrDefault", "FirstOrDefault", "FirstOrDefaultAsync",
        "single", "Single", "SingleOrDefault", "SingleOrDefaultAsync",
        "get", "Get", "GetAsync", "getOne", "getMany", "GetList", "GetListAsync",
        "find", "Find", "FindAsync", "findOne", "findMany", "findById", "findUnique", "findFirst",
        "pluck", "Pluck", "value", "Value", "count", "Count", "CountAsync",
        "exists", "Exists", "ExistsAsync", "all", "All", " toList", "ToList", "ToListAsync",
        "toArray", "ToArray", "ToArrayAsync", "fetch", "Fetch", "fetchOne", "fetchAll",
        "ExecuteQuery", "ExecuteQueryAsync", "Query", "QueryAsync",
        "retrieve", "Retrieve", "exchange", "Exchange", "bodyToMono", "bodyToFlux",
        "GetJsonAsync", "GetStringAsync", "GetBytesAsync"
    }.ToFrozenSet(StringComparer.Ordinal);

    // Superpower Island-Grammar Combinators over TokenList<CallChainTokenKind>
    // Custom zero-allocation delegate combinators instead of LINQ query comprehension + .Many()
    private static readonly TokenListParser<CallChainTokenKind, (int SelectorIndex, int TerminalIndex)> DataStoreIslandParser =
        input =>
        {
            var selectorIdx = -1;
            var terminalIdx = -1;
            var cur = input;
            while (!cur.IsAtEnd)
            {
                var next = cur.ConsumeToken();
                if (!next.HasValue) break;

                var kind = next.Value.Kind;
                var idx = next.Value.Position.Absolute;
                if (kind == CallChainTokenKind.TargetSelector)
                {
                    selectorIdx = idx;
                }
                else if (selectorIdx >= 0 &&
                         kind is CallChainTokenKind.TerminalWrite or CallChainTokenKind.TerminalRead or CallChainTokenKind.TerminalAction)
                {
                    terminalIdx = idx;
                }
                cur = next.Remainder;
            }

            return selectorIdx >= 0 && terminalIdx > selectorIdx
                ? TokenListParserResult.Value((selectorIdx, terminalIdx), input, cur)
                : TokenListParserResult.Empty<CallChainTokenKind, (int SelectorIndex, int TerminalIndex)>(input);
        };

    private static readonly TokenListParser<CallChainTokenKind, (int Count, int G0, int G1, int G2, int G3)> RouteGroupsIslandParser =
        input =>
        {
            var count = 0;
            int g0 = -1, g1 = -1, g2 = -1, g3 = -1;
            var cur = input;
            while (!cur.IsAtEnd)
            {
                var next = cur.ConsumeToken();
                if (!next.HasValue) break;

                if (next.Value.Kind == CallChainTokenKind.RouteGroup)
                {
                    var idx = next.Value.Position.Absolute;
                    switch (count)
                    {
                        case 0: g0 = idx; break;
                        case 1: g1 = idx; break;
                        case 2: g2 = idx; break;
                        default: g3 = idx; break;
                    }
                    count++;
                }
                cur = next.Remainder;
            }

            return count > 0
                ? TokenListParserResult.Value((count, g0, g1, g2, g3), input, cur)
                : TokenListParserResult.Empty<CallChainTokenKind, (int Count, int G0, int G1, int G2, int G3)>(input);
        };

    private static readonly TokenListParser<CallChainTokenKind, (int VerbIndex, int UriIndex)> OutboundHttpIslandParser =
        input =>
        {
            var verbIdx = -1;
            var uriIdx = -1;
            var cur = input;
            while (!cur.IsAtEnd)
            {
                var next = cur.ConsumeToken();
                if (!next.HasValue) break;

                var kind = next.Value.Kind;
                var idx = next.Value.Position.Absolute;
                if (kind == CallChainTokenKind.TargetSelector)
                {
                    uriIdx = idx;
                }
                else if (kind == CallChainTokenKind.VerbSelector)
                {
                    verbIdx = idx;
                }
                cur = next.Remainder;
            }

            return uriIdx >= 0
                ? TokenListParserResult.Value((verbIdx, uriIdx), input, cur)
                : TokenListParserResult.Empty<CallChainTokenKind, (int VerbIndex, int UriIndex)>(input);
        };

    /// <summary>
    /// Fast zero-allocation check whether <paramref name="callNode"/> is part of a multi-step fluent call chain
    /// (either having a call expression as its receiver or acting as the receiver of an outer call expression).
    /// </summary>
    internal static bool HasOuterOrInnerFluentChain(Node callNode)
    {
        var recv = UnwrapTransparentReceiver(GetReceiverNode(callNode));
        if (IsCallLikeNode(recv)) return true;

        return FindOutermostChainedCall(callNode) != callNode;
    }

    /// <summary>
    /// Returns true if <paramref name="callNode"/> is an inner step of a fluent chain whose outermost call
    /// is also matched by <paramref name="rule"/> (so we can skip the inner step and parse the chain once at the outermost call).
    /// </summary>
    internal static bool ShouldDeferToOutermostCallInChain(Node callNode, DataStoreAccessRule rule)
    {
        var outermost = FindOutermostChainedCall(callNode);
        if (outermost == callNode) return false;

        var outerSpan = DeclarativeSemanticExtension.GetCallMethodSpan(outermost);
        if (outerSpan.IsEmpty) return false;

        return rule.CallMethodNames.GetAlternateLookup<ReadOnlySpan<char>>().Contains(outerSpan) ||
               (rule.TargetSelectorMethods.Count > 0 && rule.TargetSelectorMethods.GetAlternateLookup<ReadOnlySpan<char>>().Contains(outerSpan)) ||
               (rule.ReadMethodNames.Count > 0 && rule.ReadMethodNames.GetAlternateLookup<ReadOnlySpan<char>>().Contains(outerSpan)) ||
               (rule.WriteMethodNames.Count > 0 && rule.WriteMethodNames.GetAlternateLookup<ReadOnlySpan<char>>().Contains(outerSpan));
    }

    /// <summary>
    /// Checks whether any receiver or intermediate step in the linearized call chain (including variable initializer hops)
    /// matches one of the required <paramref name="receiverSubstrings"/>.
    /// </summary>
    internal static bool ChainMatchesReceiver(Node callNode, FrozenSet<string> receiverSubstrings)
    {
        if (receiverSubstrings.Count == 0) return true;

        var recvNode = UnwrapTransparentReceiver(GetReceiverNode(callNode));
        if (recvNode == null || !recvNode.IsValid()) return false;

        ReadOnlySpan<char> visitedVar0 = default;
        ReadOnlySpan<char> visitedVar1 = default;
        var curr = recvNode;
        var steps = 0;

        while (curr != null && curr.IsValid() && steps++ < MaxChainDepth)
        {
            if (steps > 1 && DeclarativeUniversalRuleEvaluator.SpanContainsAny(curr.AsSpan(), receiverSubstrings))
            {
                return true;
            }

            if (IsCallLikeNode(curr))
            {
                var innerRecvSpan = DeclarativeSemanticExtension.GetCallReceiverSpan(curr);
                if (DeclarativeUniversalRuleEvaluator.SpanContainsAny(innerRecvSpan, receiverSubstrings))
                {
                    return true;
                }
                curr = UnwrapTransparentReceiver(GetReceiverNode(curr));
                continue;
            }

            if (curr is IdentifierExprSyntax)
            {
                var varSpan = curr.AsSpan().TrimStart(DollarChar);
                if (!varSpan.IsEmpty &&
                    !varSpan.SequenceEqual(visitedVar0) &&
                    !varSpan.SequenceEqual(visitedVar1))
                {
                    var initCall = TryFindVariableInitializerCallNodeSpan(callNode, varSpan);
                    if (initCall != null && initCall.IsValid())
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

        // Also check if callNode is inside an enclosing route-group callback block whose receiver matches
        var groupLookup = DefaultRouteGroupMethods.GetAlternateLookup<ReadOnlySpan<char>>();
        var parent = callNode.Parent;
        var pDepth = 0;
        while (parent.IsValid() && pDepth++ < MaxParentBlockWalk)
        {
            if (IsCallLikeNode(parent) && parent != callNode)
            {
                var pMethodSpan = DeclarativeSemanticExtension.GetCallMethodSpan(parent);
                if (!pMethodSpan.IsEmpty && groupLookup.Contains(pMethodSpan))
                {
                    var pRecvSpan = DeclarativeSemanticExtension.GetCallReceiverSpan(parent);
                    if (DeclarativeUniversalRuleEvaluator.SpanContainsAny(pRecvSpan, receiverSubstrings))
                    {
                        return true;
                    }
                }
            }
            parent = parent.Parent;
        }

        return false;
    }

    /// <summary>
    /// Attempts to evaluate a fluent data-store access chain (e.g. <c>db.table("orders").where(...).update(...)</c>
    /// or variable-split <c>const q = db.from("orders"); await q.delete()</c>) using Superpower island combinators.
    /// </summary>
    internal static bool TryMatchDataStoreChain(
        Node callNode,
        DataStoreAccessRule rule,
        bool verifyReceiverSubstrings,
        out DataStoreChainMatch match)
    {
        match = default;

        var steps = LinearizeCallChain(
            callNode,
            targetSelectors: rule.TargetSelectorMethods.Count > 0 ? rule.TargetSelectorMethods : DefaultTargetSelectorMethods,
            readTerminals: rule.ReadMethodNames.Count > 0 ? rule.ReadMethodNames : DefaultReadTerminalMethods,
            writeTerminals: rule.WriteMethodNames.Count > 0 ? rule.WriteMethodNames : DefaultWriteTerminalMethods,
            requiredReceivers: verifyReceiverSubstrings ? rule.ReceiverSubstrings : FrozenSet<string>.Empty,
            out var receiverMatched);

        if (steps.Count == 0 || (verifyReceiverSubstrings && !receiverMatched)) return false;

        if (steps.Count <= 2)
        {
            var onlyCall = steps[^1];
            var onlyArg = ExtractStepTargetArgument(onlyCall.StepNode);
            if (!string.IsNullOrEmpty(onlyArg))
            {
                var relKind = ClassifyTerminalRelationship(onlyCall.Kind, onlyCall.StepNode, rule);
                match = new DataStoreChainMatch(onlyArg, relKind, IsSqlQuery: false);
                return true;
            }
            return false;
        }

        var tokens = BuildTokenList(steps);
        var parsed = DataStoreIslandParser.TryParse(tokens);
        if (parsed.HasValue)
        {
            var selIdx = parsed.Value.SelectorIndex;
            var termIdx = parsed.Value.TerminalIndex;
            var target = ExtractStepTargetArgument(steps[selIdx].StepNode);

            if (!string.IsNullOrEmpty(target))
            {
                var relKind = ClassifyTerminalRelationship(steps[termIdx].Kind, steps[termIdx].StepNode, rule);
                match = new DataStoreChainMatch(target, relKind, IsSqlQuery: false);
                return true;
            }
        }

        // Single-call or terminal-call fallback with generic type <T> or Class.class argument
        var lastStep = steps[^1];
        var lastArg = ExtractStepTargetArgument(lastStep.StepNode);
        if (!string.IsNullOrEmpty(lastArg))
        {
            var relKind = ClassifyTerminalRelationship(lastStep.Kind, lastStep.StepNode, rule);
            match = new DataStoreChainMatch(lastArg, relKind, IsSqlQuery: false);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Attempts to accumulate all parent/chained route group prefixes (e.g. <c>app.group("/api/v1").group("/orders").post("/cancel", ...)</c>,
    /// variable-split <c>v1 := app.Group("/api/v1"); v1.POST("/orders", ...)</c>, or nested Ktor/Gin/Express callback blocks)
    /// using Superpower island combinators.
    /// </summary>
    internal static string? TryResolveChainedRouteGroupPrefix(
        Node callNode,
        InboundRouteCallRule rule,
        out bool chainMatchedReceiver)
    {
        var groupMethods = rule.GroupMethodNames.Count > 0 ? rule.GroupMethodNames : DefaultRouteGroupMethods;
        var steps = LinearizeRouteChain(callNode, groupMethods, rule.ReceiverSubstrings, out chainMatchedReceiver);
        if (steps.Count == 0) return null;

        // Fast path: single-group chain avoids Superpower parser combinator closure allocations
        if (steps.Count <= 2)
        {
            for (var i = 0; i < steps.Count; i++)
            {
                if (steps[i].Kind == CallChainTokenKind.RouteGroup && steps[i].StepNode is { } singleGroupNode)
                {
                    var seg = DeclarativeUniversalRuleEvaluator.ExtractStringArgAtOrFirst(singleGroupNode, 0);
                    return string.IsNullOrEmpty(seg) ? null : DeclarativeSemanticExtension.NormalizeDeclarativeRoute(seg);
                }
            }
            return null;
        }

        var tokens = BuildTokenList(steps);
        var parsed = RouteGroupsIslandParser.TryParse(tokens);
        if (!parsed.HasValue || parsed.Value.Count == 0) return null;

        var combinedPrefix = string.Empty;
        var groupCount = Math.Min(parsed.Value.Count, 4);
        for (var i = 0; i < groupCount; i++)
        {
            var idx = i switch
            {
                0 => parsed.Value.G0,
                1 => parsed.Value.G1,
                2 => parsed.Value.G2,
                _ => parsed.Value.G3
            };
            var stepNode = idx >= 0 && idx < steps.Count ? steps[idx].StepNode : null;
            var seg = stepNode != null ? DeclarativeUniversalRuleEvaluator.ExtractStringArgAtOrFirst(stepNode, 0) : null;
            if (!string.IsNullOrEmpty(seg))
            {
                combinedPrefix = DeclarativeSemanticExtension.CombineDeclarativeRoutes(
                    combinedPrefix,
                    DeclarativeSemanticExtension.NormalizeDeclarativeRoute(seg));
            }
        }

        return string.IsNullOrEmpty(combinedPrefix) ? null : combinedPrefix;
    }

    /// <summary>
    /// Attempts to resolve an outbound HTTP URL and verb from a fluent builder chain
    /// (e.g. <c>webClient.post().uri("https://api.internal/v1/pay").header(...).retrieve()</c>
    /// or <c>client.Request("/v1/orders").WithHeader(...).PostJsonAsync(...)</c>).
    /// </summary>
    internal static bool TryMatchOutboundHttpChain(
        Node callNode,
        OutboundHttpRule rule,
        out OutboundHttpChainMatch match)
    {
        match = default;
        var steps = LinearizeCallChain(
            callNode,
            targetSelectors: DefaultUriSelectorMethods,
            readTerminals: DefaultReadTerminalMethods,
            writeTerminals: DefaultWriteTerminalMethods);

        if (steps.Count < 2) return false;

        var tokens = BuildTokenList(steps);
        var parsed = OutboundHttpIslandParser.TryParse(tokens);
        if (!parsed.HasValue) return false;

        var uriIdx = parsed.Value.UriIndex;
        var rawUrl = ExtractStepTargetArgument(steps[uriIdx].StepNode);
        if (string.IsNullOrEmpty(rawUrl)) return false;
        if (!rawUrl.Contains(SlashChar) && !rawUrl.StartsWith(HttpPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string? verb = null;
        if (parsed.Value.VerbIndex >= 0)
        {
            var verbNode = steps[parsed.Value.VerbIndex].StepNode;
            var verbSpan = verbNode != null ? DeclarativeSemanticExtension.GetCallMethodSpan(verbNode) : ReadOnlySpan<char>.Empty;
            verb = DeclarativeCallChainAstHelper.NormalizeHttpVerbSpan(verbSpan);
        }

        match = new OutboundHttpChainMatch(verb, RouteDictionaryRegistry.NormalizeResolvedUrl(rawUrl));
        return true;
    }

    /// <summary>
    /// Attempts to resolve a messaging topic/queue/subject from a fluent builder chain or nested builder argument
    /// (e.g. <c>producer.newMessage().topic("orders.paid").value(v).send()</c> or
    /// <c>sns.publish(PublishRequest.builder().topicArn("orders").build())</c>).
    /// </summary>
    internal static string? TryExtractMessagingTopicFromChainOrBuilder(Node callNode)
    {
        // Fast guard: only linearize if callNode has a chained receiver or a call-expression argument
        var recv = UnwrapTransparentReceiver(GetReceiverNode(callNode));
        var hasChainedRecv = IsCallLikeNode(recv);

        if (hasChainedRecv)
        {
            var chainSteps = LinearizeCallChain(
                callNode,
                targetSelectors: DefaultMessagingSelectorMethods,
                readTerminals: FrozenSet<string>.Empty,
                writeTerminals: FrozenSet<string>.Empty);

            for (var i = chainSteps.Count - 1; i >= 0; i--)
            {
                if (chainSteps[i].Kind == CallChainTokenKind.TargetSelector)
                {
                    var topicArg = ExtractStepTargetArgument(chainSteps[i].StepNode);
                    if (!string.IsNullOrEmpty(topicArg)) return topicArg;
                }
            }
        }

        // 2. Check nested builder chains inside callNode's arguments (e.g. PublishRequest.builder().topic("orders").build())
        if (callNode is CallExprSyntax callExpr)
        {
            for (var i = 0; i < callExpr.Arguments.Count; i++)
            {
                var arg = callExpr.Arguments[i].Value ?? (Node)callExpr.Arguments[i];
                if (arg is CallExprSyntax { CallKind: not CallSyntaxKind.Constructor })
                {
                    var argSteps = LinearizeCallChain(
                        arg,
                        targetSelectors: DefaultMessagingSelectorMethods,
                        readTerminals: FrozenSet<string>.Empty,
                        writeTerminals: FrozenSet<string>.Empty);

                    for (var s = argSteps.Count - 1; s >= 0; s--)
                    {
                        if (argSteps[s].Kind == CallChainTokenKind.TargetSelector)
                        {
                            var topicArg = ExtractStepTargetArgument(argSteps[s].StepNode);
                            if (!string.IsNullOrEmpty(topicArg)) return topicArg;
                        }
                    }
                }
            }
            return null;
        }

        var argList = DeclarativeSemanticExtension.FindArgumentsNode(callNode);
        if (!argList.IsValid()) return null;

        for (var i = 0; i < argList.Children.Count; i++)
        {
            var arg = argList.Children[i];
            if (arg is ArgumentSyntax { Value: { } argVal }) arg = argVal;

            if (arg is CallExprSyntax { CallKind: not CallSyntaxKind.Constructor })
            {
                var argSteps = LinearizeCallChain(
                    arg,
                    targetSelectors: DefaultMessagingSelectorMethods,
                    readTerminals: FrozenSet<string>.Empty,
                    writeTerminals: FrozenSet<string>.Empty);

                for (var s = argSteps.Count - 1; s >= 0; s--)
                {
                    if (argSteps[s].Kind == CallChainTokenKind.TargetSelector)
                    {
                        var topicArg = ExtractStepTargetArgument(argSteps[s].StepNode);
                        if (!string.IsNullOrEmpty(topicArg)) return topicArg;
                    }
                }
            }
        }

        return null;
    }

    private static string ClassifyTerminalRelationship(
        CallChainTokenKind terminalKind,
        Node? terminalNode,
        DataStoreAccessRule rule)
    {
        if (!string.IsNullOrEmpty(rule.DefaultTargetRelation) &&
            rule.DefaultTargetRelation != OntologyConstants.Relationships.UsesDb)
        {
            return rule.DefaultTargetRelation;
        }

        if (rule.WriteMethodNames.Count == 0 && rule.ReadMethodNames.Count == 0)
        {
            return rule.DefaultTargetRelation;
        }

        var methodSpan = terminalNode != null ? DeclarativeSemanticExtension.GetCallMethodSpan(terminalNode) : ReadOnlySpan<char>.Empty;

        if (terminalKind == CallChainTokenKind.TerminalWrite ||
            (!methodSpan.IsEmpty && rule.WriteMethodNames.GetAlternateLookup<ReadOnlySpan<char>>().Contains(methodSpan)))
        {
            return OntologyConstants.Relationships.WritesTo;
        }

        if (terminalKind == CallChainTokenKind.TerminalRead ||
            (!methodSpan.IsEmpty && rule.ReadMethodNames.GetAlternateLookup<ReadOnlySpan<char>>().Contains(methodSpan)))
        {
            return OntologyConstants.Relationships.ReadsFrom;
        }

        return rule.DefaultTargetRelation;
    }

    private static readonly Superpower.Model.TextSpan[] IndexedTokenSpans = CreateIndexedTokenSpans();

    private static Superpower.Model.TextSpan[] CreateIndexedTokenSpans()
    {
        var arr = new Superpower.Model.TextSpan[MaxChainDepth + MaxParentBlockWalk + MaxVariableHops];
        for (var i = 0; i < arr.Length; i++)
        {
            var pos = new Position(i, SingleLineNumber, i + SingleLineNumber);
            arr[i] = new Superpower.Model.TextSpan(DummyTokenSource, pos, SingleCharLength);
        }
        return arr;
    }

    [ThreadStatic]
    private static List<CallChainStep>? _threadStepBuffer;

    [ThreadStatic]
    private static Token<CallChainTokenKind>[][]? _threadTokenArraysByLen;

    private static List<CallChainStep> GetThreadStepBuffer()
    {
        var buf = _threadStepBuffer;
        if (buf == null)
        {
            buf = new List<CallChainStep>(capacity: 8);
            _threadStepBuffer = buf;
        }
        else
        {
            buf.Clear();
        }
        return buf;
    }

    private static TokenList<CallChainTokenKind> BuildTokenList(List<CallChainStep> steps)
    {
        var count = steps.Count;
        var pools = _threadTokenArraysByLen;
        if (pools == null)
        {
            pools = new Token<CallChainTokenKind>[MaxChainDepth + MaxParentBlockWalk + MaxVariableHops][];
            _threadTokenArraysByLen = pools;
        }

        Token<CallChainTokenKind>[] tokens;
        if (count < pools.Length)
        {
            tokens = pools[count] ??= new Token<CallChainTokenKind>[count];
        }
        else
        {
            tokens = new Token<CallChainTokenKind>[count];
        }

        for (var i = 0; i < count; i++)
        {
            var span = i < IndexedTokenSpans.Length
                ? IndexedTokenSpans[i]
                : IndexedTokenSpans[^1];
            tokens[i] = new Token<CallChainTokenKind>(steps[i].Kind, span);
        }
        return new TokenList<CallChainTokenKind>(tokens);
    }

    private static List<CallChainStep> LinearizeCallChain(
        Node tailCallNode,
        FrozenSet<string> targetSelectors,
        FrozenSet<string> readTerminals,
        FrozenSet<string> writeTerminals,
        FrozenSet<string> requiredReceivers,
        out bool receiverMatched)
    {
        var outermostCall = FindOutermostChainedCall(tailCallNode);
        var reversed = GetThreadStepBuffer();
        UnrollCallNodeChain(
            outermostCall,
            targetSelectors,
            readTerminals,
            writeTerminals,
            requiredReceivers,
            isTail: true,
            reversed,
            depth: 0,
            out receiverMatched);
        reversed.Reverse();
        return reversed;
    }

    private static List<CallChainStep> LinearizeCallChain(
        Node tailCallNode,
        FrozenSet<string> targetSelectors,
        FrozenSet<string> readTerminals,
        FrozenSet<string> writeTerminals) =>
        LinearizeCallChain(tailCallNode, targetSelectors, readTerminals, writeTerminals, FrozenSet<string>.Empty, out _);

    private static Node FindOutermostChainedCall(Node startCallNode)
    {
        var curr = startCallNode;
        var depth = 0;
        while (curr.IsValid() && depth++ < MaxChainDepth)
        {
            var parent = curr.Parent;
            if (!parent.IsValid()) break;

            // Skip transparent wrappers (parentheses, await, non-null assertion)
            while (parent is ParenthesizedExprSyntax or UnaryExprSyntax { IsAwait: true } or UnaryExprSyntax { IsTryPropagate: true })
            {
                parent = parent.Parent;
            }
            if (parent == null) break;

            Node? candidateCall = null;
            if (IsCallLikeNode(parent))
            {
                candidateCall = parent;
            }
            else if (parent.Parent != null && IsCallLikeNode(parent.Parent))
            {
                candidateCall = parent.Parent;
            }

            if (candidateCall != null &&
                candidateCall != curr &&
                UnwrapTransparentReceiver(GetReceiverNode(candidateCall)) == curr)
            {
                curr = candidateCall;
                continue;
            }

            break;
        }
        return curr;
    }

    private static void UnrollCallNodeChain(
        Node curr,
        FrozenSet<string> targetSelectors,
        FrozenSet<string> readTerminals,
        FrozenSet<string> writeTerminals,
        FrozenSet<string> requiredReceivers,
        bool isTail,
        List<CallChainStep> reversed,
        int depth,
        out bool receiverMatched)
    {
        receiverMatched = requiredReceivers.Count == 0;
        var current = curr;
        var tail = isTail;
        var steps = depth;
        ReadOnlySpan<char> visitedVar0 = default;
        ReadOnlySpan<char> visitedVar1 = default;

        while (current.IsValid() && steps < MaxChainDepth)
        {
            steps++;
            var methodSpan = DeclarativeSemanticExtension.GetCallMethodSpan(current);
            if (methodSpan.IsEmpty) break;

            var kind = ClassifyChainStepSpan(methodSpan, current, tail, targetSelectors, readTerminals, writeTerminals);
            reversed.Add(new CallChainStep(kind, current));
            tail = false;

            var recvNode = UnwrapTransparentReceiver(GetReceiverNode(current));
            if (recvNode == null || !recvNode.IsValid()) break;

            if (!receiverMatched && DeclarativeUniversalRuleEvaluator.SpanContainsAny(recvNode.AsSpan(), requiredReceivers))
            {
                receiverMatched = true;
            }

            if (IsCallLikeNode(recvNode))
            {
                current = recvNode;
                continue;
            }

            if (recvNode is IdentifierExprSyntax)
            {
                var varSpan = recvNode.AsSpan().TrimStart(DollarChar);
                if (!varSpan.IsEmpty &&
                    !varSpan.SequenceEqual(visitedVar0) &&
                    !varSpan.SequenceEqual(visitedVar1))
                {
                    var initNode = TryFindVariableInitializerCallNodeSpan(current, varSpan);
                    if (initNode != null && initNode.IsValid())
                    {
                        if (visitedVar0.IsEmpty) visitedVar0 = varSpan;
                        else visitedVar1 = varSpan;

                        current = initNode;
                        continue;
                    }
                }

                reversed.Add(new CallChainStep(CallChainTokenKind.Receiver, recvNode));
            }
            break;
        }
    }

    private static List<CallChainStep> LinearizeRouteChain(
        Node routeCallNode,
        FrozenSet<string> groupMethods,
        FrozenSet<string> requiredReceivers,
        out bool chainMatchedReceiver)
    {
        chainMatchedReceiver = requiredReceivers.Count == 0;
        var reversed = GetThreadStepBuffer();
        var groupLookup = groupMethods.GetAlternateLookup<ReadOnlySpan<char>>();
        ReadOnlySpan<char> visitedVar0 = default;
        ReadOnlySpan<char> visitedVar1 = default;

        // 1. Walk receiver chain (and variable initializers) from the route call excluding the terminal verb call itself
        var hasRouteGroup = false;
        var recvNode = UnwrapTransparentReceiver(GetReceiverNode(routeCallNode));
        var steps = 0;
        while (recvNode != null && recvNode.IsValid() && steps < MaxChainDepth)
        {
            steps++;
            if (!chainMatchedReceiver && DeclarativeUniversalRuleEvaluator.SpanContainsAny(recvNode.AsSpan(), requiredReceivers))
            {
                chainMatchedReceiver = true;
            }

            if (IsCallLikeNode(recvNode))
            {
                var methodSpan = DeclarativeSemanticExtension.GetCallMethodSpan(recvNode);
                if (!methodSpan.IsEmpty)
                {
                    var isGroup = groupLookup.Contains(methodSpan) && HasTargetArgument(recvNode);
                    if (isGroup) hasRouteGroup = true;
                    var kind = isGroup ? CallChainTokenKind.RouteGroup : CallChainTokenKind.IntermediateStep;
                    reversed.Add(new CallChainStep(kind, recvNode));
                }
                recvNode = UnwrapTransparentReceiver(GetReceiverNode(recvNode));
                continue;
            }

            if (recvNode is IdentifierExprSyntax)
            {
                var varSpan = recvNode.AsSpan().TrimStart(DollarChar);
                if (!varSpan.IsEmpty &&
                    !varSpan.SequenceEqual(visitedVar0) &&
                    !varSpan.SequenceEqual(visitedVar1))
                {
                    var initCall = TryFindVariableInitializerCallNodeSpan(routeCallNode, varSpan);
                    if (initCall != null && initCall.IsValid())
                    {
                        if (visitedVar0.IsEmpty) visitedVar0 = varSpan;
                        else visitedVar1 = varSpan;

                        recvNode = initCall;
                        continue;
                    }
                }
                reversed.Add(new CallChainStep(CallChainTokenKind.Receiver, recvNode));
            }
            break;
        }

        // 2. Walk enclosing AST callback blocks for nested DSL route groups (e.g. Ktor route("/api") { post("/users") })
        var parent = routeCallNode.Parent;
        var parentDepth = 0;
        while (parent.IsValid() && parentDepth < MaxParentBlockWalk)
        {
            parentDepth++;
            if (IsCallLikeNode(parent) && parent != routeCallNode)
            {
                var pMethodSpan = DeclarativeSemanticExtension.GetCallMethodSpan(parent);
                if (!pMethodSpan.IsEmpty && groupLookup.Contains(pMethodSpan) && HasTargetArgument(parent))
                {
                    hasRouteGroup = true;
                    if (!chainMatchedReceiver)
                    {
                        var pRecvSpan = DeclarativeSemanticExtension.GetCallReceiverSpan(parent);
                        if (DeclarativeUniversalRuleEvaluator.SpanContainsAny(pRecvSpan, requiredReceivers))
                        {
                            chainMatchedReceiver = true;
                        }
                    }
                    reversed.Add(new CallChainStep(CallChainTokenKind.RouteGroup, parent));
                }
            }
            parent = parent.Parent;
        }

        if (!hasRouteGroup)
        {
            reversed.Clear();
            return reversed;
        }

        reversed.Reverse();
        return reversed;
    }

    private static CallChainTokenKind ClassifyChainStepSpan(
        ReadOnlySpan<char> methodSpan,
        Node stepNode,
        bool isTail,
        FrozenSet<string> targetSelectors,
        FrozenSet<string> readTerminals,
        FrozenSet<string> writeTerminals)
    {
        var selectorLookup = targetSelectors.GetAlternateLookup<ReadOnlySpan<char>>();
        if (isTail)
        {
            if (writeTerminals.GetAlternateLookup<ReadOnlySpan<char>>().Contains(methodSpan)) return CallChainTokenKind.TerminalWrite;
            if (readTerminals.GetAlternateLookup<ReadOnlySpan<char>>().Contains(methodSpan)) return CallChainTokenKind.TerminalRead;
            if (selectorLookup.Contains(methodSpan) && HasTargetArgument(stepNode)) return CallChainTokenKind.TargetSelector;
            return CallChainTokenKind.TerminalAction;
        }

        if (selectorLookup.Contains(methodSpan) && HasTargetArgument(stepNode))
        {
            return CallChainTokenKind.TargetSelector;
        }

        if (DefaultHttpVerbMethods.GetAlternateLookup<ReadOnlySpan<char>>().Contains(methodSpan))
        {
            var strSpan = DeclarativeCallChainAstHelper.ExtractFirstStringLiteralSpan(stepNode);
            if (!strSpan.IsEmpty && (strSpan.Contains(SlashChar) || strSpan.StartsWith(HttpPrefix.AsSpan(), StringComparison.OrdinalIgnoreCase)))
            {
                return CallChainTokenKind.TargetSelector;
            }
            return CallChainTokenKind.VerbSelector;
        }

        if (DefaultRouteGroupMethods.GetAlternateLookup<ReadOnlySpan<char>>().Contains(methodSpan) && HasTargetArgument(stepNode))
        {
            return CallChainTokenKind.RouteGroup;
        }

        return CallChainTokenKind.IntermediateStep;
    }

    private static bool HasTargetArgument(Node callNode) =>
        DeclarativeCallChainAstHelper.HasTargetArgument(callNode);

    internal static string? ExtractStepTargetArgument(Node? callNode) =>
        DeclarativeCallChainAstHelper.ExtractStepTargetArgument(callNode);

    internal static string? TryExtractGenericTypeFromCall(Node callNode) =>
        DeclarativeCallChainAstHelper.TryExtractGenericTypeFromCall(callNode);

    internal static Node? GetReceiverNode(Node? callNode) =>
        DeclarativeCallChainAstHelper.GetReceiverNode(callNode);

    internal static Node? UnwrapTransparentReceiver(Node? node) =>
        DeclarativeCallChainAstHelper.UnwrapTransparentReceiver(node);

    internal static bool IsCallLikeNode(Node? node) =>
        DeclarativeCallChainAstHelper.IsCallLikeNode(node);

    private static Node? TryFindVariableInitializerCallNodeSpan(Node contextNode, ReadOnlySpan<char> varName) =>
        DeclarativeCallChainAstHelper.TryFindVariableInitializerCallNodeSpan(contextNode, varName);
}
