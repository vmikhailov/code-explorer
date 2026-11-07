using System.Collections.Frozen;
using CodeExplorer.Core.Parser.Syntax;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Core.Parser;

/// <summary>
/// Declarative rule for mapping HTTP route annotations/attributes (e.g., Spring @GetMapping, Symfony #[Route]) to EntryPoint nodes.
/// </summary>
public sealed record AnnotationEndpointRule
{
    public required FrozenDictionary<string, string> AnnotationToHttpMethod { get; init; }
    public FrozenSet<string> AnnotationNodeTypes { get; init; } = new[] { "annotation", "marker_annotation", "attribute", "attribute_group", "decorator" }.ToFrozenSet();
    public FrozenSet<string> PathArgumentKeys { get; init; } = new[] { "value", "path", "uri" }.ToFrozenSet();
    public FrozenSet<string> RoutePrefixAnnotationNames { get; init; } = new[] { "RequestMapping", "Path", "Controller" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
    public FrozenSet<string> WebSocketAnnotationNames { get; init; } = new[] { "WebSocket", "ServerEndpoint", "ServerWebSocket" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
    public bool EnrichJavaSecurityAndSchemas { get; init; } = true;
}

/// <summary>
/// Declarative rule for mapping outbound HTTP client calls and client annotations (e.g., HttpClient, RestTemplate, Feign, Guzzle) to ExternalService nodes.
/// </summary>
public sealed record OutboundHttpRule
{
    public FrozenSet<string> CallMethodNames { get; init; } = FrozenSet<string>.Empty;
    public FrozenSet<string> ClientAnnotationSuffixes { get; init; } = FrozenSet<string>.Empty;
    public FrozenSet<string> CallNodeTypes { get; init; } = new[] { "method_invocation", "call_expression", "member_call_expression", "scoped_call_expression", "invocation_expression", "call" }.ToFrozenSet();
    public FrozenSet<string> AnnotationNodeTypes { get; init; } = new[] { "annotation", "marker_annotation", "attribute", "attribute_group" }.ToFrozenSet();
    public string FallbackTarget { get; init; } = "http:unknown-service";
}

/// <summary>
/// Declarative rule for mapping SQL execution method calls and query annotations (e.g., JdbcTemplate, JPA @Query, PDO) to Query nodes and table dependencies.
/// </summary>
public sealed record SqlSinkRule
{
    public FrozenSet<string> CallMethodNames { get; init; } = FrozenSet<string>.Empty;
    public FrozenSet<string> QueryAnnotationSuffixes { get; init; } = FrozenSet<string>.Empty;
    public bool RequireExtractableSqlOnCall { get; init; } = true;
    public bool AppendMethodNameWhenNoSql { get; init; }
    public string FallbackQueryPrefix { get; init; } = "SQL Query";
    public FrozenSet<string> CallNodeTypes { get; init; } = new[] { "method_invocation", "call_expression", "member_call_expression", "scoped_call_expression", "invocation_expression", "call", "object_creation_expression", "new_expression" }.ToFrozenSet();
    public FrozenSet<string> AnnotationNodeTypes { get; init; } = new[] { "annotation", "marker_annotation", "attribute", "attribute_group" }.ToFrozenSet();
}

/// <summary>
/// Declarative rule for mapping ORM entity/table annotations (e.g., JPA @Table/@Entity, Doctrine #[ORM\Table]) to Table nodes and PERSISTED_IN relationships.
/// </summary>
public sealed record OrmTableRule
{
    public FrozenSet<string> TableAnnotationNames { get; init; } = new[] { "Table", "Entity", "Document", "MongoEntity" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
    public FrozenSet<string> NameArgumentKeys { get; init; } = new[] { "name", "value", "collection", "tableName" }.ToFrozenSet();
    public FrozenSet<string> AnnotationNodeTypes { get; init; } = new[] { "annotation", "marker_annotation", "attribute", "attribute_group" }.ToFrozenSet();
    public FrozenSet<string> ClassDeclarationNodeTypes { get; init; } = new[] { "class_declaration" }.ToFrozenSet();
    public FrozenSet<string> BaseClassNames { get; init; } = FrozenSet<string>.Empty;
    public FrozenSet<string> RepositoryInterfaceNames { get; init; } = FrozenSet<string>.Empty;
}

/// <summary>
/// Declarative rule for mapping message broker listeners and publishers (e.g., Kafka, RabbitMQ, Spring Events, Symfony Messenger, Laravel Events) to EntryPoint/Topic relationships.
/// </summary>
public sealed record MessagingBrokerRule
{
    public string Protocol { get; init; } = "";
    public FrozenSet<string> ListenerAnnotationNames { get; init; } = FrozenSet<string>.Empty;
    public FrozenSet<string> ProducerAnnotationNames { get; init; } = FrozenSet<string>.Empty;
    public FrozenSet<string> ChannelAnnotationNames { get; init; } = FrozenSet<string>.Empty;
    public FrozenSet<string> MethodLevelEventAnnotations { get; init; } = FrozenSet<string>.Empty;
    public FrozenSet<string> TopicArgumentKeys { get; init; } = new[] { "topics", "topicPattern", "queues", "queuesToDeclare", "bindings", "value" }.ToFrozenSet();
    public string FallbackTopicName { get; init; } = "unknown-topic";

    public FrozenSet<string> PublishMethodNames { get; init; } = FrozenSet<string>.Empty;
    public FrozenSet<string> PublishReceiverSubstrings { get; init; } = FrozenSet<string>.Empty;
    public bool ExtractEventTypeFromObjectCreationArg { get; init; }
    public bool PreferSecondArgWhenThreeOrMoreArgs { get; init; }
    public int PublishTopicArgumentIndex { get; init; }
    public string? StaticPublishTopic { get; init; }

    public FrozenSet<string> SubscribeMethodNames { get; init; } = FrozenSet<string>.Empty;
    public FrozenSet<string> SubscribeReceiverSubstrings { get; init; } = FrozenSet<string>.Empty;
    public FrozenSet<string> ConsumerInterfaceSubstrings { get; init; } = FrozenSet<string>.Empty;

    public FrozenSet<string> CallNodeTypes { get; init; } = new[] { "method_invocation", "call_expression", "member_call_expression", "scoped_call_expression", "invocation_expression", "call" }.ToFrozenSet();
    public FrozenSet<string> AnnotationNodeTypes { get; init; } = new[] { "annotation", "marker_annotation", "attribute", "attribute_group", "decorator" }.ToFrozenSet();
    public FrozenSet<string> ClassDeclarationNodeTypes { get; init; } = new[] { "class_declaration" }.ToFrozenSet();
}

/// <summary>
/// Declarative rule for mapping programmatic HTTP/RPC route registration calls (e.g., FastEndpoints, Carter, Gorilla Mux, Vert.x Router) to EXPOSES relationships.
/// </summary>
public sealed record InboundRouteCallRule
{
    public required FrozenDictionary<string, string> MethodToHttpVerb { get; init; }
    public FrozenSet<string> ReceiverSubstrings { get; init; } = FrozenSet<string>.Empty;
    public FrozenSet<string> ExcludedReceivers { get; init; } = FrozenSet<string>.Empty;
    public int PathArgumentIndex { get; init; }
    public string? StaticRoute { get; init; }
    public string RoutePrefix { get; init; } = "";
    public bool EmitEntryPointNode { get; init; }
    public bool RequireLeadingSlashOnPath { get; init; }
    public bool EmitHandlerTriggers { get; init; }
    public FrozenSet<string> CallNodeTypes { get; init; } = new[] { "method_invocation", "call_expression", "member_call_expression", "scoped_call_expression", "invocation_expression", "call", "new_expression", "object_creation_expression" }.ToFrozenSet();
}

/// <summary>
/// Declarative rule for mapping database, cache, state store, or vector store method calls (e.g., jOOQ, Redisson, Supabase, Qdrant, Cosmos DB) to USES_DB relationships.
/// </summary>
public sealed record DataStoreAccessRule
{
    public required FrozenSet<string> CallMethodNames { get; init; }
    public FrozenSet<string> ReceiverSubstrings { get; init; } = FrozenSet<string>.Empty;
    public int TableArgumentIndex { get; init; }
    public string? StaticTargetName { get; init; }
    public bool ParseSqlFromTarget { get; init; }
    public FrozenSet<string> CallNodeTypes { get; init; } = new[] { "method_invocation", "call_expression", "member_call_expression", "scoped_call_expression", "invocation_expression", "call" }.ToFrozenSet();
}

/// <summary>
/// Declarative rule for mapping AI agent, DI container, or client hook method calls (e.g., Semantic Kernel, LangChain4j, Uber Fx, SWR/TanStack Query) to CALLS or DEPENDS_ON relationships.
/// </summary>
public sealed record ServiceCallSinkRule
{
    public required FrozenSet<string> CallMethodNames { get; init; }
    public FrozenSet<string> ReceiverSubstrings { get; init; } = FrozenSet<string>.Empty;
    public string TargetRelation { get; init; } = OntologyConstants.Relationships.Calls;
    public string? StaticTargetName { get; init; }
    public int TargetArgumentIndex { get; init; }
    public bool CollectAllIdentifierArguments { get; init; }
    public bool RequireUrlOrPathArgument { get; init; }
    public bool UseMethodNameAsTarget { get; init; }
    public bool LowercaseMethodNameTarget { get; init; }
    public bool AllowIdentifierArgument { get; init; }
    public string TargetPrefix { get; init; } = "";
    public FrozenSet<string> CallNodeTypes { get; init; } = new[] { "method_invocation", "call_expression", "member_call_expression", "scoped_call_expression", "invocation_expression", "call" }.ToFrozenSet();
}

/// <summary>
/// Universal profile-driven semantic extension that evaluates declarative endpoint, HTTP client, SQL sink, ORM table, messaging, route call, data store, and service sink rules.
/// </summary>
public partial class DeclarativeSemanticExtension : ISemanticExtension
{
    public virtual string Id { get; init; } = "";
    public virtual string Name { get; init; } = "";
    public virtual SemanticExtensionType Type { get; init; } = SemanticExtensionType.Framework;
    public virtual LibraryRole LibraryRole { get; init; } = LibraryRole.General;
    public virtual IReadOnlyList<string> SupportedPatterns { get; init; } = [];
    public virtual bool IsBuiltIn { get; init; }
    public virtual bool IsImplemented { get; init; } = true;
    public virtual Func<ProjectContext, bool>? CustomMatch { get; init; }
    public virtual SemanticExtensionType? PackageDescriptorTypeOverride { get; init; }

    public virtual IReadOnlyList<AnnotationEndpointRule> AnnotationEndpointRules { get; init; } = [];
    public virtual IReadOnlyList<OutboundHttpRule> OutboundHttpRules { get; init; } = [];
    public virtual IReadOnlyList<SqlSinkRule> SqlSinkRules { get; init; } = [];
    public virtual IReadOnlyList<OrmTableRule> OrmTableRules { get; init; } = [];
    public virtual IReadOnlyList<MessagingBrokerRule> MessagingBrokerRules { get; init; } = [];
    public virtual IReadOnlyList<InboundRouteCallRule> InboundRouteCallRules { get; init; } = [];
    public virtual IReadOnlyList<DataStoreAccessRule> DataStoreAccessRules { get; init; } = [];
    public virtual IReadOnlyList<ServiceCallSinkRule> ServiceCallSinkRules { get; init; } = [];
    public virtual IReadOnlyList<AnnotationSinkRule> AnnotationSinkRules { get; init; } = [];
    public virtual IReadOnlyList<ClassHierarchySinkRule> ClassHierarchySinkRules { get; init; } = [];
    public virtual IReadOnlyList<CustomAstRule> CustomAstRules { get; init; } = [];
    public virtual IReadOnlyDictionary<string, NodeSelector> Selectors => System.Collections.Immutable.ImmutableDictionary<string, NodeSelector>.Empty;

    public virtual void DiscoverDeclarations(string filePath, string content, string? projectName, ParsingContext? ctx)
    {
    }

    public virtual void DiscoverDeclarations(Node rootNode, string relativePath, string? projectName, ParsingContext? ctx)
    {
    }

    public virtual void SynthesizeCloudResources(
        CodeExplorer.Core.Common.Nodes.Layer2_Boundaries.ProjectNode project,
        string effectiveSource,
        IReadOnlySet<string> packageNames,
        string? workloadId,
        CodeExplorer.Core.Common.Nodes.Layer4_Semantic.SemanticStructureNode semanticStructureNode,
        List<CodeExplorer.Core.Database.Relationship> semanticRelationships,
        HashSet<(string From, string To, string Kind)> existingRelKeys,
        ParsingContext ctx)
    {
    }

    public virtual IEnumerable<PackageDescriptor> GetPackageDescriptors()
    {
        yield return new PackageDescriptor(
            Id,
            Name,
            LibraryRole,
            PackageDescriptorTypeOverride ?? Type,
            SupportedPatterns,
            CustomMatch);
    }

    private CompiledDeclarativeRuleIndex? _compiledIndex;
    private CompiledDeclarativeRuleIndex CompiledIndex => _compiledIndex ??= CompiledDeclarativeRuleIndex.Build(this);

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (CustomAstRules.Count > 0)
        {
            var customKind = CompiledIndex.EvaluateCustomMapNodeType(node, ctx);
            if (customKind != null) return customKind;
        }

        if (node.SyntaxKind is not (SyntaxKind.Annotation or SyntaxKind.CallExpression or SyntaxKind.TypeDeclaration or SyntaxKind.SyntaxList))
        {
            return null;
        }

        for (var i = 0; i < AnnotationEndpointRules.Count; i++)
        {
            if (MatchesAnnotationEndpoint(node, AnnotationEndpointRules[i], out _))
            {
                return OntologyConstants.NodeLabels.EntryPoint;
            }
        }

        for (var i = 0; i < OutboundHttpRules.Count; i++)
        {
            if (MatchesOutboundHttp(node, OutboundHttpRules[i]))
            {
                return OntologyConstants.NodeLabels.ExternalService;
            }
        }

        for (var i = 0; i < SqlSinkRules.Count; i++)
        {
            if (MatchesSqlSink(node, SqlSinkRules[i]))
            {
                return OntologyConstants.NodeLabels.Query;
            }
        }

        for (var i = 0; i < OrmTableRules.Count; i++)
        {
            if (MatchesOrmTableAnnotation(node, OrmTableRules[i]))
            {
                return OntologyConstants.NodeLabels.Table;
            }
        }

        for (var i = 0; i < ClassHierarchySinkRules.Count; i++)
        {
            var rule = ClassHierarchySinkRules[i];
            if (rule.EmitTableNode && DeclarativeExtendedRuleEvaluator.MatchesClassHierarchyRule(node, rule, out _))
            {
                return OntologyConstants.NodeLabels.Table;
            }
        }

        for (var i = 0; i < MessagingBrokerRules.Count; i++)
        {
            var rule = MessagingBrokerRules[i];
            if (MatchesMessagingListenerAnnotation(node, rule) || MatchesMessagingProducerAnnotation(node, rule))
            {
                return OntologyConstants.NodeLabels.EntryPoint;
            }
        }

        for (var i = 0; i < InboundRouteCallRules.Count; i++)
        {
            var rule = InboundRouteCallRules[i];
            if (rule.EmitEntryPointNode && DeclarativeUniversalRuleEvaluator.TryMatchInboundRouteCall(node, rule, out _, out _))
            {
                return OntologyConstants.NodeLabels.EntryPoint;
            }
        }

        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx) =>
        ExtractIdentifier(node, ctx, null, null);

    public string? ExtractIdentifier(Node node, ParsingContext ctx, string? projectName) =>
        ExtractIdentifier(node, ctx, projectName, null);

    public string? ExtractIdentifier(Node node, ParsingContext ctx, string? projectName, string? relativePath)
    {
        if (CustomAstRules.Count > 0)
        {
            var customId = CompiledIndex.EvaluateCustomExtractIdentifier(node, ctx, projectName, relativePath);
            if (customId != null) return customId;
        }

        if (node.SyntaxKind is not (SyntaxKind.Annotation or SyntaxKind.CallExpression or SyntaxKind.TypeDeclaration or SyntaxKind.FunctionDeclaration or SyntaxKind.SyntaxList))
        {
            return null;
        }

        for (var i = 0; i < AnnotationEndpointRules.Count; i++)
        {
            var rule = AnnotationEndpointRules[i];
            if (MatchesAnnotationEndpoint(node, rule, out var httpMethod))
            {
                return ExtractAnnotationRoute(node, rule, httpMethod, projectName);
            }
        }

        for (var i = 0; i < OutboundHttpRules.Count; i++)
        {
            var rule = OutboundHttpRules[i];
            if (MatchesOutboundHttp(node, rule))
            {
                return ExtractOutboundHttpTarget(node, rule, projectName);
            }
        }

        for (var i = 0; i < SqlSinkRules.Count; i++)
        {
            var rule = SqlSinkRules[i];
            if (MatchesSqlSink(node, rule))
            {
                var sqlText = ExtractSqlText(node);
                if (!string.IsNullOrEmpty(sqlText))
                {
                    var clean = NestedSqlParser.CleanQueryText(sqlText);
                    if (NestedSqlParser.TryParseSql(sqlText, out var firstWord, out _))
                    {
                        return $"{firstWord} Query: {clean}";
                    }
                    if (rule.AppendMethodNameWhenNoSql)
                    {
                        var mSpan = GetCallMethodSpan(node);
                        if (!mSpan.IsEmpty)
                        {
                            return $"{rule.FallbackQueryPrefix}: {mSpan} {clean}";
                        }
                    }
                    return $"{rule.FallbackQueryPrefix}: {clean}";
                }
                if (rule.AppendMethodNameWhenNoSql)
                {
                    var methodSpan = GetCallMethodSpan(node);
                    return !methodSpan.IsEmpty
                        ? $"{rule.FallbackQueryPrefix}: {methodSpan}"
                        : rule.FallbackQueryPrefix;
                }
                return rule.FallbackQueryPrefix;
            }
        }

        for (var i = 0; i < OrmTableRules.Count; i++)
        {
            var rule = OrmTableRules[i];
            if (MatchesOrmTableAnnotation(node, rule))
            {
                return ExtractOrmTableName(node, rule);
            }
        }

        for (var i = 0; i < ClassHierarchySinkRules.Count; i++)
        {
            var rule = ClassHierarchySinkRules[i];
            if (rule.EmitTableNode && DeclarativeExtendedRuleEvaluator.MatchesClassHierarchyRule(node, rule, out var tableName))
            {
                return tableName;
            }
        }

        for (var i = 0; i < MessagingBrokerRules.Count; i++)
        {
            var rule = MessagingBrokerRules[i];
            if (MatchesMessagingListenerAnnotation(node, rule) || MatchesMessagingProducerAnnotation(node, rule))
            {
                var topics = ExtractMessagingTopics(node, rule);
                var primary = topics.Count > 0 ? topics[0] : rule.FallbackTopicName;
                return $"{rule.Protocol}:{primary}";
            }

            if (MatchesMethodLevelEventListener(node, rule))
            {
                var eventType = ExtractFirstParameterType(node);
                return !string.IsNullOrEmpty(eventType) ? $"EventListener:{eventType}" : "EventListener";
            }
        }

        for (var i = 0; i < InboundRouteCallRules.Count; i++)
        {
            var rule = InboundRouteCallRules[i];
            if (rule.EmitEntryPointNode && DeclarativeUniversalRuleEvaluator.TryMatchInboundRouteCall(node, rule, out var routeId, out _))
            {
                return routeId;
            }
        }

        return null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        CompiledIndex.EvaluateCollectReferences(node, scopeSymbolId, references, ctx);
    }

    public void EnrichSymbol(Node node, SyntacticSymbol symbol, ParsingContext ctx)
    {
        if (CustomAstRules.Count > 0)
        {
            CompiledIndex.EvaluateCustomEnrichSymbol(node, symbol, ctx);
        }
        if (symbol.Kind == CodeExplorer.Cypher.Common.SyntacticSymbolKind.Query &&
            string.IsNullOrEmpty(symbol.Text) &&
            SqlSinkRules.Count > 0)
        {
            var sqlText = ExtractSqlText(node);
            if (!string.IsNullOrEmpty(sqlText))
            {
                symbol.Text = sqlText;
            }
        }

        if (node.SyntaxKind is not (SyntaxKind.Annotation or SyntaxKind.TypeDeclaration or SyntaxKind.SyntaxList))
        {
            return;
        }

        for (var i = 0; i < AnnotationEndpointRules.Count; i++)
        {
            var rule = AnnotationEndpointRules[i];
            if (MatchesAnnotationEndpoint(node, rule, out var httpMethod))
            {
                if (httpMethod == "WS")
                {
                    symbol.Protocol = "WS";
                }
                if (rule.EnrichJavaSecurityAndSchemas)
                {
                    EnrichFromEnclosingDeclarations(node, symbol);
                }
            }
        }

        for (var i = 0; i < OrmTableRules.Count; i++)
        {
            var rule = OrmTableRules[i];
            if (rule.ClassDeclarationNodeTypes.Contains(node.Type))
            {
                var annotations = GetNodeAnnotations(node, rule.AnnotationNodeTypes);
                var isEntity = annotations.Any(a =>
                {
                    var name = GetShortAnnotationName(a);
                    return name != null && rule.TableAnnotationNames.Contains(name);
                });

                var baseClassName = ExtractBaseClassName(node);
                var isBaseMatch = !string.IsNullOrEmpty(baseClassName) && rule.BaseClassNames.Contains(baseClassName);

                if (isEntity || isBaseMatch)
                {
                    string? explicitName = null;
                    foreach (var a in annotations)
                    {
                        var aName = GetShortAnnotationName(a);
                        if (aName != null && rule.TableAnnotationNames.Contains(aName))
                        {
                            var extracted = ExtractOrmTableName(a, rule);
                            if (!string.IsNullOrEmpty(extracted))
                            {
                                explicitName = extracted;
                                break;
                            }
                        }
                    }

                    var tableName = explicitName ?? symbol.Name;
                    if (!string.IsNullOrEmpty(tableName))
                    {
                        symbol.References.Add(new Reference(symbol.Name, tableName, OntologyConstants.Relationships.PersistedIn));
                    }
                }

                if (rule.RepositoryInterfaceNames.Count > 0)
                {
                    var entityType = ExtractGenericTypeFromInterfaces(node, rule.RepositoryInterfaceNames);
                    if (!string.IsNullOrEmpty(entityType))
                    {
                        symbol.References.Add(new Reference(symbol.Name, entityType, OntologyConstants.Relationships.AccessedBy));
                    }
                }
            }
        }

        foreach (var rule in MessagingBrokerRules)
        {
            if (MatchesMessagingListenerAnnotation(node, rule))
            {
                symbol.Protocol = rule.Protocol;
                var topics = ExtractMessagingTopics(node, rule);
                foreach (var topic in topics)
                {
                    symbol.References.Add(new Reference(symbol.Name, $"{rule.Protocol}:{topic}", OntologyConstants.Relationships.SubscribesTo));
                }
            }
            else if (MatchesMessagingProducerAnnotation(node, rule))
            {
                symbol.Protocol = rule.Protocol;
                var topics = ExtractMessagingTopics(node, rule);
                foreach (var topic in topics)
                {
                    symbol.References.Add(new Reference(symbol.Name, $"{rule.Protocol}:{topic}", OntologyConstants.Relationships.PublishesTo));
                }
            }
        }
    }

    #region Annotation Endpoint Helpers

    private static readonly string[] FunctionNodeTypes = ["method_declaration", "constructor_declaration", "function_declaration", "arrow_function"];
    private static readonly string[] TypeDeclNodeTypes = ["class_declaration", "interface_declaration", "record_declaration", "struct_declaration"];

    private static bool MatchesAnnotationEndpoint(Node node, AnnotationEndpointRule rule, out string httpMethod)
    {
        httpMethod = "GET";
        if (!rule.AnnotationNodeTypes.Contains(node.Type)) return false;

        var name = GetShortAnnotationName(node);
        if (string.IsNullOrEmpty(name)) return false;

        if (rule.WebSocketAnnotationNames.Contains(name))
        {
            httpMethod = "WS";
            return true;
        }

        if (!rule.AnnotationToHttpMethod.TryGetValue(name, out var m)) return false;
        httpMethod = m;

        // Class/interface level annotations (e.g. @RequestMapping("/owners")) serve as route prefixes
        // for member methods and are not standalone invokeable endpoints.
        var targetDecl = node.Parent;
        while (targetDecl.IsValid())
        {
            if (targetDecl is FunctionDeclSyntax || targetDecl.IsAny(FunctionNodeTypes))
            {
                // Check if the enclosing type has client annotations (e.g. @FeignClient, @RegisterRestClient) - client interfaces define outbound calls, not endpoints
                var typeDecl = targetDecl.Parent;
                while (typeDecl.IsValid())
                {
                    if (typeDecl is TypeDeclSyntax || typeDecl.IsAny(TypeDeclNodeTypes))
                    {
                        var typeAnnots = GetNodeAnnotations(typeDecl, rule.AnnotationNodeTypes);
                        if (typeAnnots.Any(a =>
                        {
                            var aName = GetShortAnnotationName(a);
                            return !string.IsNullOrEmpty(aName) &&
                                   (aName.EndsWith("Client", StringComparison.OrdinalIgnoreCase) ||
                                    aName.Equals("FeignClient", StringComparison.OrdinalIgnoreCase) ||
                                    aName.Equals("HttpExchange", StringComparison.OrdinalIgnoreCase) ||
                                    aName.Equals("RegisterRestClient", StringComparison.OrdinalIgnoreCase) ||
                                    aName.Equals("RestClient", StringComparison.OrdinalIgnoreCase));
                        }))
                        {
                            return false;
                        }
                        break;
                    }
                    typeDecl = typeDecl.Parent;
                }

                // If this is a route prefix annotation (@Path or @RequestMapping) on a method that also has a sibling HTTP verb (@GET, etc.), let the verb handle the endpoint
                if (rule.RoutePrefixAnnotationNames.Contains(name))
                {
                    var siblingAnnots = GetNodeAnnotations(targetDecl, rule.AnnotationNodeTypes);
                    if (siblingAnnots.Any(a => a != node &&
                        GetShortAnnotationName(a) is { } saName &&
                        !rule.RoutePrefixAnnotationNames.Contains(saName) &&
                        rule.AnnotationToHttpMethod.ContainsKey(saName)))
                    {
                        return false;
                    }
                }

                if (httpMethod == "ALL" || string.Equals(name, "RequestMapping", StringComparison.OrdinalIgnoreCase))
                {
                    var explicitMethod = ExtractHttpMethodFromAnnotation(node);
                    if (!string.IsNullOrEmpty(explicitMethod))
                    {
                        httpMethod = explicitMethod;
                    }
                }

                return true;
            }
            if (targetDecl is TypeDeclSyntax || targetDecl.IsAny(TypeDeclNodeTypes))
            {
                return false;
            }
            targetDecl = targetDecl.Parent;
        }

        return true;
    }

    private static string ExtractAnnotationRoute(Node annotationNode, AnnotationEndpointRule rule, string httpMethod, string? projectName = null)
    {
        var name = GetShortAnnotationName(annotationNode);
        if (!string.IsNullOrEmpty(name) && rule.WebSocketAnnotationNames.Contains(name))
        {
            var wsPath = ExtractNamedOrFirstStringFromAnnotation(annotationNode, rule.PathArgumentKeys) ?? "/";
            wsPath = ConfigurationIndex.ResolvePlaceholders(wsPath, projectName);
            return $"WS:{NormalizeDeclarativeRoute(wsPath)}";
        }

        var methodPath = ExtractNamedOrFirstStringFromAnnotation(annotationNode, rule.PathArgumentKeys);
        if (string.IsNullOrEmpty(methodPath))
        {
            // Check enclosing method for sibling route prefix annotation (e.g. @Path)
            var targetDecl = annotationNode.Parent;
            while (targetDecl.IsValid() && !(targetDecl is FunctionDeclSyntax || targetDecl.IsAny(FunctionNodeTypes)))
            {
                if (targetDecl is TypeDeclSyntax || targetDecl.IsAny(TypeDeclNodeTypes)) break;
                targetDecl = targetDecl.Parent;
            }
            if (targetDecl.IsValid() && (targetDecl is FunctionDeclSyntax || targetDecl.IsAny(FunctionNodeTypes)))
            {
                var siblingAnnots = GetNodeAnnotations(targetDecl, rule.AnnotationNodeTypes);
                foreach (var sa in siblingAnnots)
                {
                    var saName = GetShortAnnotationName(sa);
                    if (!string.IsNullOrEmpty(saName) && rule.RoutePrefixAnnotationNames.Contains(saName))
                    {
                        var sp = ExtractNamedOrFirstStringFromAnnotation(sa, rule.PathArgumentKeys);
                        if (!string.IsNullOrEmpty(sp))
                        {
                            methodPath = sp;
                            break;
                        }
                    }
                }
            }
        }

        methodPath ??= "";

        // Check if enclosing type has a controller-level route prefix annotation (e.g. @RequestMapping or @Path)
        string? classPrefix = null;
        var curr = annotationNode.Parent;
        while (curr.IsValid())
        {
            if (curr is TypeDeclSyntax || curr.IsAny(TypeDeclNodeTypes))
            {
                var classAnnots = GetNodeAnnotations(curr, rule.AnnotationNodeTypes);
                foreach (var ca in classAnnots)
                {
                    var aName = GetShortAnnotationName(ca);
                    if (!string.IsNullOrEmpty(aName) && (rule.RoutePrefixAnnotationNames.Contains(aName) || rule.AnnotationToHttpMethod.ContainsKey(aName)))
                    {
                        var p = ExtractNamedOrFirstStringFromAnnotation(ca, rule.PathArgumentKeys);
                        if (!string.IsNullOrEmpty(p))
                        {
                            classPrefix = p;
                            break;
                        }
                    }
                }
                break;
            }
            curr = curr.Parent;
        }

        var fullRoute = CombineDeclarativeRoutes(classPrefix ?? "", methodPath);
        fullRoute = ConfigurationIndex.ResolvePlaceholders(fullRoute, projectName);
        return $"{httpMethod}:{fullRoute}";
    }

    internal static string NormalizeDeclarativeRoute(string raw)
    {
        var trimmed = (raw ?? "").Trim();
        if (string.IsNullOrEmpty(trimmed) || trimmed == "/") return "/";
        return trimmed.StartsWith('/') ? trimmed : "/" + trimmed;
    }

    private static string? ExtractBaseClassName(Node node)
    {
        var superclass = node.Children.FirstOrDefault(c => c is BaseTypeClauseSyntax { IsImplements: false });
        if (superclass.IsValid())
        {
            var id = superclass.Children.FirstOrDefault(c => c is IdentifierExprSyntax or TypeRefSyntax);
            if (id.IsValid()) return id.Text;
            var text = superclass.Text.Replace("extends", "").Trim();
            var angle = text.IndexOf('<');
            if (angle > 0) text = text[..angle];
            return text.Trim();
        }
        return null;
    }

    internal static string? ExtractGenericTypeFromInterfaces(Node node, FrozenSet<string> repositoryInterfaceNames)
    {
        Node? ifaces = null;
        for (var i = 0; i < node.Children.Count; i++)
        {
            var c = node.Children[i];
            if (c is BaseTypeClauseSyntax { IsImplements: true } || c.Type == "base_list")
            {
                ifaces = c;
                break;
            }
        }

        var span = ifaces != null && ifaces.IsValid() ? ifaces.AsSpan() : node.AsSpan();
        var braceIdx = span.IndexOf('{');
        if (braceIdx > 0) span = span[..braceIdx];

        foreach (var rName in repositoryInterfaceNames)
        {
            var idx = span.IndexOf(rName.AsSpan(), StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                var afterMatch = span[idx..];
                var open = afterMatch.IndexOf('<');
                var close = open > 0 ? afterMatch[open..].IndexOf('>') : -1;
                if (open > 0 && close > 0)
                {
                    var genericParam = afterMatch.Slice(open + 1, close - 1).Trim();
                    var comma = genericParam.IndexOf(',');
                    if (comma > 0) genericParam = genericParam[..comma].Trim();
                    return genericParam.ToString();
                }
            }
        }
        return null;
    }

    internal static bool IsChannelEmitter(Node node)
    {
        var parent = node.Parent;
        while (parent.IsValid())
        {
            var text = parent.Text;
            if (text.Contains("Emitter", StringComparison.OrdinalIgnoreCase)) return true;
            if (parent is FunctionDeclSyntax || parent.IsAny(FunctionNodeTypes) || parent is TypeDeclSyntax || parent.IsAny(TypeDeclNodeTypes))
            {
                break;
            }
            parent = parent.Parent;
        }
        return false;
    }

    internal static string CombineDeclarativeRoutes(string prefix, string path)
    {
        prefix = (prefix ?? "").Trim('/');
        path = (path ?? "").Trim('/');
        if (string.IsNullOrEmpty(prefix) && string.IsNullOrEmpty(path)) return "/";
        if (string.IsNullOrEmpty(prefix)) return "/" + path;
        if (string.IsNullOrEmpty(path)) return "/" + prefix;
        return $"/{prefix}/{path}";
    }

    private static void EnrichFromEnclosingDeclarations(Node annotationNode, SyntacticSymbol symbol) =>
        DeclarativeOutboundAndSecurityHelper.EnrichFromEnclosingDeclarations(annotationNode, symbol);

    #endregion

    #region Outbound HTTP Helpers

    private static bool MatchesOutboundHttp(Node node, OutboundHttpRule rule) =>
        DeclarativeOutboundAndSecurityHelper.MatchesOutboundHttp(node, rule);

    private static string ExtractOutboundHttpTarget(Node node, OutboundHttpRule rule, string? projectName = null) =>
        DeclarativeOutboundAndSecurityHelper.ExtractOutboundHttpTarget(node, rule, projectName);

    #endregion

}
