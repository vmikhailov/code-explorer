using CodeExplorer.Core.Protocol;
using CodeExplorer.Cypher.Common;

namespace CodeExplorer.Core.Analysis;

public partial class ArchitectureViewEngine
{
    private async Task<List<ArchitectureEvidenceItemDto>> CollectBatchTopicEvidenceAsync(
        IReadOnlyList<EvidenceNodeRef> topics,
        IReadOnlyList<EvidenceNodeRef> services,
        bool publish,
        Dictionary<string, EvidenceScope?> scopeCache,
        List<string> warnings,
        CancellationToken ct)
    {
        var items = new List<ArchitectureEvidenceItemDto>();
        if (topics.Count == 0 || services.Count == 0) return items;

        var scopes = new List<EvidenceScope>();
        foreach (var svc in services)
        {
            var scope = await ResolveServiceScopeAsync(svc.Id, ct, scopeCache, warnings);
            if (scope != null) scopes.Add(scope);
        }
        if (scopes.Count == 0) return items;

        var topicById = topics.ToDictionary(t => t.Id, StringComparer.OrdinalIgnoreCase);
        var topicLiterals = string.Join(", ", topics.Select(t => EscapeCypherStringLiteral(t.Id)).Distinct());
        var outgoing = publish ? PublishOutgoingKinds : SubscribeOutgoingKinds;
        var incoming = publish ? PublishIncomingKinds : SubscribeIncomingKinds;

        foreach (var (pattern, kinds) in new[] { ("(sym)-[r]->(t)", outgoing), ("(t)-[r]->(sym)", incoming) })
        {
            var query = $"""
                MATCH {pattern}
                WHERE t.id IN [{topicLiterals}] AND r.kind IN [{string.Join(", ", kinds.Select(k => $"'{k}'"))}]
                  AND {string.Format(NotAContainer, "sym")}
                RETURN t.id AS t_id, sym.id AS id, sym.name AS name, labels(sym)[0] AS kind,
                       coalesce(sym.file_path, sym.path) AS path,
                       coalesce(sym.start_line, sym.line) AS line,
                       coalesce(sym.end_line, sym.line_end) AS line_end,
                       r.properties AS properties,
                       json_extract(sym.properties, '$.service') AS service
                """;

            try
            {
                var res = await db.ExecuteCypherAsync(query, null, ct);
                foreach (var row in res.Rows)
                {
                    var tId = row.GetStringProp("t_id");
                    if (!topicById.TryGetValue(tId, out var topic)) continue;

                    var rProps = row.ExtractProperties();
                    var symId = row.GetStringProp("id");
                    var path = rProps.GetValueOrDefault(OntologyConstants.PropertyNames.FilePath) is { Length: > 0 } rfp
                        ? rfp
                        : row.GetStringProp("path");
                    var symService = row.GetStringProp("service");
                    if (string.IsNullOrEmpty(path)) continue;

                    var matchedScope = scopes.FirstOrDefault(s => s.Matches(path, symService, symId));
                    if (matchedScope is null) continue;

                    int? line = ParseLineProp(rProps, OntologyConstants.PropertyNames.Line, row, "line");
                    int? lineEnd = ParseLineProp(rProps, OntologyConstants.PropertyNames.LineEnd, row, "line_end") ?? line;

                    var symName = row.GetStringProp("name");
                    items.Add(new ArchitectureEvidenceItemDto
                    {
                        Id = symId,
                        Kind = publish ? EvidenceKind.Publish : EvidenceKind.Subscribe,
                        Operation = publish ? "PUBLISH" : "SUBSCRIBE",
                        Title = $"{(publish ? "Publishes" : "Subscribes")} {topic.Name}",
                        FilePath = path,
                        LineStart = line,
                        LineEnd = lineEnd,
                        Confidence = OntologyConstants.EvidenceConfidence.Recorded,
                        CallerSymbol = symName,
                        TargetSymbol = topic.Name,
                        ServiceName = matchedScope.Name,
                        Reason = $"{row.GetStringProp("kind")} '{symName}' {(publish ? "publishes to" : "consumes from")} topic '{topic.Name}'"
                    });
                }
            }
            catch (Exception ex)
            {
                RecordEvidenceWarning(warnings, "Batch topic evidence query", ex);
            }
        }

        return items;
    }

    private async Task<List<ArchitectureEvidenceItemDto>> CollectBatchExternalCallEvidenceAsync(
        IReadOnlyList<EvidenceNodeRef> services,
        IReadOnlyList<EvidenceNodeRef> externals,
        Dictionary<string, EvidenceScope?> scopeCache,
        List<string> warnings,
        CancellationToken ct)
    {
        var items = new List<ArchitectureEvidenceItemDto>();
        if (services.Count == 0 || externals.Count == 0) return items;

        var scopes = new List<EvidenceScope>();
        foreach (var svc in services)
        {
            var scope = await ResolveServiceScopeAsync(svc.Id, ct, scopeCache, warnings);
            if (scope != null) scopes.Add(scope);
        }
        if (scopes.Count == 0) return items;

        var extById = externals.ToDictionary(e => e.Id, StringComparer.OrdinalIgnoreCase);
        var extLiterals = string.Join(", ", externals.Select(e => EscapeCypherStringLiteral(e.Id)).Distinct());
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var outgoingKinds = string.Join(", ", ExternalCallKinds.Append("TRANSITIVELY_CALLS").Distinct().Select(k => $"'{k}'"));
        var incomingKinds = "'CALLED_BY', 'ACCESSED_BY', 'DECLARED_IN'";

        foreach (var (pattern, kinds) in new[] { ("(sym)-[r]->(ext)", outgoingKinds), ("(ext)-[r]->(sym)", incomingKinds) })
        {
            var query = $"""
                MATCH {pattern}
                WHERE ext.id IN [{extLiterals}] AND r.kind IN [{kinds}]
                  AND {string.Format(NotAContainer, "sym")}
                RETURN ext.id AS ext_id, sym.id AS id, sym.name AS name, labels(sym)[0] AS kind,
                       coalesce(sym.file_path, sym.path) AS path,
                       coalesce(sym.start_line, sym.line) AS line,
                       coalesce(sym.end_line, sym.line_end) AS line_end,
                       r.kind AS rel_kind, r.properties AS properties,
                       json_extract(sym.properties, '$.service') AS service
                """;

            try
            {
                var res = await db.ExecuteCypherAsync(query, null, ct);
                foreach (var row in res.Rows)
                {
                    var extId = row.GetStringProp("ext_id");
                    if (!extById.TryGetValue(extId, out var external)) continue;

                    var rProps = row.ExtractProperties();
                    var symId = row.GetStringProp("id");
                    var path = rProps.GetValueOrDefault(OntologyConstants.PropertyNames.FilePath) is { Length: > 0 } rfp
                        ? rfp
                        : row.GetStringProp("path");
                    var symService = row.GetStringProp("service");
                    if (string.IsNullOrEmpty(path)) continue;

                    var matchedScope = scopes.FirstOrDefault(s => s.Matches(path, symService, symId));
                    if (matchedScope is null) continue;

                    var symKind = row.GetStringProp("kind");
                    if (symKind is "Endpoint" or "EntryPoint" && row.GetStringProp("rel_kind") == "ATTRIBUTED_TO") continue;

                    int? line = ParseLineProp(rProps, OntologyConstants.PropertyNames.Line, row, "line");
                    int? lineEnd = ParseLineProp(rProps, OntologyConstants.PropertyNames.LineEnd, row, "line_end") ?? line;

                    var symName = row.GetStringProp("name");
                    if (!seen.Add($"{path}:{line}:{symName}:{extId}")) continue;

                    items.Add(new ArchitectureEvidenceItemDto
                    {
                        Id = symId,
                        Kind = EvidenceKind.HttpCall,
                        Operation = "CALL",
                        Title = $"Calls {external.Name}",
                        FilePath = path,
                        LineStart = line,
                        LineEnd = lineEnd,
                        Confidence = OntologyConstants.EvidenceConfidence.Recorded,
                        CallerSymbol = symName,
                        TargetSymbol = external.Name,
                        ServiceName = matchedScope.Name,
                        Reason = $"Invokes external service '{external.Name}'"
                    });
                }
            }
            catch (Exception ex)
            {
                RecordEvidenceWarning(warnings, "Batch external call evidence query", ex);
            }
        }

        return items;
    }

    private async Task<List<ArchitectureEvidenceItemDto>> CollectBatchEndpointCallEvidenceAsync(
        IReadOnlyList<EvidenceNodeRef> callers,
        IReadOnlyList<EvidenceNodeRef> callees,
        Dictionary<string, EvidenceScope?> scopeCache,
        List<string> warnings,
        CancellationToken ct)
    {
        var items = new List<ArchitectureEvidenceItemDto>();
        var callerScopes = new List<(EvidenceNodeRef Node, EvidenceScope Scope)>();
        var calleeScopes = new List<(EvidenceNodeRef Node, EvidenceScope Scope)>();

        foreach (var c in callers)
        {
            var s = await ResolveServiceScopeAsync(c.Id, ct, scopeCache, warnings);
            if (s != null) callerScopes.Add((c, s));
        }
        foreach (var c in callees)
        {
            var s = await ResolveServiceScopeAsync(c.Id, ct, scopeCache, warnings);
            if (s != null) calleeScopes.Add((c, s));
        }
        if (callerScopes.Count == 0 || calleeScopes.Count == 0) return items;

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 1. Direct Endpoint calls: (ep:Endpoint)-[:CALLED_BY]->(sym) or legacy (sym)-[:CALLS_ENDPOINT]->(ep:Endpoint)
        var epQuery = $"""
            MATCH (sym)-[r]-(ep:Endpoint)
            WHERE r.kind IN ['CALLS_ENDPOINT', 'CALLED_BY'] AND {string.Format(NotAContainer, "sym")}
            RETURN sym.id AS id, sym.name AS name, labels(sym)[0] AS kind,
                   coalesce(sym.file_path, sym.path) AS path,
                   coalesce(sym.start_line, sym.line) AS line,
                   coalesce(sym.end_line, sym.line_end) AS line_end,
                   r.properties AS properties,
                   ep.id AS ep_id, ep.name AS ep_name, coalesce(ep.file_path, ep.path) AS ep_path,
                   ep.http_method AS http_method, ep.route_template AS route, ep.protocol AS protocol,
                   json_extract(sym.properties, '$.service') AS caller_service,
                   json_extract(ep.properties, '$.service') AS ep_service
            """;

        try
        {
            var res = await db.ExecuteCypherAsync(epQuery, null, ct);
            foreach (var row in res.Rows)
            {
                var rProps = row.ExtractProperties();
                var symId = row.GetStringProp("id");
                var epId = row.GetStringProp("ep_id");
                var path = rProps.GetValueOrDefault(OntologyConstants.PropertyNames.FilePath) is { Length: > 0 } rfp
                    ? rfp
                    : row.GetStringProp("path");
                var epPath = row.GetStringProp("ep_path");
                var callerSvc = row.GetStringProp("caller_service");
                var epSvc = row.GetStringProp("ep_service");
                if (string.IsNullOrEmpty(path)) continue;

                var matchedCaller = callerScopes.FirstOrDefault(cs => cs.Scope.Matches(path, callerSvc, symId));
                if (matchedCaller.Scope is null) continue;
                var matchedCallee = calleeScopes.FirstOrDefault(cs =>
                    !cs.Node.Id.Equals(matchedCaller.Node.Id, StringComparison.OrdinalIgnoreCase) &&
                    cs.Scope.Matches(epPath, epSvc, epId));
                if (matchedCallee.Scope is null) continue;

                var protocol = row.GetStringProp("protocol");
                var method = row.GetStringProp("http_method");
                var route = row.GetStringProp("route");
                var epName = row.GetStringProp("ep_name");
                int? line = ParseLineProp(rProps, OntologyConstants.PropertyNames.Line, row, "line");
                int? lineEnd = ParseLineProp(rProps, OntologyConstants.PropertyNames.LineEnd, row, "line_end") ?? line;

                var callerName = row.GetStringProp("name");

                var kind = protocol.Equals("grpc", StringComparison.OrdinalIgnoreCase) ? EvidenceKind.GrpcCall
                         : protocol.Equals("wcf", StringComparison.OrdinalIgnoreCase) ? EvidenceKind.WcfCall
                         : EvidenceKind.HttpCall;
                var operation = string.IsNullOrEmpty(method) || method.Equals("ALL", StringComparison.OrdinalIgnoreCase)
                    ? (kind == EvidenceKind.HttpCall ? "CALL" : kind.ToCanonicalString()[..4].ToUpperInvariant())
                    : method.ToUpperInvariant();

                if (!seen.Add($"{path}:{line}:{epName}")) continue;

                items.Add(new ArchitectureEvidenceItemDto
                {
                    Id = symId,
                    Kind = kind,
                    Operation = operation,
                    Title = string.IsNullOrEmpty(route) ? epName : $"{operation} {route}",
                    FilePath = path,
                    LineStart = line,
                    LineEnd = lineEnd,
                    Confidence = OntologyConstants.EvidenceConfidence.Recorded,
                    CallerSymbol = callerName,
                    TargetSymbol = epName,
                    ServiceName = matchedCaller.Scope.Name,
                    Reason = $"Calls endpoint '{epName}' exposed by '{matchedCallee.Scope.Name}'"
                });
            }
        }
        catch (Exception ex)
        {
            RecordEvidenceWarning(warnings, "Endpoint call evidence query", ex);
        }

        // 2. Cross-project symbol calls / inheritance / type usages: (sym1)-[:CALLS|INHERITS_FROM|IMPLEMENTS|USES_API]->(sym2)
        if (items.Count == 0 && callerScopes.Any(cs => cs.Scope.Directory is not null) && calleeScopes.Any(cs => cs.Scope.Directory is not null))
        {
            var symQuery = """
                MATCH (sym1)-[r]->(sym2)
                WHERE r.kind IN ['CALLS', 'INHERITS_FROM', 'IMPLEMENTS', 'USES_API']
                  AND (sym1:Function OR sym1:Type OR sym1:Member OR sym1:File)
                  AND (sym2:Function OR sym2:Type OR sym2:Member OR sym2:Endpoint)
                RETURN sym1.id AS id, sym1.name AS caller_name, labels(sym1)[0] AS caller_kind,
                       coalesce(sym1.file_path, sym1.path) AS caller_path,
                       coalesce(sym1.start_line, sym1.line) AS line, coalesce(sym1.end_line, sym1.line_end) AS line_end,
                       sym2.id AS target_id, sym2.name AS target_name, labels(sym2)[0] AS target_kind,
                       coalesce(sym2.file_path, sym2.path) AS target_path,
                       r.kind AS rel_kind
                """;
            try
            {
                var res = await db.ExecuteCypherAsync(symQuery, null, ct);
                foreach (var row in res.Rows)
                {
                    var sym1Id = row.GetStringProp("id");
                    var sym2Id = row.GetStringProp("target_id");
                    var cPath = row.GetStringProp("caller_path");
                    var tPath = row.GetStringProp("target_path");
                    if (string.IsNullOrEmpty(cPath) || string.IsNullOrEmpty(tPath)) continue;

                    var matchedCaller = callerScopes.FirstOrDefault(cs => cs.Scope.Matches(cPath, null, sym1Id));
                    if (matchedCaller.Scope is null) continue;
                    var matchedCallee = calleeScopes.FirstOrDefault(cs =>
                        !cs.Node.Id.Equals(matchedCaller.Node.Id, StringComparison.OrdinalIgnoreCase) &&
                        cs.Scope.Matches(tPath, null, sym2Id));
                    if (matchedCallee.Scope is null) continue;

                    int? line = row.GetNullableIntProp("line");
                    int? lineEnd = row.GetNullableIntProp("line_end");
                    if (line <= 0) line = null;
                    if (lineEnd <= 0) lineEnd = line;

                    var callerName = row.GetStringProp("caller_name");
                    var targetName = row.GetStringProp("target_name");
                    var relKind = row.GetStringProp("rel_kind", "CALLS");
                    if (!seen.Add($"{cPath}:{line}:{targetName}")) continue;

                    items.Add(new ArchitectureEvidenceItemDto
                    {
                        Id = sym1Id,
                        Kind = EvidenceKind.HttpCall,
                        Operation = relKind == "CALLS" ? "CALL" : relKind,
                        Title = $"{callerName} → {targetName}",
                        FilePath = cPath,
                        LineStart = line,
                        LineEnd = lineEnd,
                        Confidence = OntologyConstants.EvidenceConfidence.Inferred,
                        CallerSymbol = callerName,
                        TargetSymbol = targetName,
                        ServiceName = matchedCaller.Scope.Name,
                        Reason = $"Cross-service reference from '{matchedCaller.Scope.Name}' to '{targetName}' in '{matchedCallee.Scope.Name}'"
                    });
                    if (items.Count >= 100) break;
                }
            }
            catch (Exception ex)
            {
                RecordEvidenceWarning(warnings, "Cross-project symbol call evidence query", ex);
            }
        }

        // 3. Package / Library / Manifest dependency fallback: (p1:Project)-[:DEPENDS_ON]->(pkg:Package)-[:IMPLEMENTED_BY]->(p2:Project)
        if (items.Count == 0)
        {
            var pkgQuery = """
                MATCH (p1:Project)-[r:DEPENDS_ON]->(pkg:Package)-[:IMPLEMENTED_BY]->(p2:Project)
                RETURN p1.id AS p1_id, p1.name AS p1_name, coalesce(p1.file_path, p1.path) AS p1_path,
                       pkg.name AS pkg_name,
                       p2.id AS p2_id, p2.name AS p2_name, coalesce(p2.file_path, p2.path) AS p2_path
                """;
            try
            {
                var res = await db.ExecuteCypherAsync(pkgQuery, null, ct);
                foreach (var row in res.Rows)
                {
                    var p1Id = row.GetStringProp("p1_id");
                    var p2Id = row.GetStringProp("p2_id");
                    var p1Path = row.GetStringProp("p1_path");
                    var p2Path = row.GetStringProp("p2_path");
                    var p1Name = row.GetStringProp("p1_name");
                    var p2Name = row.GetStringProp("p2_name");

                    var matchedCaller = callerScopes.FirstOrDefault(cs => cs.Scope.Matches(p1Path, p1Name, p1Id));
                    if (matchedCaller.Scope is null) continue;
                    var matchedCallee = calleeScopes.FirstOrDefault(cs =>
                        !cs.Node.Id.Equals(matchedCaller.Node.Id, StringComparison.OrdinalIgnoreCase) &&
                        cs.Scope.Matches(p2Path, p2Name, p2Id));
                    if (matchedCallee.Scope is null) continue;

                    var pkgName = row.GetStringProp("pkg_name");
                    var manifestPath = string.IsNullOrEmpty(p1Path) ? $"{matchedCaller.Scope.Directory}/package.json" : p1Path;
                    if (!manifestPath.Contains('.')) manifestPath = $"{manifestPath.TrimEnd('/')}/package.json";

                    if (seen.Add($"pkg:{manifestPath}:{pkgName}"))
                    {
                        items.Add(new ArchitectureEvidenceItemDto
                        {
                            Id = p1Id,
                            Kind = EvidenceKind.LibraryCall,
                            Operation = "DEPENDS_ON",
                            Title = $"Depends on package {pkgName}",
                            FilePath = manifestPath,
                            LineStart = null,
                            LineEnd = null,
                            Confidence = OntologyConstants.EvidenceConfidence.Inferred,
                            CallerSymbol = matchedCaller.Scope.Name,
                            TargetSymbol = pkgName,
                            ServiceName = matchedCaller.Scope.Name,
                            Reason = $"Project '{matchedCaller.Scope.Name}' declares dependency on workspace library '{pkgName}' ('{matchedCallee.Scope.Name}')"
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                RecordEvidenceWarning(warnings, "Package dependency evidence query", ex);
            }
        }

        // 4. Direct project/service macro-relationship fallback if still empty and an actual graph edge exists between caller and callee
        if (items.Count == 0 && callerScopes.Any(cs => !string.IsNullOrEmpty(cs.Scope.Directory)))
        {
            var callerTerms = callerScopes.SelectMany(cs => new[] { cs.Node.Id, cs.Node.Name }).Distinct().Select(EscapeCypherStringLiteral);
            var calleeTerms = calleeScopes.SelectMany(cs => new[] { cs.Node.Id, cs.Node.Name }).Distinct().Select(EscapeCypherStringLiteral);
            var directEdgeQuery = $"""
                MATCH (s1)-[r]->(s2)
                WHERE (s1.id IN [{string.Join(", ", callerTerms)}] OR s1.name IN [{string.Join(", ", callerTerms)}])
                  AND (s2.id IN [{string.Join(", ", calleeTerms)}] OR s2.name IN [{string.Join(", ", calleeTerms)}])
                  AND r.kind IN ['SERVICE_CALL', 'DEPENDS_ON', 'TESTS', 'USES_API']
                RETURN s1.id AS s1_id, s2.id AS s2_id, r.kind AS rel_kind
                LIMIT 10
                """;
            try
            {
                var res = await db.ExecuteCypherAsync(directEdgeQuery, null, ct);
                foreach (var row in res.Rows)
                {
                    var s1Id = row.GetStringProp("s1_id");
                    var s2Id = row.GetStringProp("s2_id");
                    var matchedCaller = callerScopes.FirstOrDefault(cs => cs.Node.Id.Equals(s1Id, StringComparison.OrdinalIgnoreCase));
                    var matchedCallee = calleeScopes.FirstOrDefault(cs => cs.Node.Id.Equals(s2Id, StringComparison.OrdinalIgnoreCase));
                    if (matchedCaller.Scope is null || matchedCallee.Scope is null || string.IsNullOrEmpty(matchedCaller.Scope.Directory))
                        continue;

                    var relKind = row.GetStringProp("rel_kind", "CONFIG");
                    var manifestPath = matchedCaller.Node.Path ?? matchedCaller.Scope.Directory;
                    if (!manifestPath.Contains('.')) manifestPath = $"{manifestPath.TrimEnd('/')}/package.json";
                    if (seen.Add($"macro:{matchedCaller.Node.Id}:{matchedCallee.Node.Id}:{relKind}"))
                    {
                        items.Add(new ArchitectureEvidenceItemDto
                        {
                            Id = matchedCaller.Node.Id,
                            Kind = EvidenceKind.Config,
                            Operation = relKind,
                            Title = $"{matchedCaller.Scope.Name} → {matchedCallee.Scope.Name}",
                            FilePath = manifestPath,
                            LineStart = null,
                            LineEnd = null,
                            Confidence = OntologyConstants.EvidenceConfidence.Inferred,
                            CallerSymbol = matchedCaller.Scope.Name,
                            TargetSymbol = matchedCallee.Scope.Name,
                            ServiceName = matchedCaller.Scope.Name,
                            Reason = $"Project/service dependency ({relKind}) from '{matchedCaller.Scope.Name}' to '{matchedCallee.Scope.Name}'"
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                RecordEvidenceWarning(warnings, "Direct macro edge fallback query", ex);
            }
        }

        return items;
    }

    private async Task<List<ArchitectureEvidenceItemDto>> CollectBatchCodeLevelEvidenceAsync(
        IReadOnlyList<EvidenceNodeRef> sources,
        IReadOnlyList<EvidenceNodeRef> targets,
        List<string> warnings,
        CancellationToken ct)
    {
        var items = new List<ArchitectureEvidenceItemDto>();
        if (sources.Count == 0 || targets.Count == 0) return items;

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 1. Direct EntryPoint / Endpoint to Target file/module match
        foreach (var s in sources.Where(n => n.Label == OntologyConstants.NodeLabels.EntryPoint || n.Label == "Endpoint"))
        {
            foreach (var t in targets)
            {
                var sFile = s.Path;
                var tFile = t.Path;
                var sName = s.Name;
                var tName = t.Name;

                var isPathMatch = !string.IsNullOrEmpty(sFile) && !string.IsNullOrEmpty(tFile) &&
                    (sFile.Equals(tFile, StringComparison.OrdinalIgnoreCase) ||
                     sFile.EndsWith("/" + tFile, StringComparison.OrdinalIgnoreCase) ||
                     tFile.EndsWith("/" + sFile, StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(Path.GetFileName(sFile), Path.GetFileName(tFile), StringComparison.OrdinalIgnoreCase));

                var isNameMatch = !string.IsNullOrEmpty(sName) && !string.IsNullOrEmpty(tName) &&
                    (sName.Equals(tName, StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(Path.GetFileNameWithoutExtension(sName), Path.GetFileNameWithoutExtension(tName), StringComparison.OrdinalIgnoreCase));

                if (isPathMatch || isNameMatch)
                {
                    if (seen.Add($"entry:{s.Id}:{t.Id}"))
                    {
                        var filePath = !string.IsNullOrEmpty(sFile) ? sFile : tFile;
                        items.Add(new ArchitectureEvidenceItemDto
                        {
                            Id = s.Id,
                            Kind = EvidenceKind.EntryPoint,
                            Operation = "ENTRY",
                            Title = $"{s.Name} → {t.Name}",
                            FilePath = filePath ?? string.Empty,
                            LineStart = s.Line,
                            LineEnd = s.Line,
                            Confidence = OntologyConstants.EvidenceConfidence.Recorded,
                            CallerSymbol = s.Name,
                            TargetSymbol = t.Name,
                            Reason = $"Entry point '{s.Name}' exposes module '{t.Name}'"
                        });
                    }
                }
            }
        }

        // 2. Query code-level relationships between source files/symbols and target files/symbols
        var sourcePaths = sources.Select(s => s.Path).OfType<string>().Where(p => !string.IsNullOrEmpty(p)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var targetPaths = targets.Select(t => t.Path).OfType<string>().Where(p => !string.IsNullOrEmpty(p)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var sourceNames = sources.Select(s => s.Name).Where(n => !string.IsNullOrEmpty(n)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var targetNames = targets.Select(t => t.Name).Where(n => !string.IsNullOrEmpty(n)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var sourceIds = sources.Select(s => s.Id).Where(i => !string.IsNullOrEmpty(i)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var targetIds = targets.Select(t => t.Id).Where(i => !string.IsNullOrEmpty(i)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        var srcConditions = new List<string>();
        if (sourceIds.Count > 0)
        {
            var srcIdLit = string.Join(", ", sourceIds.Select(EscapeCypherStringLiteral));
            srcConditions.Add($"a.id IN [{srcIdLit}]");
        }
        if (sourceNames.Count > 0)
        {
            var srcNameLit = string.Join(", ", sourceNames.Select(EscapeCypherStringLiteral));
            srcConditions.Add($"a.name IN [{srcNameLit}]");
            srcConditions.Add($"a.enclosing_type IN [{srcNameLit}]");
        }
        if (sourcePaths.Count > 0)
        {
            var srcFileLit = string.Join(", ", sourcePaths.Select(EscapeCypherStringLiteral));
            srcConditions.Add($"coalesce(a.file_path, a.path) IN [{srcFileLit}]");
            foreach (var p in sourcePaths)
            {
                var cleanP = p.TrimStart('/');
                srcConditions.Add($"coalesce(a.file_path, a.path) = {EscapeCypherStringLiteral(cleanP)}");
                srcConditions.Add($"coalesce(a.file_path, a.path) ENDS WITH {EscapeCypherStringLiteral("/" + cleanP)}");
                var fn = Path.GetFileName(p);
                if (!string.IsNullOrEmpty(fn) && !fn.Equals(cleanP, StringComparison.OrdinalIgnoreCase))
                {
                    srcConditions.Add($"coalesce(a.file_path, a.path) ENDS WITH {EscapeCypherStringLiteral("/" + fn)}");
                }
            }
        }

        var tgtConditions = new List<string>();
        if (targetIds.Count > 0)
        {
            var tgtIdLit = string.Join(", ", targetIds.Select(EscapeCypherStringLiteral));
            tgtConditions.Add($"b.id IN [{tgtIdLit}]");
        }
        if (targetNames.Count > 0)
        {
            var tgtNameLit = string.Join(", ", targetNames.Select(EscapeCypherStringLiteral));
            tgtConditions.Add($"b.name IN [{tgtNameLit}]");
            tgtConditions.Add($"b.enclosing_type IN [{tgtNameLit}]");
        }
        if (targetPaths.Count > 0)
        {
            var tgtFileLit = string.Join(", ", targetPaths.Select(EscapeCypherStringLiteral));
            tgtConditions.Add($"coalesce(b.file_path, b.path) IN [{tgtFileLit}]");
            foreach (var p in targetPaths)
            {
                var cleanP = p.TrimStart('/');
                tgtConditions.Add($"coalesce(b.file_path, b.path) = {EscapeCypherStringLiteral(cleanP)}");
                tgtConditions.Add($"coalesce(b.file_path, b.path) ENDS WITH {EscapeCypherStringLiteral("/" + cleanP)}");
                var fn = Path.GetFileName(p);
                if (!string.IsNullOrEmpty(fn) && !fn.Equals(cleanP, StringComparison.OrdinalIgnoreCase))
                {
                    tgtConditions.Add($"coalesce(b.file_path, b.path) ENDS WITH {EscapeCypherStringLiteral("/" + fn)}");
                }
            }
        }

        if (srcConditions.Count == 0 || tgtConditions.Count == 0) return items;

        var srcClause = string.Join(" OR ", srcConditions);
        var tgtClause = string.Join(" OR ", tgtConditions);

        var query = $"""
            MATCH (a)-[r]->(b)
            WHERE r.kind IN ['CALLS', 'USES_TYPE', 'IMPLEMENTS', 'INHERITS_FROM', 'EXPOSED_BY', 'TRIGGERS', 'READS', 'WRITES', 'INSTANTIATES', 'ACCESSES', 'ACCESSED_BY']
              AND ({srcClause})
              AND ({tgtClause})
            RETURN r.kind AS kind,
                   a.id AS from_id,
                   a.name AS from_name,
                   coalesce(a.file_path, a.path) AS from_file,
                   coalesce(a.start_line, a.line) AS from_line,
                   coalesce(a.end_line, a.line_end) AS from_end_line,
                   b.id AS to_id,
                   b.name AS to_name,
                   coalesce(b.file_path, b.path) AS to_file,
                   coalesce(b.start_line, b.line) AS to_line,
                   coalesce(r.call_snippet, r.snippet) AS snippet
            LIMIT 100
            """;

        try
        {
            var res = await db.ExecuteCypherAsync(query, null, ct);
            foreach (var row in res.Rows)
            {
                var kind = row.GetStringProp("kind", "CALLS");
                var fromName = row.GetStringProp("from_name");
                var toName = row.GetStringProp("to_name");
                var file = row.GetStringProp("from_file");
                if (string.IsNullOrEmpty(file))
                {
                    file = row.GetStringProp("to_file");
                }
                var line = row.GetNullableIntProp("from_line");
                var endLine = row.GetNullableIntProp("from_end_line");
                var snippet = row.GetStringProp("snippet");

                var itemKey = $"{kind}:{file}:{line}:{fromName}:{toName}";
                if (!seen.Add(itemKey)) continue;

                var op = kind == "CALLS" ? "CALL"
                       : kind == "USES_TYPE" ? "USE"
                       : kind;

                var evKind = kind is "USES_TYPE" or "IMPLEMENTS" or "INHERITS_FROM"
                    ? EvidenceKind.TypeReference
                    : EvidenceKind.LibraryCall;

                items.Add(new ArchitectureEvidenceItemDto
                {
                    Id = $"{row.GetStringProp("from_id")}->{row.GetStringProp("to_id")}:{kind}",
                    Kind = evKind,
                    Operation = op,
                    Title = !string.IsNullOrEmpty(fromName) && !string.IsNullOrEmpty(toName)
                        ? $"{fromName}() → {toName}()"
                        : $"{fromName ?? "call"} → {toName ?? "target"}",
                    FilePath = file,
                    LineStart = line,
                    LineEnd = endLine,
                    Confidence = OntologyConstants.EvidenceConfidence.Recorded,
                    CallerSymbol = fromName,
                    TargetSymbol = toName,
                    CodeSnippet = snippet,
                    Reason = $"Code reference ({kind}) from '{fromName}' to '{toName}'"
                });
            }
        }
        catch (Exception ex)
        {
            RecordEvidenceWarning(warnings, "Batch code-level evidence query", ex);
        }

        return items;
    }

}
