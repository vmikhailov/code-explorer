using System.Text.Json;
using CodeExplorer.Core.Protocol;
using CodeExplorer.Cypher.Common;

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
            EdgeKind = EnumExtensions.ParseRelationshipKind(edgeKind)
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
            var res = await db.ExecuteCypherAsync(rolledQuery, null, ct);
            foreach (var row in res.Rows)
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
            var cRes = await db.ExecuteCypherAsync(contribQuery, null, ct);
            foreach (var row in cRes.Rows)
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
                var parsedEvKind = TaxonomyViewEnumExtensions.ParseEvidenceKind(relEvKind);
                var kind = parsedEvKind != EvidenceKind.Unspecified
                    ? parsedEvKind
                    : relKind switch
                    {
                        OntologyConstants.Relationships.CallsEndpoint or OntologyConstants.Relationships.CalledBy when effectiveResourceKind == OntologyConstants.NodeLabels.Endpoint =>
                            protocol.Equals("grpc", StringComparison.OrdinalIgnoreCase) ? EvidenceKind.GrpcCall
                            : protocol.Equals("wcf", StringComparison.OrdinalIgnoreCase) ? EvidenceKind.WcfCall
                            : EvidenceKind.HttpCall,
                        OntologyConstants.Relationships.PublishesTo or OntologyConstants.Relationships.PublishedBy =>
                            EvidenceKind.Publish,
                        OntologyConstants.Relationships.SubscribesTo or OntologyConstants.Relationships.SubscribedBy or OntologyConstants.Relationships.Triggers =>
                            EvidenceKind.Subscribe,
                        OntologyConstants.Relationships.PersistedIn =>
                            EvidenceKind.Orm,
                        OntologyConstants.Relationships.AccessedBy when tgtKind == OntologyConstants.NodeLabels.Type =>
                            EvidenceKind.Orm,
                        OntologyConstants.Relationships.Configures or OntologyConstants.Relationships.DeclaredIn when tgtKind == OntologyConstants.NodeLabels.File =>
                            EvidenceKind.Config,
                        _ when effectiveResourceKind == OntologyConstants.NodeLabels.Procedure =>
                            EvidenceKind.Procedure,
                        _ when effectiveResourceKind is OntologyConstants.NodeLabels.Database or OntologyConstants.NodeLabels.Table =>
                            EvidenceKind.Query,
                        _ => EvidenceKind.HttpCall
                    };

                var operation = !string.IsNullOrEmpty(relOp)
                    ? relOp
                    : kind switch
                    {
                        EvidenceKind.Publish => "PUBLISH",
                        EvidenceKind.Subscribe => "SUBSCRIBE",
                        EvidenceKind.Orm => "ORM",
                        EvidenceKind.Config => "CONFIG",
                        EvidenceKind.Procedure => "EXEC",
                        EvidenceKind.Query => InferSqlVerb(srcVerb, queryText, isDownwardResourceEdge ? tgtName : srcName) ?? "SELECT",
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
                        ? $"{(kind == EvidenceKind.Publish ? "Publishes" : "Subscribes")} {topicTargetName}"
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

    private static int EvidenceKindOrder(EvidenceKind kind) => kind switch
    {
        EvidenceKind.HttpCall or EvidenceKind.GrpcCall or EvidenceKind.WcfCall or EvidenceKind.Publish or EvidenceKind.Subscribe => 0,
        EvidenceKind.Query or EvidenceKind.Procedure => 1,
        EvidenceKind.Orm or EvidenceKind.Table => 2,
        EvidenceKind.Endpoint or EvidenceKind.EntryPoint => 3,
        EvidenceKind.LibraryCall or EvidenceKind.TestCall => 4,
        EvidenceKind.Config => 5,
        EvidenceKind.Migration => 6,
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
            var res = await db.ExecuteCypherAsync(query, null, ct);
            foreach (var row in res.Rows)
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

}
