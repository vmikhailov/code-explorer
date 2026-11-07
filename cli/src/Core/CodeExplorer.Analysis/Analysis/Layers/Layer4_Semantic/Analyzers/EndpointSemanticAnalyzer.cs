using System.Text.RegularExpressions;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Common.Relationships;
using CodeExplorer.Core.Parser.Syntax;

namespace CodeExplorer.Core.Analysis.Layers.Layer4_Semantic.Analyzers;

/// <summary>
/// Layer 4 Semantic Analyzer for endpoints, entry points, external services, databases, and message topics.
/// Converts syntactic semantic symbols collected during Layer 3 AST passes into rich Layer 4 ontology nodes.
/// </summary>
public static class EndpointSemanticAnalyzer
{
    private const string PropSchemaName = "schema_name";
    private const string PropSchema = "schema";
    private const string PropMethod = "method";
    private const string PropTestFramework = "test_framework";
    private const string SchemeHttpColon = "http:";
    private const string SchemeHttpsColon = "https:";
    private const string SchemeWsColon = "ws:";
    private const string SchemeGrpcColon = "grpc:";
    private const string SchemeRpcColon = "rpc:";
    private const string SchemeDubboColon = "dubbo:";
    private const string SchemeTriColon = "tri:";
    private const char CharLf = '\n';
    private const char CharCr = '\r';
    private const char CharSpace = ' ';

    public static void Enrich(
        SyntaxTree syntaxTree,
        ProjectNode? project = null,
        ParsingContext? ctx = null,
        List<Relationship>? relationships = null)
    {
        if (syntaxTree.SemanticSymbols == null || syntaxTree.SemanticSymbols.Count == 0) return;

        var relativePath = !string.IsNullOrEmpty(syntaxTree.RelativePath) ? syntaxTree.RelativePath : syntaxTree.FilePath;
        var workspaceId = ctx?.WorkspaceId ?? "";
        var projectName = project?.Name ?? syntaxTree.FileNode?.ProjectName;
        var projectKind = (project?.EntityKind != ProjectEntityKind.Unknown ? project?.EntityKind : null) ?? syntaxTree.FileNode?.ProjectKind;
        var checkRelPath = !string.IsNullOrEmpty(syntaxTree.RelativePath) ? syntaxTree.RelativePath : relativePath;
        var symbolIsTest = (syntaxTree.FileNode?.IsTest ?? false) || WorkspaceConventions.IsTestFilePath(checkRelPath) || project?.IsTest == true;

        foreach (var syntactic in syntaxTree.SemanticSymbols)
        {
            var node = MapSemanticSymbolToOntology(syntactic, relativePath, workspaceId, project, ctx, projectName, symbolIsTest, projectKind, relationships);
            if (node != null && syntaxTree.FileNode != null)
            {
                if (node is ExternalServiceNode)
                {
                    syntaxTree.FileNode.Children.Add(node);
                }
                else if (!syntaxTree.FileNode.Children.Any(c => c.Id == node.Id))
                {
                    syntaxTree.FileNode.Children.Add(node);
                }
            }
        }

        syntaxTree.SemanticSymbols.Clear();
    }

    private static IOntologyNode? MapSemanticSymbolToOntology(
        SyntacticSymbol syntactic,
        string relativePath,
        string workspaceId,
        ProjectNode? project,
        ParsingContext? ctx,
        string? projectName,
        bool symbolIsTest,
        ProjectEntityKind? projectKind,
        List<Relationship>? relationships)
    {
        var node = syntactic.Node;
        var kind = CodeExplorer.Cypher.Common.TaxonomyEnumExtendedExtensions.ToCanonicalString(syntactic.Kind);
        var name = syntactic.Name;
        var idName = (name.Length > 80 || name.AsSpan().IndexOfAny(CharLf, CharCr) >= 0) ? $"{name[..Math.Min(48, name.Length)].Replace(CharLf, CharSpace).Replace(CharCr, CharSpace).Trim()}_{Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(name))[..6])}" : name;
        var symbolId = $"{workspaceId}:{OntologyConstants.IdPrefixes.Symbol}:{relativePath}:{kind}:{idName}";

        IOntologyNode? typedNode = null;
        if (kind == OntologyConstants.NodeLabels.Query)
        {
            if (WorkspaceConventions.IsFrontendFilePath(relativePath))
            {
                var litId = $"{workspaceId}:{OntologyConstants.IdPrefixes.CallLiteral}:{relativePath}:{node.StartPosition.Row + 1}:{node.StartPosition.Column + 1}";
                typedNode = new CallLiteralNode(litId, name, syntactic.Text ?? node.Text, "", 0, relativePath, node.StartPosition.Row + 1, node.StartPosition.Column + 1, symbolIsTest, projectKind, projectName, "UI", false);
            }
            else
            {
                var sqlCandidate = syntactic.Text ?? node.Text;
                if (!NestedSqlParser.TryParseSql(sqlCandidate, out _, out _))
                {
                    if (name.Contains(':'))
                    {
                        var afterColon = name[(name.IndexOf(':') + 1)..].Trim();
                        if (NestedSqlParser.TryParseSql(afterColon, out _, out _))
                        {
                            sqlCandidate = afterColon;
                        }
                    }
                }
                var qStartLine = (int)node.StartPosition.Row + 1;
                var qEndLine = (int)node.EndPosition.Row + 1;
                typedNode = NestedSqlParser.ParseNestedSql(sqlCandidate, symbolId, relativePath, ctx, name, qStartLine, qEndLine) ??
                            new QueryNode(symbolId, name, NestedSqlParser.CleanQueryText(sqlCandidate), relativePath, qStartLine, qEndLine);
                var parentScopeId = syntactic.ParentScopeId;
                if (!string.IsNullOrEmpty(parentScopeId) && !IsFileNodeScope(parentScopeId))
                {
                    var accessedRel = Relationship.FromRelationship(new AccessedByRelationship(typedNode.Id, parentScopeId)
                    {
                        FilePath = relativePath,
                        Line = qStartLine,
                        LineEnd = qEndLine,
                        Operation = "SQL",
                        EvidenceKind = OntologyConstants.EvidenceKinds.Query
                    });
                    ctx?.AddGlobalProjectDependency(accessedRel);
                    relationships?.Add(accessedRel);
                }
            }
        }
        else if (kind == OntologyConstants.NodeLabels.EntryPoint)
        {
            var colonIdx = name.IndexOf(':');
            var isEndpoint = false;
            if (colonIdx > 0)
            {
                var method = name[..colonIdx].ToUpperInvariant();
                isEndpoint = method is "GET" or "POST" or "PUT" or "DELETE" or "PATCH" or "OPTIONS" or "HEAD" or "TRACE" or "ANY" or "ALL" or "API"
                    or "GRAPHQL" or "RPC" or "GRPC" or "QUERY" or "MUTATION" or "SUBSCRIPTION"
                    or "WS" or "HUB" or "WCF" or "GRAIN"
                    or "MSG" or "CONSUMER" or "CONSUME" or "HANDLE" or "EVENT"
                    or "KAFKA" or "RABBITMQ" or "GCP" or "SQS" or "SNS" or "NATS" or "ACTIVE-MQ" or "ACTIVEMQ" or "AZURE-SB" or "MQTT" or "REDIS" or "EVENTBUS" or "MASSTRANSIT";
            }
            if (syntactic.Protocol is "GraphQL" or "gRPC" or "REST" or "WS" or "WCF" or "MSG" or "RPC")
            {
                isEndpoint = true;
            }
            var parentScopeId = syntactic.ParentScopeId;
            if (isEndpoint)
            {
                if (symbolIsTest)
                {
                    return null;
                }
                var epNode = CreateEndpointNode(name, node, relativePath, workspaceId, syntactic, projectName);
                typedNode = epNode;
                if (!string.IsNullOrEmpty(parentScopeId) && !IsFileNodeScope(parentScopeId))
                {
                    var expRel = Relationship.FromRelationship(new ExposedByRelationship(typedNode.Id, parentScopeId)
                    {
                        FilePath = relativePath,
                        Line = (int)node.StartPosition.Row + 1,
                        LineEnd = (int)node.EndPosition.Row + 1,
                        Operation = epNode.HttpMethod,
                        EvidenceKind = OntologyConstants.EvidenceKinds.Endpoint
                    });
                    ctx?.AddGlobalProjectDependency(expRel);
                    relationships?.Add(expRel);
                }
            }
            else
            {
                typedNode = CreateEntryPointNode(name, node, relativePath, workspaceId, projectName);
                if (!string.IsNullOrEmpty(parentScopeId) && !IsFileNodeScope(parentScopeId))
                {
                    var implRel = Relationship.FromRelationship(new ImplementedByRelationship(typedNode.Id, parentScopeId)
                    {
                        FilePath = relativePath,
                        Line = (int)node.StartPosition.Row + 1,
                        Column = (int)node.StartPosition.Column + 1
                    });
                    ctx?.AddGlobalProjectDependency(implRel);
                    relationships?.Add(implRel);

                    var fnImplRel = Relationship.FromRelationship(new ImplementsRelationship(parentScopeId, typedNode.Id)
                    {
                        FilePath = relativePath,
                        Line = (int)node.StartPosition.Row + 1,
                        Column = (int)node.StartPosition.Column + 1
                    });
                    ctx?.AddGlobalProjectDependency(fnImplRel);
                    relationships?.Add(fnImplRel);
                }
            }
        }
        else if (kind == OntologyConstants.NodeLabels.ExternalService)
        {
            var parentScopeId = syntactic.ParentScopeId;
            var esCandidate = CreateExternalServiceNode(name, node, relativePath, workspaceId, parentScopeId);
            if (esCandidate.DomainOrService == "unknown-service" &&
                esCandidate.IsRelativePath != true &&
                !syntactic.Properties.ContainsKey(PropTestFramework))
            {
                return null;
            }
            typedNode = esCandidate;
            if (typedNode is ExternalServiceNode esNode && syntactic.Properties.TryGetValue(PropMethod, out var meth) && !string.IsNullOrEmpty(meth))
            {
                esNode.Method = meth;
            }
            if (!string.IsNullOrEmpty(parentScopeId))
            {
                var startLine = (int)node.StartPosition.Row + 1;
                var endLine = (int)node.EndPosition.Row + 1;
                var calledByRel = Relationship.FromRelationship(new CalledByRelationship(typedNode.Id, parentScopeId)
                {
                    FilePath = relativePath,
                    Line = startLine,
                    LineEnd = endLine,
                    Operation = (typedNode is ExternalServiceNode es && !string.IsNullOrEmpty(es.Method)) ? es.Method : "CALL",
                    EvidenceKind = OntologyConstants.EvidenceKinds.HttpCall
                });
                ctx?.AddGlobalProjectDependency(calledByRel);
                relationships?.Add(calledByRel);
            }
        }
        else if (kind == OntologyConstants.NodeLabels.Table)
        {
            if (symbolIsTest)
            {
                return null;
            }
            var resolvedName = name;
            if (ConstantRegistry.TryResolve(relativePath, name, out var rName))
            {
                if (!string.IsNullOrWhiteSpace(rName) &&
                    !rName.Contains("://") &&
                    !rName.StartWithIC("http") &&
                    !rName.Contains('/') &&
                    !rName.Contains(' '))
                {
                    resolvedName = rName;
                }
            }
            var explicitSchema = syntactic.GetPropertyOrDefault(PropSchemaName) ?? syntactic.GetPropertyOrDefault(PropSchema);
            if (string.IsNullOrEmpty(explicitSchema) && !string.IsNullOrEmpty(node.Text))
            {
                var sm = Regex.Match(node.Text, @"schema_name\s*=\s*""([^""]+)""", RegexOptions.IgnoreCase);
                if (sm.Success) explicitSchema = sm.Groups[1].Value;
            }
            var tableId = $"{workspaceId}:{OntologyConstants.IdPrefixes.Table}:{resolvedName.ToLowerInvariant()}";
            typedNode = new TableNode(tableId, resolvedName, relativePath)
            {
                Schema = explicitSchema
            };
            var parentScopeId = syntactic.ParentScopeId;
            if (!string.IsNullOrEmpty(parentScopeId) && !IsFileNodeScope(parentScopeId))
            {
                var tStartLine = (int)node.StartPosition.Row + 1;
                var tEndLine = (int)node.EndPosition.Row + 1;
                var accRel = Relationship.FromRelationship(new AccessedByRelationship(typedNode.Id, parentScopeId)
                {
                    FilePath = relativePath,
                    Line = tStartLine,
                    LineEnd = tEndLine,
                    Operation = "ORM",
                    EvidenceKind = OntologyConstants.EvidenceKinds.Orm
                });
                ctx?.AddGlobalProjectDependency(accRel);
                relationships?.Add(accRel);

                if (parentScopeId.Contains(":Type:", StringComparison.Ordinal) &&
                    !resolvedName.Equals("Entity", StringComparison.OrdinalIgnoreCase) &&
                    !resolvedName.Equals("Table", StringComparison.OrdinalIgnoreCase) &&
                    !resolvedName.Equals("ViewEntity", StringComparison.OrdinalIgnoreCase))
                {
                    var implRel = Relationship.FromRelationship(new ImplementedByRelationship(typedNode.Id, parentScopeId)
                    {
                        FilePath = relativePath,
                        Line = tStartLine,
                        LineEnd = tEndLine,
                        Operation = "ORM",
                        EvidenceKind = OntologyConstants.EvidenceKinds.Orm
                    });
                    ctx?.AddGlobalProjectDependency(implRel);
                    relationships?.Add(implRel);
                }
            }
        }
        else if (kind == OntologyConstants.NodeLabels.Topic)
        {
            var broker = syntactic.GetPropertyOrDefault(OntologyConstants.PropertyNames.BrokerType) ?? "event";
            var topicId = $"{workspaceId}:{OntologyConstants.IdPrefixes.Topic}:{broker}:{name}";
            var startLine = (int)node.StartPosition.Row + 1;
            var endLine = (int)node.EndPosition.Row + 1;
            var isSubscriber = string.Equals(
                syntactic.GetPropertyOrDefault(OntologyConstants.PropertyNames.MessagingDirection),
                OntologyConstants.EvidenceKinds.Subscribe,
                StringComparison.OrdinalIgnoreCase);
            var msgDirection = isSubscriber
                ? OntologyConstants.EvidenceKinds.Subscribe
                : OntologyConstants.EvidenceKinds.Publish;
            typedNode = new TopicNode(
                topicId,
                name,
                relativePath,
                broker)
            {
                FilePath = relativePath,
                Line = startLine,
                LineEnd = endLine,
                MessagingDirection = msgDirection
            };
            var parentScopeId = syntactic.ParentScopeId;
            if (!string.IsNullOrEmpty(parentScopeId))
            {
                var flowRel = isSubscriber
                    ? Relationship.FromRelationship(new SubscribedByRelationship(typedNode.Id, parentScopeId)
                    {
                        FilePath = relativePath,
                        Line = startLine,
                        LineEnd = endLine,
                        MessagingDirection = msgDirection,
                        Operation = "SUBSCRIBE",
                        EvidenceKind = msgDirection
                    })
                    : Relationship.FromRelationship(new PublishedByRelationship(typedNode.Id, parentScopeId)
                    {
                        FilePath = relativePath,
                        Line = startLine,
                        LineEnd = endLine,
                        MessagingDirection = msgDirection,
                        Operation = "PUBLISH",
                        EvidenceKind = msgDirection
                    });
                ctx?.AddGlobalProjectDependency(flowRel);
                relationships?.Add(flowRel);
            }
        }
        else if (kind == OntologyConstants.NodeLabels.Function)
        {
            var stageOrder = syntactic.TryGetProperty(OntologyConstants.PropertyNames.StageOrder, out var so) && int.TryParse(so, out var parsedOrder) ? parsedOrder : (int?)null;
            typedNode = new FunctionNode(symbolId, name, symbolId, relativePath, relativePath,
                node.StartPosition.Row + 1, node.EndPosition.Row + 1, node.StartPosition.Column + 1, node.EndPosition.Column + 1,
                symbolIsTest, projectKind, projectName, stageOrder);
        }
        else if (kind is "Class" or "Struct" or "Enum" || kind == OntologyConstants.NodeLabels.Type)
        {
            var typeKind = syntactic.TryGetProperty(OntologyConstants.PropertyNames.TypeKind, out var tk)
                ? tk
                : (kind == OntologyConstants.NodeLabels.Type ? "class" : kind.ToLowerInvariant());
            var stageOrder = syntactic.TryGetProperty(OntologyConstants.PropertyNames.StageOrder, out var so) && int.TryParse(so, out var parsedOrder) ? parsedOrder : (int?)null;
            typedNode = new TypeNode(symbolId, name, symbolId, relativePath, relativePath,
                node.StartPosition.Row + 1, node.EndPosition.Row + 1, node.StartPosition.Column + 1, node.EndPosition.Column + 1, typeKind,
                symbolIsTest, projectKind, projectName, stageOrder);
        }
        else if (kind == "Interface")
        {
            var stageOrder = syntactic.TryGetProperty(OntologyConstants.PropertyNames.StageOrder, out var so) && int.TryParse(so, out var parsedOrder) ? parsedOrder : (int?)null;
            typedNode = new TypeNode(symbolId, name, symbolId, relativePath, relativePath,
                node.StartPosition.Row + 1, node.EndPosition.Row + 1, node.StartPosition.Column + 1, node.EndPosition.Column + 1, "interface",
                symbolIsTest, projectKind, projectName, stageOrder);
        }
        else if (kind == OntologyConstants.NodeLabels.Member)
        {
            var memberKind = syntactic.TryGetProperty(OntologyConstants.PropertyNames.MemberKind, out var mk) ? mk : "field";
            var dataType = syntactic.TryGetProperty(OntologyConstants.PropertyNames.DataType, out var dt) ? dt : null;
            typedNode = new MemberNode(symbolId, name, symbolId, relativePath, relativePath,
                node.StartPosition.Row + 1, node.EndPosition.Row + 1, node.StartPosition.Column + 1, node.EndPosition.Column + 1,
                memberKind, dataType);
        }
        else if (kind == OntologyConstants.NodeLabels.CallLiteral)
        {
            var val = syntactic.Text ?? syntactic.GetPropertyOrDefault(OntologyConstants.PropertyNames.Value) ?? syntactic.Name;
            var callee = syntactic.Callee ?? syntactic.GetPropertyOrDefault(OntologyConstants.PropertyNames.Callee) ?? "";
            var argIdx = syntactic.ArgIndex != 0 ? syntactic.ArgIndex : (int.TryParse(syntactic.GetPropertyOrDefault(OntologyConstants.PropertyNames.ArgIndex), out var ai) ? ai : 0);
            var line = int.TryParse(syntactic.GetPropertyOrDefault(OntologyConstants.PropertyNames.Line), out var ln) ? ln : (int)node.StartPosition.Row + 1;
            var col = int.TryParse(syntactic.GetPropertyOrDefault(OntologyConstants.PropertyNames.Column), out var cl) ? cl : (int)node.StartPosition.Column + 1;
            var role = syntactic.Role ?? syntactic.GetPropertyOrDefault(OntologyConstants.PropertyNames.Role) ?? "business";
            var isDecorator = syntactic.IsDecorator || (bool.TryParse(syntactic.GetPropertyOrDefault(OntologyConstants.PropertyNames.IsDecorator), out var isDec) && isDec);
            var litId = $"{workspaceId}:{OntologyConstants.IdPrefixes.CallLiteral}:{relativePath}:{line}:{col}";
            typedNode = new CallLiteralNode(litId, name, val, callee, argIdx, relativePath, line, col, symbolIsTest, projectKind, projectName, role, isDecorator);
        }
        else if (kind == OntologyConstants.NodeLabels.CatchClause)
        {
            var exType = syntactic.RequestType ?? syntactic.GetPropertyOrDefault(OntologyConstants.PropertyNames.ExceptionType) ?? "";
            var isEmpty = syntactic.IsEmptyCatch || (bool.TryParse(syntactic.GetPropertyOrDefault(OntologyConstants.PropertyNames.IsEmpty), out var ie) && ie);
            var hasComment = syntactic.HasCatchComment || (bool.TryParse(syntactic.GetPropertyOrDefault(OntologyConstants.PropertyNames.HasComment), out var hc) && hc);
            var stmtCount = syntactic.ArgIndex != 0 ? syntactic.ArgIndex : (int.TryParse(syntactic.GetPropertyOrDefault(OntologyConstants.PropertyNames.StatementCount), out var sc) ? sc : 0);
            var line = int.TryParse(syntactic.GetPropertyOrDefault(OntologyConstants.PropertyNames.Line), out var ln) ? ln : (int)node.StartPosition.Row + 1;
            var col = int.TryParse(syntactic.GetPropertyOrDefault(OntologyConstants.PropertyNames.Column), out var cl) ? cl : (int)node.StartPosition.Column + 1;
            var catchId = $"{workspaceId}:{OntologyConstants.IdPrefixes.CatchClause}:{relativePath}:{line}:{col}";
            typedNode = new CatchClauseNode(catchId, name, exType, relativePath, line, col, isEmpty, hasComment, stmtCount, symbolIsTest, projectKind, projectName);
        }

        if (typedNode == null) return null;

        foreach (var reference in syntactic.References)
        {
            var target = reference.TargetName;
            var resolvedScopeId = string.IsNullOrEmpty(reference.ScopeSymbolId) ? typedNode.Id : reference.ScopeSymbolId;
            if (reference.Kind == OntologyConstants.Relationships.PersistedIn &&
                ConstantRegistry.TryResolve(relativePath, target, out var resolvedTarget))
            {
                target = resolvedTarget;
            }
            var refArgFlow = string.IsNullOrEmpty(reference.ArgFlow) ? relativePath : reference.ArgFlow;
            typedNode.References.Add(reference with { ScopeSymbolId = resolvedScopeId, TargetName = target, ArgFlow = refArgFlow });
        }

        if (syntactic.HasProperties)
        {
            foreach (var (k, v) in syntactic.Properties)
            {
                typedNode.ApplySyntacticProperty(k, v);
            }
        }

        foreach (var childSyntactic in syntactic.Children)
        {
            var childNode = MapSemanticSymbolToOntology(childSyntactic, relativePath, workspaceId, project, ctx, projectName, symbolIsTest, projectKind, relationships);
            if (childNode != null)
            {
                typedNode.Children.Add(childNode);
            }
        }

        return typedNode;
    }

    private static string CombineRoutes(string prefix, string route)
    {
        prefix = (prefix ?? "").Trim('/');
        route = (route ?? "").Trim('/');
        if (string.IsNullOrEmpty(prefix)) return "/" + route;
        if (string.IsNullOrEmpty(route)) return "/" + prefix;
        return $"/{prefix}/{route}";
    }

    private static EndpointNode CreateEndpointNode(
        string name,
        SyntaxNode node,
        string relativePath,
        string workspaceId,
        SyntacticSymbol? syntactic = null,
        string? projectName = null)
    {
        var idx = name.IndexOf(':');
        var rawMethod = idx > 0 ? name[..idx].ToUpperInvariant() : (syntactic?.Protocol == "gRPC" ? "RPC" : (syntactic?.Protocol == "WS" ? "WS" : (syntactic?.Protocol == "MSG" ? "MSG" : "GET")));
        var route = idx > 0 ? name[(idx + 1)..] : name;

        var protocol = syntactic?.Protocol;
        var method = rawMethod;

        if (rawMethod is "WS" or "HUB" || protocol == "WS")
        {
            protocol = "WS";
            method = "WS";
            name = $"WS:{route}";
        }
        else if (rawMethod is "MSG" or "CONSUMER" or "CONSUME" or "HANDLE" or "EVENT" or "KAFKA" or "RABBITMQ" or "GCP" or "SQS" or "SNS" or "NATS" or "ACTIVE-MQ" or "ACTIVEMQ" or "AZURE-SB" or "MQTT" or "REDIS" or "EVENTBUS" or "MASSTRANSIT" || protocol == "MSG")
        {
            protocol = "MSG";
            method = "MSG";
            name = $"MSG:{route}";
        }
        else if (rawMethod == "GRAIN")
        {
            protocol = "RPC";
            method = "RPC";
            name = $"RPC:{route}";
        }
        else if (rawMethod == "WCF" || protocol == "WCF")
        {
            protocol = "WCF";
            method = "WCF";
            name = $"WCF:{route}";
        }
        else
        {
            protocol ??= method is "RPC" or "GRPC" ? "gRPC" : (method is "GRAPHQL" or "QUERY" or "MUTATION" or "SUBSCRIPTION" ? "GraphQL" : "REST");
        }

        var resolvedProj = !string.IsNullOrEmpty(projectName) ? projectName : GetProjectNameFromRelativePath(relativePath);
        if (!string.IsNullOrEmpty(resolvedProj) &&
            (resolvedProj.StartWithIC("internal-service-") ||
             resolvedProj.StartWithIC("integration-service-") ||
             resolvedProj.StartWithIC("external-service-")))
        {
            var stripped = WorkspaceConventions.NormalizeServiceName(resolvedProj);
            if (!string.IsNullOrEmpty(stripped)) resolvedProj = stripped;
        }

        var operationType = syntactic?.OperationType is { } opEnum && opEnum != CodeExplorer.Cypher.Common.OperationType.Unspecified
            ? CodeExplorer.Cypher.Common.TaxonomyEnumExtendedExtensions.ToCanonicalString(opEnum)
            : (protocol == "GraphQL"
                ? (method is "MUTATION" ? "Mutation" : (method is "SUBSCRIPTION" ? "Subscription" : "Query"))
                : (protocol == "gRPC" ? "Unary" : null));

        // Apply file/module mount prefix if discovered (e.g. sub-router mount)
        if (protocol == "REST" &&
            !string.IsNullOrEmpty(relativePath) &&
            RouteDictionaryRegistry.TryGetMountPrefix(relativePath, out var mountPrefix) &&
            !string.IsNullOrEmpty(mountPrefix))
        {
            var normRoute = route.Trim('/');
            if (!normRoute.StartWithIC(mountPrefix))
            {
                route = CombineRoutes(mountPrefix, route);
                name = $"{method}:{route}";
            }
        }

        // Apply project global route prefix (e.g. 'api/v1' or 'api') if discovered and not already present (REST only)
        if (protocol == "REST" &&
            !string.IsNullOrEmpty(resolvedProj) &&
            RouteDictionaryRegistry.TryGetGlobalPrefix(resolvedProj, out var globalPrefix) &&
            !string.IsNullOrEmpty(globalPrefix))
        {
            var normRoute = route.Trim('/');
            if (!normRoute.StartWithIC(globalPrefix) &&
                !normRoute.Equals("ping", StringComparison.OrdinalIgnoreCase))
            {
                route = CombineRoutes(globalPrefix, route);
                name = $"{method}:{route}";
            }
        }

        var projPart = (!string.IsNullOrEmpty(resolvedProj) && resolvedProj != "default") ? $"{resolvedProj}:" : "";
        var schemaPart = syntactic != null &&
                         syntactic.TryGetProperty(OntologyConstants.PropertyNames.GraphqlSchema, out var gqlSchema) &&
                         !string.IsNullOrEmpty(gqlSchema)
            ? $"{gqlSchema}:"
            : "";
        var endpointId = $"{workspaceId}:{OntologyConstants.IdPrefixes.Endpoint}:{projPart}{schemaPart}{method}:{route}";
        var startLine = (int)node.StartPosition.Row + 1;
        var endLine = (int)node.EndPosition.Row + 1;
        return new EndpointNode(
            endpointId,
            name,
            relativePath,
            method,
            route,
            protocol,
            syntactic?.IsAnonymous ?? false,
            syntactic?.RequiredRoles,
            syntactic?.Policies,
            syntactic?.RequestType,
            syntactic?.ResponseType,
            operationType)
        {
            FilePath = relativePath,
            StartLine = startLine,
            EndLine = endLine,
            Line = startLine,
            LineEnd = endLine,
            Operation = method,
            EvidenceKind = OntologyConstants.EvidenceKinds.Endpoint
        };
    }

    private static EntryPointNode CreateEntryPointNode(
        string name,
        SyntaxNode node,
        string relativePath,
        string workspaceId,
        string? projectName = null)
    {
        var resolvedProj = !string.IsNullOrEmpty(projectName) ? projectName : GetProjectNameFromRelativePath(relativePath);
        if (!string.IsNullOrEmpty(resolvedProj) &&
            (resolvedProj.StartWithIC("internal-service-") ||
             resolvedProj.StartWithIC("integration-service-") ||
             resolvedProj.StartWithIC("external-service-")))
        {
            var stripped = WorkspaceConventions.NormalizeServiceName(resolvedProj);
            if (!string.IsNullOrEmpty(stripped)) resolvedProj = stripped;
        }
        if (string.IsNullOrEmpty(resolvedProj)) resolvedProj = "default";

        var entryType = "grpc";
        var cleanName = name;

        if (name.StartWithIC("ws:"))
        {
            entryType = "queue-listener";
            cleanName = name[3..];
        }
        else if (name.StartWithIC("event:"))
        {
            entryType = "queue-listener";
            cleanName = name[6..];
        }
        else if (name.Contains(':'))
        {
            var idx = name.IndexOf(':');
            entryType = name[..idx];
            cleanName = name[(idx + 1)..];
        }

        if ((cleanName == "__main__" || (entryType == "main-method" && cleanName == "main")) && !string.IsNullOrEmpty(relativePath))
        {
            var dir = Path.GetFileName(Path.GetDirectoryName(relativePath.Replace('\\', '/')));
            var fn = (!string.IsNullOrEmpty(dir) && dir != "src" && dir != "cmd") ? dir : Path.GetFileName(relativePath);
            if (!string.IsNullOrEmpty(fn)) cleanName = fn;
        }

        var projPart = (!string.IsNullOrEmpty(resolvedProj) && resolvedProj != "default") ? $"{resolvedProj}:" : "";
        var entryPointId = $"{workspaceId}:{OntologyConstants.IdPrefixes.EntryPoint}:{projPart}{entryType}:{cleanName}";

        return new EntryPointNode(
            entryPointId,
            cleanName,
            relativePath,
            entryType)
        {
            FilePath = relativePath,
            StartLine = (int)node.StartPosition.Row + 1,
            StartCol = (int)node.StartPosition.Column + 1
        };
    }

    private static ExternalServiceNode CreateExternalServiceNode(
        string name,
        SyntaxNode node,
        string relativePath,
        string workspaceId,
        string? parentScopeId = null)
    {
        var cleanName = name?.Trim('"', '\'', '`', ' ', ';') ?? "";
        if (!WorkspaceConventions.IsTestFilePath(relativePath) &&
            cleanName.StartWithIC("graphql:"))
        {
            var gqlRem = cleanName["graphql:".Length..].Trim();
            var gqlOpType = "Query";
            var gqlField = gqlRem;
            var colonIdx = gqlRem.IndexOf(':');
            if (colonIdx > 0)
            {
                var maybeOp = gqlRem[..colonIdx].ToUpperInvariant();
                if (maybeOp is "QUERY" or "MUTATION" or "SUBSCRIPTION")
                {
                    gqlOpType = maybeOp switch
                    {
                        "MUTATION" => "Mutation",
                        "SUBSCRIPTION" => "Subscription",
                        _ => "Query"
                    };
                    gqlField = gqlRem[(colonIdx + 1)..].Trim();
                }
            }

            if (!string.IsNullOrEmpty(gqlField))
            {
                var gqlPath = "/" + gqlField.TrimStart('/');
                var scopeKey = !string.IsNullOrEmpty(parentScopeId) ? parentScopeId : relativePath;
                var gqlExtId = $"{workspaceId}:{OntologyConstants.IdPrefixes.ExternalService}:graphql:{gqlOpType}:{gqlField}:{scopeKey}";
                return new ExternalServiceNode(
                    gqlExtId,
                    gqlField,
                    "graphql",
                    "graphql",
                    gqlPath)
                {
                    FilePath = relativePath,
                    StartLine = (int)node.StartPosition.Row + 1,
                    StartCol = (int)node.StartPosition.Column + 1,
                    GraphqlField = gqlField,
                    OperationType = gqlOpType,
                    IsInternal = true,
                    Url = gqlPath,
                    CallerSymbolId = !string.IsNullOrEmpty(parentScopeId) ? parentScopeId : null
                };
            }
        }

        // Strip query string if present (? or &)
        var qEarly = cleanName.IndexOf('?');
        if (qEarly >= 0) cleanName = cleanName[..qEarly];
        var ampEarly = cleanName.IndexOf('&');
        if (ampEarly >= 0) cleanName = cleanName[..ampEarly];

        // Normalize wildcard-relative path like */api/v1/... to /api/v1/...
        if (cleanName.StartsWith("*/"))
        {
            cleanName = cleanName[1..];
        }

        var stripped = cleanName.Trim('*', '?', '&', '=', '/', ' ');
        if (string.IsNullOrWhiteSpace(stripped))
        {
            cleanName = "unknown-service";
        }

        if (WorkspaceConventions.IsTestFilePath(relativePath) ||
            string.IsNullOrWhiteSpace(cleanName) ||
            cleanName == "unknown-service" ||
            cleanName.StartsWith('*') ||
            cleanName.StartsWith('?') ||
            cleanName.StartsWith('&') ||
            cleanName.StartsWith('=') ||
            cleanName.Contains("api-version=") ||
            cleanName.Length > 256 ||
            cleanName.Contains('\n') ||
            cleanName.Contains('\r') ||
            (!cleanName.StartWithIC("http") && !IsPathWithParameters(cleanName) && (cleanName.Contains('{') || cleanName.Contains('}'))) ||
            cleanName.Contains('<') ||
            cleanName.Contains('>') ||
            cleanName.Contains('+') ||
            cleanName.Contains('(') ||
            cleanName.Contains(')') ||
            cleanName.Contains('"') ||
            cleanName.Contains('\'') ||
            cleanName.StartsWith('$') ||
            cleanName.Contains("=>") ||
            cleanName.Contains(';') ||
            int.TryParse(cleanName, out _))
        {
            cleanName = "unknown-service";
        }

        var protocol = "http";
        var domainOrService = cleanName;
        const string schemeDubboColon = "dubbo:";
        const string schemeTriColon = "tri:";
        var hasExplicitScheme = domainOrService.Contains("://") ||
                                domainOrService.StartWithIC("http:") ||
                                domainOrService.StartWithIC("https:") ||
                                domainOrService.StartWithIC("ws:") ||
                                domainOrService.StartWithIC("wss:") ||
                                domainOrService.StartWithIC("grpc:") ||
                                domainOrService.StartWithIC("rpc:") ||
                                domainOrService.StartWithIC(schemeDubboColon) ||
                                domainOrService.StartWithIC(schemeTriColon);

        if (domainOrService.Contains("://"))
        {
            var pIdx = domainOrService.IndexOf("://");
            protocol = domainOrService[..pIdx];
            domainOrService = domainOrService[(pIdx + 3)..];
        }
        else if (domainOrService.StartWithIC(SchemeWsColon))
        {
            protocol = "ws";
            domainOrService = domainOrService[3..];
        }
        else if (domainOrService.StartWithIC(SchemeHttpColon))
        {
            protocol = "http";
            domainOrService = domainOrService[5..];
        }
        else if (domainOrService.StartWithIC(SchemeHttpsColon))
        {
            protocol = "https";
            domainOrService = domainOrService[6..];
        }
        else if (domainOrService.StartWithIC(SchemeGrpcColon))
        {
            hasExplicitScheme = true;
            protocol = "grpc";
            domainOrService = domainOrService[5..];
        }
        else if (domainOrService.StartWithIC(SchemeRpcColon))
        {
            hasExplicitScheme = true;
            protocol = "grpc";
            domainOrService = domainOrService[4..];
        }
        else if (domainOrService.StartWithIC(SchemeDubboColon))
        {
            hasExplicitScheme = true;
            protocol = "dubbo";
            domainOrService = domainOrService[6..];
        }
        else if (domainOrService.StartWithIC(SchemeTriColon))
        {
            hasExplicitScheme = true;
            protocol = "tri";
            domainOrService = domainOrService[4..];
        }
        else if (domainOrService.Contains(':'))
        {
            var idx = domainOrService.IndexOf(':');
            var candidateProto = domainOrService[..idx].ToLowerInvariant();
            if (candidateProto is "http" or "https" or "ws" or "wss" or "grpc" or "rpc" or "dubbo" or "tri")
            {
                hasExplicitScheme = true;
                protocol = candidateProto;
                domainOrService = domainOrService[(idx + 1)..];
            }
        }

        var hadExplicitPath = domainOrService.Contains('/');
        var path = "/";
        var slashIdx = domainOrService.IndexOf('/');
        if (protocol is "grpc" or "dubbo" or "tri")
        {
            var grpcSegments = domainOrService.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            domainOrService = grpcSegments.Length > 0 ? grpcSegments[0] : domainOrService.Trim('/');
            path = grpcSegments.Length > 1 ? "/" + string.Join('/', grpcSegments.Skip(1)) : "/" + domainOrService;
            slashIdx = -1;
        }
        else if (slashIdx > 0)
        {
            var firstSegment = domainOrService[..slashIdx].ToLowerInvariant();
            if (firstSegment is "api" or "rest" or "v1" or "v2" or "v3" or "v4" or "v5" or "graphql")
            {
                domainOrService = "/" + domainOrService;
                slashIdx = 0;
            }
            else
            {
                path = domainOrService[slashIdx..];
                domainOrService = domainOrService[..slashIdx];
            }
        }

        if (slashIdx == 0)
        {
            var qIdx0 = domainOrService.IndexOf('?');
            if (qIdx0 >= 0) domainOrService = domainOrService[..qIdx0];
            var hIdx0 = domainOrService.IndexOf('#');
            if (hIdx0 >= 0) domainOrService = domainOrService[..hIdx0];

            path = domainOrService;
            var segments = domainOrService.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            string? candidate = null;
            for (var i = 0; i < segments.Length; i++)
            {
                var seg = segments[i];
                if (seg.Equals("api", StringComparison.OrdinalIgnoreCase)) continue;
                if (seg.StartWithIC("v") && seg.Length <= 6 && seg.Skip(1).All(c => char.IsDigit(c) || c == '.')) continue;
                if (seg.Equals("assets", StringComparison.OrdinalIgnoreCase) || seg.Equals("static", StringComparison.OrdinalIgnoreCase) || seg.Equals("public", StringComparison.OrdinalIgnoreCase))
                {
                    candidate = null;
                    break;
                }
                if ((seg.Equals("gateway", StringComparison.OrdinalIgnoreCase) || seg.Equals("gw", StringComparison.OrdinalIgnoreCase)) && i + 1 < segments.Length)
                {
                    continue;
                }
                if (seg.All(c => c is '-' or '_'))
                {
                    continue;
                }
                candidate = seg;
                break;
            }

            if (!string.IsNullOrWhiteSpace(candidate) && candidate.All(c => char.IsLetterOrDigit(c) || c is '-' or '_'))
            {
                domainOrService = candidate;
            }
            else
            {
                domainOrService = "unknown-service";
            }
        }

        var qIdx = path.IndexOf('?');
        if (qIdx >= 0) path = path[..qIdx];
        var hIdx = path.IndexOf('#');
        if (hIdx >= 0) path = path[..hIdx];
        if (string.IsNullOrEmpty(path)) path = "/";

        var colonPortIdx = domainOrService.LastIndexOf(':');
        if (colonPortIdx > 0 && int.TryParse(domainOrService[(colonPortIdx + 1)..], out _))
        {
            domainOrService = domainOrService[..colonPortIdx];
        }

        var qIdxDomain = domainOrService.IndexOf('?');
        if (qIdxDomain >= 0) domainOrService = domainOrService[..qIdxDomain];
        var ampIdxDomain = domainOrService.IndexOf('&');
        if (ampIdxDomain >= 0) domainOrService = domainOrService[..ampIdxDomain];
        var eqIdxDomain = domainOrService.IndexOf('=');
        if (eqIdxDomain >= 0) domainOrService = domainOrService[..eqIdxDomain];

        if (domainOrService.StartsWith('*') ||
            domainOrService.StartsWith('?') ||
            domainOrService.StartsWith('&') ||
            domainOrService.StartsWith('=') ||
            domainOrService.Contains('&') ||
            domainOrService.Contains('?') ||
            domainOrService.Contains('=') ||
            domainOrService.Contains('*') ||
            domainOrService.Contains('{') ||
            domainOrService.Contains('}') ||
            domainOrService.Contains(' ') ||
            string.IsNullOrWhiteSpace(domainOrService.Trim('*', '?', '&', '=', '/', ' ')))
        {
            domainOrService = "unknown-service";
        }
        else if (domainOrService.Contains('.'))
        {
            if (domainOrService.Contains(' ') || domainOrService.Any(c => c is '"' or '\'' or '{' or '}'))
            {
                domainOrService = "unknown-service";
            }
        }
        else if (domainOrService != "*" &&
                 protocol != "ws" && protocol != "wss" && protocol != "grpc")
        {
            if (string.IsNullOrWhiteSpace(domainOrService) ||
                domainOrService.Contains(' ') ||
                !Regex.IsMatch(domainOrService, @"^[a-zA-Z0-9_\-]+$"))
            {
                domainOrService = "unknown-service";
            }
        }

        var normalizedDomain = WorkspaceConventions.NormalizeServiceName(domainOrService);
        if (!string.IsNullOrEmpty(normalizedDomain) && !domainOrService.Contains('.'))
        {
            domainOrService = normalizedDomain;
        }

        var isGarbage = WorkspaceConventions.IsGarbageExternalService(domainOrService) ||
                        WorkspaceConventions.IsGenericUrlStemOrVariable(domainOrService);
        if (isGarbage)
        {
            domainOrService = "unknown-service";
        }

        var serviceScopeSuffix = domainOrService == "unknown-service"
            ? $":{ConstantRegistry.ExtractProjectName(relativePath) ?? "generic"}"
            : "";
        var extServiceId = $"{workspaceId}:{OntologyConstants.IdPrefixes.ExternalService}:{protocol}:{domainOrService}{serviceScopeSuffix}";

        bool? isRelativePath = null;
        bool? inferredFromPath = null;
        bool? isInternal = null;
        bool? isExternal;
        bool? inferredFromIdentifier = null;
        string? url = null;

        if (slashIdx == 0 || (isGarbage && path.StartsWith('/') && path != "/"))
        {
            isRelativePath = true;
            inferredFromPath = true;
            isInternal = true;
            isExternal = false;
            url = path;
        }
        else if (domainOrService is "localhost" or "127.0.0.1" || domainOrService.StartsWith("localhost:") || domainOrService.StartsWith("127.0.0.1:"))
        {
            isInternal = true;
            isExternal = false;
        }
        else if (isGarbage || domainOrService == "unknown-service" ||
                 domainOrService.StartsWith('*') || domainOrService.StartsWith('?') || domainOrService.StartsWith('&') || domainOrService.StartsWith('='))
        {
            isExternal = false;
        }
        else if (!hasExplicitScheme && !domainOrService.Contains('.') && !hadExplicitPath)
        {
            isExternal = false;
            inferredFromIdentifier = true;
        }
        else
        {
            isExternal = true;
        }
        return new ExternalServiceNode(
            extServiceId,
            domainOrService,
            protocol,
            domainOrService,
            path)
        {
            FilePath = relativePath,
            StartLine = (int)node.StartPosition.Row + 1,
            StartCol = (int)node.StartPosition.Column + 1,
            CallerSymbolId = !string.IsNullOrEmpty(parentScopeId) ? parentScopeId : null,
            Url = url,
            IsInternal = isInternal,
            IsExternal = isExternal,
            IsRelativePath = isRelativePath,
            InferredFromPath = inferredFromPath,
            InferredFromIdentifier = inferredFromIdentifier
        };
    }

    public static bool IsRouteOrConfigCandidate(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        var p = path.ToLowerInvariant();
        return (p.EndsWith(".ts") || p.EndsWith(".js") || p.EndsWith(".cs") || p.EndsWith(".go") || p.EndsWith(".py") || p.EndsWith(".json")) &&
               (p.Contains("route") || p.Contains("const") || p.Contains("config") || p.Contains("api") || p.Contains("endpoint") || p.Contains("url") || p.Contains("env") ||
                p.Contains("client") || p.Contains("service") || p.Contains("importer") || p.Contains("catalog") || p.Contains("gateway") || p.Contains("http") ||
                p.Contains("main.") || p.Contains("app.") || p.Contains("bootstrap.") || p.Contains("index.") || p.Contains("program.") || p.Contains("startup."));
    }

    public static string GetProjectNameFromRelativePath(string relativePath)
    {
        var cleanPath = relativePath.Replace('\\', '/').Trim('/');
        var parts = cleanPath.Split('/');
        if (parts.Length == 0) return "default";

        if (parts.Length >= 2 && (parts[0] is "Core" or "Parsers" or "Tests"))
        {
            return parts[1];
        }

        return parts[0];
    }

    private static bool IsPathWithParameters(string path)
    {
        if (string.IsNullOrEmpty(path) || !path.StartsWith('/')) return false;
        var stripped = Regex.Replace(path, @"\{[a-zA-Z0-9_]+\}", "");
        return !stripped.Contains('{') && !stripped.Contains('}');
    }

    private static bool IsFileNodeScope(string scopeId) =>
        scopeId.Contains($":{OntologyConstants.IdPrefixes.File}:") || scopeId.Contains(":file:");
}
