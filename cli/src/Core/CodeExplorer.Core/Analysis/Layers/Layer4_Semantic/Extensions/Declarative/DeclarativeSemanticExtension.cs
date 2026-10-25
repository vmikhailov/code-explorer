using System.Collections.Frozen;
using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using TreeSitter;

namespace CodeExplorer.Core.Parser;

/// <summary>
/// Declarative rule for mapping HTTP route annotations/attributes (e.g., Spring @GetMapping, Symfony #[Route]) to EntryPoint nodes.
/// </summary>
public sealed record AnnotationEndpointRule
{
    public required FrozenDictionary<string, string> AnnotationToHttpMethod { get; init; }
    public FrozenSet<string> AnnotationNodeTypes { get; init; } = new[] { "annotation", "marker_annotation", "attribute", "attribute_group", "decorator" }.ToFrozenSet();
    public FrozenSet<string> PathArgumentKeys { get; init; } = new[] { "value", "path" }.ToFrozenSet();
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
    public FrozenSet<string> TableAnnotationNames { get; init; } = new[] { "Table", "Entity" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
    public FrozenSet<string> NameArgumentKeys { get; init; } = new[] { "name", "value" }.ToFrozenSet();
    public FrozenSet<string> AnnotationNodeTypes { get; init; } = new[] { "annotation", "marker_annotation", "attribute", "attribute_group" }.ToFrozenSet();
    public FrozenSet<string> ClassDeclarationNodeTypes { get; init; } = new[] { "class_declaration" }.ToFrozenSet();
}

/// <summary>
/// Declarative rule for mapping message broker listeners and publishers (e.g., Kafka, RabbitMQ, Spring Events, Symfony Messenger, Laravel Events) to EntryPoint/Topic relationships.
/// </summary>
public sealed record MessagingBrokerRule
{
    public required string Protocol { get; init; }
    public FrozenSet<string> ListenerAnnotationNames { get; init; } = FrozenSet<string>.Empty;
    public FrozenSet<string> MethodLevelEventAnnotations { get; init; } = FrozenSet<string>.Empty;
    public FrozenSet<string> TopicArgumentKeys { get; init; } = new[] { "topics", "topicPattern", "queues", "queuesToDeclare", "bindings", "value" }.ToFrozenSet();
    public string FallbackTopicName { get; init; } = "unknown-topic";

    public FrozenSet<string> PublishMethodNames { get; init; } = FrozenSet<string>.Empty;
    public FrozenSet<string> PublishReceiverSubstrings { get; init; } = FrozenSet<string>.Empty;
    public bool ExtractEventTypeFromObjectCreationArg { get; init; }
    public bool PreferSecondArgWhenThreeOrMoreArgs { get; init; }

    public FrozenSet<string> CallNodeTypes { get; init; } = new[] { "method_invocation", "call_expression", "member_call_expression", "scoped_call_expression" }.ToFrozenSet();
    public FrozenSet<string> AnnotationNodeTypes { get; init; } = new[] { "annotation", "marker_annotation", "attribute", "attribute_group" }.ToFrozenSet();
}

/// <summary>
/// Universal profile-driven semantic extension that evaluates declarative endpoint, HTTP client, SQL sink, ORM table, and messaging rules.
/// </summary>
public class DeclarativeSemanticExtension : ISemanticExtension
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Type { get; init; } = "framework";
    public LibraryRole LibraryRole { get; init; } = LibraryRole.General;
    public IReadOnlyList<string> SupportedPatterns { get; init; } = [];
    public bool IsBuiltIn { get; init; }
    public bool IsImplemented { get; init; } = true;
    public Func<ProjectContext, bool>? CustomMatch { get; init; }
    public string? PackageDescriptorTypeOverride { get; init; }

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
            if (MatchesMessagingListenerAnnotation(node, rule))
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
            if (MatchesMessagingListenerAnnotation(node, rule))
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
            }
        }
    }

    public void EnrichSymbol(Node node, SyntacticSymbol symbol, ParsingContext ctx)
    {
        foreach (var rule in AnnotationEndpointRules)
        {
            if (MatchesAnnotationEndpoint(node, rule, out _) && rule.EnrichJavaSecurityAndSchemas)
            {
                EnrichFromEnclosingDeclarations(node, symbol);
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

                if (isEntity)
                {
                    var tableAnnot = annotations.FirstOrDefault(a =>
                        string.Equals(GetShortAnnotationName(a), "Table", StringComparison.OrdinalIgnoreCase));
                    var tableName = tableAnnot.IsValid() ? ExtractOrmTableName(tableAnnot, rule) : symbol.Name;
                    if (!string.IsNullOrEmpty(tableName))
                    {
                        symbol.References.Add(new Reference(symbol.Name, tableName, OntologyConstants.Relationships.PersistedIn));
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
        }
    }

    #region Annotation Endpoint Helpers

    private static bool MatchesAnnotationEndpoint(Node node, AnnotationEndpointRule rule, out string httpMethod)
    {
        httpMethod = "GET";
        if (!rule.AnnotationNodeTypes.Contains(node.Type)) return false;

        var name = GetShortAnnotationName(node);
        if (string.IsNullOrEmpty(name)) return false;

        return rule.AnnotationToHttpMethod.TryGetValue(name, out httpMethod!);
    }

    private static string ExtractAnnotationRoute(Node annotationNode, AnnotationEndpointRule rule, string httpMethod)
    {
        var routePath = ExtractNamedOrFirstStringFromAnnotation(annotationNode, rule.PathArgumentKeys) ?? "";
        if (string.IsNullOrEmpty(routePath))
        {
            routePath = "/";
        }
        else if (!routePath.StartsWith('/'))
        {
            routePath = "/" + routePath;
        }
        return $"{httpMethod}:{routePath}";
    }

    private static void EnrichFromEnclosingDeclarations(Node annotationNode, SyntacticSymbol symbol)
    {
        var targetDecl = annotationNode.Parent;
        while (targetDecl.IsValid())
        {
            if (targetDecl.IsAny(TreeSitterSyntax.Java.MethodDeclaration, TreeSitterSyntax.Java.ClassDeclaration))
            {
                EnrichSecurityFromAnnotations(targetDecl, symbol);
                break;
            }
            targetDecl = targetDecl.Parent;
        }

        if (targetDecl.IsValid() && targetDecl.Is(TreeSitterSyntax.Java.MethodDeclaration))
        {
            ExtractMethodPayloadSchemas(targetDecl, symbol);

            var classDecl = targetDecl.Parent;
            while (classDecl.IsValid())
            {
                if (classDecl.Is(TreeSitterSyntax.Java.ClassDeclaration))
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
        var typeNode = methodDecl.GetField(TreeSitterSyntax.Fields.Type);
        if (typeNode.IsValid())
        {
            var ret = UnwrapGenericResponseType(typeNode.Text);
            if (!string.IsNullOrEmpty(ret) && ret != "void")
            {
                symbol.ResponseType = ret;
            }
        }

        var paramList = methodDecl.GetField(TreeSitterSyntax.Fields.Parameters);
        if (paramList.IsValid())
        {
            foreach (var param in paramList.Children)
            {
                if (!param.Type.Contains("parameter", StringComparison.Ordinal)) continue;

                var isRequestBody = param.Text.Contains("@RequestBody", StringComparison.Ordinal) ||
                                    param.Text.Contains("@ModelAttribute", StringComparison.Ordinal);
                var pType = param.GetField(TreeSitterSyntax.Fields.Type);
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

        if (node.Is(TreeSitterSyntax.Java.InterfaceDeclaration) && rule.ClientAnnotationSuffixes.Count > 0)
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
        if (node.Is(TreeSitterSyntax.Java.InterfaceDeclaration))
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

            var str = targetNode.FindChildOfType(TreeSitterSyntax.Java.StringLiteral);
            if (str.IsValid()) nameVal = str.Text.Trim('"', '\'');

            var pairs = new List<Node>();
            CollectNodesByType(targetNode, TreeSitterSyntax.Java.ElementValuePair, pairs);
            foreach (var pair in pairs)
            {
                var key = pair.FindChildOfType(TreeSitterSyntax.Java.Identifier);
                var val = pair.FindChildOfType(TreeSitterSyntax.Java.StringLiteral);
                if (key.IsValid() && val.IsValid())
                {
                    var keyText = key.Text;
                    var valText = val.Text.Trim('"', '\'');
                    if (keyText is "name" or "value")
                    {
                        nameVal = valText;
                    }
                    else if (keyText is "path")
                    {
                        pathVal = valText;
                    }
                    else if (keyText is "url")
                    {
                        return RouteDictionaryRegistry.NormalizeResolvedUrl(valText);
                    }
                }
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
                    else if (child.Is(TreeSitterSyntax.Java.Identifier))
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
                    else if (child.Is(TreeSitterSyntax.Common.BinaryExpression))
                    {
                        var left = child.GetField(TreeSitterSyntax.Fields.Left) ?? (child.Children.Count >= 1 ? child.Children[0] : null);
                        if (left.IsValid() && IsStringLiteral(left))
                        {
                            var text = left.Text.Trim('"', '\'');
                            if (text.Contains('/') || text.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                            {
                                return RouteDictionaryRegistry.NormalizeResolvedUrl(text.EndsWith('/') ? text + "*" : text);
                            }
                        }

                        var right = child.GetField(TreeSitterSyntax.Fields.Right) ?? (child.Children.Count >= 3 ? child.Children[2] : null);
                        if (right.IsValid() && IsStringLiteral(right))
                        {
                            return RouteDictionaryRegistry.NormalizeResolvedUrl(right.Text.Trim('"', '\''));
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
            if (curr.IsAny(TreeSitterSyntax.Java.Block, TreeSitterSyntax.Java.MethodDeclaration, TreeSitterSyntax.Java.ClassBody, "statement_block", "compound_statement"))
            {
                foreach (var child in curr.Children)
                {
                    if (child.IsAny(TreeSitterSyntax.Java.LocalVariableDeclaration, TreeSitterSyntax.Java.FieldDeclaration, "variable_declaration", "property_declaration"))
                    {
                        foreach (var decl in child.Children)
                        {
                            if (!decl.IsAny(TreeSitterSyntax.Java.VariableDeclarator, "property_element")) continue;

                            var nameNode = decl.GetChildForField(TreeSitterSyntax.Fields.Name) ??
                                           decl.Children.FirstOrDefault(c => c.IsAny(TreeSitterSyntax.Java.Identifier, "variable_name"));
                            if (nameNode.IsValid() && nameNode.Text.TrimStart('$') == varName.TrimStart('$'))
                            {
                                var valNode = decl.GetChildForField(TreeSitterSyntax.Fields.Value) ??
                                              (decl.Children.Count >= 3 ? decl.Children[^1] : null);
                                if (valNode.IsValid())
                                {
                                    if (IsStringLiteral(valNode))
                                    {
                                        return valNode.Text.Trim('"', '\'');
                                    }
                                    if (valNode.Is(TreeSitterSyntax.Common.BinaryExpression))
                                    {
                                        var left = valNode.GetChildForField(TreeSitterSyntax.Fields.Left) ??
                                                   (valNode.Children.Count > 0 ? valNode.Children[0] : null);
                                        var right = valNode.GetChildForField(TreeSitterSyntax.Fields.Right) ??
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
                                    if (valNode.Is(TreeSitterSyntax.Java.MethodInvocation))
                                    {
                                        var argList = FindArgumentsNode(valNode);
                                        var str = argList?.Children.FirstOrDefault(IsStringLiteral);
                                        if (str.IsValid()) return str.Text.Trim('"', '\'');
                                    }
                                }
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

    #region SQL Sink & ORM Table Helpers

    private static bool MatchesSqlSink(Node node, SqlSinkRule rule)
    {
        if (rule.AnnotationNodeTypes.Contains(node.Type) && rule.QueryAnnotationSuffixes.Count > 0)
        {
            var name = GetShortAnnotationName(node);
            if (!string.IsNullOrEmpty(name) &&
                rule.QueryAnnotationSuffixes.Any(s => name.EndsWith(s, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        if (rule.CallNodeTypes.Contains(node.Type))
        {
            var methodName = GetCallMethodName(node);
            if (!string.IsNullOrEmpty(methodName) && rule.CallMethodNames.Contains(methodName))
            {
                if (!rule.RequireExtractableSqlOnCall) return true;
                return !string.IsNullOrEmpty(ExtractSqlText(node));
            }
        }

        return false;
    }

    private static string? ExtractSqlText(Node node)
    {
        if (node.IsAny(TreeSitterSyntax.Java.MarkerAnnotation, TreeSitterSyntax.Java.Annotation, "attribute", "attribute_group"))
        {
            var strNode = node.FindChildOfType(TreeSitterSyntax.Java.StringLiteral)
                          ?? node.FindChildOfType(TreeSitterSyntax.Java.TextBlock);
            if (strNode.IsValid())
            {
                return strNode.Text.Trim('"', '\'').Trim();
            }

            var pairs = new List<Node>();
            CollectNodesByType(node, TreeSitterSyntax.Java.ElementValuePair, pairs);
            foreach (var pair in pairs)
            {
                var key = pair.FindChildOfType(TreeSitterSyntax.Java.Identifier);
                if (key.IsValid() && key.Text is "value" or "query" or "nativeQuery")
                {
                    var val = pair.FindChildOfType(TreeSitterSyntax.Java.StringLiteral)
                              ?? pair.FindChildOfType(TreeSitterSyntax.Java.TextBlock);
                    if (val.IsValid()) return val.Text.Trim('"', '\'').Trim();
                }
            }

            var text = node.Text;
            var f = text.IndexOf('"');
            var l = text.LastIndexOf('"');
            if (f >= 0 && l > f) return text[(f + 1)..l].Trim();
        }

        var argList = FindArgumentsNode(node);
        if (argList.IsValid())
        {
            var firstStr = argList.Children.FirstOrDefault(IsStringLiteral);
            if (firstStr.IsValid())
            {
                return firstStr.Text.Trim('"', '\'').Trim();
            }
        }

        return null;
    }

    private static bool MatchesOrmTableAnnotation(Node node, OrmTableRule rule)
    {
        if (!rule.AnnotationNodeTypes.Contains(node.Type)) return false;
        var name = GetShortAnnotationName(node);
        return !string.IsNullOrEmpty(name) && rule.TableAnnotationNames.Contains(name);
    }

    private static string? ExtractOrmTableName(Node annotNode, OrmTableRule rule)
    {
        return ExtractNamedOrFirstStringFromAnnotation(annotNode, rule.NameArgumentKeys);
    }

    #endregion

    #region Messaging Helpers

    private static bool MatchesMessagingListenerAnnotation(Node node, MessagingBrokerRule rule)
    {
        if (rule.ListenerAnnotationNames.Count == 0 || !rule.AnnotationNodeTypes.Contains(node.Type))
            return false;

        var name = GetShortAnnotationName(node);
        return !string.IsNullOrEmpty(name) && rule.ListenerAnnotationNames.Contains(name);
    }

    private static bool MatchesMethodLevelEventListener(Node node, MessagingBrokerRule rule)
    {
        if (rule.MethodLevelEventAnnotations.Count == 0 || !node.Is(TreeSitterSyntax.Java.MethodDeclaration))
            return false;

        var annotations = GetNodeAnnotations(node, rule.AnnotationNodeTypes);
        return annotations.Any(a =>
        {
            var name = GetShortAnnotationName(a);
            return !string.IsNullOrEmpty(name) && rule.MethodLevelEventAnnotations.Contains(name);
        });
    }

    private static List<string> ExtractMessagingTopics(Node annotNode, MessagingBrokerRule rule)
    {
        var list = new List<string>();

        foreach (var child in annotNode.Children)
        {
            if (child.Type is "annotation_argument_list" or "arguments")
            {
                foreach (var argChild in child.Children)
                {
                    if (argChild.Type == "element_value_pair")
                    {
                        var keyNode = argChild.FindChildOfType(TreeSitterSyntax.Java.Identifier);
                        if (keyNode.IsValid() && rule.TopicArgumentKeys.Contains(keyNode.Text))
                        {
                            ExtractStringsFromSubtree(argChild, list);
                        }
                    }
                    else if (IsStringLiteral(argChild))
                    {
                        var val = argChild.Text.Trim('"', '\'');
                        if (!string.IsNullOrEmpty(val) && !list.Contains(val)) list.Add(val);
                    }
                }
            }
        }

        if (list.Count == 0)
        {
            ExtractStringsFromSubtree(annotNode, list);
        }

        if (list.Count == 0)
        {
            var text = annotNode.Text;
            var first = text.IndexOf('"');
            var last = text.LastIndexOf('"');
            if (first >= 0 && last > first)
            {
                list.Add(text[(first + 1)..last]);
            }
        }

        return list;
    }

    private static void TryCollectMessagingPublishReferences(
        Node callNode,
        string scopeSymbolId,
        List<Reference> references,
        MessagingBrokerRule rule)
    {
        var methodName = GetCallMethodName(callNode);
        if (string.IsNullOrEmpty(methodName) || !rule.PublishMethodNames.Contains(methodName))
            return;

        if (rule.PublishReceiverSubstrings.Count > 0)
        {
            var objNode = callNode.GetField(TreeSitterSyntax.Fields.Object)
                          ?? callNode.GetField(TreeSitterSyntax.Fields.Operand)
                          ?? callNode.GetField("scope");
            var objText = objNode.IsValid() ? objNode.Text : "";
            if (!rule.PublishReceiverSubstrings.Any(sub => objText.Contains(sub, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }
        }

        var argList = FindArgumentsNode(callNode);
        if (!argList.IsValid()) return;

        if (rule.ExtractEventTypeFromObjectCreationArg)
        {
            var objCreation = argList.Children.FirstOrDefault(c => c.IsAny(TreeSitterSyntax.Java.ObjectCreationExpression, "object_creation_expression"));
            if (objCreation.IsValid())
            {
                var typeNode = objCreation.GetField(TreeSitterSyntax.Fields.Type)
                               ?? objCreation.Children.FirstOrDefault(c => c.Type is "type_identifier" or "identifier" or "name");
                if (typeNode.IsValid())
                {
                    references.Add(new Reference(scopeSymbolId, $"{rule.Protocol}:{typeNode.Text}", OntologyConstants.Relationships.PublishesTo));
                }
            }
            return;
        }

        var args = argList.Children.Where(c => c.Type is not "(" and not ")" and not ",").ToList();
        if (args.Count == 0) return;

        string? target = null;
        if (rule.PreferSecondArgWhenThreeOrMoreArgs && args.Count >= 3)
        {
            target = CleanPublishTargetArg(args[1]) ?? CleanPublishTargetArg(args[0]);
        }
        else
        {
            target = CleanPublishTargetArg(args[0]);
        }

        if (!string.IsNullOrEmpty(target))
        {
            references.Add(new Reference(scopeSymbolId, $"{rule.Protocol}:{target}", OntologyConstants.Relationships.PublishesTo));
        }
    }

    private static string? CleanPublishTargetArg(Node argNode)
    {
        if (IsStringLiteral(argNode))
        {
            return argNode.Text.Trim('"', '\'');
        }

        if (argNode.IsAny(TreeSitterSyntax.Java.ObjectCreationExpression, "object_creation_expression"))
        {
            var innerArgs = FindArgumentsNode(argNode);
            if (innerArgs.IsValid())
            {
                var firstInner = innerArgs.Children.FirstOrDefault(c => c.Type is not "(" and not ")" and not ",");
                if (firstInner.IsValid())
                {
                    return CleanPublishTargetArg(firstInner);
                }
            }
        }

        if (argNode.IsAny(TreeSitterSyntax.Java.Identifier, TreeSitterSyntax.Java.ScopedIdentifier, "identifier", "scoped_identifier"))
        {
            var text = argNode.Text.Trim('"', '\'');
            if (text.Contains('.')) text = text[(text.LastIndexOf('.') + 1)..];
            return text;
        }

        return null;
    }

    #endregion

    #region Shared AST Utility Helpers

    private static string? GetShortAnnotationName(Node annot)
    {
        var nameNode = annot.FindChildOfType(TreeSitterSyntax.Java.Identifier)
                       ?? annot.FindChildOfType(TreeSitterSyntax.Java.ScopedIdentifier)
                       ?? annot.GetField(TreeSitterSyntax.Fields.Name)
                       ?? annot.Children.FirstOrDefault(c => c.Type.Contains("identifier", StringComparison.Ordinal) || c.Type.Contains("name", StringComparison.Ordinal));
        if (!nameNode.IsValid()) return null;

        var name = nameNode.Text.TrimStart('@', '#');
        if (name.Contains('.')) name = name[(name.LastIndexOf('.') + 1)..];
        if (name.Contains('\\')) name = name[(name.LastIndexOf('\\') + 1)..];
        return name;
    }

    private static string? GetCallMethodName(Node callNode)
    {
        var nameNode = callNode.GetField(TreeSitterSyntax.Fields.Name)
                       ?? callNode.GetField("field")
                       ?? callNode.GetField("member");
        if (nameNode.IsValid()) return nameNode.Text;

        var funcNode = callNode.GetField(TreeSitterSyntax.Fields.Function);
        if (funcNode.IsValid())
        {
            var field = funcNode.GetField("field") ?? funcNode.GetField(TreeSitterSyntax.Fields.Name);
            if (field.IsValid()) return field.Text;
            return funcNode.Text;
        }

        return null;
    }

    private static Node? FindArgumentsNode(Node callNode)
    {
        return callNode.FindChildOfType(TreeSitterSyntax.Java.ArgumentList)
               ?? callNode.FindChildOfType(TreeSitterSyntax.Common.Arguments)
               ?? callNode.GetField("arguments")
               ?? callNode.Children.FirstOrDefault(c => c.Type.Contains("argument", StringComparison.Ordinal));
    }

    private static bool IsStringLiteral(Node node)
    {
        return node.IsValid() &&
               (node.IsAny(TreeSitterSyntax.Java.StringLiteral, TreeSitterSyntax.Java.TextBlock, "string", "encapsed_string") ||
                node.Type.Contains("string_literal", StringComparison.Ordinal));
    }

    private static List<Node> GetNodeAnnotations(Node declNode, FrozenSet<string> annotationNodeTypes)
    {
        var result = new List<Node>();
        foreach (var child in declNode.Children)
        {
            if (!child.IsValid()) continue;
            if (annotationNodeTypes.Contains(child.Type))
            {
                result.Add(child);
            }
            else if (child.Type is "modifiers" or "attribute_list" or "decorator_list")
            {
                foreach (var modChild in child.Children)
                {
                    if (modChild.IsValid() && annotationNodeTypes.Contains(modChild.Type))
                    {
                        result.Add(modChild);
                    }
                }
            }
        }
        return result;
    }

    private static string? ExtractNamedOrFirstStringFromAnnotation(Node annotNode, FrozenSet<string> acceptedKeys)
    {
        foreach (var child in annotNode.Children)
        {
            if (child.Is(TreeSitterSyntax.Java.ElementValuePair))
            {
                var key = child.FindChildOfType(TreeSitterSyntax.Java.Identifier);
                if (key.IsValid() && acceptedKeys.Contains(key.Text))
                {
                    var val = child.FindChildOfType(TreeSitterSyntax.Java.StringLiteral);
                    if (val.IsValid()) return val.Text.Trim('"', '\'');
                }
            }
            else if (child.Type is "annotation_argument_list" or "arguments")
            {
                foreach (var argChild in child.Children)
                {
                    if (argChild.Is(TreeSitterSyntax.Java.ElementValuePair) || argChild.Type == "argument")
                    {
                        var key = argChild.FindChildOfType(TreeSitterSyntax.Java.Identifier) ?? argChild.GetField("name");
                        if (key.IsValid() && acceptedKeys.Contains(key.Text))
                        {
                            var val = argChild.FindChildOfType(TreeSitterSyntax.Java.StringLiteral)
                                      ?? argChild.Children.FirstOrDefault(IsStringLiteral);
                            if (val.IsValid()) return val.Text.Trim('"', '\'');
                        }
                    }
                    else if (IsStringLiteral(argChild))
                    {
                        return argChild.Text.Trim('"', '\'');
                    }
                }
            }
            else if (IsStringLiteral(child))
            {
                return child.Text.Trim('"', '\'');
            }
        }

        var text = annotNode.Text;
        if (text.Contains('"'))
        {
            var f = text.IndexOf('"');
            var l = text.LastIndexOf('"');
            if (l > f) return text[(f + 1)..l];
        }
        return null;
    }

    private static void ExtractStringsFromSubtree(Node node, List<string> results)
    {
        if (IsStringLiteral(node))
        {
            var clean = node.Text.Trim('"', '\'');
            if (!string.IsNullOrEmpty(clean) && !results.Contains(clean))
            {
                results.Add(clean);
            }
            return;
        }

        foreach (var child in node.Children)
        {
            ExtractStringsFromSubtree(child, results);
        }
    }

    private static void CollectNodesByType(Node node, string nodeType, List<Node> result)
    {
        if (node.Is(nodeType))
        {
            result.Add(node);
        }
        foreach (var child in node.Children)
        {
            CollectNodesByType(child, nodeType, result);
        }
    }

    private static string? FindEnclosingMethodName(Node annotNode)
    {
        var current = annotNode.Parent;
        while (current.IsValid())
        {
            if (current.IsAny(TreeSitterSyntax.Java.MethodDeclaration, "function_definition", "method_declaration"))
            {
                var nameField = current.GetField(TreeSitterSyntax.Fields.Name);
                if (nameField.IsValid()) return nameField.Text;

                foreach (var child in current.Children)
                {
                    if (child.Is(TreeSitterSyntax.Java.Identifier)) return child.Text;
                }
            }
            current = current.Parent;
        }
        return null;
    }

    private static string? ExtractFirstParameterType(Node methodNode)
    {
        var paramList = methodNode.FindChildOfType(TreeSitterSyntax.Java.FormalParameters)
                        ?? methodNode.GetField(TreeSitterSyntax.Fields.Parameters);
        if (!paramList.IsValid()) return null;

        var firstParam = paramList.FindChildOfType(TreeSitterSyntax.Java.FormalParameter)
                         ?? paramList.Children.FirstOrDefault(c => c.Type.Contains("parameter", StringComparison.Ordinal));
        if (!firstParam.IsValid()) return null;

        var typeNode = firstParam.GetField(TreeSitterSyntax.Fields.Type);
        return typeNode.IsValid() ? typeNode.Text : null;
    }

    #endregion
}
