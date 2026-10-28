using System.Text.Json;
using CodeExplorer.Core.Protocol;

namespace CodeExplorer.Core.Analysis;

public partial class ArchitectureViewEngine
{
    private sealed record EvidenceNodeRef(string Id, string Name, string Label, string? Path, int? Line = null);

    private static readonly string[] PublishOutgoingKinds = [OntologyConstants.Relationships.PublishesTo];
    private static readonly string[] PublishIncomingKinds = [OntologyConstants.Relationships.PublishedBy];
    private static readonly string[] SubscribeOutgoingKinds = [OntologyConstants.Relationships.SubscribesTo, OntologyConstants.Relationships.Handles];
    private static readonly string[] SubscribeIncomingKinds = [OntologyConstants.Relationships.Triggers, OntologyConstants.Relationships.SubscribedBy];
    private static readonly string[] ExternalCallKinds =
    [
        OntologyConstants.Relationships.ServiceCall, OntologyConstants.Relationships.CallsEndpoint, OntologyConstants.Relationships.Calls,
        OntologyConstants.Relationships.UsesApi, OntologyConstants.Relationships.UsesCloud
    ];

    private const string NotAContainer = "NOT ({0}:Project OR {0}:Service OR {0}:Workspace OR {0}:SemanticStructure)";

    /// <summary>
    /// Resolves the source-code evidence behind a rendered edge. Group edges pass every member id on
    /// each side; evidence is aggregated and de-duplicated across all member pairs in a bounded number of queries.
    /// </summary>
    public async Task<EdgeEvidenceResponseDto> GetEdgeEvidenceAsync(
        IReadOnlyList<string> sourceIds,
        IReadOnlyList<string> targetIds,
        string? edgeKind,
        CancellationToken ct = default)
    {
        var response = new EdgeEvidenceResponseDto
        {
            SourceId = sourceIds.FirstOrDefault() ?? string.Empty,
            TargetId = targetIds.FirstOrDefault() ?? string.Empty,
            EdgeKind = edgeKind ?? string.Empty
        };

        var warnings = new List<string>();
        var scopeCache = new Dictionary<string, EvidenceScope?>(StringComparer.OrdinalIgnoreCase);

        var sources = await ResolveEvidenceNodesAsync(sourceIds, ct, warnings);
        var targets = await ResolveEvidenceNodesAsync(targetIds, ct, warnings);
        await PreloadServiceScopesAsync(sources.Concat(targets), scopeCache, warnings, ct);

        var items = new List<ArchitectureEvidenceItemDto>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddItem(ArchitectureEvidenceItemDto item)
        {
            if (seen.Add($"{item.Kind}:{item.FilePath}:{item.LineStart}:{item.TargetSymbol}:{item.Operation}"))
            {
                items.Add(item);
            }
        }

        // 1. Batched lookup of recorded evidence_ids on rolled-up edges in the rendered direction (sources -> targets)
        var pairsWithRecordedEvidence = await CollectRecordedRolledUpEdgeEvidenceAsync(sources, targets, scopeCache, warnings, ct);
        foreach (var item in pairsWithRecordedEvidence.Items)
        {
            AddItem(item);
        }

        // 2. Batched resolution for pairs that did not have recorded evidence_ids on a rolled-up edge
        var unrecordedSources = new List<EvidenceNodeRef>();
        var unrecordedTargets = new List<EvidenceNodeRef>();
        var hasUnrecordedServiceToService = false;
        var hasUnrecordedServiceToTopic = false;
        var hasUnrecordedTopicToService = false;
        var hasUnrecordedServiceToExternal = false;

        foreach (var s in sources)
        {
            foreach (var t in targets)
            {
                if (s.Id.Equals(t.Id, StringComparison.OrdinalIgnoreCase)) continue;
                if (pairsWithRecordedEvidence.CoveredPairs.Contains((s.Id, t.Id))) continue;

                if (!unrecordedSources.Any(n => n.Id.Equals(s.Id, StringComparison.OrdinalIgnoreCase)))
                    unrecordedSources.Add(s);
                if (!unrecordedTargets.Any(n => n.Id.Equals(t.Id, StringComparison.OrdinalIgnoreCase)))
                    unrecordedTargets.Add(t);

                if (t.Label == OntologyConstants.NodeLabels.Database)
                {
                    var dbRes = await GetDatabaseEvidenceCoreAsync(t.Id, s.Id, ct, scopeCache);
                    if (dbRes.Warnings != null) warnings.AddRange(dbRes.Warnings.Where(w => !warnings.Contains(w)));
                    foreach (var item in dbRes.Items) AddItem(item);
                }
                else if (s.Label == OntologyConstants.NodeLabels.Database)
                {
                    var dbRes = await GetDatabaseEvidenceCoreAsync(s.Id, t.Id, ct, scopeCache);
                    if (dbRes.Warnings != null) warnings.AddRange(dbRes.Warnings.Where(w => !warnings.Contains(w)));
                    foreach (var item in dbRes.Items) AddItem(item);
                }
                else if (t.Label == OntologyConstants.NodeLabels.Topic)
                {
                    hasUnrecordedServiceToTopic = true;
                }
                else if (s.Label == OntologyConstants.NodeLabels.Topic)
                {
                    hasUnrecordedTopicToService = true;
                }
                else if (t.Label is OntologyConstants.NodeLabels.ExternalService or OntologyConstants.NodeLabels.CloudService ||
                         s.Label is OntologyConstants.NodeLabels.ExternalService or OntologyConstants.NodeLabels.CloudService)
                {
                    hasUnrecordedServiceToExternal = true;
                }
                else
                {
                    hasUnrecordedServiceToService = true;
                }
            }
        }

        if (hasUnrecordedServiceToTopic)
        {
            var svcNodes = unrecordedSources.Where(n => n.Label != OntologyConstants.NodeLabels.Topic).ToList();
            var topNodes = unrecordedTargets.Where(n => n.Label == OntologyConstants.NodeLabels.Topic).ToList();
            var isSubEdge = string.Equals(edgeKind, OntologyConstants.Relationships.SubscribesTo, StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(edgeKind, OntologyConstants.Relationships.SubscribedBy, StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(edgeKind, OntologyConstants.Relationships.Triggers, StringComparison.OrdinalIgnoreCase);
            foreach (var item in await CollectBatchTopicEvidenceAsync(topNodes, svcNodes, publish: !isSubEdge, scopeCache, warnings, ct))
            {
                AddItem(item);
            }
        }

        if (hasUnrecordedTopicToService)
        {
            var topNodes = unrecordedSources.Where(n => n.Label == OntologyConstants.NodeLabels.Topic).ToList();
            var svcNodes = unrecordedTargets.Where(n => n.Label != OntologyConstants.NodeLabels.Topic).ToList();
            var isPubEdge = string.Equals(edgeKind, OntologyConstants.Relationships.PublishesTo, StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(edgeKind, OntologyConstants.Relationships.PublishedBy, StringComparison.OrdinalIgnoreCase);
            foreach (var item in await CollectBatchTopicEvidenceAsync(topNodes, svcNodes, publish: isPubEdge, scopeCache, warnings, ct))
            {
                AddItem(item);
            }
        }

        if (hasUnrecordedServiceToExternal)
        {
            var svcNodes = unrecordedSources
                .Where(n => n.Label is not (OntologyConstants.NodeLabels.ExternalService or OntologyConstants.NodeLabels.CloudService))
                .ToList();
            var extNodes = unrecordedTargets
                .Where(n => n.Label is OntologyConstants.NodeLabels.ExternalService or OntologyConstants.NodeLabels.CloudService)
                .ToList();
            if (svcNodes.Count > 0 && extNodes.Count > 0)
            {
                foreach (var item in await CollectBatchExternalCallEvidenceAsync(svcNodes, extNodes, scopeCache, warnings, ct))
                {
                    AddItem(item);
                }
            }
        }

        if (hasUnrecordedServiceToService)
        {
            var callerNodes = unrecordedSources
                .Where(n => n.Label is not (OntologyConstants.NodeLabels.Database or OntologyConstants.NodeLabels.Topic or OntologyConstants.NodeLabels.ExternalService or OntologyConstants.NodeLabels.CloudService))
                .ToList();
            var calleeNodes = unrecordedTargets
                .Where(n => n.Label is not (OntologyConstants.NodeLabels.Database or OntologyConstants.NodeLabels.Topic or OntologyConstants.NodeLabels.ExternalService or OntologyConstants.NodeLabels.CloudService))
                .ToList();
            if (callerNodes.Count > 0 && calleeNodes.Count > 0)
            {
                foreach (var item in await CollectBatchEndpointCallEvidenceAsync(callerNodes, calleeNodes, scopeCache, warnings, ct))
                {
                    AddItem(item);
                }
            }
        }

        // 3. Batched code-level references (calls, uses_type, implements, inherits_from, exposed_by)
        var codeCallers = unrecordedSources
            .Where(n => n.Label is not (OntologyConstants.NodeLabels.Database or OntologyConstants.NodeLabels.Topic))
            .ToList();
        var codeCallees = unrecordedTargets
            .Where(n => n.Label is not (OntologyConstants.NodeLabels.Topic))
            .ToList();
        if (codeCallers.Count > 0 && codeCallees.Count > 0)
        {
            foreach (var item in await CollectBatchCodeLevelEvidenceAsync(codeCallers, codeCallees, warnings, ct))
            {
                AddItem(item);
            }
        }

        response.Items = items
            .OrderBy(i => EvidenceKindOrder(i.Kind))
            .ThenBy(i => i.FilePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(i => i.LineStart ?? int.MaxValue)
            .ToList();
        response.TotalCount = response.Items.Count;
        if (warnings.Count > 0)
        {
            response.Warnings = warnings;
        }
        return response;
    }

    private async Task<(List<ArchitectureEvidenceItemDto> Items, HashSet<(string SourceId, string TargetId)> CoveredPairs)> CollectRecordedRolledUpEdgeEvidenceAsync(
        IReadOnlyList<EvidenceNodeRef> sources,
        IReadOnlyList<EvidenceNodeRef> targets,
        Dictionary<string, EvidenceScope?> scopeCache,
        List<string> warnings,
        CancellationToken ct)
    {
        var items = new List<ArchitectureEvidenceItemDto>();
        var coveredPairs = new HashSet<(string SourceId, string TargetId)>();
        if (sources.Count == 0 || targets.Count == 0)
        {
            return (items, coveredPairs);
        }

        var srcLiterals = string.Join(", ", sources.Select(s => EscapeCypherStringLiteral(s.Id)).Distinct());
        var tgtLiterals = string.Join(", ", targets.Select(t => EscapeCypherStringLiteral(t.Id)).Distinct());
        var parsedIdentities = new List<(string PairSourceId, string PairTargetId, string FromId, string Kind, string ToId)>();

        try
        {
            var rolledQuery = $"""
                MATCH (s)-[r]->(t)
                WHERE s.id IN [{srcLiterals}] AND t.id IN [{tgtLiterals}]
                RETURN s.id AS s_id, t.id AS t_id, r.properties AS properties
                """;
            var json = await db.ExecuteQueryAsync(rolledQuery, null, ct);
            using var doc = JsonDocument.Parse(json);
            foreach (var row in doc.RootElement.EnumerateArray())
            {
                var sId = row.GetStringProp("s_id");
                var tId = row.GetStringProp("t_id");
                var edgeProps = row.ExtractProperties();
                var rawEv = edgeProps.GetValueOrDefault(OntologyConstants.PropertyNames.EvidenceIds);
                if (string.IsNullOrEmpty(sId) || string.IsNullOrEmpty(tId) || string.IsNullOrEmpty(rawEv)) continue;

                List<string>? idList = null;
                try
                {
                    idList = JsonSerializer.Deserialize<List<string>>(rawEv);
                }
                catch
                {
                    // Ignore malformed JSON array
                }
                if (idList is null || idList.Count == 0) continue;

                foreach (var entry in idList)
                {
                    if (EvidenceProvenanceHelper.TryParseEdgeIdentity(entry, out var edge))
                    {
                        parsedIdentities.Add((sId, tId, edge.From, edge.Kind, edge.To));
                        coveredPairs.Add((sId, tId));
                    }
                }
            }
        }
        catch (Exception ex)
        {
            RecordEvidenceWarning(warnings, "Rolled-up edge provenance lookup", ex);
        }

        if (parsedIdentities.Count == 0)
        {
            return (items, coveredPairs);
        }

        var fromLiterals = string.Join(", ", parsedIdentities.Select(p => EscapeCypherStringLiteral(p.FromId)).Distinct());
        var toLiterals = string.Join(", ", parsedIdentities.Select(p => EscapeCypherStringLiteral(p.ToId)).Distinct());
        var wantedTriples = parsedIdentities
            .Select(p => (p.FromId, p.Kind, p.ToId))
            .ToHashSet();
        var sourceNameByFromId = parsedIdentities
            .GroupBy(p => p.FromId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => sources.FirstOrDefault(s => s.Id.Equals(g.First().PairSourceId, StringComparison.OrdinalIgnoreCase))?.Name,
                StringComparer.OrdinalIgnoreCase);

        try
        {
            var contribQuery = $"""
                MATCH (src)-[rel]->(tgt)
                WHERE src.id IN [{fromLiterals}] AND tgt.id IN [{toLiterals}]
                RETURN src.id AS src_id, src.name AS src_name, labels(src)[0] AS src_kind,
                       coalesce(src.file_path, src.path) AS file_path,
                       coalesce(src.start_line, src.line) AS line,
                       coalesce(src.end_line, src.line_end) AS line_end,
                       tgt.id AS tgt_id, tgt.name AS tgt_name, labels(tgt)[0] AS tgt_kind,
                       coalesce(tgt.file_path, tgt.path) AS tgt_file_path,
                       coalesce(tgt.start_line, tgt.line) AS tgt_line,
                       coalesce(tgt.end_line, tgt.line_end) AS tgt_line_end,
                       coalesce(tgt.http_method, src.http_method) AS http_method,
                       coalesce(tgt.route_template, src.route_template) AS route,
                       coalesce(tgt.protocol, src.protocol) AS protocol,
                       rel.kind AS rel_kind,
                       rel.properties AS properties,
                       coalesce(json_extract(src.properties, '$.query_text'), json_extract(tgt.properties, '$.query_text')) AS query_text,
                       coalesce(json_extract(src.properties, '$.verb'), json_extract(tgt.properties, '$.verb')) AS src_verb
                """;
            var cJson = await db.ExecuteQueryAsync(contribQuery, null, ct);
            using var cDoc = JsonDocument.Parse(cJson);
            foreach (var row in cDoc.RootElement.EnumerateArray())
            {
                var srcId = row.GetStringProp("src_id");
                var tgtId = row.GetStringProp("tgt_id");
                var relKind = row.GetStringProp("rel_kind");
                if (!wantedTriples.Any(w =>
                        w.FromId.Equals(srcId, StringComparison.OrdinalIgnoreCase) &&
                        w.ToId.Equals(tgtId, StringComparison.OrdinalIgnoreCase) &&
                        w.Kind.Equals(relKind, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                var relProps = row.ExtractProperties();
                var srcKind = row.GetStringProp("src_kind");
                var tgtKind = row.GetStringProp("tgt_kind");
                var isDownwardResourceEdge = relKind is
                    OntologyConstants.Relationships.AccessedBy or
                    OntologyConstants.Relationships.CalledBy or
                    OntologyConstants.Relationships.PublishedBy or
                    OntologyConstants.Relationships.SubscribedBy or
                    OntologyConstants.Relationships.ExposedBy or
                    OntologyConstants.Relationships.DeclaredIn or
                    OntologyConstants.Relationships.QueriedBy;

                var filePath = relProps.GetValueOrDefault(OntologyConstants.PropertyNames.FilePath) is { Length: > 0 } rfp
                    ? rfp
                    : isDownwardResourceEdge
                        ? (row.GetStringProp("tgt_file_path") is { Length: > 0 } tfp ? tfp : row.GetStringProp("file_path"))
                        : (row.GetStringProp("file_path") is { Length: > 0 } sfp ? sfp : row.GetStringProp("tgt_file_path"));
                if (string.IsNullOrEmpty(filePath)) continue;

                var srcName = row.GetStringProp("src_name", srcId);
                var tgtName = row.GetStringProp("tgt_name", tgtId);
                var relOp = relProps.GetValueOrDefault(OntologyConstants.PropertyNames.Operation) ?? "";
                var relEvKind = relProps.GetValueOrDefault(OntologyConstants.PropertyNames.EvidenceKind) ?? "";
                var queryText = row.GetStringProp("query_text");
                var srcVerb = row.GetStringProp("src_verb");
                var httpMethod = row.GetStringProp("http_method");
                var route = row.GetStringProp("route");
                var protocol = row.GetStringProp("protocol");

                int? line = ParseLineProp(relProps, OntologyConstants.PropertyNames.Line, row, isDownwardResourceEdge ? "tgt_line" : "line")
                            ?? ParseLineProp(relProps, OntologyConstants.PropertyNames.Line, row, "line");
                int? lineEnd = ParseLineProp(relProps, OntologyConstants.PropertyNames.LineEnd, row, isDownwardResourceEdge ? "tgt_line_end" : "line_end")
                               ?? line;

                var effectiveResourceKind = isDownwardResourceEdge ? srcKind : tgtKind;
                var kind = !string.IsNullOrEmpty(relEvKind)
                    ? relEvKind
                    : relKind switch
                    {
                        OntologyConstants.Relationships.CallsEndpoint or OntologyConstants.Relationships.CalledBy when effectiveResourceKind == OntologyConstants.NodeLabels.Endpoint =>
                            protocol.Equals("grpc", StringComparison.OrdinalIgnoreCase) ? OntologyConstants.EvidenceKinds.GrpcCall
                            : protocol.Equals("wcf", StringComparison.OrdinalIgnoreCase) ? OntologyConstants.EvidenceKinds.WcfCall
                            : OntologyConstants.EvidenceKinds.HttpCall,
                        OntologyConstants.Relationships.PublishesTo or OntologyConstants.Relationships.PublishedBy =>
                            OntologyConstants.EvidenceKinds.Publish,
                        OntologyConstants.Relationships.SubscribesTo or OntologyConstants.Relationships.SubscribedBy or OntologyConstants.Relationships.Triggers =>
                            OntologyConstants.EvidenceKinds.Subscribe,
                        OntologyConstants.Relationships.PersistedIn =>
                            OntologyConstants.EvidenceKinds.Orm,
                        OntologyConstants.Relationships.AccessedBy when tgtKind == OntologyConstants.NodeLabels.Type =>
                            OntologyConstants.EvidenceKinds.Orm,
                        OntologyConstants.Relationships.Configures or OntologyConstants.Relationships.DeclaredIn when tgtKind == OntologyConstants.NodeLabels.File =>
                            OntologyConstants.EvidenceKinds.Config,
                        _ when effectiveResourceKind == OntologyConstants.NodeLabels.Procedure =>
                            OntologyConstants.EvidenceKinds.Procedure,
                        _ when effectiveResourceKind is OntologyConstants.NodeLabels.Database or OntologyConstants.NodeLabels.Table =>
                            OntologyConstants.EvidenceKinds.Query,
                        _ => OntologyConstants.EvidenceKinds.HttpCall
                    };

                var operation = !string.IsNullOrEmpty(relOp)
                    ? relOp
                    : kind switch
                    {
                        OntologyConstants.EvidenceKinds.Publish => "PUBLISH",
                        OntologyConstants.EvidenceKinds.Subscribe => "SUBSCRIBE",
                        OntologyConstants.EvidenceKinds.Orm => "ORM",
                        OntologyConstants.EvidenceKinds.Config => "CONFIG",
                        OntologyConstants.EvidenceKinds.Procedure => "EXEC",
                        OntologyConstants.EvidenceKinds.Query => InferSqlVerb(srcVerb, queryText, isDownwardResourceEdge ? tgtName : srcName) ?? "SELECT",
                        _ => !string.IsNullOrEmpty(httpMethod) && !httpMethod.Equals("ALL", StringComparison.OrdinalIgnoreCase)
                            ? httpMethod.ToUpperInvariant()
                            : "CALL"
                    };

                var isTopic = effectiveResourceKind == OntologyConstants.NodeLabels.Topic || srcId.Contains(":top:") || tgtId.Contains(":top:");
                var topicTargetName = tgtKind == OntologyConstants.NodeLabels.Topic ? tgtName : srcName;
                var callerSym = isDownwardResourceEdge ? tgtName : (tgtKind == OntologyConstants.NodeLabels.Topic ? srcName : (srcKind == OntologyConstants.NodeLabels.Topic ? tgtName : srcName));
                var targetSym = isDownwardResourceEdge ? srcName : (tgtKind == OntologyConstants.NodeLabels.Topic ? tgtName : (srcKind == OntologyConstants.NodeLabels.Topic ? srcName : tgtName));

                var title = !string.IsNullOrEmpty(route)
                    ? $"{operation} {route}"
                    : isTopic
                        ? $"{(kind == OntologyConstants.EvidenceKinds.Publish ? "Publishes" : "Subscribes")} {topicTargetName}"
                        : $"{callerSym} → {targetSym}";

                items.Add(new ArchitectureEvidenceItemDto
                {
                    Id = isDownwardResourceEdge ? tgtId : srcId,
                    Kind = kind,
                    Operation = operation,
                    Title = title,
                    FilePath = filePath,
                    LineStart = line,
                    LineEnd = lineEnd,
                    Confidence = OntologyConstants.EvidenceConfidence.Recorded,
                    CodeSnippet = !string.IsNullOrWhiteSpace(queryText) ? queryText : null,
                    CallerSymbol = callerSym,
                    TargetSymbol = targetSym,
                    ServiceName = sourceNameByFromId.GetValueOrDefault(srcId),
                    Reason = $"Recorded {relKind} evidence from '{callerSym}' to '{targetSym}'"
                });
            }
        }
        catch (Exception ex)
        {
            RecordEvidenceWarning(warnings, "Contributing symbol edge lookup", ex);
        }

        if (items.Count == 0)
        {
            coveredPairs.Clear();
        }

        return (items, coveredPairs);
    }

    private static int EvidenceKindOrder(string kind) => kind switch
    {
        "http_call" or "grpc_call" or "wcf_call" or "publish" or "subscribe" => 0,
        "query" or "procedure" => 1,
        "orm" or "table" => 2,
        "endpoint" or "entrypoint" => 3,
        "library_call" or "test_call" => 4,
        "config" => 5,
        "migration" => 6,
        _ => 7
    };

    private static int NodeKindResolutionRank(string kind, string rawId, bool rawHadServicePrefix)
    {
        if (rawHadServicePrefix)
        {
            return kind switch
            {
                "Service" or "App" or "Worker" or "CliTool" or "Library" or "TestSuite" => 100,
                "Project" => 90,
                "Database" or "Topic" or "ExternalService" or "CloudService" => 20,
                _ => 10
            };
        }

        if (rawId.StartsWith("pm:cls:", StringComparison.OrdinalIgnoreCase))
        {
            return kind switch
            {
                "Type" or "Class" or "Interface" => 100,
                "File" => 90,
                "Folder" => 60,
                "Function" or "Method" or "Member" => 40,
                _ => 5
            };
        }

        if (rawId.StartsWith("pm:ep:", StringComparison.OrdinalIgnoreCase) ||
            rawId.StartsWith("ws:entry:", StringComparison.OrdinalIgnoreCase) ||
            rawId.StartsWith("ws:ep:", StringComparison.OrdinalIgnoreCase))
        {
            return kind switch
            {
                "EntryPoint" or "Endpoint" => 100,
                _ => 5
            };
        }

        if (rawId.StartsWith("pm:mod:", StringComparison.OrdinalIgnoreCase) ||
            rawId.StartsWith("pm:file:", StringComparison.OrdinalIgnoreCase) ||
            rawId.StartsWith("pm:sub:", StringComparison.OrdinalIgnoreCase))
        {
            return kind switch
            {
                "File" or "Folder" => 100,
                "Project" or "Service" => 90,
                "Type" or "Class" => 80,
                _ => 5
            };
        }

        if (rawId.StartsWith("pm:res:", StringComparison.OrdinalIgnoreCase))
        {
            return kind switch
            {
                "Database" or "Topic" or "ExternalService" or "CloudService" => 100,
                "Table" or "Procedure" => 90,
                _ => 5
            };
        }

        return kind switch
        {
            "Database" or "Topic" or "ExternalService" or "CloudService" => 100,
            "Service" or "App" or "Worker" or "CliTool" or "Library" or "TestSuite" => 95,
            "Project" => 90,
            "Endpoint" or "EntryPoint" or "Table" or "Procedure" => 50,
            "Type" or "Class" or "Interface" or "File" or "Folder" => 40,
            "Function" or "Method" or "Member" => 30,
            _ => 10
        };
    }

    private async Task<List<EvidenceNodeRef>> ResolveEvidenceNodesAsync(
        IReadOnlyList<string> ids,
        CancellationToken ct,
        List<string>? warnings = null)
    {
        var result = new List<EvidenceNodeRef>();
        var distinctRawIds = ids
            .Where(i => !string.IsNullOrWhiteSpace(i))
            .Select(i => i.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (distinctRawIds.Count == 0) return result;

        var candidateTerms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var cleanSuffixes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var id in distinctRawIds)
        {
            var cleanId = StripEntityIdPrefix(id);
            if (string.IsNullOrWhiteSpace(cleanId)) cleanId = id;
            candidateTerms.Add(id);
            candidateTerms.Add(cleanId);
            candidateTerms.Add($"ws:f:{cleanId}");
            cleanSuffixes.Add($":{cleanId}");
            cleanSuffixes.Add($":{cleanId}:");
            cleanSuffixes.Add($"/{cleanId}");
            if (!cleanId.Contains('.'))
            {
                cleanSuffixes.Add($"/{cleanId}.ts");
                cleanSuffixes.Add($"/{cleanId}.tsx");
                cleanSuffixes.Add($"/{cleanId}.js");
                cleanSuffixes.Add($"/{cleanId}.jsx");
                cleanSuffixes.Add($"/{cleanId}.py");
                cleanSuffixes.Add($"/{cleanId}.cs");
                cleanSuffixes.Add($"/{cleanId}.go");
                cleanSuffixes.Add($"/{cleanId}.java");
            }
        }

        var termLiterals = string.Join(", ", candidateTerms.Select(EscapeCypherStringLiteral));
        var suffixChecks = string.Join(" OR ", cleanSuffixes.Select(s => $"n.id ENDS WITH {EscapeCypherStringLiteral(s)} OR coalesce(n.file_path, n.path) ENDS WITH {EscapeCypherStringLiteral(s)}"));
        var whereClause = string.IsNullOrEmpty(suffixChecks)
            ? $"n.id IN [{termLiterals}] OR n.name IN [{termLiterals}] OR coalesce(n.file_path, n.path) IN [{termLiterals}]"
            : $"n.id IN [{termLiterals}] OR n.name IN [{termLiterals}] OR coalesce(n.file_path, n.path) IN [{termLiterals}] OR {suffixChecks}";

        var candidates = new List<(string Id, string Name, string Kind, string? Path, int? Line)>();
        try
        {
            var query = $"""
                MATCH (n)
                WHERE {whereClause}
                RETURN n.id AS id, n.name AS name, labels(n)[0] AS kind, coalesce(n.file_path, n.path) AS path,
                       coalesce(n.start_line, n.line) AS line
                """;
            var json = await db.ExecuteQueryAsync(query, null, ct);
            using var doc = JsonDocument.Parse(json);
            foreach (var row in doc.RootElement.EnumerateArray())
            {
                var rId = row.GetStringProp("id");
                if (string.IsNullOrEmpty(rId)) continue;
                candidates.Add((
                    rId,
                    row.GetStringProp("name", rId),
                    row.GetStringProp("kind"),
                    row.GetStringProp("path"),
                    row.GetNullableIntProp("line")));
            }
        }
        catch (Exception ex)
        {
            RecordEvidenceWarning(warnings, "Batch evidence node resolution", ex);
            return result;
        }

        foreach (var id in distinctRawIds)
        {
            var cleanId = StripEntityIdPrefix(id);
            if (string.IsNullOrWhiteSpace(cleanId)) cleanId = id;
            var rawHadServicePrefix = !id.Equals(cleanId, StringComparison.OrdinalIgnoreCase) &&
                                      !id.StartsWith("pm:", StringComparison.OrdinalIgnoreCase) &&
                                      !id.StartsWith("ws:db:", StringComparison.OrdinalIgnoreCase) &&
                                      !id.StartsWith("ws:top:", StringComparison.OrdinalIgnoreCase) &&
                                      !id.StartsWith("ws:es:", StringComparison.OrdinalIgnoreCase) &&
                                      !id.StartsWith("ws:cloud:", StringComparison.OrdinalIgnoreCase) &&
                                      !id.StartsWith("ws:res:", StringComparison.OrdinalIgnoreCase);

            EvidenceNodeRef? best = null;
            var bestScore = -1;
            foreach (var (rId, rName, rKind, rPath, rLine) in candidates)
            {
                var matchesThisId = rId.Equals(id, StringComparison.OrdinalIgnoreCase) ||
                                    rName.Equals(id, StringComparison.OrdinalIgnoreCase) ||
                                    rId.Equals(cleanId, StringComparison.OrdinalIgnoreCase) ||
                                    rName.Equals(cleanId, StringComparison.OrdinalIgnoreCase) ||
                                    rId.EndsWith(":" + cleanId, StringComparison.OrdinalIgnoreCase) ||
                                    rId.EndsWith(":" + cleanId + ":", StringComparison.OrdinalIgnoreCase) ||
                                    (rPath != null && (rPath.Equals(cleanId, StringComparison.OrdinalIgnoreCase) ||
                                                       rPath.EndsWith("/" + cleanId, StringComparison.OrdinalIgnoreCase) ||
                                                       string.Equals(Path.GetFileNameWithoutExtension(rPath), cleanId, StringComparison.OrdinalIgnoreCase)));
                if (!matchesThisId) continue;

                var rank = NodeKindResolutionRank(rKind, id, rawHadServicePrefix);
                if (rank == 0 && !rId.Equals(id, StringComparison.OrdinalIgnoreCase)) continue;

                var isExactIdMatch = rId.Equals(id, StringComparison.OrdinalIgnoreCase) ||
                                     rId.Equals(cleanId, StringComparison.OrdinalIgnoreCase);
                var exactBonus = isExactIdMatch ? 1000
                               : rName.Equals(cleanId, StringComparison.OrdinalIgnoreCase) ? 200
                               : (rPath != null && string.Equals(Path.GetFileNameWithoutExtension(rPath), cleanId, StringComparison.OrdinalIgnoreCase)) ? 150
                               : 0;
                var score = exactBonus + rank;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = new EvidenceNodeRef(rId, rName, rKind, rPath, rLine);
                }
            }
            if (best is not null && !result.Any(r => r.Id.Equals(best.Id, StringComparison.OrdinalIgnoreCase)))
            {
                result.Add(best);
            }
        }

        return result;
    }

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
                var json = await db.ExecuteQueryAsync(query, null, ct);
                using var doc = JsonDocument.Parse(json);
                foreach (var row in doc.RootElement.EnumerateArray())
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
                        Kind = publish ? OntologyConstants.EvidenceKinds.Publish : OntologyConstants.EvidenceKinds.Subscribe,
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
                var json = await db.ExecuteQueryAsync(query, null, ct);
                using var doc = JsonDocument.Parse(json);
                foreach (var row in doc.RootElement.EnumerateArray())
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
                        Kind = OntologyConstants.EvidenceKinds.HttpCall,
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
            var json = await db.ExecuteQueryAsync(epQuery, null, ct);
            using var doc = JsonDocument.Parse(json);
            foreach (var row in doc.RootElement.EnumerateArray())
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

                var kind = protocol.Equals("grpc", StringComparison.OrdinalIgnoreCase) ? OntologyConstants.EvidenceKinds.GrpcCall
                         : protocol.Equals("wcf", StringComparison.OrdinalIgnoreCase) ? OntologyConstants.EvidenceKinds.WcfCall
                         : OntologyConstants.EvidenceKinds.HttpCall;
                var operation = string.IsNullOrEmpty(method) || method.Equals("ALL", StringComparison.OrdinalIgnoreCase)
                    ? (kind == OntologyConstants.EvidenceKinds.HttpCall ? "CALL" : kind[..4].ToUpperInvariant())
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
                var json = await db.ExecuteQueryAsync(symQuery, null, ct);
                using var doc = JsonDocument.Parse(json);
                foreach (var row in doc.RootElement.EnumerateArray())
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
                        Kind = OntologyConstants.EvidenceKinds.HttpCall,
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
                var json = await db.ExecuteQueryAsync(pkgQuery, null, ct);
                using var doc = JsonDocument.Parse(json);
                foreach (var row in doc.RootElement.EnumerateArray())
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
                            Kind = OntologyConstants.EvidenceKinds.LibraryCall,
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
                var json = await db.ExecuteQueryAsync(directEdgeQuery, null, ct);
                using var doc = JsonDocument.Parse(json);
                foreach (var row in doc.RootElement.EnumerateArray())
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
                            Kind = OntologyConstants.EvidenceKinds.Config,
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
                            Kind = "EXPOSED_BY",
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
            var json = await db.ExecuteQueryAsync(query, null, ct);
            using var doc = JsonDocument.Parse(json);
            foreach (var row in doc.RootElement.EnumerateArray())
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

                items.Add(new ArchitectureEvidenceItemDto
                {
                    Id = $"{row.GetStringProp("from_id")}->{row.GetStringProp("to_id")}:{kind}",
                    Kind = kind,
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

    private sealed record DatabaseObjectRef(string Id, string Name, string Label);

    /// <summary>Tables and procedures that belong to the database, in either relationship direction.</summary>
    private async Task<List<DatabaseObjectRef>> FindDatabaseObjectsAsync(string dbId, string dbName, string rawId, CancellationToken ct)
    {
        var found = new Dictionary<string, DatabaseObjectRef>(StringComparer.OrdinalIgnoreCase);
        var parameters = new Dictionary<string, object> { ["id"] = dbId, ["name"] = dbName, ["rawId"] = rawId };

        foreach (var pattern in new[] { "(obj)-[r]->(tgt)", "(tgt)-[r]->(obj)" })
        {
            var query = $"""
                MATCH {pattern}
                WHERE (obj:Table OR obj:Procedure) AND (tgt.id = $id OR tgt.name = $name OR tgt.id = $rawId)
                RETURN obj.id AS id, obj.name AS name, labels(obj)[0] AS kind
                """;
            var json = await db.ExecuteQueryAsync(query, parameters, ct);
            using var doc = JsonDocument.Parse(json);
            foreach (var row in doc.RootElement.EnumerateArray())
            {
                var id = row.GetStringProp("id");
                if (!string.IsNullOrEmpty(id))
                {
                    found.TryAdd(id, new DatabaseObjectRef(id, row.GetStringProp("name", id), row.GetStringProp("kind")));
                }
            }
        }

        return [.. found.Values];
    }

    /// <summary>
    /// Resolves source-code evidence proving a rendered node's role and capabilities
    /// (Endpoints, EntryPoints, ORM entities, Queries, Topic publishers/consumers, External calls, Configs).
    /// Grouped nodes pass every member id.
    /// </summary>
    public async Task<NodeEvidenceResponseDto> GetNodeEvidenceAsync(
        IReadOnlyList<string> nodeIds,
        string? nodeKind = null,
        CancellationToken ct = default)
    {
        var response = new NodeEvidenceResponseDto
        {
            NodeId = nodeIds.FirstOrDefault() ?? string.Empty,
            NodeKind = nodeKind ?? string.Empty
        };

        var warnings = new List<string>();
        var scopeCache = new Dictionary<string, EvidenceScope?>(StringComparer.OrdinalIgnoreCase);
        var resolvedNodes = await ResolveEvidenceNodesAsync(nodeIds, ct, warnings);
        await PreloadServiceScopesAsync(resolvedNodes, scopeCache, warnings, ct);
        if (resolvedNodes.Count > 0)
        {
            response.NodeName = resolvedNodes[0].Name;
            if (string.IsNullOrEmpty(response.NodeKind))
            {
                response.NodeKind = resolvedNodes[0].Label;
            }
        }

        var items = new List<ArchitectureEvidenceItemDto>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var node in resolvedNodes)
        {
            var nodeItems = await CollectSingleNodeEvidenceAsync(node, scopeCache, warnings, ct);
            foreach (var item in nodeItems)
            {
                if (seen.Add($"{item.Kind}:{item.FilePath}:{item.LineStart}:{item.Title}:{item.Operation}"))
                {
                    items.Add(item);
                }
            }
        }

        response.Items = items
            .OrderBy(i => EvidenceKindOrder(i.Kind))
            .ThenBy(i => i.FilePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(i => i.LineStart ?? int.MaxValue)
            .Take(150)
            .ToList();
        response.TotalCount = response.Items.Count;
        if (warnings.Count > 0)
        {
            response.Warnings = warnings;
        }
        return response;
    }

    private async Task<List<ArchitectureEvidenceItemDto>> CollectSingleNodeEvidenceAsync(
        EvidenceNodeRef node,
        Dictionary<string, EvidenceScope?> scopeCache,
        List<string> warnings,
        CancellationToken ct)
    {
        if (node.Label == OntologyConstants.NodeLabels.Database)
        {
            var dbRes = await GetDatabaseEvidenceCoreAsync(node.Id, null, ct, scopeCache);
            if (dbRes.Warnings != null) warnings.AddRange(dbRes.Warnings.Where(w => !warnings.Contains(w)));
            return dbRes.Items;
        }

        if (node.Label == OntologyConstants.NodeLabels.Topic)
        {
            return await CollectAllTopicNodeEvidenceAsync(node, warnings, ct);
        }

        if (node.Label is OntologyConstants.NodeLabels.ExternalService or OntologyConstants.NodeLabels.CloudService)
        {
            return await CollectAllExternalNodeEvidenceAsync(node, warnings, ct);
        }

        return await CollectServiceOrProjectNodeEvidenceAsync(node, scopeCache, warnings, ct);
    }

    private async Task<List<ArchitectureEvidenceItemDto>> CollectAllTopicNodeEvidenceAsync(
        EvidenceNodeRef topic,
        List<string> warnings,
        CancellationToken ct)
    {
        var items = new List<ArchitectureEvidenceItemDto>();
        foreach (var (publish, kinds, dir) in new[]
        {
            (true, PublishOutgoingKinds, "(sym)-[r]->(t)"),
            (true, PublishIncomingKinds, "(t)-[r]->(sym)"),
            (false, SubscribeOutgoingKinds, "(sym)-[r]->(t)"),
            (false, SubscribeIncomingKinds, "(t)-[r]->(sym)")
        })
        {
            var query = $"""
                MATCH {dir}
                WHERE (t.id = $topicId OR t.name = $topicName)
                  AND r.kind IN [{string.Join(", ", kinds.Select(k => $"'{k}'"))}]
                  AND {string.Format(NotAContainer, "sym")}
                RETURN sym.id AS id, sym.name AS name, labels(sym)[0] AS kind,
                       coalesce(sym.file_path, sym.path) AS path,
                       coalesce(sym.start_line, sym.line) AS line,
                       coalesce(sym.end_line, sym.line_end) AS line_end,
                       r.properties AS properties,
                       json_extract(sym.properties, '$.service') AS service
                """;
            try
            {
                var json = await db.ExecuteQueryAsync(query, new Dictionary<string, object>
                {
                    ["topicId"] = topic.Id,
                    ["topicName"] = topic.Name
                }, ct);
                using var doc = JsonDocument.Parse(json);
                foreach (var row in doc.RootElement.EnumerateArray())
                {
                    var rProps = row.ExtractProperties();
                    var path = rProps.GetValueOrDefault(OntologyConstants.PropertyNames.FilePath) is { Length: > 0 } rfp
                        ? rfp
                        : row.GetStringProp("path");
                    if (string.IsNullOrEmpty(path)) continue;
                    int? line = ParseLineProp(rProps, OntologyConstants.PropertyNames.Line, row, "line");
                    int? lineEnd = ParseLineProp(rProps, OntologyConstants.PropertyNames.LineEnd, row, "line_end") ?? line;

                    var symName = row.GetStringProp("name");
                    var svc = row.GetStringProp("service");
                    items.Add(new ArchitectureEvidenceItemDto
                    {
                        Id = row.GetStringProp("id"),
                        Kind = publish ? OntologyConstants.EvidenceKinds.Publish : OntologyConstants.EvidenceKinds.Subscribe,
                        Operation = publish ? "PUBLISH" : "SUBSCRIBE",
                        Title = $"{(publish ? "Publishes" : "Subscribes")} {topic.Name}",
                        FilePath = path,
                        LineStart = line,
                        LineEnd = lineEnd,
                        Confidence = OntologyConstants.EvidenceConfidence.Recorded,
                        CallerSymbol = symName,
                        TargetSymbol = topic.Name,
                        ServiceName = string.IsNullOrEmpty(svc) ? null : svc,
                        Reason = $"{row.GetStringProp("kind")} '{symName}' {(publish ? "publishes to" : "consumes from")} topic '{topic.Name}'"
                    });
                }
            }
            catch (Exception ex)
            {
                RecordEvidenceWarning(warnings, $"Topic node evidence query for '{topic.Name}'", ex);
            }
        }
        return items;
    }

    private async Task<List<ArchitectureEvidenceItemDto>> CollectAllExternalNodeEvidenceAsync(
        EvidenceNodeRef external,
        List<string> warnings,
        CancellationToken ct)
    {
        var items = new List<ArchitectureEvidenceItemDto>();
        var outgoingKinds = string.Join(", ", ExternalCallKinds.Append("TRANSITIVELY_CALLS").Distinct().Select(k => $"'{k}'"));
        foreach (var (pattern, kinds) in new[] { ("(sym)-[r]->(ext)", outgoingKinds), ("(ext)-[r]->(sym)", "'CALLED_BY', 'ACCESSED_BY', 'DECLARED_IN'") })
        {
            var query = $"""
                MATCH {pattern}
                WHERE (ext.id = $extId OR ext.name = $extName)
                  AND r.kind IN [{kinds}]
                  AND {string.Format(NotAContainer, "sym")}
                RETURN sym.id AS id, sym.name AS name, labels(sym)[0] AS kind,
                       coalesce(sym.file_path, sym.path) AS path,
                       coalesce(sym.start_line, sym.line) AS line,
                       coalesce(sym.end_line, sym.line_end) AS line_end,
                       r.kind AS rel_kind, r.properties AS properties,
                       json_extract(sym.properties, '$.service') AS service
                """;
            try
            {
                var json = await db.ExecuteQueryAsync(query, new Dictionary<string, object>
                {
                    ["extId"] = external.Id,
                    ["extName"] = external.Name
                }, ct);
                using var doc = JsonDocument.Parse(json);
                foreach (var row in doc.RootElement.EnumerateArray())
                {
                    var rProps = row.ExtractProperties();
                    var path = rProps.GetValueOrDefault(OntologyConstants.PropertyNames.FilePath) is { Length: > 0 } rfp
                        ? rfp
                        : row.GetStringProp("path");
                    if (string.IsNullOrEmpty(path)) continue;
                    var symKind = row.GetStringProp("kind");
                    if (symKind is "Endpoint" or "EntryPoint" && row.GetStringProp("rel_kind") == "ATTRIBUTED_TO") continue;

                    int? line = ParseLineProp(rProps, OntologyConstants.PropertyNames.Line, row, "line");
                    int? lineEnd = ParseLineProp(rProps, OntologyConstants.PropertyNames.LineEnd, row, "line_end") ?? line;

                    var symName = row.GetStringProp("name");
                    var svc = row.GetStringProp("service");
                    items.Add(new ArchitectureEvidenceItemDto
                    {
                        Id = row.GetStringProp("id"),
                        Kind = OntologyConstants.EvidenceKinds.HttpCall,
                        Operation = "CALL",
                        Title = $"Calls {external.Name}",
                        FilePath = path,
                        LineStart = line,
                        LineEnd = lineEnd,
                        Confidence = OntologyConstants.EvidenceConfidence.Recorded,
                        CallerSymbol = symName,
                        TargetSymbol = external.Name,
                        ServiceName = string.IsNullOrEmpty(svc) ? null : svc,
                        Reason = $"Invokes external service '{external.Name}'"
                    });
                }
            }
            catch (Exception ex)
            {
                RecordEvidenceWarning(warnings, $"External node evidence query for '{external.Name}'", ex);
            }
        }
        return items;
    }

    private async Task<List<ArchitectureEvidenceItemDto>> CollectServiceOrProjectNodeEvidenceAsync(
        EvidenceNodeRef serviceNode,
        Dictionary<string, EvidenceScope?> scopeCache,
        List<string> warnings,
        CancellationToken ct)
    {
        var items = new List<ArchitectureEvidenceItemDto>();
        var scope = await ResolveServiceScopeAsync(serviceNode.Id, ct, scopeCache, warnings);
        if (scope is null) return items;

        // 1. Endpoints exposed by this service
        var epQuery = """
            MATCH (ep:Endpoint)
            RETURN ep.id AS id, ep.name AS name, coalesce(ep.file_path, ep.path) AS path,
                   coalesce(ep.start_line, ep.line) AS line, coalesce(ep.end_line, ep.line_end) AS line_end,
                   ep.http_method AS http_method, ep.route_template AS route, ep.protocol AS protocol,
                   json_extract(ep.properties, '$.service') AS service
            """;
        try
        {
            var json = await db.ExecuteQueryAsync(epQuery, null, ct);
            using var doc = JsonDocument.Parse(json);
            foreach (var row in doc.RootElement.EnumerateArray())
            {
                var epId = row.GetStringProp("id");
                var path = row.GetStringProp("path");
                if (string.IsNullOrEmpty(path) || !scope.Matches(path, row.GetStringProp("service"), epId)) continue;

                var method = row.GetStringProp("http_method");
                var route = row.GetStringProp("route");
                var epName = row.GetStringProp("name");
                int? line = row.GetNullableIntProp("line");
                int? lineEnd = row.GetNullableIntProp("line_end");
                if (line <= 0) line = null;
                if (lineEnd <= 0) lineEnd = line;

                var op = string.IsNullOrEmpty(method) || method.Equals("ALL", StringComparison.OrdinalIgnoreCase) ? "ENDPOINT" : method.ToUpperInvariant();

                items.Add(new ArchitectureEvidenceItemDto
                {
                    Id = epId,
                    Kind = OntologyConstants.EvidenceKinds.Endpoint,
                    Operation = op,
                    Title = string.IsNullOrEmpty(route) ? epName : $"{op} {route}",
                    FilePath = path,
                    LineStart = line,
                    LineEnd = lineEnd,
                    Confidence = OntologyConstants.EvidenceConfidence.Recorded,
                    TargetSymbol = epName,
                    ServiceName = scope.Name,
                    Reason = $"HTTP/RPC endpoint exposed by '{scope.Name}'"
                });
            }
        }
        catch (Exception ex)
        {
            RecordEvidenceWarning(warnings, $"Service endpoint evidence query for '{scope.Name}'", ex);
        }

        // 2. EntryPoints (bootstrap / workers / CLI / UI components) in this service
        var entryQuery = """
            MATCH (ep:EntryPoint)
            RETURN ep.id AS id, ep.name AS name, coalesce(ep.file_path, ep.path) AS path,
                   coalesce(ep.start_line, ep.line) AS line, coalesce(ep.end_line, ep.line_end) AS line_end,
                   json_extract(ep.properties, '$.kind') AS ep_kind,
                   json_extract(ep.properties, '$.service') AS service
            """;
        try
        {
            var json = await db.ExecuteQueryAsync(entryQuery, null, ct);
            using var doc = JsonDocument.Parse(json);
            var entryCount = 0;
            foreach (var row in doc.RootElement.EnumerateArray())
            {
                var entryId = row.GetStringProp("id");
                var path = row.GetStringProp("path");
                if (string.IsNullOrEmpty(path) || !scope.Matches(path, row.GetStringProp("service"), entryId)) continue;

                var name = row.GetStringProp("name");
                int? line = row.GetNullableIntProp("line");
                int? lineEnd = row.GetNullableIntProp("line_end");
                if (line <= 0) line = null;
                if (lineEnd <= 0) lineEnd = line;

                items.Add(new ArchitectureEvidenceItemDto
                {
                    Id = entryId,
                    Kind = OntologyConstants.EvidenceKinds.EntryPoint,
                    Operation = "ENTRY",
                    Title = name,
                    FilePath = path,
                    LineStart = line,
                    LineEnd = lineEnd,
                    Confidence = OntologyConstants.EvidenceConfidence.Recorded,
                    CallerSymbol = name,
                    ServiceName = scope.Name,
                    Reason = $"Application/worker entry point in '{scope.Name}'"
                });
                if (++entryCount >= 25) break;
            }
        }
        catch (Exception ex)
        {
            RecordEvidenceWarning(warnings, $"Service entrypoint evidence query for '{scope.Name}'", ex);
        }

        // 3. ORM Entities (Table -[:ACCESSED_BY|DECLARED_IN]-> Type or legacy Type -[:PERSISTED_IN]-> Table) in this service
        var ormQuery = """
            MATCH (t:Type)-[r]-(tbl:Table)
            WHERE r.kind IN ['PERSISTED_IN', 'ACCESSED_BY', 'DECLARED_IN']
            RETURN t.id AS id, t.name AS name, coalesce(t.file_path, t.path) AS path,
                   coalesce(t.start_line, t.line) AS line, coalesce(t.end_line, t.line_end) AS line_end,
                   tbl.name AS tbl_name, json_extract(t.properties, '$.service') AS service
            """;
        try
        {
            var json = await db.ExecuteQueryAsync(ormQuery, null, ct);
            using var doc = JsonDocument.Parse(json);
            foreach (var row in doc.RootElement.EnumerateArray())
            {
                var typeId = row.GetStringProp("id");
                var path = row.GetStringProp("path");
                if (string.IsNullOrEmpty(path) || !scope.Matches(path, row.GetStringProp("service"), typeId)) continue;

                var name = row.GetStringProp("name");
                var tblName = row.GetStringProp("tbl_name");
                int? line = row.GetNullableIntProp("line");
                int? lineEnd = row.GetNullableIntProp("line_end");
                if (line <= 0) line = null;
                if (lineEnd <= 0) lineEnd = line;

                items.Add(new ArchitectureEvidenceItemDto
                {
                    Id = typeId,
                    Kind = OntologyConstants.EvidenceKinds.Orm,
                    Operation = "ORM",
                    Title = $"{name} → {tblName}",
                    FilePath = path,
                    LineStart = line,
                    LineEnd = lineEnd,
                    Confidence = OntologyConstants.EvidenceConfidence.Recorded,
                    CallerSymbol = name,
                    TargetSymbol = tblName,
                    ServiceName = scope.Name,
                    Reason = $"ORM entity '{name}' persisted in table '{tblName}'"
                });
            }
        }
        catch (Exception ex)
        {
            RecordEvidenceWarning(warnings, $"Service ORM entity evidence query for '{scope.Name}'", ex);
        }

        // 4. Primary project manifest / declaration fallback
        if (!string.IsNullOrEmpty(scope.Directory))
        {
            var manifestPath = serviceNode.Path ?? scope.Directory;
            if (!manifestPath.Contains('.')) manifestPath = $"{manifestPath.TrimEnd('/')}/package.json";
            items.Add(new ArchitectureEvidenceItemDto
            {
                Id = serviceNode.Id,
                Kind = OntologyConstants.EvidenceKinds.Config,
                Operation = "MANIFEST",
                Title = $"{scope.Name} ({serviceNode.Label})",
                FilePath = manifestPath,
                LineStart = null,
                LineEnd = null,
                Confidence = OntologyConstants.EvidenceConfidence.Recorded,
                ServiceName = scope.Name,
                Reason = $"Project / service manifest for '{scope.Name}'"
            });
        }

        return items;
    }
}
