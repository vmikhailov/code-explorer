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

            if (enclosingClass?.RepositoryEntity != null)
            {
                if (funName.StartsWith("find", StringComparison.OrdinalIgnoreCase) ||
                    funName.StartsWith("get", StringComparison.OrdinalIgnoreCase) ||
                    funName.StartsWith("exists", StringComparison.OrdinalIgnoreCase) ||
                    funName.StartsWith("count", StringComparison.OrdinalIgnoreCase) ||
                    funName.StartsWith("read", StringComparison.OrdinalIgnoreCase) ||
                    funName.StartsWith("query", StringComparison.OrdinalIgnoreCase))
                {
                    fileNode.References.Add(new Reference(funcNode.Id, enclosingClass.RepositoryEntity, OntologyConstants.Relationships.ReadsFrom, funLine, funCol));
                    fileNode.References.Add(new Reference(enclosingClass.Node.Id, enclosingClass.RepositoryEntity, OntologyConstants.Relationships.ReadsFrom, funLine, funCol));
                }
                else if (funName.StartsWith("delete", StringComparison.OrdinalIgnoreCase) ||
                         funName.StartsWith("remove", StringComparison.OrdinalIgnoreCase))
                {
                    fileNode.References.Add(new Reference(funcNode.Id, enclosingClass.RepositoryEntity, OntologyConstants.Relationships.WritesTo, funLine, funCol));
                    fileNode.References.Add(new Reference(enclosingClass.Node.Id, enclosingClass.RepositoryEntity, OntologyConstants.Relationships.WritesTo, funLine, funCol));
                }
                else if (funName.StartsWith("save", StringComparison.OrdinalIgnoreCase) ||
                         funName.StartsWith("insert", StringComparison.OrdinalIgnoreCase) ||
                         funName.StartsWith("update", StringComparison.OrdinalIgnoreCase))
                {
                    fileNode.References.Add(new Reference(funcNode.Id, enclosingClass.RepositoryEntity, OntologyConstants.Relationships.WritesTo, funLine, funCol));
                    fileNode.References.Add(new Reference(enclosingClass.Node.Id, enclosingClass.RepositoryEntity, OntologyConstants.Relationships.WritesTo, funLine, funCol));
                }
            }

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

                        string? reqPayload = null;
                        var sigSnippet = content.Substring(fm.Index, Math.Min(content.Length - fm.Index, fm.Length + 400));
                        var rbMatch = Regex.Match(sigSnippet, @"@RequestBody\s+(?:(?:val|var)\s+)?(?:[a-zA-Z0-9_]+:\s*)?([A-Z][a-zA-Z0-9_<>, ]*)");
                        if (rbMatch.Success)
                        {
                            reqPayload = rbMatch.Groups[1].Value.Trim();
                            if (reqPayload.Contains(',')) reqPayload = reqPayload.Split(',')[0].Trim();
                            if (reqPayload.Contains(')')) reqPayload = reqPayload.Split(')')[0].Trim();
                        }

                        string? retCandidate = null;
                        var funParenOpen = sigSnippet.IndexOf('(');
                        var parenClose = funParenOpen >= 0 ? FindMatchingParen(sigSnippet, funParenOpen) : -1;
                        if (parenClose >= 0)
                        {
                            var colonIdx = sigSnippet.IndexOf(':', parenClose);
                            if (colonIdx >= 0)
                            {
                                var stopIdx = sigSnippet.IndexOfAny(['{', '=', '\n'], colonIdx);
                                var candidate = stopIdx > colonIdx
                                    ? sigSnippet[(colonIdx + 1)..stopIdx].Trim()
                                    : sigSnippet[(colonIdx + 1)..].Trim();
                                if (!string.IsNullOrWhiteSpace(candidate) && char.IsLetter(candidate[0]))
                                {
                                    retCandidate = candidate;
                                }
                            }
                        }

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
                            EvidenceKind = OntologyConstants.EvidenceKinds.Endpoint,
                            RequestType = reqPayload,
                            ResponseType = retCandidate
                        };
                        epNode.CustomProperties ??= new();
                        epNode.CustomProperties["path"] = fullRoute;
                        epNode.CustomProperties["route"] = fullRoute;
                        epNode.CustomProperties["route_template"] = fullRoute;
                        epNode.CustomProperties["method"] = httpMethod;
                        epNode.CustomProperties["http_method"] = httpMethod;
                        epNode.CustomProperties["file_path"] = relativePath;
                        if (!string.IsNullOrEmpty(reqPayload))
                        {
                            epNode.CustomProperties["request_type"] = reqPayload;
                        }
                        if (!string.IsNullOrEmpty(retCandidate))
                        {
                            epNode.CustomProperties["response_type"] = retCandidate;
                        }

                        fileNode.Children.Add(epNode);

                        fileNode.References.Add(new Reference(epId, funcNode.Id, OntologyConstants.Relationships.ExposedBy, funLine, funCol));
                    }
                }
            }

            var sqlAnn = annotations.FirstOrDefault(a =>
                a.Name.Equals("Select", StringComparison.OrdinalIgnoreCase) ||
                a.Name.Equals("Insert", StringComparison.OrdinalIgnoreCase) ||
                a.Name.Equals("Update", StringComparison.OrdinalIgnoreCase) ||
                a.Name.Equals("Delete", StringComparison.OrdinalIgnoreCase) ||
                a.Name.Equals("Upsert", StringComparison.OrdinalIgnoreCase) ||
                a.Name.Equals("SelectProvider", StringComparison.OrdinalIgnoreCase) ||
                a.Name.Equals("InsertProvider", StringComparison.OrdinalIgnoreCase) ||
                a.Name.Equals("UpdateProvider", StringComparison.OrdinalIgnoreCase) ||
                a.Name.Equals("DeleteProvider", StringComparison.OrdinalIgnoreCase) ||
                a.Name.Equals("Query", StringComparison.OrdinalIgnoreCase));

            if (sqlAnn != null)
            {
                var isProvider = sqlAnn.Name.EndsWith("Provider", StringComparison.OrdinalIgnoreCase);
                if (isProvider)
                {
                    KotlinDatabaseSqlExtractor.ParseMyBatisProvider(sqlAnn, funcNode, enclosingClass, fileNode, workspaceId, relativePath, funLine, funCol);
                }
                else
                {
                    var sqlCandidate = ExtractMyBatisOrQuerySql(sqlAnn.Args);
                    if (!string.IsNullOrWhiteSpace(sqlCandidate))
                    {
                        KotlinDatabaseSqlExtractor.ParseMyBatisOrQuerySql(sqlAnn, sqlCandidate, funcNode, enclosingClass, fileNode, workspaceId, relativePath, funLine, funCol);
                    }
                    else if (sqlAnn.Name is "Insert" or "Upsert" or "Update" or "Delete")
                    {
                        KotlinDatabaseSqlExtractor.ParseRoomDaoCrud(sqlAnn, fm, content, funcNode, enclosingClass, fileNode, workspaceId, relativePath, funLine, funCol);
                    }
                }
            }
        }

        ParseKtorEndpoints(commentMasked, masked, lineOffsets, fileNode, workspaceId, relativePath, funSpans);
        ParseKtorClientCalls(content, commentMasked, masked, lineOffsets, fileNode, workspaceId, relativePath, funSpans, classScopes);
        KotlinDatabaseSqlExtractor.ParseExposedOperations(commentMasked, masked, lineOffsets, fileNode, workspaceId, relativePath, funSpans);
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

    internal static FunctionNode? FindEnclosingFunction(int pos, List<(int Start, int End, FunctionNode Node)> funSpans)
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
}
