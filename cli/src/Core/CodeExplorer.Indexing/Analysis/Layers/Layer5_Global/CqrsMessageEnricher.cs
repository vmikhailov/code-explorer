using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Common.Relationships;

namespace CodeExplorer.Core.Parser.Layers;

internal static class CqrsMessageEnricher
{
    private const string QueryHandlerToken = "QueryHandler";
    private const string CommandHandlerToken = "CommandHandler";
    private const string EventsHandlerToken = "EventsHandler";
    private const string EventHandlerToken = "EventHandler";
    private const string EventSourcingHandlerToken = "EventSourcingHandler";
    private const string SagaEventHandlerToken = "SagaEventHandler";
    private const string NotificationHandlerToken = "NotificationHandler";
    private const string DomainEventSubscriberToken = "DomainEventSubscriber";
    private const string EventSubscriberToken = "EventSubscriber";
    private const string EventListenerToken = "EventListener";
    private const string SnakeQueryHandlerToken = "query_handler";
    private const string SnakeCommandHandlerToken = "command_handler";
    private const string SnakeEventHandlerToken = "event_handler";
    private const string AsMessageHandlerToken = "AsMessageHandler";
    private const string RequestHandlerToken = "RequestHandler";
    private const string MediatorHandlerToken = "MediatorHandler";
    private const string MessageHandlerToken = "MessageHandler";
    private const string ConsumerToken = "IConsumer<";
    private const string HandlerSuffix = "Handler";
    private const string QuerySuffix = "Query";
    private const string CommandSuffix = "Command";
    private const string EventSuffix = "Event";
    private const string NotificationSuffix = "Notification";
    private const string GoExtension = ".go";
    private const string QueriesFolderToken = "/queries/";
    private const string QueryFolderToken = "/query/";
    private const string CommandsFolderToken = "/commands/";
    private const string CommandFolderToken = "/command/";
    private const string CqrsPrefix = "cqrs:";
    private const string MediatrPrefix = "mediatr:";
    private const string FrameworkMediatr = "mediatr";
    private const string FrameworkNestJs = "nestjs-cqrs";
    private const string FrameworkAxon = "axon";
    private const string FrameworkSymfony = "symfony-messenger";
    private const string FrameworkCqrs = "cqrs";

    private static readonly HashSet<string> HandlerMethodNames = new(StringComparer.Ordinal)
    {
        "Handle", "HandleAsync", "Execute", "ExecuteAsync", "Consume", "ConsumeAsync",
        "handle", "execute", "consume", "__invoke", "on", "On"
    };

    private static readonly HashSet<string> IgnoredFrameworkHandlerSuffixes = new(StringComparer.OrdinalIgnoreCase)
    {
        "HttpClientHandler", "DelegatingHandler", "ExceptionHandler", "AuthenticationHandler",
        "AuthorizationHandler", "RouteHandler", "SignalHandler", "ErrorHandler", "WebSocketHandler"
    };

    private static readonly HashSet<string> IgnoredParameterTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "CancellationToken", "Context", "context.Context", "ServerCallContext", "HttpContext",
        "MessageContext", "IMessageHandlerContext", "ConsumeContext", "Metadata", "MetaData",
        "string", "int", "long", "bool", "Guid", "UUID", "object", "any", "unknown"
    };

    internal sealed record EnrichmentOutput(List<MessageNode> Messages, List<Relationship> Relationships);

    public static EnrichmentOutput EnrichCqrsMessages(
        Layer4Result? l4Result,
        Layer5Context ctx,
        Layer5AnalysisParser.SymbolLookups symbols,
        Dictionary<string, List<string>> messageHandlersByType,
        HashSet<(string From, string To, string Kind)> existingRefRels)
    {
        var messagesById = new Dictionary<string, MessageNode>(StringComparer.Ordinal);
        var rels = new List<Relationship>();
        if (l4Result == null) return new EnrichmentOutput([], rels);

        var syntaxStruct = ctx.SyntaxStructure;
        if (syntaxStruct == null) return new EnrichmentOutput([], rels);

        var typesById = new Dictionary<string, TypeNode>(StringComparer.Ordinal);
        var typesByName = new Dictionary<string, TypeNode>(StringComparer.OrdinalIgnoreCase);
        var typeFunctions = new Dictionary<string, List<FunctionNode>>(StringComparer.Ordinal);
        CollectSyntaxTypesAndMethods(syntaxStruct, typesById, typesByName, typeFunctions);

        var filesByPath = new Dictionary<string, FileNode>(StringComparer.OrdinalIgnoreCase);
        if (ctx.FilesStructure != null)
        {
            CollectFiles(ctx.FilesStructure, filesByPath);
        }

        var modules = new List<ModuleNode>();
        CollectModules(l4Result.SemanticStructure, modules);

        var paramTypesByFn = BuildFunctionParameterMap(ctx, typeFunctions);
        var inheritedContracts = BuildInheritedContractsMap(symbols, typesById);

        foreach (var (typeId, typeNode) in typesById)
        {
            if (typeNode.IsTest || typeNode.TypeKind is TypeKind.Interface or TypeKind.Annotation || IsNonCqrsFrameworkHandler(typeNode.Name)) continue;
            if (!typeFunctions.TryGetValue(typeId, out var methods) || methods.Count == 0) continue;

            // 1. Method-level annotations (@QueryHandler, @CommandHandler, @EventHandler, #[AsMessageHandler])
            var handledMethods = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < methods.Count; i++)
            {
                var fn = methods[i];
                if (fn.IsTest) continue;
                if (TryClassifyMethodAnnotation(fn, paramTypesByFn, typesByName, out var mRole, out var mMsg, out var mFw))
                {
                    handledMethods.Add(fn.Id);
                    EmitMessageAndEdges(
                        ctx, fn, mMsg, mRole, mFw, typesByName, filesByPath, modules,
                        messageHandlersByType, messagesById, rels, existingRefRels);
                }
            }

            // 2. Class-level contracts, decorators, or CQRS handler conventions
            if (TryClassifyTypeContract(typeNode, methods, paramTypesByFn, inheritedContracts, typesByName, out var cRole, out var cMsg, out var cFw))
            {
                var primaryFn = SelectPrimaryHandlerMethod(methods, handledMethods);
                if (primaryFn != null && !primaryFn.IsTest)
                {
                    EmitMessageAndEdges(
                        ctx, primaryFn, cMsg, cRole, cFw, typesByName, filesByPath, modules,
                        messageHandlersByType, messagesById, rels, existingRefRels);
                }
            }
        }

        return new EnrichmentOutput(messagesById.Values.ToList(), rels);
    }

    private static void CollectSyntaxTypesAndMethods(
        IOntologyNode node,
        Dictionary<string, TypeNode> typesById,
        Dictionary<string, TypeNode> typesByName,
        Dictionary<string, List<FunctionNode>> typeFunctions)
    {
        if (node is TypeNode tn)
        {
            typesById[tn.Id] = tn;
            if (!string.IsNullOrEmpty(tn.Name) && (!typesByName.ContainsKey(tn.Name) || !tn.IsTest))
            {
                typesByName[tn.Name] = tn;
            }
            var fnList = new List<FunctionNode>();
            for (var i = 0; i < tn.Children.Count; i++)
            {
                if (tn.Children[i] is FunctionNode fn) fnList.Add(fn);
            }
            typeFunctions[tn.Id] = fnList;
        }

        for (var i = 0; i < node.Children.Count; i++)
        {
            CollectSyntaxTypesAndMethods(node.Children[i], typesById, typesByName, typeFunctions);
        }
    }

    private static void CollectFiles(IOntologyNode node, Dictionary<string, FileNode> filesByPath)
    {
        if (node is FileNode fn && !string.IsNullOrEmpty(fn.Path))
        {
            filesByPath[fn.Path.Replace('\\', '/')] = fn;
        }
        for (var i = 0; i < node.Children.Count; i++)
        {
            CollectFiles(node.Children[i], filesByPath);
        }
    }

    private static void CollectModules(IOntologyNode node, List<ModuleNode> modules)
    {
        if (node is ModuleNode mn && mn is not TestSuiteNode)
        {
            modules.Add(mn);
        }
        for (var i = 0; i < node.Children.Count; i++)
        {
            CollectModules(node.Children[i], modules);
        }
    }

    private static Dictionary<(string FilePath, string FnName), List<string>> BuildFunctionParameterMap(
        Layer5Context ctx,
        Dictionary<string, List<FunctionNode>> typeFunctions)
    {
        var map = new Dictionary<(string FilePath, string FnName), List<string>>();
        foreach (var methods in typeFunctions.Values)
        {
            for (var i = 0; i < methods.Count; i++)
            {
                var fn = methods[i];
                var normFile = fn.FilePath.Replace('\\', '/');
                for (var j = 0; j < fn.Children.Count; j++)
                {
                    if (fn.Children[j] is ParameterNode pn && !string.IsNullOrWhiteSpace(pn.DataType))
                    {
                        AddCandidateParamType(map, normFile, fn.Name, pn.DataType);
                    }
                }
            }
        }

        for (var i = 0; i < ctx.RawTypeBindings.Count; i++)
        {
            var b = ctx.RawTypeBindings[i];
            if (string.IsNullOrWhiteSpace(b.ScopeId) || string.IsNullOrWhiteSpace(b.TypeName)) continue;
            AddCandidateParamType(map, b.FilePath.Replace('\\', '/'), b.ScopeId, b.TypeName);
        }
        return map;
    }

    private static void AddCandidateParamType(
        Dictionary<(string FilePath, string FnName), List<string>> map,
        string normFile,
        string fnName,
        string rawType)
    {
        var clean = CleanTypeIdentifier(rawType);
        if (string.IsNullOrEmpty(clean) || IgnoredParameterTypes.Contains(clean)) return;
        var key = (normFile, fnName);
        if (!map.TryGetValue(key, out var list))
        {
            list = [];
            map[key] = list;
        }
        if (!list.Contains(clean)) list.Add(clean);
    }

    private static Dictionary<string, List<string>> BuildInheritedContractsMap(
        Layer5AnalysisParser.SymbolLookups symbols,
        Dictionary<string, TypeNode> typesById)
    {
        var map = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var (childId, parentId) in symbols.InheritanceRels)
        {
            if (!typesById.TryGetValue(parentId, out var parentType) || string.IsNullOrEmpty(parentType.Name)) continue;
            if (!map.TryGetValue(childId, out var list))
            {
                list = [];
                map[childId] = list;
            }
            if (!list.Contains(parentType.Name)) list.Add(parentType.Name);
        }
        return map;
    }

    private static bool TryClassifyMethodAnnotation(
        FunctionNode fn,
        Dictionary<(string FilePath, string FnName), List<string>> paramTypesByFn,
        Dictionary<string, TypeNode> typesByName,
        out OperationType role,
        out string messageName,
        out string framework)
    {
        role = OperationType.Unspecified;
        messageName = string.Empty;
        framework = FrameworkCqrs;
        var attrs = fn.Attributes;
        if (string.IsNullOrEmpty(attrs)) return false;

        if (attrs.Contains(QueryHandlerToken, StringComparison.Ordinal) || attrs.Contains(SnakeQueryHandlerToken, StringComparison.OrdinalIgnoreCase))
        {
            role = OperationType.Query;
            framework = FrameworkAxon;
        }
        else if (attrs.Contains(CommandHandlerToken, StringComparison.Ordinal) || attrs.Contains(SnakeCommandHandlerToken, StringComparison.OrdinalIgnoreCase))
        {
            role = OperationType.Command;
            framework = FrameworkAxon;
        }
        else if (attrs.Contains(EventSourcingHandlerToken, StringComparison.Ordinal) ||
                 attrs.Contains(SagaEventHandlerToken, StringComparison.Ordinal) ||
                 attrs.Contains(EventHandlerToken, StringComparison.Ordinal) ||
                 attrs.Contains(EventsHandlerToken, StringComparison.Ordinal) ||
                 attrs.Contains(EventListenerToken, StringComparison.Ordinal) ||
                 attrs.Contains(SnakeEventHandlerToken, StringComparison.OrdinalIgnoreCase))
        {
            role = OperationType.Event;
            framework = FrameworkAxon;
        }
        else if (attrs.Contains(AsMessageHandlerToken, StringComparison.Ordinal))
        {
            framework = FrameworkSymfony;
        }
        else
        {
            return false;
        }

        var firstParam = GetFirstMessageParam(fn, paramTypesByFn);
        if (string.IsNullOrEmpty(firstParam)) return false;
        if (role == OperationType.Unspecified)
        {
            role = InferRoleFromMessageType(firstParam, typesByName);
            if (role == OperationType.Unspecified) return false;
        }

        messageName = firstParam;
        return true;
    }

    private static bool TryClassifyTypeContract(
        TypeNode tn,
        List<FunctionNode> methods,
        Dictionary<(string FilePath, string FnName), List<string>> paramTypesByFn,
        Dictionary<string, List<string>> inheritedContracts,
        Dictionary<string, TypeNode> typesByName,
        out OperationType role,
        out string messageName,
        out string framework)
    {
        role = OperationType.Unspecified;
        messageName = string.Empty;
        framework = FrameworkCqrs;

        // A. Decorators / Attributes (@QueryHandler(FindInvoiceQuery), #[AsMessageHandler])
        var attrs = tn.Attributes ?? string.Empty;
        if (attrs.Length > 0)
        {
            if (attrs.Contains(QueryHandlerToken, StringComparison.Ordinal) || attrs.Contains(SnakeQueryHandlerToken, StringComparison.OrdinalIgnoreCase))
            {
                role = OperationType.Query;
                framework = FrameworkNestJs;
            }
            else if (attrs.Contains(CommandHandlerToken, StringComparison.Ordinal) || attrs.Contains(SnakeCommandHandlerToken, StringComparison.OrdinalIgnoreCase))
            {
                role = OperationType.Command;
                framework = FrameworkNestJs;
            }
            else if (attrs.Contains(EventsHandlerToken, StringComparison.Ordinal) || attrs.Contains(EventHandlerToken, StringComparison.Ordinal) || attrs.Contains(SnakeEventHandlerToken, StringComparison.OrdinalIgnoreCase))
            {
                role = OperationType.Event;
                framework = FrameworkNestJs;
            }
            else if (attrs.Contains(AsMessageHandlerToken, StringComparison.Ordinal))
            {
                framework = FrameworkSymfony;
            }

            if (role != OperationType.Unspecified || framework == FrameworkSymfony)
            {
                messageName = ExtractMessageFromReferencesOrMethods(tn, methods, paramTypesByFn);
                if (role == OperationType.Unspecified && !string.IsNullOrEmpty(messageName))
                {
                    role = InferRoleFromMessageType(messageName, typesByName);
                }
                if (role != OperationType.Unspecified && !string.IsNullOrEmpty(messageName)) return true;
            }
        }

        // B. Implemented Interfaces & Base Types (C#, TS, Java, PHP, Python, Rust)
        var contractsText = CombineContracts(tn, inheritedContracts);
        if (contractsText.Length > 0)
        {
            var genericArg = ExtractFirstGenericMessageArg(contractsText);
            if (contractsText.Contains(QueryHandlerToken, StringComparison.Ordinal))
            {
                role = OperationType.Query;
            }
            else if (contractsText.Contains(CommandHandlerToken, StringComparison.Ordinal))
            {
                role = OperationType.Command;
            }
            else if (contractsText.Contains(NotificationHandlerToken, StringComparison.Ordinal) ||
                     contractsText.Contains(EventHandlerToken, StringComparison.Ordinal) ||
                     contractsText.Contains(DomainEventSubscriberToken, StringComparison.Ordinal) ||
                     contractsText.Contains(EventSubscriberToken, StringComparison.Ordinal))
            {
                role = OperationType.Event;
            }
            else if (contractsText.Contains(RequestHandlerToken, StringComparison.Ordinal) ||
                     contractsText.Contains(MediatorHandlerToken, StringComparison.Ordinal) ||
                     contractsText.Contains(MessageHandlerToken, StringComparison.Ordinal) ||
                     contractsText.Contains(ConsumerToken, StringComparison.Ordinal))
            {
                framework = FrameworkMediatr;
                var candidate = !string.IsNullOrEmpty(genericArg)
                    ? genericArg
                    : ExtractMessageFromReferencesOrMethods(tn, methods, paramTypesByFn);
                role = InferRoleFromMessageType(candidate, typesByName);
                messageName = candidate;
                if (role != OperationType.Unspecified && !string.IsNullOrEmpty(messageName)) return true;
            }

            if (role != OperationType.Unspecified)
            {
                messageName = !string.IsNullOrEmpty(genericArg)
                    ? genericArg
                    : ExtractMessageFromReferencesOrMethods(tn, methods, paramTypesByFn);
                if (!string.IsNullOrEmpty(messageName)) return true;
            }
        }

        // C. Duck-typed / Go / Convention Handler Structs (*Handler with Handle/Execute/__invoke)
        if (!tn.Name.EndsWith(HandlerSuffix, StringComparison.Ordinal) || tn.Name.Length <= HandlerSuffix.Length)
        {
            return false;
        }

        var handlerFn = SelectPrimaryHandlerMethod(methods, null);
        if (handlerFn == null) return false;

        var paramMsg = GetFirstMessageParam(handlerFn, paramTypesByFn);
        var inferredFromName = tn.Name[..^HandlerSuffix.Length];
        if (string.IsNullOrEmpty(paramMsg))
        {
            if (typesByName.ContainsKey(inferredFromName + QuerySuffix))
                inferredFromName += QuerySuffix;
            else if (typesByName.ContainsKey(inferredFromName + CommandSuffix))
                inferredFromName += CommandSuffix;
        }
        var candidateMsg = !string.IsNullOrEmpty(paramMsg) ? paramMsg : inferredFromName;
        role = InferRoleFromMessageType(candidateMsg, typesByName);

        if (role == OperationType.Unspecified && tn.FilePath.EndsWith(GoExtension, StringComparison.OrdinalIgnoreCase))
        {
            var normLower = tn.FilePath.Replace('\\', '/').ToLowerInvariant();
            if (normLower.Contains(QueriesFolderToken, StringComparison.Ordinal) || normLower.Contains(QueryFolderToken, StringComparison.Ordinal))
            {
                role = OperationType.Query;
            }
            else if (normLower.Contains(CommandsFolderToken, StringComparison.Ordinal) || normLower.Contains(CommandFolderToken, StringComparison.Ordinal))
            {
                role = OperationType.Command;
            }
        }

        if (role == OperationType.Unspecified || string.IsNullOrEmpty(candidateMsg)) return false;
        messageName = candidateMsg;
        return true;
    }

    private static OperationType InferRoleFromMessageType(string msgName, Dictionary<string, TypeNode> typesByName)
    {
        if (string.IsNullOrEmpty(msgName)) return OperationType.Unspecified;
        if (typesByName.TryGetValue(msgName, out var msgType))
        {
            var sig = $"{msgType.Implements} {msgType.Extends}";
            if (sig.Contains(QuerySuffix, StringComparison.Ordinal)) return OperationType.Query;
            if (sig.Contains(CommandSuffix, StringComparison.Ordinal)) return OperationType.Command;
            if (sig.Contains(NotificationSuffix, StringComparison.Ordinal) || sig.Contains(EventSuffix, StringComparison.Ordinal))
            {
                return OperationType.Event;
            }
        }

        if (msgName.EndsWith(QuerySuffix, StringComparison.Ordinal) && msgName.Length > QuerySuffix.Length)
            return OperationType.Query;
        if (msgName.EndsWith(CommandSuffix, StringComparison.Ordinal) && msgName.Length > CommandSuffix.Length)
            return OperationType.Command;
        if ((msgName.EndsWith(EventSuffix, StringComparison.Ordinal) && msgName.Length > EventSuffix.Length) ||
            (msgName.EndsWith(NotificationSuffix, StringComparison.Ordinal) && msgName.Length > NotificationSuffix.Length))
            return OperationType.Event;

        return OperationType.Unspecified;
    }

    private static string CombineContracts(TypeNode tn, Dictionary<string, List<string>> inheritedContracts)
    {
        inheritedContracts.TryGetValue(tn.Id, out var resolvedList);
        var resolved = resolvedList is { Count: > 0 } ? string.Join(',', resolvedList) : string.Empty;
        return $"{tn.Implements} {tn.Extends} {resolved}".Trim();
    }

    private static string ExtractFirstGenericMessageArg(string contractsText)
    {
        var lt = contractsText.IndexOf('<');
        if (lt <= 0) return string.Empty;
        var gt = contractsText.IndexOf('>', lt + 1);
        if (gt <= lt + 1) return string.Empty;
        var inner = contractsText[(lt + 1)..gt];
        var comma = inner.IndexOf(',');
        var first = comma > 0 ? inner[..comma] : inner;
        return CleanTypeIdentifier(first);
    }

    private static string ExtractMessageFromReferencesOrMethods(
        TypeNode tn,
        List<FunctionNode> methods,
        Dictionary<(string FilePath, string FnName), List<string>> paramTypesByFn)
    {
        for (var i = 0; i < tn.References.Count; i++)
        {
            var tName = tn.References[i].TargetName;
            if (tName.StartsWith(CqrsPrefix, StringComparison.OrdinalIgnoreCase))
                return CleanTypeIdentifier(tName[CqrsPrefix.Length..]);
            if (tName.StartsWith(MediatrPrefix, StringComparison.OrdinalIgnoreCase))
                return CleanTypeIdentifier(tName[MediatrPrefix.Length..]);
        }

        var fn = SelectPrimaryHandlerMethod(methods, null);
        if (fn != null)
        {
            var p = GetFirstMessageParam(fn, paramTypesByFn);
            if (!string.IsNullOrEmpty(p)) return p;
        }

        if (tn.Name.EndsWith(HandlerSuffix, StringComparison.Ordinal) && tn.Name.Length > HandlerSuffix.Length)
        {
            return tn.Name[..^HandlerSuffix.Length];
        }
        return string.Empty;
    }

    private static FunctionNode? SelectPrimaryHandlerMethod(List<FunctionNode> methods, HashSet<string>? excludeIds)
    {
        for (var i = 0; i < methods.Count; i++)
        {
            var m = methods[i];
            if (excludeIds != null && excludeIds.Contains(m.Id)) continue;
            var shortName = m.Name.Contains('.') ? m.Name[(m.Name.LastIndexOf('.') + 1)..] : m.Name;
            if (HandlerMethodNames.Contains(shortName)) return m;
        }
        return null;
    }

    private static string GetFirstMessageParam(
        FunctionNode fn,
        Dictionary<(string FilePath, string FnName), List<string>> paramTypesByFn)
    {
        static string PickNonHandler(List<string> candidates)
        {
            for (var i = 0; i < candidates.Count; i++)
            {
                if (!candidates[i].EndsWith(HandlerSuffix, StringComparison.Ordinal))
                {
                    return candidates[i];
                }
            }
            return string.Empty;
        }

        var normFile = fn.FilePath.Replace('\\', '/');
        if (paramTypesByFn.TryGetValue((normFile, fn.Name), out var list) && list.Count > 0)
        {
            var picked = PickNonHandler(list);
            if (!string.IsNullOrEmpty(picked)) return picked;
        }
        var shortName = fn.Name.Contains('.') ? fn.Name[(fn.Name.LastIndexOf('.') + 1)..] : fn.Name;
        if (shortName != fn.Name && paramTypesByFn.TryGetValue((normFile, shortName), out var shortList) && shortList.Count > 0)
        {
            return PickNonHandler(shortList);
        }
        return string.Empty;
    }

    private static string CleanTypeIdentifier(string raw)
    {
        var s = raw.Trim().TrimStart('*', '&').TrimEnd('?');
        var lt = s.IndexOf('<');
        if (lt > 0) s = s[..lt].Trim();
        var slash = s.LastIndexOf('\\');
        if (slash >= 0) s = s[(slash + 1)..];
        var dot = s.LastIndexOf('.');
        if (dot >= 0) s = s[(dot + 1)..];
        return s.Trim();
    }

    private static bool IsNonCqrsFrameworkHandler(string typeName)
    {
        foreach (var suffix in IgnoredFrameworkHandlerSuffixes)
        {
            if (typeName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    private static void EmitMessageAndEdges(
        Layer5Context ctx,
        FunctionNode handlerFn,
        string messageName,
        OperationType role,
        string framework,
        Dictionary<string, TypeNode> typesByName,
        Dictionary<string, FileNode> filesByPath,
        List<ModuleNode> modules,
        Dictionary<string, List<string>> messageHandlersByType,
        Dictionary<string, MessageNode> messagesById,
        List<Relationship> rels,
        HashSet<(string From, string To, string Kind)> existingRefRels)
    {
        var roleKey = role.ToCanonicalString();
        var msgId = $"{ctx.WorkspaceId}:{OntologyConstants.IdPrefixes.Message}:{roleKey}:{messageName}";

        typesByName.TryGetValue(messageName, out var msgType);
        var declPath = (msgType?.FilePath ?? handlerFn.FilePath).Replace('\\', '/');

        if (!messagesById.TryGetValue(msgId, out var msgNode))
        {
            msgNode = new MessageNode(msgId, messageName, declPath, role)
            {
                Framework = framework,
                FilePath = declPath,
                StartLine = msgType?.StartLine ?? handlerFn.StartLine
            };
            messagesById[msgId] = msgNode;

            if (msgType != null)
            {
                AddUniqueRel(new ImplementedByRelationship(msgId, msgType.Id), rels, existingRefRels);
            }
            if (filesByPath.TryGetValue(declPath, out var fileNode))
            {
                AddUniqueRel(new DeclaredInRelationship(msgId, fileNode.Id), rels, existingRefRels);
            }
            var ownerMod = FindOwningModule(declPath, modules);
            if (ownerMod != null)
            {
                AddUniqueRel(new ContainsRelationship(ownerMod.Id, msgId), rels, existingRefRels);
            }
        }

        AddUniqueRel(new TriggersRelationship(msgId, handlerFn.Id), rels, existingRefRels);

        IOntologyRelationship handlesRel = role switch
        {
            OperationType.Query => new HandlesQueryRelationship(handlerFn.Id, msgId),
            OperationType.Command => new HandlesCommandRelationship(handlerFn.Id, msgId),
            _ => new HandlesEventRelationship(handlerFn.Id, msgId)
        };
        AddUniqueRel(handlesRel, rels, existingRefRels);

        if (!messageHandlersByType.TryGetValue(messageName, out var hList))
        {
            hList = [];
            messageHandlersByType[messageName] = hList;
        }
        if (!hList.Contains(handlerFn.Id))
        {
            hList.Add(handlerFn.Id);
        }
    }

    private static ModuleNode? FindOwningModule(string normFilePath, List<ModuleNode> modules)
    {
        ModuleNode? best = null;
        var bestLen = -1;
        for (var i = 0; i < modules.Count; i++)
        {
            var m = modules[i];
            var mp = m.Path.Replace('\\', '/').TrimEnd('/');
            if (mp.Length == 0 || normFilePath.StartsWith(mp + '/', StringComparison.OrdinalIgnoreCase) || string.Equals(normFilePath, mp, StringComparison.OrdinalIgnoreCase))
            {
                if (mp.Length > bestLen)
                {
                    best = m;
                    bestLen = mp.Length;
                }
            }
        }
        return best;
    }

    private static void AddUniqueRel(
        IOntologyRelationship ontRel,
        List<Relationship> rels,
        HashSet<(string From, string To, string Kind)> existingRefRels)
    {
        var r = Relationship.FromRelationship(ontRel);
        if (existingRefRels.Add((r.From, r.To, GraphKindCodec.DecodeRelKind((int)r.Kind))))
        {
            rels.Add(r);
        }
    }
}
