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
    public FrozenSet<string> PathArgumentKeys { get; init; } = new[] { "value", "path" }.ToFrozenSet();
    public FrozenSet<string> RoutePrefixAnnotationNames { get; init; } = new[] { "RequestMapping", "Path" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
    public FrozenSet<string> WebSocketAnnotationNames { get; init; } = new[] { "WebSocket", "ServerEndpoint" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
    public bool EnrichJavaSecurityAndSchemas { get; init; } = true;
}

/// <summary>
/// Declarative rule for mapping outbound HTTP client calls and client annotations (e.g., HttpClient, RestTemplate, Feign, Guzzle) to ExternalService nodes.
/// </summary>
public sealed record OutboundHttpRule
{
    public FrozenSet<string> CallMethodNames { get; init; } = FrozenSet<string>.Empty;
    public FrozenSet<string> ClientAnnotationSuffixes { get; init; } = FrozenSet<string>.Empty;
    public FrozenSet<string> CallNodeTypes { get; init; } = new[] { "method_invocation", "call_expression", "member_call_expression", "scoped_call_expression" }.ToFrozenSet();
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
    public string FallbackQueryPrefix { get; init; } = "SQL Query";
    public FrozenSet<string> CallNodeTypes { get; init; } = new[] { "method_invocation", "call_expression", "member_call_expression", "scoped_call_expression" }.ToFrozenSet();
    public FrozenSet<string> AnnotationNodeTypes { get; init; } = new[] { "annotation", "marker_annotation", "attribute", "attribute_group" }.ToFrozenSet();
}

/// <summary>
/// Declarative rule for mapping ORM entity/table annotations (e.g., JPA @Table/@Entity, Doctrine #[ORM\Table]) to Table nodes and PERSISTED_IN relationships.
/// </summary>
public sealed record OrmTableRule
{
    public FrozenSet<string> TableAnnotationNames { get; init; } = new[] { "Table", "Entity", "Document", "MongoEntity" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
    public FrozenSet<string> NameArgumentKeys { get; init; } = new[] { "name", "value", "collection" }.ToFrozenSet();
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
    public required string Protocol { get; init; }
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

    public FrozenSet<string> SubscribeMethodNames { get; init; } = FrozenSet<string>.Empty;
    public FrozenSet<string> SubscribeReceiverSubstrings { get; init; } = FrozenSet<string>.Empty;

    public FrozenSet<string> CallNodeTypes { get; init; } = new[] { "method_invocation", "call_expression", "member_call_expression", "scoped_call_expression" }.ToFrozenSet();
    public FrozenSet<string> AnnotationNodeTypes { get; init; } = new[] { "annotation", "marker_annotation", "attribute", "attribute_group" }.ToFrozenSet();
}

/// <summary>
/// Universal profile-driven semantic extension that evaluates declarative endpoint, HTTP client, SQL sink, ORM table, and messaging rules.
/// </summary>
public partial class DeclarativeSemanticExtension : ISemanticExtension
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public SemanticExtensionType Type { get; init; } = SemanticExtensionType.Framework;
    public LibraryRole LibraryRole { get; init; } = LibraryRole.General;
    public IReadOnlyList<string> SupportedPatterns { get; init; } = [];
    public bool IsBuiltIn { get; init; }
    public bool IsImplemented { get; init; } = true;
    public Func<ProjectContext, bool>? CustomMatch { get; init; }
    public SemanticExtensionType? PackageDescriptorTypeOverride { get; init; }

    public IReadOnlyList<AnnotationEndpointRule> AnnotationEndpointRules { get; init; } = [];
    public IReadOnlyList<OutboundHttpRule> OutboundHttpRules { get; init; } = [];
    public IReadOnlyList<SqlSinkRule> SqlSinkRules { get; init; } = [];
    public IReadOnlyList<OrmTableRule> OrmTableRules { get; init; } = [];
    public IReadOnlyList<MessagingBrokerRule> MessagingBrokerRules { get; init; } = [];

    public IEnumerable<PackageDescriptor> GetPackageDescriptors()
    {
        yield return new PackageDescriptor(
            Id,
            Name,
            LibraryRole,
            PackageDescriptorTypeOverride ?? Type,
            SupportedPatterns,
            CustomMatch);
    }

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        foreach (var rule in AnnotationEndpointRules)
        {
            if (MatchesAnnotationEndpoint(node, rule, out _))
            {
                return OntologyConstants.NodeLabels.EntryPoint;
            }
        }

        foreach (var rule in OutboundHttpRules)
        {
            if (MatchesOutboundHttp(node, rule))
            {
                return OntologyConstants.NodeLabels.ExternalService;
            }
        }

        foreach (var rule in SqlSinkRules)
        {
            if (MatchesSqlSink(node, rule))
            {
                return OntologyConstants.NodeLabels.Query;
            }
        }

        foreach (var rule in OrmTableRules)
        {
            if (MatchesOrmTableAnnotation(node, rule))
            {
                return OntologyConstants.NodeLabels.Table;
            }
        }

        foreach (var rule in MessagingBrokerRules)
        {
            if (MatchesMessagingListenerAnnotation(node, rule) || MatchesMessagingProducerAnnotation(node, rule))
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
        foreach (var rule in AnnotationEndpointRules)
        {
            if (MatchesAnnotationEndpoint(node, rule, out var httpMethod))
            {
                return ExtractAnnotationRoute(node, rule, httpMethod);
            }
        }

        foreach (var rule in OutboundHttpRules)
        {
            if (MatchesOutboundHttp(node, rule))
            {
                return ExtractOutboundHttpTarget(node, rule);
            }
        }

        foreach (var rule in SqlSinkRules)
        {
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
                    return $"{rule.FallbackQueryPrefix}: {clean}";
                }
                return rule.FallbackQueryPrefix;
            }
        }

        foreach (var rule in OrmTableRules)
        {
            if (MatchesOrmTableAnnotation(node, rule))
            {
                return ExtractOrmTableName(node, rule);
            }
        }

        foreach (var rule in MessagingBrokerRules)
        {
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

        return null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        foreach (var rule in SqlSinkRules)
        {
            if (MatchesSqlSink(node, rule))
            {
                var sqlText = ExtractSqlText(node);
                if (!string.IsNullOrEmpty(sqlText))
                {
                    NestedSqlParser.TryDetectSqlDependencies(sqlText, scopeSymbolId, references);
                }
            }
        }

        foreach (var rule in OrmTableRules)
        {
            if (MatchesOrmTableAnnotation(node, rule))
            {
                var tableName = ExtractOrmTableName(node, rule);
                if (!string.IsNullOrEmpty(tableName))
                {
                    references.Add(new Reference(scopeSymbolId, tableName, OntologyConstants.Relationships.PersistedIn));
                }
            }
        }

        foreach (var rule in MessagingBrokerRules)
        {
            if (MatchesMessagingListenerAnnotation(node, rule))
            {
                var topics = ExtractMessagingTopics(node, rule);
                foreach (var topic in topics)
                {
                    references.Add(new Reference(scopeSymbolId, $"{rule.Protocol}:{topic}", OntologyConstants.Relationships.SubscribesTo));
                }

                var methodName = FindEnclosingMethodName(node);
                if (!string.IsNullOrEmpty(methodName))
                {
                    references.Add(new Reference(scopeSymbolId, methodName, OntologyConstants.Relationships.Triggers));
                }
            }
            else if (MatchesMessagingProducerAnnotation(node, rule))
            {
                var topics = ExtractMessagingTopics(node, rule);
                foreach (var topic in topics)
                {
                    references.Add(new Reference(scopeSymbolId, $"{rule.Protocol}:{topic}", OntologyConstants.Relationships.PublishesTo));
                }
            }
            else if (MatchesMessagingChannelAnnotation(node, rule))
            {
                var topics = ExtractMessagingTopics(node, rule);
                var isEmitter = IsChannelEmitter(node);
                var rel = isEmitter ? OntologyConstants.Relationships.PublishesTo : OntologyConstants.Relationships.SubscribesTo;
                foreach (var topic in topics)
                {
                    references.Add(new Reference(scopeSymbolId, $"{rule.Protocol}:{topic}", rel));
                }
            }
            else if (MatchesMethodLevelEventListener(node, rule))
            {
                var eventType = ExtractFirstParameterType(node);
                if (!string.IsNullOrEmpty(eventType))
                {
                    references.Add(new Reference(scopeSymbolId, $"{rule.Protocol}:{eventType}", OntologyConstants.Relationships.SubscribesTo));
                }
            }
            else if (rule.CallNodeTypes.Contains(node.Type))
            {
                TryCollectMessagingPublishReferences(node, scopeSymbolId, references, rule);
                TryCollectMessagingSubscribeReferences(node, scopeSymbolId, references, rule);
            }
        }
    }

    public void EnrichSymbol(Node node, SyntacticSymbol symbol, ParsingContext ctx)
    {
        foreach (var rule in AnnotationEndpointRules)
        {
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

        foreach (var rule in OrmTableRules)
        {
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

    private static string ExtractAnnotationRoute(Node annotationNode, AnnotationEndpointRule rule, string httpMethod)
    {
        var name = GetShortAnnotationName(annotationNode);
        if (!string.IsNullOrEmpty(name) && rule.WebSocketAnnotationNames.Contains(name))
        {
            var wsPath = ExtractNamedOrFirstStringFromAnnotation(annotationNode, rule.PathArgumentKeys) ?? "/";
            return $"WS:{NormalizeRoute(wsPath)}";
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

        var fullRoute = CombineRoutes(classPrefix ?? "", methodPath);
        return $"{httpMethod}:{fullRoute}";
    }

    private static string NormalizeRoute(string raw)
    {
        var trimmed = (raw ?? "").Trim();
        if (string.IsNullOrEmpty(trimmed) || trimmed == "/") return "/";
        return trimmed.StartsWith('/') ? trimmed : "/" + trimmed;
    }

    private static string? ExtractBaseClassName(Node node)
    {
        var superclass = node.Children.FirstOrDefault(c => c.Type is "superclass" or "extends_clause");
        if (superclass.IsValid())
        {
            var id = superclass.Children.FirstOrDefault(c => c.Type is "type_identifier" or "identifier" or "scoped_type_identifier");
            if (id.IsValid()) return id.Text;
            var text = superclass.Text.Replace("extends", "").Trim();
            var angle = text.IndexOf('<');
            if (angle > 0) text = text[..angle];
            return text.Trim();
        }
        return null;
    }

    private static string? ExtractGenericTypeFromInterfaces(Node node, FrozenSet<string> repositoryInterfaceNames)
    {
        var ifaces = node.Children.FirstOrDefault(c => c.Type is "interfaces" or "implements_clause" or "extends_interfaces" or "super_interfaces");
        var text = ifaces.IsValid() ? ifaces.Text : node.Text;
        foreach (var rName in repositoryInterfaceNames)
        {
            var idx = text.IndexOf(rName, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                var open = text.IndexOf('<', idx);
                var close = open > 0 ? text.IndexOf('>', open) : -1;
                if (open > 0 && close > open)
                {
                    var genericParam = text[(open + 1)..close].Trim();
                    var comma = genericParam.IndexOf(',');
                    if (comma > 0) genericParam = genericParam[..comma].Trim();
                    return genericParam;
                }
            }
        }
        return null;
    }

    private static bool IsChannelEmitter(Node node)
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

    private static string CombineRoutes(string prefix, string path)
    {
        prefix = (prefix ?? "").Trim('/');
        path = (path ?? "").Trim('/');
        if (string.IsNullOrEmpty(prefix) && string.IsNullOrEmpty(path)) return "/";
        if (string.IsNullOrEmpty(prefix)) return "/" + path;
        if (string.IsNullOrEmpty(path)) return "/" + prefix;
        return $"/{prefix}/{path}";
    }

    private static void EnrichFromEnclosingDeclarations(Node annotationNode, SyntacticSymbol symbol)
    {
        var targetDecl = annotationNode.Parent;
        while (targetDecl.IsValid())
        {
            if (targetDecl is FunctionDeclSyntax or TypeDeclSyntax)
            {
                EnrichSecurityFromAnnotations(targetDecl, symbol);
                break;
            }
            targetDecl = targetDecl.Parent;
        }

        if (targetDecl is FunctionDeclSyntax)
        {
            ExtractMethodPayloadSchemas(targetDecl, symbol);

            var classDecl = targetDecl.Parent;
            while (classDecl.IsValid())
            {
                if (classDecl is TypeDeclSyntax)
                {
                    EnrichSecurityFromAnnotations(classDecl, symbol);
                    break;
                }
                classDecl = classDecl.Parent;
            }
        }
    }

    private static void ExtractMethodPayloadSchemas(Node methodDecl, SyntacticSymbol symbol)
    {
        var typeNode = methodDecl.GetField(SyntaxFields.Type);
        if (typeNode.IsValid())
        {
            var ret = UnwrapGenericResponseType(typeNode.Text);
            if (!string.IsNullOrEmpty(ret) && ret != "void")
            {
                symbol.ResponseType = ret;
            }
        }

        var paramList = methodDecl.GetField(SyntaxFields.Parameters);
        if (paramList.IsValid())
        {
            foreach (var param in paramList.Children)
            {
                if (param is not ParameterDeclSyntax && !param.Type.Contains("parameter", StringComparison.Ordinal)) continue;

                var isRequestBody = param.Text.Contains("@RequestBody", StringComparison.Ordinal) ||
                                    param.Text.Contains("@ModelAttribute", StringComparison.Ordinal);
                var pType = param.GetField(SyntaxFields.Type);
                if (pType.IsValid())
                {
                    var pTypeName = UnwrapGenericResponseType(pType.Text);
                    if (isRequestBody || (!IsPrimitiveOrFrameworkParamType(pTypeName) && symbol.RequestType == null))
                    {
                        symbol.RequestType = pTypeName;
                        if (isRequestBody) break;
                    }
                }
            }
        }
    }

    private static string UnwrapGenericResponseType(string rawType)
    {
        if (string.IsNullOrWhiteSpace(rawType)) return "";
        var type = rawType.Trim();
        while (true)
        {
            var genericIdx = type.IndexOf('<');
            if (genericIdx > 0 && type.EndsWith('>'))
            {
                var outer = type[..genericIdx].Trim();
                if (outer is "ResponseEntity" or "CompletableFuture" or "Mono" or "Flux" or "List" or "Set" or "Collection" or "Optional" or "HttpEntity")
                {
                    type = type[(genericIdx + 1)..^1].Trim();
                    continue;
                }
            }
            break;
        }
        return type;
    }

    private static bool IsPrimitiveOrFrameworkParamType(string type)
    {
        return type is "int" or "long" or "String" or "boolean" or "double" or "float" or "byte" or "short" or "char"
            or "Integer" or "Long" or "Boolean" or "Double" or "Float" or "UUID" or "Date" or "Instant" or "LocalDate"
            or "LocalDateTime" or "HttpServletRequest" or "HttpServletResponse" or "Principal" or "Authentication"
            or "HttpSession" or "Object";
    }

    private static void EnrichSecurityFromAnnotations(Node targetDecl, SyntacticSymbol symbol)
    {
        var annotations = GetNodeAnnotations(targetDecl, new[] { "annotation", "marker_annotation", "attribute", "attribute_group" }.ToFrozenSet());
        foreach (var annot in annotations)
        {
            var name = GetShortAnnotationName(annot);
            if (string.IsNullOrEmpty(name)) continue;

            if (name is "PermitAll" or "AnonymousAllowed" or "AllowAnonymous")
            {
                symbol.IsAnonymous = true;
            }
            else if (name is "RolesAllowed" or "Secured")
            {
                var role = ExtractNamedOrFirstStringFromAnnotation(annot, new[] { "value", "roles" }.ToFrozenSet());
                if (!string.IsNullOrEmpty(role))
                {
                    symbol.RequiredRoles = string.IsNullOrEmpty(symbol.RequiredRoles) ? role : $"{symbol.RequiredRoles},{role}";
                }
            }
            else if (name is "PreAuthorize" or "PostAuthorize")
            {
                var expr = ExtractNamedOrFirstStringFromAnnotation(annot, new[] { "value" }.ToFrozenSet());
                if (!string.IsNullOrEmpty(expr))
                {
                    symbol.Policies = string.IsNullOrEmpty(symbol.Policies) ? expr : $"{symbol.Policies},{expr}";
                    if (expr.Contains("hasRole", StringComparison.OrdinalIgnoreCase) || expr.Contains("hasAuthority", StringComparison.OrdinalIgnoreCase))
                    {
                        var startQuote = expr.IndexOf('\'');
                        var endQuote = expr.LastIndexOf('\'');
                        if (startQuote >= 0 && endQuote > startQuote)
                        {
                            var role = expr[(startQuote + 1)..endQuote];
                            symbol.RequiredRoles = string.IsNullOrEmpty(symbol.RequiredRoles) ? role : $"{symbol.RequiredRoles},{role}";
                        }
                    }
                }
            }
            else if (name is "QueryMapping" or "MutationMapping" or "SubscriptionMapping")
            {
                symbol.Protocol = "GraphQL";
            }
            else if (name is "GrpcService")
            {
                symbol.Protocol = "gRPC";
            }
        }
    }

    #endregion

    #region Outbound HTTP Helpers

    private static bool MatchesOutboundHttp(Node node, OutboundHttpRule rule)
    {
        if (rule.CallNodeTypes.Contains(node.Type))
        {
            var name = GetCallMethodName(node);
            if (!string.IsNullOrEmpty(name) && rule.CallMethodNames.Contains(name))
            {
                return true;
            }
        }

        if (rule.AnnotationNodeTypes.Contains(node.Type))
        {
            var annotName = GetShortAnnotationName(node);
            if (!string.IsNullOrEmpty(annotName) &&
                rule.ClientAnnotationSuffixes.Any(suffix => annotName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        if (node is TypeDeclSyntax { Kind: TypeSyntaxKind.Interface } && rule.ClientAnnotationSuffixes.Count > 0)
        {
            var annotations = GetNodeAnnotations(node, rule.AnnotationNodeTypes);
            if (annotations.Any(a =>
                {
                    var n = GetShortAnnotationName(a);
                    return !string.IsNullOrEmpty(n) &&
                           rule.ClientAnnotationSuffixes.Any(s => n.EndsWith(s, StringComparison.OrdinalIgnoreCase));
                }))
            {
                return true;
            }
        }

        return false;
    }

    private static string ExtractOutboundHttpTarget(Node node, OutboundHttpRule rule)
    {
        var targetNode = node;
        if (node is TypeDeclSyntax { Kind: TypeSyntaxKind.Interface })
        {
            var annotations = GetNodeAnnotations(node, rule.AnnotationNodeTypes);
            var clientAnnot = annotations.FirstOrDefault(a =>
            {
                var n = GetShortAnnotationName(a);
                return !string.IsNullOrEmpty(n) &&
                       rule.ClientAnnotationSuffixes.Any(s => n.EndsWith(s, StringComparison.OrdinalIgnoreCase));
            });
            if (clientAnnot.IsValid())
            {
                targetNode = clientAnnot;
            }
        }

        if (rule.AnnotationNodeTypes.Contains(targetNode.Type))
        {
            string? nameVal = null;
            string? pathVal = null;

            var str = targetNode.Children.FirstOrDefault(IsStringLiteral);
            if (str.IsValid()) nameVal = str.Text.Trim('"', '\'');

            var pairs = new List<Node>();
            CollectNodesByType(targetNode, "element_value_pair", pairs);
            foreach (var pair in pairs)
            {
                var key = pair.Children.FirstOrDefault(c => c is IdentifierExprSyntax);
                var val = pair.Children.FirstOrDefault(IsStringLiteral);
                if (key.IsValid() && val.IsValid())
                {
                    var keyText = key.Text;
                    var valText = val.Text.Trim('"', '\'');
                    if (keyText is "name" or "value" or "configKey" or "serviceId")
                    {
                        nameVal = valText;
                    }
                    else if (keyText is "path")
                    {
                        pathVal = valText;
                    }
                    else if (keyText is "url")
                    {
                        var rawUrl = valText;
                        if (rawUrl.StartsWith("${") && rawUrl.EndsWith('}'))
                        {
                            var propKey = rawUrl[2..^1].Trim();
                            if (ConstantRegistry.TryResolve(null, propKey, out var constVal) && !string.IsNullOrEmpty(constVal))
                            {
                                rawUrl = constVal;
                            }
                            else if (ConfigurationIndex.TryGetConfig(null, propKey, out var confVal) && !string.IsNullOrEmpty(confVal))
                            {
                                rawUrl = confVal;
                            }
                        }
                        var norm = RouteDictionaryRegistry.NormalizeResolvedUrl(rawUrl);
                        if (!string.IsNullOrEmpty(norm) && norm != "*")
                        {
                            return norm;
                        }
                    }
                }
            }

            if (string.IsNullOrEmpty(nameVal) && node is TypeDeclSyntax typeDecl)
            {
                nameVal = typeDecl.Name;
            }

            if (!string.IsNullOrEmpty(nameVal))
            {
                var combined = !string.IsNullOrEmpty(pathVal) ? $"{nameVal}/{pathVal.TrimStart('/')}" : nameVal;
                return RouteDictionaryRegistry.NormalizeResolvedUrl(combined);
            }
        }

        if (rule.CallNodeTypes.Contains(targetNode.Type))
        {
            var argList = FindArgumentsNode(targetNode);
            if (argList.IsValid())
            {
                foreach (var child in argList.Children)
                {
                    if (IsStringLiteral(child))
                    {
                        var text = child.Text.Trim('"', '\'');
                        if (text.Contains('/') || text.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                        {
                            return RouteDictionaryRegistry.NormalizeResolvedUrl(text);
                        }
                    }
                    else if (child is IdentifierExprSyntax)
                    {
                        var varName = child.Text;
                        if (RouteDictionaryRegistry.TryResolve(varName, out var rPath, out var rService))
                        {
                            var cleanPath = rPath.Split('?')[0];
                            return !string.IsNullOrEmpty(rService) ? $"{rService}{cleanPath}" : cleanPath;
                        }

                        var resolved = FindVariableInitializerInEnclosingScopes(child, varName);
                        if (!string.IsNullOrEmpty(resolved))
                        {
                            return RouteDictionaryRegistry.NormalizeResolvedUrl(resolved);
                        }
                    }
                    else if (child is BinaryExprSyntax)
                    {
                        var left = child.GetField(SyntaxFields.Left) ?? (child.Children.Count >= 1 ? child.Children[0] : null);
                        string? leftText = null;
                        if (left.IsValid())
                        {
                            if (IsStringLiteral(left))
                            {
                                leftText = left.Text.Trim('"', '\'');
                            }
                            else if (left is IdentifierExprSyntax or CallExprSyntax)
                            {
                                var varName = left.Text.TrimStart('$');
                                if (left is CallExprSyntax)
                                {
                                    var callName = GetCallMethodName(left);
                                    if (!string.IsNullOrEmpty(callName)) varName = callName;
                                }
                                leftText = FindVariableInitializerInEnclosingScopes(child, varName);
                            }
                        }

                        var right = child.GetField(SyntaxFields.Right) ?? (child.Children.Count >= 3 ? child.Children[2] : null);
                        string? rightText = null;
                        if (right.IsValid() && IsStringLiteral(right))
                        {
                            rightText = right.Text.Trim('"', '\'');
                        }

                        if (!string.IsNullOrEmpty(leftText) && !string.IsNullOrEmpty(rightText))
                        {
                            var combined = leftText.TrimEnd('/') + "/" + rightText.TrimStart('/');
                            return RouteDictionaryRegistry.NormalizeResolvedUrl(combined);
                        }
                        if (!string.IsNullOrEmpty(leftText) && (leftText.Contains('/') || leftText.StartsWith("http", StringComparison.OrdinalIgnoreCase)))
                        {
                            return RouteDictionaryRegistry.NormalizeResolvedUrl(leftText.EndsWith('/') ? leftText + "*" : leftText);
                        }
                        if (!string.IsNullOrEmpty(rightText))
                        {
                            return RouteDictionaryRegistry.NormalizeResolvedUrl(rightText);
                        }
                    }
                }
            }
        }

        return rule.FallbackTarget;
    }

    private static string? FindVariableInitializerInEnclosingScopes(Node node, string varName)
    {
        var curr = node.Parent;
        while (curr.IsValid())
        {
            if (curr is BlockStmtSyntax or FunctionDeclSyntax or TypeDeclSyntax || curr.IsAny("class_body", "statement_block", "compound_statement"))
            {
                foreach (var child in curr.Children)
                {
                    if (child is VariableDeclSyntax or MemberDeclSyntax)
                    {
                        foreach (var decl in child.Children)
                        {
                            if (decl is not VariableDeclSyntax && decl.Type != "property_element") continue;

                            var nameNode = decl.GetChildForField(SyntaxFields.Name) ??
                                           decl.Children.FirstOrDefault(c => c is IdentifierExprSyntax);
                            if (nameNode.IsValid() && nameNode.Text.TrimStart('$') == varName.TrimStart('$'))
                            {
                                var valNode = decl.GetChildForField(SyntaxFields.Value) ??
                                              (decl.Children.Count >= 3 ? decl.Children[^1] : null);
                                if (valNode.IsValid())
                                {
                                    if (IsStringLiteral(valNode))
                                    {
                                        return valNode.Text.Trim('"', '\'');
                                    }
                                    if (valNode is BinaryExprSyntax)
                                    {
                                        var left = valNode.GetChildForField(SyntaxFields.Left) ??
                                                   (valNode.Children.Count > 0 ? valNode.Children[0] : null);
                                        var right = valNode.GetChildForField(SyntaxFields.Right) ??
                                                    (valNode.Children.Count >= 3 ? valNode.Children[2] : null);

                                        if (left.IsValid() && IsStringLiteral(left))
                                        {
                                            var leftText = left.Text.Trim('"', '\'');
                                            if (right.IsValid() && IsStringLiteral(right))
                                            {
                                                return leftText + right.Text.Trim('"', '\'');
                                            }
                                            return leftText + "*";
                                        }
                                        if (right.IsValid() && IsStringLiteral(right))
                                        {
                                            return right.Text.Trim('"', '\'');
                                        }
                                    }
                                    if (valNode is CallExprSyntax)
                                    {
                                        var argList = FindArgumentsNode(valNode);
                                        var str = argList?.Children.FirstOrDefault(IsStringLiteral);
                                        if (str.IsValid()) return str.Text.Trim('"', '\'');
                                    }
                                }
                            }
                        }
                    }
                    if (child is FunctionDeclSyntax fn)
                    {
                        var fnName = fn.GetChildForField(SyntaxFields.Name)?.Text ?? fn.Children.FirstOrDefault(c => c is IdentifierExprSyntax)?.Text;
                        if (!string.IsNullOrEmpty(fnName) && fnName.Equals(varName, StringComparison.OrdinalIgnoreCase))
                        {
                            var str = fn.Descendants().FirstOrDefault(d => IsStringLiteral(d) && (d.Text.Contains('/') || d.Text.Contains('-') || d.Text.Contains(':')));
                            if (str.IsValid())
                            {
                                var s = str.Text.Trim('"', '\'');
                                if (!string.IsNullOrEmpty(s)) return s.StartsWith("http", StringComparison.OrdinalIgnoreCase) || s.StartsWith("lb:", StringComparison.OrdinalIgnoreCase) ? s : $"http://{s}";
                            }
                        }
                    }
                }
            }
            curr = curr.Parent;
        }
        return null;
    }

    #endregion

}
