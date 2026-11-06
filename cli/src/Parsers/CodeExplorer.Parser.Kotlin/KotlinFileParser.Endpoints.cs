using System.Text.RegularExpressions;
using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Parser.Syntax;

namespace CodeExplorer.Parser.Kotlin;

public partial class KotlinFileParser
{
    private const string ProtocolGrpc = "gRPC";
    private const string ProtocolWs = "WS";
    private const string ProtocolHttp = "http";
    private const string OperationRpc = "RPC";
    private const string MethodGet = "GET";
    private const string MethodPost = "POST";
    private const string MethodWs = "WS";
    private const string OpSelect = "SELECT";
    private const string OpInsert = "INSERT";
    private const string OpUpdate = "UPDATE";
    private const string OpDelete = "DELETE";
    private const string OpUpsert = "UPSERT";

    private sealed record KtorRouteScope(int OpenBrace, int CloseBrace, string RawPath, string Prefix);

    private static readonly Regex KtorRouteScopeRegex = new(
        @"(?<![a-zA-Z0-9_])\broute\s*\(\s*(?<quote>['""])(?<path>[^'""]*)\k<quote>(?:\s*,\s*[^)]*)?\)\s*\{",
        RegexOptions.Compiled);

    private static readonly Regex KtorRoutingBlockRegex = new(
        @"(?<![a-zA-Z0-9_])\brouting\s*\{",
        RegexOptions.Compiled);

    private static readonly Regex KtorVerbWithPathRegex = new(
        @"(?<!\b(?:val|var|fun|class|return|if|while|for)\b)(?<!\.)\b(?<verb>get|post|put|delete|patch|head|options)\s*\(\s*(?<quote>['""])(?<path>[^'""]*)\k<quote>(?:\s*,\s*[^)]*)?\)\s*\{",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex KtorVerbNoPathRegex = new(
        @"(?<!\b(?:val|var|fun|class|return|if|while|for)\b)(?<!\.)\b(?<verb>get|post|put|delete|patch|head|options)\s*\{",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex KtorStaticResourcesRegex = new(
        @"(?<![a-zA-Z0-9_])\b(?:staticResources|static)\s*\(\s*(?<quote>['""])(?<path>[^'""]*)\k<quote>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex KtorWebSocketRegex = new(
        @"(?<![a-zA-Z0-9_])\bwebSocket\s*(?:\(\s*(?<quote>['""])(?<path>[^'""]*)\k<quote>[^)]*\))?\s*\{",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex KtorClientDirectCallRegex = new(
        @"(?<!\b(?:fun|class|val|var)\s+)\b(?<clientVar>[a-zA-Z0-9_]*[cC]lient|[hH]ttpClient|[hH]ttp)\s*\.\s*(?<method>get|post|put|delete|patch|head|options)\s*(?:<[^>]+>\s*)?\(\s*(?<arg>[^)\n]+)\)",
        RegexOptions.Compiled);

    private static readonly Regex KtorClientLambdaCallRegex = new(
        @"(?<!\b(?:fun|class|val|var)\s+)\b(?<clientVar>[a-zA-Z0-9_]*[cC]lient|[hH]ttpClient|[hH]ttp)\s*\.\s*(?<method>get|post|put|delete|patch|head|options)\s*\{(?<body>[^}]*)\}",
        RegexOptions.Compiled);

    private static readonly Regex ApiClientHelperCallRegex = new(
        @"\b(?<method>get|post|put|delete|patch)\s*\(\s*[""](?<path>[^""]+)[""]\s*\)",
        RegexOptions.Compiled);

    private static readonly Regex ExposedOperationRegex = new(
        @"(?<!\bval\b\s*|var\s*)\b(?<tableOrObj>[A-Z][a-zA-Z0-9_]*)\s*\.\s*(?<op>selectAll|select|insert|insertIgnore|insertAndGetId|batchInsert|update|deleteWhere|deleteAll|upsert)\b",
        RegexOptions.Compiled);

    private static readonly Regex ExposedExecRegex = new(
        @"\bexec\s*\(\s*(?<quote>['""])(?<sql>(?:\\.|(?!\k<quote>).)+)\k<quote>",
        RegexOptions.Compiled);

    private static void ParseFunctionsAndEndpoints(
        string content,
        string commentMasked,
        string masked,
        int[] lineOffsets,
        List<ClassScope> classScopes,
        FileNode fileNode,
        string workspaceId,
        string relativePath,
        List<SyntacticSymbol> semanticSymbols)
    {
        var funMatches = FunDeclRegex.Matches(masked);
        var funSpans = new List<(int Start, int End, FunctionNode Node)>();

        foreach (Match fm in funMatches)
        {
            var funName = fm.Groups["name"].Value.Trim();
            var (funLine, funCol) = GetLineAndColumn(lineOffsets, fm.Index);

            ClassScope? enclosingClass = null;
            var bestSpan = int.MaxValue;
            foreach (var cs in classScopes)
            {
                if (cs.OpenBraceIndex >= 0 && cs.CloseBraceIndex >= 0 &&
                    fm.Index >= cs.OpenBraceIndex && fm.Index <= cs.CloseBraceIndex)
                {
                    var span = cs.CloseBraceIndex - cs.OpenBraceIndex;
                    if (span < bestSpan)
                    {
                        bestSpan = span;
                        enclosingClass = cs;
                    }
                }
            }

            var funId = $"{workspaceId}:{OntologyConstants.IdPrefixes.Symbol}:{relativePath}:Function:{funName}";
            var funcNode = new FunctionNode(
                funId,
                funName,
                funName,
                relativePath,
                relativePath,
                funLine,
                funLine,
                funCol,
                funCol
            );

            if (enclosingClass != null)
            {
                enclosingClass.Node.Children.Add(funcNode);
            }
            else
            {
                fileNode.Children.Add(funcNode);
            }

            var funOpenBrace = masked.IndexOf('{', fm.Index + fm.Length);
            var funCloseBrace = funOpenBrace >= 0 ? FindMatchingCloseBrace(masked, funOpenBrace) : -1;
            var funEnd = funCloseBrace > funOpenBrace ? funCloseBrace : fm.Index + fm.Length;
            funSpans.Add((fm.Index, funEnd, funcNode));

            if (enclosingClass?.IsGrpcService == true &&
                !funName.StartsWith('<') && funName != "equals" && funName != "hashCode" && funName != "toString")
            {
                var serviceName = enclosingClass.Name.Replace("GrpcService", string.Empty);
                var fullRoute = $"/{serviceName}/{funName}";
                var epId = $"{workspaceId}:{OntologyConstants.IdPrefixes.Endpoint}:{OperationRpc}:{fullRoute}";
                var epName = $"{OperationRpc} {fullRoute}";

                var epNode = new EndpointNode(
                    epId,
                    epName,
                    relativePath,
                    OperationRpc,
                    fullRoute,
                    ProtocolGrpc
                )
                {
                    FilePath = relativePath,
                    StartLine = funLine,
                    EndLine = funLine,
                    Line = funLine,
                    LineEnd = funLine,
                    Operation = OperationRpc,
                    EvidenceKind = OntologyConstants.EvidenceKinds.Endpoint
                };
                epNode.CustomProperties ??= new();
                epNode.CustomProperties["path"] = fullRoute;
                epNode.CustomProperties["route"] = fullRoute;
                epNode.CustomProperties["route_template"] = fullRoute;
                epNode.CustomProperties["method"] = OperationRpc;
                epNode.CustomProperties["http_method"] = OperationRpc;
                epNode.CustomProperties["file_path"] = relativePath;
                fileNode.Children.Add(epNode);

                fileNode.References.Add(new Reference(epId, funcNode.Id, OntologyConstants.Relationships.ExposedBy, funLine, funCol));
            }

            var annotations = ExtractPrecedingAnnotations(commentMasked, masked, fm.Index);

            var fnSyntaxNode = new FunctionDeclSyntax
            {
                Name = funName,
                Span = new TextSpan(fm.Index, fm.Index + fm.Length, funLine - 1, funCol - 1, funLine - 1, funCol - 1)
            };

            foreach (var ann in annotations)
            {
                if (ann.Name.Equals("Path", StringComparison.OrdinalIgnoreCase) &&
                    annotations.Any(a => a != ann && a.Name is "GET" or "POST" or "PUT" or "DELETE" or "PATCH" or "HEAD" or "OPTIONS"))
                {
                    continue;
                }

                var httpMethod = ann.Name.ToUpperInvariant() switch
                {
                    "GETMAPPING" or "GET" => "GET",
                    "POSTMAPPING" or "POST" => "POST",
                    "PUTMAPPING" or "PUT" => "PUT",
                    "DELETEMAPPING" or "DELETE" => "DELETE",
                    "PATCHMAPPING" or "PATCH" => "PATCH",
                    "HEAD" => "HEAD",
                    "OPTIONS" => "OPTIONS",
                    "PATH" => "ALL",
                    "SERVERWEBSOCKET" => "WS",
                    "REQUESTMAPPING" => DetermineHttpMethodFromRequestMapping(ann.Args),
                    _ => null
                };

                if (httpMethod != null && enclosingClass?.IsClient == true)
                {
                    var paths = ExtractRoutePathsFromAnnotation(ann.Args);
                    if (paths.Count == 0 || (paths.Count == 1 && string.IsNullOrEmpty(paths[0])))
                    {
                        var siblingPathAnn = annotations.FirstOrDefault(a => a.Name.Equals("Path", StringComparison.OrdinalIgnoreCase));
                        if (siblingPathAnn != null)
                        {
                            paths = ExtractRoutePathsFromAnnotation(siblingPathAnn.Args);
                        }
                    }

                    if (paths.Count == 0 || (paths.Count == 1 && string.IsNullOrEmpty(paths[0])))
                    {
                        paths = ["/"];
                    }

                    foreach (var rawPath in paths)
                    {
                        var fullRoute = CombineRoutes(enclosingClass?.RoutePrefix, rawPath);
                        var cleanPath = fullRoute.StartsWith('/') ? fullRoute : "/" + fullRoute;
                        var extId = $"{workspaceId}:{OntologyConstants.IdPrefixes.ExternalService}:{relativePath}:{funcNode.Name}:{funLine}:{funCol}";
                        var extName = $"{httpMethod} {cleanPath}";

                        var domain = enclosingClass?.Name ?? "backend";
                        var extNode = new ExternalServiceNode(
                            extId,
                            extName,
                            ProtocolHttp,
                            domain,
                            cleanPath
                        )
                        {
                            FilePath = relativePath,
                            StartLine = funLine,
                            StartCol = funCol,
                            CallerSymbolId = funcNode.Id,
                            Url = cleanPath,
                            Method = httpMethod,
                            IsExternal = true
                        };
                        extNode.CustomProperties ??= new();
                        extNode.CustomProperties["file_path"] = relativePath;
                        extNode.CustomProperties["target"] = cleanPath;
                        extNode.CustomProperties["method"] = httpMethod;
                        extNode.CustomProperties["http_method"] = httpMethod;
                        extNode.CustomProperties["domain_or_service"] = domain;
                        extNode.CustomProperties["is_external"] = "true";
                        fileNode.Children.Add(extNode);

                        fileNode.References.Add(new Reference(funcNode.Id, extId, OntologyConstants.Relationships.Calls, funLine, funCol));
                    }
                }
                else if (httpMethod != null && enclosingClass?.IsClient != true)
                {
                    var paths = ExtractRoutePathsFromAnnotation(ann.Args);
                    if (paths.Count == 0 || (paths.Count == 1 && string.IsNullOrEmpty(paths[0])))
                    {
                        var siblingPathAnn = annotations.FirstOrDefault(a => a.Name.Equals("Path", StringComparison.OrdinalIgnoreCase));
                        if (siblingPathAnn != null)
                        {
                            paths = ExtractRoutePathsFromAnnotation(siblingPathAnn.Args);
                        }
                    }

                    foreach (var rawPath in paths)
                    {
                        var fullRoute = CombineRoutes(enclosingClass?.RoutePrefix, rawPath);
                        var epId = $"{workspaceId}:{OntologyConstants.IdPrefixes.Endpoint}:{httpMethod}:{fullRoute}";
                        var epName = $"{httpMethod} {fullRoute}";

                        var epNode = new EndpointNode(
                            epId,
                            epName,
                            relativePath,
                            httpMethod,
                            fullRoute,
                            ProtocolRest
                        )
                        {
                            FilePath = relativePath,
                            StartLine = funLine,
                            EndLine = funLine,
                            Line = funLine,
                            LineEnd = funLine,
                            Operation = httpMethod,
                            EvidenceKind = OntologyConstants.EvidenceKinds.Endpoint
                        };
                        epNode.CustomProperties ??= new();
                        epNode.CustomProperties["path"] = fullRoute;
                        epNode.CustomProperties["route"] = fullRoute;
                        epNode.CustomProperties["route_template"] = fullRoute;
                        epNode.CustomProperties["method"] = httpMethod;
                        epNode.CustomProperties["http_method"] = httpMethod;
                        epNode.CustomProperties["file_path"] = relativePath;
                        fileNode.Children.Add(epNode);

                        fileNode.References.Add(new Reference(epId, funcNode.Id, OntologyConstants.Relationships.ExposedBy, funLine, funCol));
                    }
                }

                if (ann.Name.Equals("Query", StringComparison.OrdinalIgnoreCase))
                {
                    var sqlCandidate = ExtractQuerySql(ann.Args);
                    if (!string.IsNullOrWhiteSpace(sqlCandidate))
                    {
                        var cleanSql = NestedSqlParser.CleanQueryText(sqlCandidate);
                        var verb = "SELECT";
                        if (NestedSqlParser.TryParseSql(cleanSql, out var firstWord, out _) && !string.IsNullOrEmpty(firstWord))
                        {
                            verb = firstWord.ToUpperInvariant();
                        }

                        var qId = $"{workspaceId}:{OntologyConstants.IdPrefixes.Query}:{relativePath}:{funLine}:{funCol}";
                        var qName = $"SQL Query: {(cleanSql.Length > 40 ? cleanSql[..40] + "..." : cleanSql)}";

                        var queryNode = NestedSqlParser.ParseNestedSql(cleanSql, qId, relativePath, null, qName, funLine, funLine) ??
                                        new QueryNode(qId, qName, cleanSql, relativePath, funLine, funLine)
                                        {
                                            Verb = verb,
                                            Operation = verb,
                                            EvidenceKind = OntologyConstants.EvidenceKinds.Query,
                                            FilePath = relativePath,
                                            Line = funLine,
                                            LineEnd = funLine
                                        };
                        queryNode.CustomProperties ??= new();
                        queryNode.CustomProperties["file_path"] = relativePath;
                        fileNode.Children.Add(queryNode);

                        fileNode.References.Add(new Reference(queryNode.Id, funcNode.Id, OntologyConstants.Relationships.AccessedBy, funLine, funCol));

                        var relKind = verb is "INSERT" or "UPDATE" or "DELETE"
                            ? OntologyConstants.Relationships.WritesTo
                            : OntologyConstants.Relationships.ReadsFrom;

                        var tableNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        CollectTables(queryNode, tableNames);
                        if (tableNames.Count == 0)
                        {
                            ExtractTablesFromSql(cleanSql, tableNames);
                        }
                        foreach (var tbl in tableNames)
                        {
                            fileNode.References.Add(new Reference(queryNode.Id, tbl, relKind, funLine, funCol));
                        }
                    }
                }
            }

            var crudAnn = annotations.FirstOrDefault(a =>
                a.Name.Equals("Insert", StringComparison.OrdinalIgnoreCase) ||
                a.Name.Equals("Upsert", StringComparison.OrdinalIgnoreCase) ||
                a.Name.Equals("Update", StringComparison.OrdinalIgnoreCase) ||
                a.Name.Equals("Delete", StringComparison.OrdinalIgnoreCase));

            if (crudAnn != null && !annotations.Any(a => a.Name.Equals("Query", StringComparison.OrdinalIgnoreCase)))
            {
                var verb = crudAnn.Name.ToUpperInvariant();
                var sigSnippet = content.Substring(fm.Index, Math.Min(content.Length - fm.Index, fm.Length + 250));
                var entityMatch = Regex.Match(sigSnippet, @"(?:<|:\s*)([A-Z][a-zA-Z0-9_]*Entity|[A-Z][a-zA-Z0-9_]*CrossRef|[A-Z][a-zA-Z0-9_]+)>?");
                var targetEntity = entityMatch.Success ? entityMatch.Groups[1].Value : enclosingClass?.Name ?? "Entity";

                var qId = $"{workspaceId}:{OntologyConstants.IdPrefixes.Query}:{relativePath}:{funLine}:{funCol}";
                var qName = $"Room {verb}: {funName}";
                var sqlText = verb == "DELETE" ? $"DELETE FROM {targetEntity}" : $"{verb} INTO {targetEntity}";

                var queryNode = new QueryNode(qId, qName, sqlText, relativePath, funLine, funLine)
                {
                    Verb = verb,
                    Operation = verb,
                    EvidenceKind = OntologyConstants.EvidenceKinds.Orm,
                    FilePath = relativePath,
                    Line = funLine,
                    LineEnd = funLine
                };
                queryNode.CustomProperties ??= new();
                queryNode.CustomProperties["file_path"] = relativePath;
                fileNode.Children.Add(queryNode);

                fileNode.References.Add(new Reference(queryNode.Id, funcNode.Id, OntologyConstants.Relationships.AccessedBy, funLine, funCol));
                fileNode.References.Add(new Reference(queryNode.Id, targetEntity, OntologyConstants.Relationships.WritesTo, funLine, funCol));
            }
        }

        ParseKtorEndpoints(commentMasked, masked, lineOffsets, fileNode, workspaceId, relativePath, funSpans);
        ParseKtorClientCalls(content, commentMasked, masked, lineOffsets, fileNode, workspaceId, relativePath, funSpans, classScopes);
        ParseExposedOperations(commentMasked, masked, lineOffsets, fileNode, workspaceId, relativePath, funSpans);
    }

    private static int FindMatchingCloseBrace(string masked, int openBrace)
    {
        if (openBrace < 0 || openBrace >= masked.Length) return -1;
        var depth = 1;
        for (var i = openBrace + 1; i < masked.Length; i++)
        {
            if (masked[i] == '{') depth++;
            else if (masked[i] == '}')
            {
                depth--;
                if (depth == 0) return i;
            }
        }
        return masked.Length - 1;
    }

    private static FunctionNode? FindEnclosingFunction(int pos, List<(int Start, int End, FunctionNode Node)> funSpans)
    {
        FunctionNode? best = null;
        var bestSpan = int.MaxValue;
        foreach (var (start, end, node) in funSpans)
        {
            if (pos >= start && pos <= end)
            {
                var span = end - start;
                if (span < bestSpan)
                {
                    bestSpan = span;
                    best = node;
                }
            }
        }
        return best;
    }

    private static void ParseKtorEndpoints(
        string commentMasked,
        string masked,
        int[] lineOffsets,
        FileNode fileNode,
        string workspaceId,
        string relativePath,
        List<(int Start, int End, FunctionNode Node)> funSpans)
    {
        var rawScopes = new List<(int OpenBrace, int CloseBrace, string RawPath)>();

        var routeMatches = KtorRouteScopeRegex.Matches(commentMasked);
        foreach (Match rm in routeMatches)
        {
            var openBrace = masked.IndexOf('{', rm.Index);
            if (openBrace >= 0)
            {
                var closeBrace = FindMatchingCloseBrace(masked, openBrace);
                rawScopes.Add((openBrace, closeBrace, rm.Groups["path"].Value.Trim()));
            }
        }

        var routingMatches = KtorRoutingBlockRegex.Matches(commentMasked);
        foreach (Match rbm in routingMatches)
        {
            var openBrace = masked.IndexOf('{', rbm.Index);
            if (openBrace >= 0)
            {
                var closeBrace = FindMatchingCloseBrace(masked, openBrace);
                rawScopes.Add((openBrace, closeBrace, string.Empty));
            }
        }

        rawScopes.Sort((a, b) => (b.CloseBrace - b.OpenBrace).CompareTo(a.CloseBrace - a.OpenBrace));

        var scopes = new List<KtorRouteScope>();
        foreach (var rs in rawScopes)
        {
            KtorRouteScope? parent = null;
            var bestSpan = int.MaxValue;
            foreach (var existing in scopes)
            {
                if (rs.OpenBrace > existing.OpenBrace && rs.CloseBrace < existing.CloseBrace)
                {
                    var span = existing.CloseBrace - existing.OpenBrace;
                    if (span < bestSpan)
                    {
                        bestSpan = span;
                        parent = existing;
                    }
                }
            }
            var prefix = parent != null ? CombineRoutes(parent.Prefix, rs.RawPath) : CombineRoutes(string.Empty, rs.RawPath);
            scopes.Add(new KtorRouteScope(rs.OpenBrace, rs.CloseBrace, rs.RawPath, prefix));
        }

        KtorRouteScope? FindInnermostScope(int pos)
        {
            KtorRouteScope? best = null;
            var bestSpan = int.MaxValue;
            foreach (var s in scopes)
            {
                if (pos >= s.OpenBrace && pos <= s.CloseBrace)
                {
                    var span = s.CloseBrace - s.OpenBrace;
                    if (span < bestSpan)
                    {
                        bestSpan = span;
                        best = s;
                    }
                }
            }
            return best;
        }

        void AddEndpoint(string verb, string rawPath, int matchIndex, string protocol = ProtocolRest)
        {
            var scope = FindInnermostScope(matchIndex);
            var fullRoute = CombineRoutes(scope?.Prefix, rawPath);
            if (string.IsNullOrEmpty(fullRoute)) fullRoute = "/";
            if (!fullRoute.StartsWith('/')) fullRoute = "/" + fullRoute;
            if (fullRoute.Length > 1 && fullRoute.EndsWith('/')) fullRoute = fullRoute[..^1];

            var (line, col) = GetLineAndColumn(lineOffsets, matchIndex);
            var epId = $"{workspaceId}:{OntologyConstants.IdPrefixes.Endpoint}:{verb.ToUpperInvariant()}:{fullRoute}";
            var epName = $"{verb.ToUpperInvariant()} {fullRoute}";

            if (!fileNode.Children.Any(c => c.Id == epId))
            {
                var epNode = new EndpointNode(
                    epId,
                    epName,
                    relativePath,
                    verb.ToUpperInvariant(),
                    fullRoute,
                    protocol
                )
                {
                    FilePath = relativePath,
                    StartLine = line,
                    EndLine = line,
                    Line = line,
                    LineEnd = line,
                    Operation = verb.ToUpperInvariant(),
                    EvidenceKind = OntologyConstants.EvidenceKinds.Endpoint
                };
                epNode.CustomProperties ??= new();
                epNode.CustomProperties["path"] = fullRoute;
                epNode.CustomProperties["route"] = fullRoute;
                epNode.CustomProperties["route_template"] = fullRoute;
                epNode.CustomProperties["method"] = verb.ToUpperInvariant();
                epNode.CustomProperties["http_method"] = verb.ToUpperInvariant();
                epNode.CustomProperties["file_path"] = relativePath;
                fileNode.Children.Add(epNode);

                var enclosingFunc = FindEnclosingFunction(matchIndex, funSpans);
                if (enclosingFunc != null)
                {
                    fileNode.References.Add(new Reference(epId, enclosingFunc.Id, OntologyConstants.Relationships.ExposedBy, line, col));
                }
            }
        }

        foreach (Match m in KtorVerbWithPathRegex.Matches(commentMasked))
        {
            var verb = m.Groups["verb"].Value.ToUpperInvariant();
            var path = m.Groups["path"].Value.Trim();
            AddEndpoint(verb, path, m.Index);
        }

        foreach (Match m in KtorVerbNoPathRegex.Matches(commentMasked))
        {
            var scope = FindInnermostScope(m.Index);
            var enclosingFunc = FindEnclosingFunction(m.Index, funSpans);
            if (scope != null || enclosingFunc != null)
            {
                var verb = m.Groups["verb"].Value.ToUpperInvariant();
                AddEndpoint(verb, string.Empty, m.Index);
            }
        }

        foreach (Match m in KtorStaticResourcesRegex.Matches(commentMasked))
        {
            var path = m.Groups["path"].Value.Trim();
            AddEndpoint(MethodGet, path, m.Index);
        }

        foreach (Match m in KtorWebSocketRegex.Matches(commentMasked))
        {
            var path = m.Groups["path"].Success ? m.Groups["path"].Value.Trim() : string.Empty;
            AddEndpoint(MethodWs, path, m.Index, ProtocolWs);
        }
    }

    private static void ParseKtorClientCalls(
        string content,
        string commentMasked,
        string masked,
        int[] lineOffsets,
        FileNode fileNode,
        string workspaceId,
        string relativePath,
        List<(int Start, int End, FunctionNode Node)> funSpans,
        List<ClassScope> classScopes)
    {
        void AddExternalCall(string method, string target, int matchIndex)
        {
            var cleanTarget = target.Trim().Trim('"', '\'');
            if (string.IsNullOrWhiteSpace(cleanTarget) || cleanTarget == "/") return;

            if (cleanTarget.StartsWith("$origin/", StringComparison.OrdinalIgnoreCase))
            {
                cleanTarget = cleanTarget["$origin/".Length..];
            }
            else if (cleanTarget.StartsWith("$origin", StringComparison.OrdinalIgnoreCase))
            {
                cleanTarget = cleanTarget["$origin".Length..];
            }
            else if (cleanTarget.StartsWith("$baseUrl/", StringComparison.OrdinalIgnoreCase))
            {
                cleanTarget = cleanTarget["$baseUrl/".Length..];
            }

            var (line, col) = GetLineAndColumn(lineOffsets, matchIndex);
            var enclosingFunc = FindEnclosingFunction(matchIndex, funSpans);

            string domainOrService;
            string cleanPath;

            if (cleanTarget.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                cleanTarget.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                if (Uri.TryCreate(cleanTarget, UriKind.Absolute, out var uri))
                {
                    domainOrService = uri.Host;
                    cleanPath = uri.PathAndQuery;
                }
                else
                {
                    domainOrService = cleanTarget;
                    cleanPath = "/";
                }
            }
            else
            {
                cleanPath = cleanTarget.StartsWith('/') ? cleanTarget : "/" + cleanTarget;
                domainOrService = "backend";
            }

            var extId = $"{workspaceId}:{OntologyConstants.IdPrefixes.ExternalService}:{relativePath}:{enclosingFunc?.Name ?? "client"}:{line}:{col}";
            var extNode = new ExternalServiceNode(
                extId,
                domainOrService,
                ProtocolHttp,
                domainOrService,
                cleanPath
            )
            {
                FilePath = relativePath,
                StartLine = line,
                StartCol = col,
                CallerSymbolId = enclosingFunc?.Id,
                Url = cleanPath,
                Method = method.ToUpperInvariant()
            };
            extNode.CustomProperties ??= new();
            extNode.CustomProperties["file_path"] = relativePath;
            extNode.CustomProperties["target"] = cleanPath;
            extNode.CustomProperties["method"] = method.ToUpperInvariant();
            extNode.CustomProperties["http_method"] = method.ToUpperInvariant();
            fileNode.Children.Add(extNode);

            if (enclosingFunc != null)
            {
                fileNode.References.Add(new Reference(enclosingFunc.Id, extId, OntologyConstants.Relationships.Calls, line, col));
            }
        }

        foreach (Match m in KtorClientDirectCallRegex.Matches(commentMasked))
        {
            var method = m.Groups["method"].Value.ToUpperInvariant();
            var arg = m.Groups["arg"].Value.Trim();
            if (arg.StartsWith('{')) continue;
            AddExternalCall(method, arg, m.Index);
        }

        foreach (Match m in KtorClientLambdaCallRegex.Matches(commentMasked))
        {
            var method = m.Groups["method"].Value.ToUpperInvariant();
            var body = m.Groups["body"].Value;

            var targetMatch = Regex.Match(body, @"(?:apiUrl|url|path)\s*(?:\(\s*|\s*=\s*)['""]([^'""]+)['""]");
            if (targetMatch.Success)
            {
                AddExternalCall(method, targetMatch.Groups[1].Value.Trim(), m.Index);
            }
            else
            {
                var anyStrMatch = Regex.Match(body, @"['""]([^'""]+)['""]");
                if (anyStrMatch.Success)
                {
                    AddExternalCall(method, anyStrMatch.Groups[1].Value.Trim(), m.Index);
                }
            }
        }

        var isApiClientClass = classScopes.Any(cs =>
            cs.Name.EndsWith("Api", StringComparison.OrdinalIgnoreCase) ||
            cs.Name.EndsWith("Client", StringComparison.OrdinalIgnoreCase));
        if (isApiClientClass)
        {
            foreach (Match m in ApiClientHelperCallRegex.Matches(commentMasked))
            {
                var method = m.Groups["method"].Value.ToUpperInvariant();
                var path = m.Groups["path"].Value.Trim();
                AddExternalCall(method, path, m.Index);
            }
        }
    }

    private static void ParseExposedOperations(
        string commentMasked,
        string masked,
        int[] lineOffsets,
        FileNode fileNode,
        string workspaceId,
        string relativePath,
        List<(int Start, int End, FunctionNode Node)> funSpans)
    {
        foreach (Match m in ExposedOperationRegex.Matches(commentMasked))
        {
            var tableOrObj = m.Groups["tableOrObj"].Value.Trim();
            var op = m.Groups["op"].Value.Trim();

            if (tableOrObj is "String" or "Int" or "Boolean" or "List" or "Map" or "Set" or "Array" or "File" or "Path" or "Log" or "Logger")
            {
                continue;
            }

            var operation = op switch
            {
                "selectAll" or "select" => OpSelect,
                "insert" or "insertIgnore" or "insertAndGetId" or "batchInsert" => OpInsert,
                "update" => OpUpdate,
                "deleteWhere" or "deleteAll" => OpDelete,
                "upsert" => OpUpsert,
                _ => OpSelect
            };

            var edgeKind = operation == OpSelect
                ? OntologyConstants.Relationships.ReadsFrom
                : OntologyConstants.Relationships.WritesTo;

            var cleanSql = operation switch
            {
                OpSelect => $"SELECT * FROM {tableOrObj}",
                OpInsert => $"INSERT INTO {tableOrObj}",
                OpUpdate => $"UPDATE {tableOrObj}",
                OpDelete => $"DELETE FROM {tableOrObj}",
                _ => $"{operation} {tableOrObj}"
            };

            var (opLine, opCol) = GetLineAndColumn(lineOffsets, m.Index);
            var qId = $"{workspaceId}:{OntologyConstants.IdPrefixes.Query}:{relativePath}:{opLine}:{opCol}";
            var qName = $"Exposed: {operation} {tableOrObj}";

            var queryNode = new QueryNode(qId, qName, cleanSql, relativePath, opLine, opLine)
            {
                FilePath = relativePath,
                Line = opLine,
                LineEnd = opLine,
                Operation = operation,
                Verb = operation,
                EvidenceKind = OntologyConstants.EvidenceKinds.Query
            };
            queryNode.CustomProperties ??= new();
            queryNode.CustomProperties["file_path"] = relativePath;
            fileNode.Children.Add(queryNode);

            var tableId = $"{workspaceId}:{OntologyConstants.IdPrefixes.Table}:{tableOrObj.ToLowerInvariant()}";
            fileNode.References.Add(new Reference(qId, tableId, edgeKind, opLine, opCol));

            var enclosingFunc = FindEnclosingFunction(m.Index, funSpans);
            if (enclosingFunc != null)
            {
                fileNode.References.Add(new Reference(qId, enclosingFunc.Id, OntologyConstants.Relationships.AccessedBy, opLine, opCol));
            }
        }

        foreach (Match m in ExposedExecRegex.Matches(commentMasked))
        {
            var rawSql = m.Groups["sql"].Value.Trim();
            if (string.IsNullOrWhiteSpace(rawSql)) continue;

            var cleanSql = NestedSqlParser.CleanQueryText(rawSql);
            var operation = OpSelect;
            if (cleanSql.StartsWith("INSERT", StringComparison.OrdinalIgnoreCase)) operation = OpInsert;
            else if (cleanSql.StartsWith("UPDATE", StringComparison.OrdinalIgnoreCase)) operation = OpUpdate;
            else if (cleanSql.StartsWith("DELETE", StringComparison.OrdinalIgnoreCase)) operation = OpDelete;

            var edgeKind = operation == OpSelect
                ? OntologyConstants.Relationships.ReadsFrom
                : OntologyConstants.Relationships.WritesTo;

            var (opLine, opCol) = GetLineAndColumn(lineOffsets, m.Index);
            var qId = $"{workspaceId}:{OntologyConstants.IdPrefixes.Query}:{relativePath}:{opLine}:{opCol}";
            var qName = $"SQL Query: {(cleanSql.Length > 30 ? cleanSql[..30] + "..." : cleanSql)}";

            var queryNode = new QueryNode(qId, qName, cleanSql, relativePath, opLine, opLine)
            {
                FilePath = relativePath,
                Line = opLine,
                LineEnd = opLine,
                Operation = operation,
                Verb = operation,
                EvidenceKind = OntologyConstants.EvidenceKinds.Query
            };
            queryNode.CustomProperties ??= new();
            queryNode.CustomProperties["file_path"] = relativePath;
            fileNode.Children.Add(queryNode);

            var tblMatch = Regex.Match(cleanSql, @"\b(?:FROM|INTO|UPDATE)\s+[""']?([a-zA-Z0-9_]+)[""']?", RegexOptions.IgnoreCase);
            if (tblMatch.Success)
            {
                var tblName = tblMatch.Groups[1].Value.Trim();
                var tableId = $"{workspaceId}:{OntologyConstants.IdPrefixes.Table}:{tblName.ToLowerInvariant()}";
                fileNode.References.Add(new Reference(qId, tableId, edgeKind, opLine, opCol));
            }

            var enclosingFunc = FindEnclosingFunction(m.Index, funSpans);
            if (enclosingFunc != null)
            {
                fileNode.References.Add(new Reference(qId, enclosingFunc.Id, OntologyConstants.Relationships.AccessedBy, opLine, opCol));
            }
        }
    }

    private static string DetermineHttpMethodFromRequestMapping(string args)
    {
        if (string.IsNullOrWhiteSpace(args)) return "ALL";
        if (args.Contains("RequestMethod.POST", StringComparison.OrdinalIgnoreCase)) return "POST";
        if (args.Contains("RequestMethod.PUT", StringComparison.OrdinalIgnoreCase)) return "PUT";
        if (args.Contains("RequestMethod.DELETE", StringComparison.OrdinalIgnoreCase)) return "DELETE";
        if (args.Contains("RequestMethod.PATCH", StringComparison.OrdinalIgnoreCase)) return "PATCH";
        if (args.Contains("RequestMethod.HEAD", StringComparison.OrdinalIgnoreCase)) return "HEAD";
        if (args.Contains("RequestMethod.OPTIONS", StringComparison.OrdinalIgnoreCase)) return "OPTIONS";
        if (args.Contains("RequestMethod.GET", StringComparison.OrdinalIgnoreCase)) return "GET";
        return "ALL";
    }

    private static void CollectTables(IOntologyNode node, HashSet<string> tableNames)
    {
        if (node is TableNode tbl && !string.IsNullOrWhiteSpace(tbl.Name))
        {
            tableNames.Add(tbl.Name);
        }
        foreach (var child in node.Children)
        {
            CollectTables(child, tableNames);
        }
    }

    private static void ExtractTablesFromSql(string sql, HashSet<string> tableNames)
    {
        var matches = Regex.Matches(sql, @"\b(?:FROM|JOIN|INTO|UPDATE|TABLE)\s+([a-zA-Z0-9_]+)", RegexOptions.IgnoreCase);
        foreach (Match m in matches)
        {
            var tbl = m.Groups[1].Value.Trim();
            if (!NestedSqlParser.IsSqlKeyword(tbl))
            {
                tableNames.Add(tbl);
            }
        }
    }
}
