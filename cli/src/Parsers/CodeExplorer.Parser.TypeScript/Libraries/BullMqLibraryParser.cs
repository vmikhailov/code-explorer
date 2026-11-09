using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public class BullMqLibraryParser : DeclarativeSemanticExtension
{
    private const string ThisDotPrefix = "this.";
    private const string PropQueueName = "queueName";
    private const string PropQueue = "queue";
    private const string PropName = "name";
    public override SemanticExtensionType Type => SemanticExtensionType.Cloud;
    public override string Name => "BullMQ";
    public override string Id => "bullmq";
    public override IReadOnlyList<string> SupportedPatterns =>
    [
        "bullmq",
        "bull",
        "@nestjs/bull",
        "@nestjs/bullmq",
        "*message-queue*",
        "*queue.decorator*",
        "*queue.service*",
        "*decorators*",
        "*enum*",
        "*job.repository*"
    ];
    public override LibraryRole LibraryRole => LibraryRole.WorkerService;

    private static readonly IReadOnlyList<CustomAstRule> Rules =
    [
        new CustomAstRule
        {
            NodeTypes =
            [
                TreeSitterSyntax.TypeScript.Decorator,
                TreeSitterSyntax.TypeScript.NewExpression,
                TreeSitterSyntax.TypeScript.CallExpression,
                TreeSitterSyntax.TypeScript.MethodDefinition
            ],
            CollectReferences = CollectReferencesCore,
            EnrichSymbol = EnrichSymbolCore
        }
    ];

    public override IReadOnlyList<CustomAstRule> CustomAstRules => Rules;

    private static void CollectReferencesCore(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        // 1. Check for `@Processor('queue-name')` or `@Processor(MessageQueue.emailQueue)` or `@Processor({ queueName: MessageQueue.emailQueue })`
        if (node.Is(TreeSitterSyntax.TypeScript.Decorator))
        {
            var callExpr = node.FindChildOfType(TreeSitterSyntax.TypeScript.CallExpression);
            if (callExpr.IsValid())
            {
                var func = callExpr.GetFunctionNode();
                if (func.IsValid() && string.Equals(func.Text, "Processor", StringComparison.Ordinal))
                {
                    var args = AstHelper.GetCallArguments(callExpr);
                    if (args.Count > 0)
                    {
                        var queueName = ResolveBullMqQueueArgument(args[0], ctx?.WorkspaceId);
                        if (!string.IsNullOrEmpty(queueName) && WorkspaceConventions.IsValidTopicOrQueueName(queueName))
                        {
                            references.Add(new Reference(scopeSymbolId, "bullmq:" + queueName, OntologyConstants.Relationships.SubscribesTo));
                        }
                    }
                }
            }
        }
        // 2. Check for `new Worker('queue-name', ...)`
        else if (node.Is(TreeSitterSyntax.TypeScript.NewExpression))
        {
            var ctorNode = node.GetField(TreeSitterSyntax.Fields.Constructor) ??
                           node.Children.FirstOrDefault(c => c.Is(TreeSitterSyntax.TypeScript.Identifier));
            if (ctorNode.IsValid() && string.Equals(ctorNode.Text, "Worker", StringComparison.OrdinalIgnoreCase))
            {
                var args = AstHelper.GetCallArguments(node);
                var queueName = args.Count > 0
                    ? ResolveBullMqQueueArgument(args[0], ctx?.WorkspaceId)
                    : AstHelper.ExtractFirstStringArgument(node);
                if (!string.IsNullOrEmpty(queueName) && WorkspaceConventions.IsValidTopicOrQueueName(queueName))
                {
                    references.Add(new Reference(scopeSymbolId, "bullmq:" + queueName, OntologyConstants.Relationships.SubscribesTo));
                }
            }
        }
        // 3. Check for `queue.add(...)`, `this.messageQueueService.add(...)`, `this.messageQueueService.addCron(...)`, `this.messageQueueService.bulkAdd(...)`
        else if (node.Is(TreeSitterSyntax.TypeScript.CallExpression))
        {
            if (AstHelper.TryGetMemberAccess(node, out var objNode, out var propName))
            {
                if (propName is "add" or "addBulk" or "bulkAdd" or "addCron")
                {
                    var objText = objNode.IsValid() ? objNode.Text : "";
                    var cleanField = objText.StartWithIC(ThisDotPrefix) ? objText[5..] : objText;

                    // A. Try resolving the queue bound to the field via @InjectQueue(...) / @InjectMessageQueue(...) in enclosing class constructor
                    var injectedQueue = ResolveInjectedQueueFromEnclosingClass(node, cleanField, ctx?.WorkspaceId);
                    if (!string.IsNullOrEmpty(injectedQueue) && WorkspaceConventions.IsValidTopicOrQueueName(injectedQueue))
                    {
                        references.Add(new Reference(scopeSymbolId, "bullmq:" + injectedQueue, OntologyConstants.Relationships.PublishesTo));
                        return;
                    }

                    // B. Check if first argument is an explicit queue enum/constant (e.g. driver.add(MessageQueue.emailQueue, ...))
                    var args = AstHelper.GetCallArguments(node);
                    if (args.Count > 0 && IsExplicitQueueArgument(args[0]))
                    {
                        var explicitQueue = ResolveBullMqQueueArgument(args[0], ctx?.WorkspaceId);
                        if (!string.IsNullOrEmpty(explicitQueue) && WorkspaceConventions.IsValidTopicOrQueueName(explicitQueue))
                        {
                            references.Add(new Reference(scopeSymbolId, "bullmq:" + explicitQueue, OntologyConstants.Relationships.PublishesTo));
                            return;
                        }
                    }

                    // C. Fallback to standard BullMQ `queue.add('job-name', ...)` only when receiver is explicitly named `queue` or `*Queue` (not Set.add, Map, List, etc.)
                    if (propName is "add" or "addBulk" &&
                        (cleanField.Equals("queue", StringComparison.OrdinalIgnoreCase) ||
                         (cleanField.EndsWith("Queue", StringComparison.Ordinal) && !cleanField.EndsWith("Service", StringComparison.Ordinal))))
                    {
                        if ((ctx != null && ConstantRegistry.TryResolve(ctx.WorkspaceId, cleanField, out var resolvedQueue) && WorkspaceConventions.IsValidTopicOrQueueName(resolvedQueue)) ||
                            (ConstantRegistry.TryResolve(null, cleanField, out resolvedQueue) && WorkspaceConventions.IsValidTopicOrQueueName(resolvedQueue)) ||
                            (TryResolveLocalQueueInitializer(node, cleanField, ctx?.WorkspaceId, out resolvedQueue) && WorkspaceConventions.IsValidTopicOrQueueName(resolvedQueue)))
                        {
                            references.Add(new Reference(scopeSymbolId, "bullmq:" + resolvedQueue, OntologyConstants.Relationships.PublishesTo));
                            return;
                        }

                        var jobName = AstHelper.ExtractFirstStringArgument(node);
                        var identifier = !string.IsNullOrEmpty(jobName) ? $"{cleanField}:{jobName}" : cleanField;

                        if (WorkspaceConventions.IsValidTopicOrQueueName(identifier))
                        {
                            references.Add(new Reference(scopeSymbolId, "bullmq:" + identifier, OntologyConstants.Relationships.PublishesTo));
                        }
                    }
                }
                else if (propName is "queue" or "queueAll")
                {
                    {
                        {
                            CollectJobRepositoryQueueReferences(node, objNode, scopeSymbolId, references, ctx);
                        }
                    }
                }
            }
        }
    }

    private static void CollectJobRepositoryQueueReferences(
        Node node,
        Node? objNode,
        string scopeSymbolId,
        List<Reference> references,
        ParsingContext ctx)
    {
        var objText = objNode.IsValid() ? objNode.Text : "";
        var cleanField = objText.StartWithIC(ThisDotPrefix) ? objText[5..] : objText;
        if (!cleanField.Contains("job", StringComparison.OrdinalIgnoreCase) &&
            !cleanField.Contains("queue", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var args = AstHelper.GetCallArguments(node);
        if (args.Count == 0) return;

        var qArg = ResolveBullMqQueueArgument(args[0], ctx?.WorkspaceId);
        if (!string.IsNullOrEmpty(qArg) && WorkspaceConventions.IsValidTopicOrQueueName(qArg))
        {
            references.Add(new Reference(scopeSymbolId, "bullmq:" + qArg, OntologyConstants.Relationships.PublishesTo));
            return;
        }

        foreach (var innerObj in args[0].FindDescendantsOfType(TreeSitterSyntax.TypeScript.Object))
        {
            var innerQ = ResolveBullMqQueueArgument(innerObj, ctx?.WorkspaceId);
            if (!string.IsNullOrEmpty(innerQ) && WorkspaceConventions.IsValidTopicOrQueueName(innerQ))
            {
                references.Add(new Reference(scopeSymbolId, "bullmq:" + innerQ, OntologyConstants.Relationships.PublishesTo));
                break;
            }
        }
    }

    private static void EnrichSymbolCore(Node node, SyntacticSymbol symbol, ParsingContext ctx)
    {
        if (!node.Is(TreeSitterSyntax.TypeScript.MethodDefinition) ||
            symbol.Kind != CodeExplorer.Cypher.Common.SyntacticSymbolKind.Function)
        {
            return;
        }

        foreach (var dec in GetPrecedingDecorators(node))
        {
            var callExpr = dec.FindChildOfType(TreeSitterSyntax.TypeScript.CallExpression);
            var decName = callExpr.IsValid() ? (callExpr.GetFunctionNode()?.Text ?? "") : dec.Text.TrimStart('@');

            if (string.Equals(decName, "OnJob", StringComparison.Ordinal) && callExpr.IsValid())
            {
                var args = AstHelper.GetCallArguments(callExpr);
                if (args.Count > 0 && args[0].Is(TreeSitterSyntax.TypeScript.Object))
                {
                    if (AstHelper.TryGetObjectProperty(args[0], PropQueue, out var qProp) && qProp is not null)
                    {
                        var qVal = ResolveBullMqQueueArgument(qProp, ctx?.WorkspaceId);
                        if (!string.IsNullOrEmpty(qVal) && WorkspaceConventions.IsValidTopicOrQueueName(qVal))
                        {
                            symbol.References.Add(new Reference("", "bullmq:" + qVal, OntologyConstants.Relationships.SubscribesTo));
                        }
                    }
                    if (AstHelper.TryGetObjectProperty(args[0], PropName, out var nProp) && nProp is not null)
                    {
                        var nVal = ResolveBullMqQueueArgument(nProp, ctx?.WorkspaceId);
                        if (!string.IsNullOrEmpty(nVal) && WorkspaceConventions.IsValidTopicOrQueueName(nVal))
                        {
                            symbol.References.Add(new Reference("", "bullmq:" + nVal, OntologyConstants.Relationships.SubscribesTo));
                        }
                    }
                }
            }
            else if (string.Equals(decName, "Process", StringComparison.Ordinal))
            {
                var classQueue = ResolveClassProcessorQueue(node, ctx?.WorkspaceId);
                if (!string.IsNullOrEmpty(classQueue) && WorkspaceConventions.IsValidTopicOrQueueName(classQueue))
                {
                    symbol.References.Add(new Reference("", "bullmq:" + classQueue, OntologyConstants.Relationships.SubscribesTo));
                }
            }
        }
    }

    private static string? ResolveClassProcessorQueue(Node methodNode, string? contextOrProject)
    {
        var curr = methodNode.Parent;
        while (curr.IsValid() && !curr.IsAny(TreeSitterSyntax.TypeScript.ClassDeclaration, TreeSitterSyntax.TypeScript.ClassExpression))
        {
            curr = curr.Parent;
        }
        if (!curr.IsValid()) return null;

        var decorators = new List<Node>();
        decorators.AddRange(curr.FindChildrenOfType(TreeSitterSyntax.TypeScript.Decorator));
        decorators.AddRange(GetPrecedingDecorators(curr));
        if (curr.Parent.IsValid() && curr.Parent.Is(TreeSitterSyntax.TypeScript.ExportStatement))
        {
            decorators.AddRange(curr.Parent.FindChildrenOfType(TreeSitterSyntax.TypeScript.Decorator));
            decorators.AddRange(GetPrecedingDecorators(curr.Parent));
        }

        foreach (var dec in decorators)
        {
            var callExpr = dec.FindChildOfType(TreeSitterSyntax.TypeScript.CallExpression);
            if (!callExpr.IsValid()) continue;
            var func = callExpr.GetFunctionNode();
            if (func.IsValid() && string.Equals(func.Text, "Processor", StringComparison.Ordinal))
            {
                var args = AstHelper.GetCallArguments(callExpr);
                if (args.Count > 0)
                {
                    return ResolveBullMqQueueArgument(args[0], contextOrProject);
                }
            }
        }
        return null;
    }

    private static List<Node> GetPrecedingDecorators(Node node)
    {
        var result = new List<Node>();
        var parent = node.Parent;
        if (!parent.IsValid()) return result;
        var children = parent.Children;
        var idx = -1;
        for (var i = 0; i < children.Count; i++)
        {
            if (children[i].Id == node.Id)
            {
                idx = i;
                break;
            }
        }
        if (idx <= 0) return result;

        for (var i = idx - 1; i >= 0; i--)
        {
            var sibling = children[i];
            if (sibling.Is(TreeSitterSyntax.TypeScript.Decorator))
            {
                result.Add(sibling);
            }
            else
            {
                break;
            }
        }
        return result;
    }

    private static bool IsExplicitQueueArgument(Node argNode)
    {
        if (!argNode.IsValid()) return false;
        var text = argNode.Text;
        return text.Contains("Queue.", StringComparison.Ordinal) ||
               text.Contains("QUEUE", StringComparison.Ordinal) ||
               (argNode.Is(TreeSitterSyntax.TypeScript.Object) &&
                (AstHelper.TryGetObjectProperty(argNode, PropQueueName, out _) ||
                 AstHelper.TryGetObjectProperty(argNode, PropQueue, out _)));
    }

    private static string? ResolveBullMqQueueArgument(Node argNode, string? contextOrProject)
    {
        if (!argNode.IsValid()) return null;

        if (argNode.Is(TreeSitterSyntax.TypeScript.Object))
        {
            if (AstHelper.TryGetObjectProperty(argNode, PropQueueName, out var qProp) ||
                AstHelper.TryGetObjectProperty(argNode, PropName, out qProp) ||
                AstHelper.TryGetObjectProperty(argNode, PropQueue, out qProp))
            {
                return qProp is not null ? ResolveBullMqQueueArgument(qProp, contextOrProject) : null;
            }
            return null;
        }

        var resolved = AstHelper.ResolveTopicOrQueue(argNode, contextOrProject);
        if (!string.IsNullOrEmpty(resolved)) return resolved;

        // If member expression like MessageQueue.emailQueue and ConstantRegistry hasn't parsed the enum file yet,
        // emit the qualified constant reference so Layer 5 ResolveMessagingReference resolves it via ConstantRegistry!
        if (argNode.Is(TreeSitterSyntax.TypeScript.MemberExpression) || argNode.Is(TreeSitterSyntax.TypeScript.Identifier))
        {
            var raw = argNode.Text.Trim();
            if (!string.IsNullOrEmpty(raw) && !raw.StartsWith("this.", StringComparison.Ordinal))
            {
                if (ConstantRegistry.TryResolve(contextOrProject, raw, out var cVal) && !string.IsNullOrEmpty(cVal))
                {
                    return cVal;
                }
                return raw;
            }
        }

        return null;
    }

    private static string? ResolveInjectedQueueFromEnclosingClass(Node callNode, string fieldName, string? contextOrProject)
    {
        if (string.IsNullOrWhiteSpace(fieldName)) return null;

        var curr = callNode.Parent;
        while (curr.IsValid() && !curr.IsAny(TreeSitterSyntax.TypeScript.ClassDeclaration, TreeSitterSyntax.TypeScript.ClassExpression))
        {
            curr = curr.Parent;
        }
        if (!curr.IsValid()) return null;

        // Look through constructor parameters for `@InjectQueue(X)` or `@InjectMessageQueue(X)` decorating `fieldName`
        foreach (var dec in curr.FindDescendantsOfType(TreeSitterSyntax.TypeScript.Decorator))
        {
            var decCall = dec.FindChildOfType(TreeSitterSyntax.TypeScript.CallExpression);
            if (!decCall.IsValid()) continue;

            var decFunc = decCall.GetFunctionNode();
            if (!decFunc.IsValid()) continue;

            var decName = decFunc.Text;
            if (!decName.Equals("InjectQueue", StringComparison.Ordinal) &&
                !decName.Equals("InjectMessageQueue", StringComparison.Ordinal) &&
                !decName.EndsWith("Queue", StringComparison.Ordinal))
            {
                continue;
            }

            var paramNode = dec.Parent;
            if (paramNode.IsValid() && paramNode.Text.Contains(fieldName, StringComparison.Ordinal))
            {
                var args = AstHelper.GetCallArguments(decCall);
                if (args.Count > 0)
                {
                    return ResolveBullMqQueueArgument(args[0], contextOrProject);
                }
            }
        }

        return null;
    }

    private static bool TryResolveLocalQueueInitializer(
        Node callNode,
        string queueVarName,
        string? contextOrProject,
        out string? resolvedQueue)
    {
        resolvedQueue = null;
        var doc = callNode.Document;
        if (doc == null) return false;

        for (var i = 0; i < doc.AllVariables.Count; i++)
        {
            var decl = doc.AllVariables[i];
            if (!decl.IsDeclarator || !string.Equals(decl.Name, queueVarName, StringComparison.Ordinal)) continue;

            var initNode = decl.Initializer ?? decl.GetField(TreeSitterSyntax.Fields.Value);
            if (!initNode.IsValid() || !initNode.Is(TreeSitterSyntax.TypeScript.NewExpression)) continue;

            var ctor = initNode.GetField(TreeSitterSyntax.Fields.Constructor) ?? initNode.GetFunctionNode();
            var ctorText = ctor.IsValid() ? ctor.Text : "";
            if (ctorText is "Queue" or "Bull" || ctorText.StartsWith("Queue<", StringComparison.Ordinal))
            {
                var args = AstHelper.GetCallArguments(initNode);
                if (args.Count > 0 && args[0].IsValid())
                {
                    var argNode = args[0];
                    if (AstHelper.IsStringLiteralNode(argNode))
                    {
                        resolvedQueue = argNode.Text.Trim('\'', '"', '`');
                        return !string.IsNullOrEmpty(resolvedQueue);
                    }
                    if (argNode.Is(TreeSitterSyntax.TypeScript.Identifier))
                    {
                        var ident = argNode.Text;
                        if (ConstantRegistry.TryResolve(contextOrProject, ident, out var cVal) ||
                            ConstantRegistry.TryResolve(null, ident, out cVal))
                        {
                            resolvedQueue = cVal;
                            return !string.IsNullOrEmpty(resolvedQueue);
                        }
                        var localConst = AstHelper.FindVariableInitializerInAst(initNode, ident, 0, null, contextOrProject);
                        if (!string.IsNullOrEmpty(localConst))
                        {
                            resolvedQueue = localConst.Trim('\'', '"', '`');
                            return !string.IsNullOrEmpty(resolvedQueue);
                        }
                    }
                }
            }
        }

        return false;
    }
}
