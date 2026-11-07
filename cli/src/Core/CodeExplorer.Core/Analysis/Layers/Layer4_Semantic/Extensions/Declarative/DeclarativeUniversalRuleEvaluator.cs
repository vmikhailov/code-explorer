using System.Collections.Frozen;
using CodeExplorer.Core.Parser.Syntax;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Core.Parser;

internal static class DeclarativeUniversalRuleEvaluator
{
    private const string ArgumentKind = "argument";
    private const string SlashPrefix = "/";
    private const string HttpPrefix = "http";
    private const char DotChar = '.';
    private const char OpenAngleChar = '<';
    private static readonly char[] QuoteChars = ['"', '\'', '`'];

    internal static string FormatBrokerTopic(string protocol, string topic) =>
        string.IsNullOrEmpty(protocol) ? topic : $"{protocol}:{topic}";

    internal static ReadOnlySpan<char> GetOrResolveCallMethodSpan(Node callNode, ref ReadOnlySpan<char> cachedMethodSpan, ref bool methodNameResolved)
    {
        if (!methodNameResolved)
        {
            cachedMethodSpan = DeclarativeSemanticExtension.GetCallMethodSpan(callNode);
            methodNameResolved = true;
        }
        return cachedMethodSpan;
    }

    internal static bool SpanContainsAny(ReadOnlySpan<char> span, FrozenSet<string> substrings)
    {
        if (span.IsEmpty) return false;
        foreach (var sub in substrings)
        {
            if (span.Contains(sub.AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    internal static bool IsCallNode(Node node, FrozenSet<string> callNodeTypes) =>
        node is CallExprSyntax { CallKind: CallSyntaxKind.Method or CallSyntaxKind.Function or CallSyntaxKind.Constructor } ||
        (node.SyntaxKind == SyntaxKind.CallExpression && callNodeTypes.Contains(node.Type));

    internal static void CollectMessagingRuleReferences(
        Node node,
        ref ReadOnlySpan<char> cachedMethodSpan,
        ref bool methodNameResolved,
        string scopeSymbolId,
        List<Reference> references,
        MessagingBrokerRule rule)
    {
        if ((rule.PublishMethodNames.Count > 0 || rule.SubscribeMethodNames.Count > 0) && IsCallNode(node, rule.CallNodeTypes))
        {
            var methodSpan = GetOrResolveCallMethodSpan(node, ref cachedMethodSpan, ref methodNameResolved);
            if (!methodSpan.IsEmpty)
            {
                DeclarativeSemanticExtension.TryCollectMessagingPublishReferences(node, methodSpan, scopeSymbolId, references, rule);
                DeclarativeSemanticExtension.TryCollectMessagingSubscribeReferences(node, methodSpan, scopeSymbolId, references, rule);
            }
            return;
        }

        if (rule.ConsumerInterfaceSubstrings.Count > 0 && (node is TypeDeclSyntax { Kind: TypeSyntaxKind.Class } || (node.SyntaxKind == SyntaxKind.TypeDeclaration && rule.ClassDeclarationNodeTypes.Contains(node.Type))))
        {
            var msgType = DeclarativeSemanticExtension.ExtractGenericTypeFromInterfaces(node, rule.ConsumerInterfaceSubstrings);
            if (!string.IsNullOrEmpty(msgType))
            {
                references.Add(new Reference(scopeSymbolId, FormatBrokerTopic(rule.Protocol, msgType), OntologyConstants.Relationships.SubscribesTo));
            }
            return;
        }

        if (node.SyntaxKind is not (SyntaxKind.Annotation or SyntaxKind.FunctionDeclaration or SyntaxKind.SyntaxList))
        {
            return;
        }

        if (DeclarativeSemanticExtension.MatchesMessagingListenerAnnotation(node, rule))
        {
            var topics = DeclarativeSemanticExtension.ExtractMessagingTopics(node, rule);
            foreach (var topic in topics)
            {
                references.Add(new Reference(scopeSymbolId, FormatBrokerTopic(rule.Protocol, topic), OntologyConstants.Relationships.SubscribesTo));
            }

            var methodName = DeclarativeSemanticExtension.FindEnclosingMethodName(node);
            if (!string.IsNullOrEmpty(methodName))
            {
                references.Add(new Reference(scopeSymbolId, methodName, OntologyConstants.Relationships.Triggers));
            }
        }
        else if (DeclarativeSemanticExtension.MatchesMessagingProducerAnnotation(node, rule))
        {
            var topics = DeclarativeSemanticExtension.ExtractMessagingTopics(node, rule);
            foreach (var topic in topics)
            {
                references.Add(new Reference(scopeSymbolId, FormatBrokerTopic(rule.Protocol, topic), OntologyConstants.Relationships.PublishesTo));
            }
        }
        else if (DeclarativeSemanticExtension.MatchesMessagingChannelAnnotation(node, rule))
        {
            var topics = DeclarativeSemanticExtension.ExtractMessagingTopics(node, rule);
            var isEmitter = DeclarativeSemanticExtension.IsChannelEmitter(node);
            var rel = isEmitter ? OntologyConstants.Relationships.PublishesTo : OntologyConstants.Relationships.SubscribesTo;
            foreach (var topic in topics)
            {
                references.Add(new Reference(scopeSymbolId, FormatBrokerTopic(rule.Protocol, topic), rel));
            }
        }
        else if (DeclarativeSemanticExtension.MatchesMethodLevelEventListener(node, rule))
        {
            var eventType = DeclarativeSemanticExtension.ExtractFirstParameterType(node);
            if (!string.IsNullOrEmpty(eventType))
            {
                references.Add(new Reference(scopeSymbolId, FormatBrokerTopic(rule.Protocol, eventType), OntologyConstants.Relationships.SubscribesTo));
            }
        }
    }

    internal static bool TryMatchInboundRouteCall(
        Node callNode,
        InboundRouteCallRule rule,
        out string routeId,
        out string? handlerName)
    {
        var cachedMethodSpan = ReadOnlySpan<char>.Empty;
        var methodNameResolved = false;
        return TryMatchInboundRouteCall(callNode, ref cachedMethodSpan, ref methodNameResolved, rule, out routeId, out handlerName);
    }

    internal static bool TryMatchInboundRouteCall(
        Node callNode,
        ref ReadOnlySpan<char> cachedMethodSpan,
        ref bool methodNameResolved,
        InboundRouteCallRule rule,
        out string routeId,
        out string? handlerName)
    {
        routeId = string.Empty;
        handlerName = null;

        if (!IsCallNode(callNode, rule.CallNodeTypes)) return false;

        var methodSpan = GetOrResolveCallMethodSpan(callNode, ref cachedMethodSpan, ref methodNameResolved);
        if (methodSpan.IsEmpty || !rule.MethodToHttpVerb.GetAlternateLookup<ReadOnlySpan<char>>().TryGetValue(methodSpan, out var verb))
            return false;

        if (rule.ReceiverSubstrings.Count > 0 || rule.ExcludedReceivers.Count > 0)
        {
            var objSpan = DeclarativeSemanticExtension.GetCallReceiverSpan(callNode);
            if (rule.ReceiverSubstrings.Count > 0 && !SpanContainsAny(objSpan, rule.ReceiverSubstrings))
            {
                return false;
            }
            if (rule.ExcludedReceivers.Count > 0 && !objSpan.IsEmpty && rule.ExcludedReceivers.GetAlternateLookup<ReadOnlySpan<char>>().Contains(objSpan))
            {
                return false;
            }
        }

        if (!string.IsNullOrEmpty(rule.StaticRoute))
        {
            routeId = string.IsNullOrEmpty(verb)
                ? rule.StaticRoute
                : $"{verb}:{DeclarativeSemanticExtension.CombineRoutes(rule.RoutePrefix, DeclarativeSemanticExtension.NormalizeRoute(rule.StaticRoute))}";
            return true;
        }

        if (rule.RequireLeadingSlashOnPath && CountCallArguments(callNode) < 2)
        {
            return false;
        }

        var route = ExtractStringArgAtOrFirst(callNode, rule.PathArgumentIndex);
        if (rule.RequireLeadingSlashOnPath && (string.IsNullOrEmpty(route) || !route.StartsWith(SlashPrefix, StringComparison.Ordinal)))
        {
            return false;
        }

        if (string.IsNullOrEmpty(route))
        {
            if (!rule.EmitEntryPointNode) return false;
            route = SlashPrefix;
        }

        var combined = DeclarativeSemanticExtension.CombineRoutes(rule.RoutePrefix, DeclarativeSemanticExtension.NormalizeRoute(route));
        routeId = string.IsNullOrEmpty(verb) ? combined : $"{verb}:{combined}";

        if (rule.EmitHandlerTriggers)
        {
            handlerName = ExtractLastIdentifierArg(callNode);
        }

        return true;
    }

    private static int CountCallArguments(Node callNode)
    {
        if (callNode is CallExprSyntax callExpr) return callExpr.Arguments.Count;
        var argList = DeclarativeSemanticExtension.FindArgumentsNode(callNode);
        if (!argList.IsValid()) return 0;
        var count = 0;
        for (var i = 0; i < argList.Children.Count; i++)
        {
            if (argList.Children[i].Type is not ("(" or ")" or ",")) count++;
        }
        return count;
    }

    private static string? ExtractLastIdentifierArg(Node callNode)
    {
        if (callNode is CallExprSyntax { Arguments.Count: > 0 } callExpr)
        {
            Node last = callExpr.Arguments[^1];
            if (last is ArgumentSyntax { Value: { } v }) last = v;
            if (last is IdentifierExprSyntax) return last.Text;
        }
        var argList = DeclarativeSemanticExtension.FindArgumentsNode(callNode);
        if (!argList.IsValid()) return null;
        for (var i = argList.Children.Count - 1; i >= 0; i--)
        {
            var child = argList.Children[i];
            if (child.Type is "(" or ")" or ",") continue;
            if (child is IdentifierExprSyntax || child.Type == "identifier") return child.Text;
            break;
        }
        return null;
    }

    internal static void TryCollectInboundRouteCallReferences(
        Node callNode,
        ref ReadOnlySpan<char> cachedMethodSpan,
        ref bool methodNameResolved,
        string scopeSymbolId,
        List<Reference> references,
        InboundRouteCallRule rule)
    {
        if (!TryMatchInboundRouteCall(callNode, ref cachedMethodSpan, ref methodNameResolved, rule, out var routeId, out var handlerName))
            return;

        if (rule.EmitEntryPointNode)
        {
            if (rule.EmitHandlerTriggers && !string.IsNullOrEmpty(handlerName))
            {
                references.Add(new Reference(scopeSymbolId, handlerName, OntologyConstants.Relationships.Triggers));
            }
            return;
        }

        references.Add(new Reference(scopeSymbolId, routeId, OntologyConstants.Relationships.Exposes));
    }

    internal static void TryCollectDataStoreAccessReferences(
        Node callNode,
        ref ReadOnlySpan<char> cachedMethodSpan,
        ref bool methodNameResolved,
        string scopeSymbolId,
        List<Reference> references,
        DataStoreAccessRule rule)
    {
        if (!IsCallNode(callNode, rule.CallNodeTypes)) return;

        var methodSpan = GetOrResolveCallMethodSpan(callNode, ref cachedMethodSpan, ref methodNameResolved);
        if (methodSpan.IsEmpty || !rule.CallMethodNames.GetAlternateLookup<ReadOnlySpan<char>>().Contains(methodSpan))
            return;

        if (rule.ReceiverSubstrings.Count > 0)
        {
            var objSpan = DeclarativeSemanticExtension.GetCallReceiverSpan(callNode);
            if (!SpanContainsAny(objSpan, rule.ReceiverSubstrings))
            {
                return;
            }
        }

        if (!string.IsNullOrEmpty(rule.StaticTargetName))
        {
            references.Add(new Reference(scopeSymbolId, rule.StaticTargetName, OntologyConstants.Relationships.UsesDb));
            if (rule.ParseSqlFromTarget)
            {
                var sql = ExtractStringArgAtOrFirst(callNode, 0);
                if (!string.IsNullOrEmpty(sql))
                {
                    NestedSqlParser.TryDetectSqlDependencies(sql, scopeSymbolId, references);
                }
            }
            return;
        }

        var target = ExtractStringArgAtOrFirst(callNode, rule.TableArgumentIndex);
        if (!string.IsNullOrEmpty(target))
        {
            references.Add(new Reference(scopeSymbolId, target, OntologyConstants.Relationships.UsesDb));
        }
    }

    internal static void TryCollectServiceCallSinkReferences(
        Node callNode,
        ref ReadOnlySpan<char> cachedMethodSpan,
        ref bool methodNameResolved,
        string scopeSymbolId,
        List<Reference> references,
        ServiceCallSinkRule rule)
    {
        if (!IsCallNode(callNode, rule.CallNodeTypes)) return;

        var methodSpan = GetOrResolveCallMethodSpan(callNode, ref cachedMethodSpan, ref methodNameResolved);
        if (methodSpan.IsEmpty || !rule.CallMethodNames.GetAlternateLookup<ReadOnlySpan<char>>().Contains(methodSpan))
            return;

        if (rule.ReceiverSubstrings.Count > 0)
        {
            var objSpan = DeclarativeSemanticExtension.GetCallReceiverSpan(callNode);
            if (!SpanContainsAny(objSpan, rule.ReceiverSubstrings))
            {
                return;
            }
        }

        if (!string.IsNullOrEmpty(rule.StaticTargetName))
        {
            references.Add(new Reference(scopeSymbolId, rule.StaticTargetName, rule.TargetRelation));
            return;
        }

        if (rule.UseMethodNameAsTarget)
        {
            var suffix = rule.LowercaseMethodNameTarget ? methodSpan.ToString().ToLowerInvariant() : methodSpan.ToString();
            references.Add(new Reference(scopeSymbolId, $"{rule.TargetPrefix}{suffix}", rule.TargetRelation));
            return;
        }

        if (rule.CollectAllIdentifierArguments)
        {
            var argList = DeclarativeSemanticExtension.FindArgumentsNode(callNode);
            if (!argList.IsValid()) return;
            for (var i = 0; i < argList.Children.Count; i++)
            {
                var arg = argList.Children[i];
                if (arg.Type is "(" or ")" or ",") continue;
                var span = arg.AsSpan().Trim(QuoteChars);
                if (!span.IsEmpty)
                {
                    references.Add(new Reference(scopeSymbolId, $"{rule.TargetPrefix}{span}", rule.TargetRelation));
                }
            }
            return;
        }

        var target = ExtractStringArgAtOrFirst(callNode, rule.TargetArgumentIndex);
        if (string.IsNullOrEmpty(target) && rule.AllowIdentifierArgument)
        {
            target = ExtractIdentifierArgAt(callNode, rule.TargetArgumentIndex);
        }
        if (string.IsNullOrEmpty(target)) return;

        if (rule.RequireUrlOrPathArgument &&
            !target.StartsWith(SlashPrefix, StringComparison.Ordinal) &&
            !target.StartsWith(HttpPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        references.Add(new Reference(scopeSymbolId, $"{rule.TargetPrefix}{target}", rule.TargetRelation));
    }

    internal static string? ExtractStringArgAtOrFirst(Node callNode, int index)
    {
        if (callNode is CallExprSyntax { Arguments.Count: > 0 } callExpr)
        {
            string? firstDirect = null;
            for (var i = 0; i < callExpr.Arguments.Count; i++)
            {
                var str = ExtractSingleStringFromArg(callExpr.Arguments[i]);
                if (!string.IsNullOrEmpty(str))
                {
                    if (i == index) return str;
                    firstDirect ??= str;
                }
            }
            if (firstDirect != null) return firstDirect;
        }

        var argList = DeclarativeSemanticExtension.FindArgumentsNode(callNode);
        if (!argList.IsValid()) return null;

        string? firstCandidate = null;
        var argPos = 0;
        for (var i = 0; i < argList.Children.Count; i++)
        {
            var child = argList.Children[i];
            if (child.Type is "(" or ")" or ",") continue;
            var str = ExtractSingleStringFromArg(child);
            if (!string.IsNullOrEmpty(str))
            {
                if (argPos == index) return str;
                firstCandidate ??= str;
            }
            argPos++;
        }

        return firstCandidate;
    }

    internal static string? ExtractSingleStringFromArg(Node argNode)
    {
        if (argNode is ArgumentSyntax { Value: { } argVal })
        {
            argNode = argVal;
        }
        else if (argNode.SyntaxKind == SyntaxKind.Argument && argNode.Children.Count > 0)
        {
            argNode = argNode.Children[^1];
        }

        if (DeclarativeSemanticExtension.IsStringLiteral(argNode))
        {
            return argNode.AsSpan().Trim(QuoteChars).ToString();
        }

        for (var i = 0; i < argNode.Children.Count; i++)
        {
            var c = argNode.Children[i];
            if (DeclarativeSemanticExtension.IsStringLiteral(c))
            {
                return c.AsSpan().Trim(QuoteChars).ToString();
            }
        }
        return null;
    }

    internal static string? ExtractIdentifierArgAt(Node callNode, int index)
    {
        if (callNode is CallExprSyntax { Arguments.Count: > 0 } callExpr && index < callExpr.Arguments.Count)
        {
            var rawSpan = callExpr.Arguments[index].AsSpan().Trim().Trim(QuoteChars);
            var leaf = ExtractLeafMethodSpan(rawSpan);
            if (!leaf.IsEmpty) return leaf.ToString();
        }

        var argList = DeclarativeSemanticExtension.FindArgumentsNode(callNode);
        if (!argList.IsValid()) return null;

        var argPos = 0;
        for (var i = 0; i < argList.Children.Count; i++)
        {
            var child = argList.Children[i];
            if (child.Type is "(" or ")" or ",") continue;
            if (argPos == index)
            {
                var rawSpan = child.AsSpan().Trim().Trim(QuoteChars);
                var leaf = ExtractLeafMethodSpan(rawSpan);
                return leaf.IsEmpty ? null : leaf.ToString();
            }
            argPos++;
        }
        return null;
    }

    internal static ReadOnlySpan<char> ExtractLeafMethodSpan(ReadOnlySpan<char> raw)
    {
        var text = StripGenericTypeSuffixSpan(raw);
        var dot = text.LastIndexOf(DotChar);
        return dot >= 0 ? text[(dot + 1)..].Trim() : text.Trim();
    }

    internal static ReadOnlySpan<char> StripGenericTypeSuffixSpan(ReadOnlySpan<char> raw)
    {
        var angle = raw.IndexOf(OpenAngleChar);
        return angle > 0 ? raw[..angle].Trim() : raw.Trim();
    }

    internal static string ExtractLeafMethodName(string raw) => ExtractLeafMethodSpan(raw.AsSpan()).ToString();

    internal static string StripGenericTypeSuffix(string raw) => StripGenericTypeSuffixSpan(raw.AsSpan()).ToString();
}
