using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public class GcpLibraryParser : ISemanticExtension
{
    public string Type => OntologyConstants.LibraryTypes.Queue;
    public string Name => "GCP";
    public string Id => "gcp";
    public IReadOnlyList<string> SupportedPatterns =>
    [
        "@google-cloud",
        "@google-cloud/*",
        "firebase",
        "firebase-admin",
        "*pubsub*",
        "*pub-sub*"
    ];
    public bool IsImplemented => true;
    public LibraryRole LibraryRole => LibraryRole.CloudSdk;

    public string? MapNodeType(Node node, ParsingContext ctx) => null;
    public string? ExtractIdentifier(Node node, ParsingContext ctx) => null;

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (!node.Is(TreeSitterSyntax.TypeScript.CallExpression)) return;

        var funcNode = node.GetFunctionNode();
        if (!funcNode.IsValid()) return;

        var funcText = funcNode.Text;
        var args = AstHelper.GetCallArguments(node);

        // --- 1. PUBLISHERS ---

        // --- 1. PUBLISHERS ---

        // A. Direct pubsub.topic(topicName)
        if (funcText.EndsWith(".topic", StringComparison.Ordinal))
        {
            if (IsChainedSubscription(node))
            {
                return;
            }

            var enclosingScope = FindEnclosingScope(node);
            var isSubscriberOnly = enclosingScope.IsValid() && IsSubscriberOnlyScope(enclosingScope);

            if (args.Count > 0)
            {
                if (!AstValueResolver.TryResolveTopicOrQueue(args[0], scopeSymbolId, out var topic) || string.IsNullOrEmpty(topic))
                {
                    topic = AstHelper.ResolveTopicOrQueue(args[0], scopeSymbolId);
                }

                if (isSubscriberOnly && enclosingScope.IsValid())
                {
                    var scopeText = enclosingScope.Text;
                    if (scopeText.Contains("initSubscription", StringComparison.Ordinal) ||
                        scopeText.Contains("on('message'", StringComparison.Ordinal) ||
                        scopeText.Contains("on(\"message\"", StringComparison.Ordinal))
                    {
                        if (!scopeText.Contains(".subscription(", StringComparison.Ordinal) &&
                            !scopeText.Contains(".createSubscription(", StringComparison.Ordinal))
                        {
                            AddSubscribeReference(references, scopeSymbolId, topic);
                        }
                    }
                }
                else
                {
                    AddPublishReference(references, scopeSymbolId, topic);
                }
            }
        }
        // B. Chained publish: pubsub.topic(topicName).publishMessage(...) or pubsub.topic(topicName).publish(...)
        else if ((funcText.EndsWith(".publishMessage", StringComparison.Ordinal) ||
                  funcText.EndsWith(".publish", StringComparison.Ordinal)) &&
                 funcNode.Is(TreeSitterSyntax.TypeScript.MemberExpression))
        {
            var obj = funcNode.GetField(TreeSitterSyntax.Fields.Object);
            string? topic = null;

            // Check if object is a call like pubsub.topic(topicName)
            if (obj.IsValid() && obj.Is(TreeSitterSyntax.TypeScript.CallExpression))
            {
                var innerFunc = obj.GetFunctionNode();
                if (innerFunc.IsValid() && innerFunc.Text.EndsWith(".topic", StringComparison.Ordinal))
                {
                    var innerArgs = AstHelper.GetCallArguments(obj);
                    if (innerArgs.Count > 0)
                    {
                        if (!AstValueResolver.TryResolveTopicOrQueue(innerArgs[0], scopeSymbolId, out topic) || string.IsNullOrEmpty(topic))
                        {
                            topic = AstHelper.ResolveTopicOrQueue(innerArgs[0], scopeSymbolId);
                        }
                    }
                }
            }

            // Check if arguments contain { topicName: ... } first (e.g. pubSubService.publishMessage({ topicName: ... }))
            if (string.IsNullOrEmpty(topic) && args.Count > 0 && args[0].Is(TreeSitterSyntax.TypeScript.Object))
            {
                if (AstHelper.TryGetObjectProperty(args[0], "topicName", out var tp) ||
                    AstHelper.TryGetObjectProperty(args[0], "topic", out tp))
                {
                    if (!AstValueResolver.TryResolveTopicOrQueue(tp, scopeSymbolId, out topic) || string.IsNullOrEmpty(topic))
                    {
                        topic = AstHelper.ResolveTopicOrQueue(tp, scopeSymbolId);
                    }
                }
            }

            // Check receiver variable name (e.g. this.topic, this.ruleTreeTopic, customTopic)
            if (string.IsNullOrEmpty(topic) && obj.IsValid())
            {
                topic = ResolveTopicFromReceiverInScope(node, obj, scopeSymbolId);
            }

            // If not found from receiver call or variable, check non-object first argument
            if (string.IsNullOrEmpty(topic) && args.Count > 0 && !args[0].Is(TreeSitterSyntax.TypeScript.Object))
            {
                if (!AstValueResolver.TryResolveTopicOrQueue(args[0], scopeSymbolId, out topic) || string.IsNullOrEmpty(topic))
                {
                    topic = AstHelper.ResolveTopicOrQueue(args[0], scopeSymbolId);
                }
            }

            AddPublishReference(references, scopeSymbolId, topic);
        }

        // --- 2. SUBSCRIBERS ---

        // A. Subscription setup: subscribeToMessages, initSubscription, listenSubscription, subscription, createSubscription, subscribe
        else if (funcText.EndsWith(".subscribeToMessages", StringComparison.Ordinal) ||
                 funcText.EndsWith(".initSubscription", StringComparison.Ordinal) ||
                 funcText.EndsWith(".listenSubscription", StringComparison.Ordinal) ||
                 funcText.EndsWith(".createSubscription", StringComparison.Ordinal) ||
                 funcText.EndsWith(".subscription", StringComparison.Ordinal) ||
                 funcText.EndsWith(".subscribe", StringComparison.Ordinal))
        {
            if (funcText.EndsWith(".subscription", StringComparison.Ordinal) ||
                funcText.EndsWith(".createSubscription", StringComparison.Ordinal))
            {
                string? topic = null;
                if (funcNode.Is(TreeSitterSyntax.TypeScript.MemberExpression))
                {
                    var obj = funcNode.GetField(TreeSitterSyntax.Fields.Object);
                    if (obj.IsValid())
                    {
                        topic = ResolveTopicFromReceiverInScope(node, obj, scopeSymbolId);
                    }
                }

                string? sub = null;
                if (args.Count > 0)
                {
                    if (!AstValueResolver.TryResolveTopicOrQueue(args[0], scopeSymbolId, out sub) || string.IsNullOrEmpty(sub))
                    {
                        sub = AstHelper.ResolveTopicOrQueue(args[0], scopeSymbolId);
                    }
                    if (string.IsNullOrEmpty(sub))
                    {
                        sub = ResolveTopicVariableInScope(node, args[0].Text, scopeSymbolId);
                    }
                }

                var finalTopic = IsValidTopicName(topic) ? topic : sub;
                AddSubscribeReference(references, scopeSymbolId, finalTopic);
            }
            else if (funcText.EndsWith(".initSubscription", StringComparison.Ordinal))
            {
                var isCallback = args.Count > 0 && (args[0].Is(TreeSitterSyntax.TypeScript.ArrowFunction) ||
                                                    args[0].Is(TreeSitterSyntax.TypeScript.FunctionExpression) ||
                                                    args[0].Is(TreeSitterSyntax.Common.FunctionDeclaration));

                if (isCallback || args.Count == 0)
                {
                    if (ConstantRegistry.TryResolve(scopeSymbolId, "receiveTopicName", out var recTopic) && IsValidTopicName(recTopic))
                    {
                        AddSubscribeReference(references, scopeSymbolId, recTopic);
                    }
                    else if (ConstantRegistry.TryResolve(scopeSymbolId, "subscriptionName", out var subTopic) && IsValidTopicName(subTopic))
                    {
                        AddSubscribeReference(references, scopeSymbolId, subTopic);
                    }
                }
                else
                {
                    if (!AstValueResolver.TryResolveTopicOrQueue(args[0], scopeSymbolId, out var sub) || string.IsNullOrEmpty(sub))
                    {
                        sub = AstHelper.ResolveTopicOrQueue(args[0], scopeSymbolId);
                    }
                    AddSubscribeReference(references, scopeSymbolId, sub);
                }
            }
            else
            {
                if (args.Count > 0)
                {
                    if (!AstValueResolver.TryResolveTopicOrQueue(args[0], scopeSymbolId, out var sub) || string.IsNullOrEmpty(sub))
                    {
                        sub = AstHelper.ResolveTopicOrQueue(args[0], scopeSymbolId);
                    }
                    if (string.IsNullOrEmpty(sub))
                    {
                        sub = ResolveTopicVariableInScope(node, args[0].Text, scopeSymbolId);
                    }
                    AddSubscribeReference(references, scopeSymbolId, sub);
                }
            }
        }

        // --- 3. DECLARATIVE MODULE CONFIGURATION ---
        if (funcText.Contains("PubSubModule", StringComparison.OrdinalIgnoreCase) ||
            funcText.EndsWith(".forRootWithAuthFile", StringComparison.Ordinal) ||
            funcText.EndsWith(".forRoot", StringComparison.Ordinal) ||
            funcText.EndsWith(".register", StringComparison.Ordinal))
        {
            CollectDeclarativePubSubModuleTopics(node, scopeSymbolId, references);
        }
    }

    private static void AddPublishReference(List<Reference> references, string scopeSymbolId, string? topic)
    {
        if (IsValidTopicName(topic))
        {
            references.Add(new Reference("", "gcp:" + topic!.Trim(), OntologyConstants.Relationships.PublishesTo));
        }
    }

    private static void AddSubscribeReference(List<Reference> references, string scopeSymbolId, string? topic)
    {
        if (IsValidTopicName(topic))
        {
            references.Add(new Reference("", "gcp:" + topic!.Trim(), OntologyConstants.Relationships.SubscribesTo));
        }
    }

    private static bool IsValidTopicName(string? topic)
    {
        return WorkspaceConventions.IsValidTopicOrQueueName(topic);
    }

    private static bool IsChainedSubscription(Node callNode)
    {
        var curr = callNode.Parent;
        while (curr.IsValid() && (curr.Is("parenthesized_expression") || curr.Is("await_expression")))
        {
            curr = curr.Parent;
        }
        if (curr.IsValid() && curr.Is(TreeSitterSyntax.TypeScript.MemberExpression))
        {
            var memberName = curr.GetField(TreeSitterSyntax.Fields.Property);
            var text = memberName.IsValid() ? memberName.Text : curr.Text;
            if (text.EndsWith("subscription", StringComparison.OrdinalIgnoreCase) ||
                text.EndsWith("createSubscription", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    private static Node? FindEnclosingScope(Node node)
    {
        var curr = node.Parent;
        while (curr.IsValid())
        {
            if (curr.IsAny(
                TreeSitterSyntax.TypeScript.MethodDefinition,
                TreeSitterSyntax.Common.MethodDeclaration,
                TreeSitterSyntax.Common.FunctionDeclaration,
                TreeSitterSyntax.TypeScript.ArrowFunction,
                TreeSitterSyntax.TypeScript.FunctionExpression))
            {
                return curr;
            }
            if (curr.Is(TreeSitterSyntax.Common.ClassDeclaration))
            {
                return curr;
            }
            curr = curr.Parent;
        }
        return null;
    }

    private static bool IsSubscriberOnlyScope(Node scopeNode)
    {
        if (!scopeNode.IsValid()) return false;
        var text = scopeNode.Text;
        var hasSubscriber = text.Contains(".createSubscription", StringComparison.Ordinal) ||
                            text.Contains(".subscription(", StringComparison.Ordinal) ||
                            text.Contains(".initSubscription(", StringComparison.Ordinal) ||
                            text.Contains(".subscribeToMessages(", StringComparison.Ordinal) ||
                            text.Contains(".listenSubscription(", StringComparison.Ordinal) ||
                            text.Contains(".on('message'", StringComparison.Ordinal) ||
                            text.Contains(".on(\"message\"", StringComparison.Ordinal);

        var hasPublisher = text.Contains(".publishMessage(", StringComparison.Ordinal) ||
                           text.Contains(".publishJSON(", StringComparison.Ordinal) ||
                           text.Contains(".publish(", StringComparison.Ordinal);

        return hasSubscriber && !hasPublisher;
    }

    private static string? ResolveTopicFromReceiverInScope(Node callNode, Node receiverObj, string? scopeSymbolId)
    {
        // 1. If receiverObj is itself a call or wrapped call, e.g. pubsub.topic(X).subscription(Y)
        var unwrappedObj = receiverObj;
        while (unwrappedObj.IsValid() && (unwrappedObj.Is("parenthesized_expression") || unwrappedObj.Is("await_expression")))
        {
            unwrappedObj = unwrappedObj.Children.FirstOrDefault(c => c.IsValid() && c.Type is not "await" and not "(" and not ")") ?? unwrappedObj;
        }

        if (unwrappedObj.IsValid() && unwrappedObj.Is(TreeSitterSyntax.TypeScript.CallExpression))
        {
            var fn = unwrappedObj.GetFunctionNode();
            if (fn.IsValid() && fn.Text.EndsWith(".topic", StringComparison.Ordinal))
            {
                var innerArgs = AstHelper.GetCallArguments(unwrappedObj);
                if (innerArgs.Count > 0)
                {
                    if (AstValueResolver.TryResolveTopicOrQueue(innerArgs[0], scopeSymbolId, out var innerT) && IsValidTopicName(innerT))
                    {
                        return innerT;
                    }
                    var helperT = AstHelper.ResolveTopicOrQueue(innerArgs[0], scopeSymbolId);
                    if (IsValidTopicName(helperT)) return helperT;
                }
            }
        }

        var recvText = receiverObj.Text.Trim();
        var cleanName = recvText.StartsWith("this.", StringComparison.OrdinalIgnoreCase) ? recvText[5..].Trim() : recvText;
        if (cleanName.EndsWith("Service", StringComparison.OrdinalIgnoreCase) ||
            cleanName.EndsWith("Client", StringComparison.OrdinalIgnoreCase) ||
            cleanName.Equals("pubsub", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        // 2. Search enclosing scopes for declarations or assignments of recvText
        var curr = callNode.Parent;
        while (curr.IsValid())
        {
            if (AstScopeResolver.IsScopeBoundary(curr.Type))
            {
                var declarators = curr.FindDescendantsOfType(TreeSitterSyntax.TypeScript.VariableDeclarator);
                foreach (var decl in declarators)
                {
                    var nameNode = decl.GetField(TreeSitterSyntax.Fields.Name) ??
                                   decl.FindChildOfType(TreeSitterSyntax.TypeScript.Identifier);
                    if (nameNode.IsValid() && (nameNode.Text == cleanName || nameNode.Text == recvText))
                    {
                        var valNode = decl.GetField(TreeSitterSyntax.Fields.Value) ??
                                      (decl.Children.Count >= 3 && decl.Children[1].Type == "=" ? decl.Children[2] : null);
                        if (valNode.IsValid())
                        {
                            var topic = ExtractTopicFromExpression(valNode, scopeSymbolId);
                            if (IsValidTopicName(topic)) return topic;
                        }
                    }
                }

                var assignments = curr.FindDescendantsOfType(TreeSitterSyntax.Common.AssignmentExpression);
                foreach (var assign in assignments)
                {
                    var left = assign.GetField(TreeSitterSyntax.Fields.Left);
                    var right = assign.GetField(TreeSitterSyntax.Fields.Right);
                    if (left.IsValid() && (left.Text == cleanName || left.Text == recvText || left.Text.EndsWith($".{cleanName}")) && right.IsValid())
                    {
                        var topic = ExtractTopicFromExpression(right, scopeSymbolId);
                        if (IsValidTopicName(topic)) return topic;
                    }
                }
            }
            curr = curr.Parent;
        }

        // 3. Class field definitions: readonly topic: Topic = this.pubsub.topic(...)
        var fieldInit = AstScopeResolver.FindClassFieldInitializer(callNode, cleanName);
        if (fieldInit.IsValid())
        {
            var topic = ExtractTopicFromExpression(fieldInit, scopeSymbolId);
            if (IsValidTopicName(topic)) return topic;
        }

        // 4. Try ConstantRegistry with receiver variable name
        return ResolveTopicVariableInScope(callNode, recvText, scopeSymbolId);
    }

    private static string? ExtractTopicFromExpression(Node expr, string? scopeSymbolId)
    {
        var unwrapped = expr;
        while (unwrapped.IsValid() && (unwrapped.Is("parenthesized_expression") || unwrapped.Is("await_expression")))
        {
            unwrapped = unwrapped.Children.FirstOrDefault(c => c.IsValid() && c.Type is not "await" and not "(" and not ")") ?? unwrapped;
        }

        if (unwrapped.Is(TreeSitterSyntax.TypeScript.CallExpression))
        {
            var fn = unwrapped.GetFunctionNode();
            if (fn.IsValid() && fn.Text.EndsWith(".topic", StringComparison.Ordinal))
            {
                var callArgs = AstHelper.GetCallArguments(unwrapped);
                if (callArgs.Count > 0)
                {
                    if (AstValueResolver.TryResolveTopicOrQueue(callArgs[0], scopeSymbolId, out var t) && IsValidTopicName(t))
                    {
                        return t;
                    }
                    var helperT = AstHelper.ResolveTopicOrQueue(callArgs[0], scopeSymbolId);
                    if (IsValidTopicName(helperT)) return helperT;
                }
            }
        }

        if (AstValueResolver.TryResolveTopicOrQueue(unwrapped, scopeSymbolId, out var directT) && IsValidTopicName(directT))
        {
            return directT;
        }

        return AstHelper.ResolveTopicOrQueue(unwrapped, scopeSymbolId);
    }

    private static void CollectDeclarativePubSubModuleTopics(Node callNode, string scopeSymbolId, List<Reference> references)
    {
        var pairs = callNode.FindDescendantsOfType(TreeSitterSyntax.TypeScript.Pair);
        foreach (var pair in pairs)
        {
            var keyNode = pair.GetField(TreeSitterSyntax.Fields.Key);
            if (!keyNode.IsValid() || !keyNode.Text.Trim('\'', '"').Equals("topics", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var valNode = pair.GetField(TreeSitterSyntax.Fields.Value);
            if (!valNode.IsValid()) continue;

            var arrNode = valNode.Is(TreeSitterSyntax.TypeScript.Array) ? valNode : valNode.FindDescendantsOfType(TreeSitterSyntax.TypeScript.Array).FirstOrDefault();
            if (!arrNode.IsValid()) continue;

            foreach (var elem in arrNode.Children)
            {
                if (!elem.IsValid() || !elem.Is(TreeSitterSyntax.TypeScript.Object)) continue;

                Node? namePropNode = null;
                Node? subsPropNode = null;

                foreach (var objPair in elem.FindChildrenOfType(TreeSitterSyntax.TypeScript.Pair))
                {
                    var pKey = objPair.GetField(TreeSitterSyntax.Fields.Key);
                    if (!pKey.IsValid()) continue;
                    var keyText = pKey.Text.Trim('\'', '"');

                    if (keyText is "name" or "topic")
                    {
                        namePropNode = objPair.GetField(TreeSitterSyntax.Fields.Value);
                    }
                    else if (keyText is "subscriptions" or "subscription")
                    {
                        subsPropNode = objPair.GetField(TreeSitterSyntax.Fields.Value);
                    }
                }

                if (namePropNode.IsValid())
                {
                    var topic = AstValueResolver.TryResolveTopicOrQueue(namePropNode, scopeSymbolId, out var t) && IsValidTopicName(t)
                        ? t
                        : AstHelper.ResolveTopicOrQueue(namePropNode, scopeSymbolId);

                    if (IsValidTopicName(topic))
                    {
                        AddSubscribeReference(references, scopeSymbolId, topic);
                    }
                }

                if (subsPropNode.IsValid())
                {
                    var subsArr = subsPropNode.Is(TreeSitterSyntax.TypeScript.Array)
                        ? subsPropNode
                        : subsPropNode.FindDescendantsOfType(TreeSitterSyntax.TypeScript.Array).FirstOrDefault();

                    if (subsArr.IsValid())
                    {
                        foreach (var subElem in subsArr.Children)
                        {
                            if (!subElem.IsValid() || subElem.Type is "[" or "]" or ",") continue;
                            var subTopic = AstValueResolver.TryResolveTopicOrQueue(subElem, scopeSymbolId, out var st) && IsValidTopicName(st)
                                ? st
                                : AstHelper.ResolveTopicOrQueue(subElem, scopeSymbolId);

                            if (IsValidTopicName(subTopic))
                            {
                                AddSubscribeReference(references, scopeSymbolId, subTopic);
                            }
                        }
                    }
                }
            }
        }
    }

    private static string? ResolveTopicVariableInScope(Node node, string varName, string? scopeSymbolId = null)
    {
        var cleanName = varName.StartsWith("this.", StringComparison.OrdinalIgnoreCase) ? varName[5..] : varName;

        if (ConstantRegistry.TryResolve(scopeSymbolId, cleanName, out var resolved) && IsValidTopicName(resolved))
        {
            return resolved;
        }

        var declNode = AstValueResolver.FindVariableDeclarationInScope(node, cleanName) ??
                       AstValueResolver.FindClassFieldInitializer(node, cleanName);

        if (declNode.IsValid())
        {
            if (declNode.Is(TreeSitterSyntax.TypeScript.CallExpression))
            {
                var args = AstHelper.GetCallArguments(declNode);
                if (args.Count > 0)
                {
                    if (AstValueResolver.TryResolveTopicOrQueue(args[0], scopeSymbolId, out var tName) && IsValidTopicName(tName))
                    {
                        return tName;
                    }
                    var helperArgT = AstHelper.ResolveTopicOrQueue(args[0], scopeSymbolId);
                    if (IsValidTopicName(helperArgT)) return helperArgT;
                }
            }

            if (AstValueResolver.TryResolveTopicOrQueue(declNode, scopeSymbolId, out var tRes) && IsValidTopicName(tRes))
            {
                return tRes;
            }
        }

        var varValDirect = AstHelper.FindVariableInitializerInAst(node, cleanName, contextOrProject: scopeSymbolId);
        if (!string.IsNullOrEmpty(varValDirect) && IsValidTopicName(varValDirect))
        {
            return varValDirect;
        }

        return null;
    }
}
