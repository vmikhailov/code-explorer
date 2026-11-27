namespace CodeExplorer.Core.Mcp;

internal static partial class McpContextPackBuilder
{
    private const int MaxSearchProbes = 10;
    private const int SearchProbeLimit = 30;
    private const int MaxMethodsPerType = 10;
    private const int MaxSideEffectsCap = 10;
    private const int MaxInboundCallersCap = 8;
    private const int MaxReadPlanCap = 6;
    private const string ColId = "id";
    private const string ColName = "name";
    private const string ColOwnerId = "ownerId";
    private const string ColOwnerName = "ownerName";
    private const string ColOwnerPath = "ownerPath";
    private const string ColFilePath = "filePath";
    private const string ColStartLine = "startLine";
    private const string ColEndLine = "endLine";
    private const string ColCalleeName = "calleeName";
    private const string ColCalleeOwner = "calleeOwner";
    private const string ColRelName = "relName";
    private const string ColCallerName = "callerName";
    private const string ColCallerOwner = "callerOwner";
    private const string ColRoute = "route";
    private const string ColHttpMethod = "httpMethod";
    private const string ColNodeLabel = "nodeLabel";
    private const string ColHandlerId = "handlerId";
    private const string ColHandlerName = "handlerName";
    private const string SingleQuote = "'";
    private const string EscapedSingleQuote = "''";
    private const string CommaSep = ", ";

    private const string FallbackSymbolSearchQuery =
        "MATCH (n) WHERE (n:Type OR n:Function OR n:Endpoint OR n:Table) " +
        "AND n.name IS NOT NULL AND toLower(n.name) CONTAINS toLower($term) " +
        "OPTIONAL MATCH (n)-[:DECLARED_IN]->(f:File) " +
        "RETURN n.id AS id, n.name AS name, labels(n)[0] AS nodeLabel, " +
        "coalesce(f.path, n.file_path, n.path, '') AS filePath, coalesce(n.start_line, 0) AS startLine LIMIT 25";

    private const string RouteEndpointSearchQuery =
        "MATCH (ep:Endpoint) WHERE coalesce(ep.route_template, ep.route) IS NOT NULL AND toLower(coalesce(ep.route_template, ep.route)) CONTAINS toLower($term) " +
        "OPTIONAL MATCH (ep)-[:TRIGGERS|CALLS|IMPLEMENTED_BY]->(fn:Function) " +
        "OPTIONAL MATCH (fn)<-[:HAS_METHOD]-(owner:Type) " +
        "RETURN ep.id AS id, coalesce(ep.http_method, 'HTTP') + ' ' + coalesce(ep.route_template, ep.route) AS name, " +
        "coalesce(owner.name + '.' + fn.name, fn.name, '') AS handlerName, " +
        "coalesce(fn.id, '') AS handlerId, coalesce(owner.id, '') AS ownerId, " +
        "coalesce(fn.file_path, ep.file_path, ep.path, '') AS filePath, coalesce(fn.start_line, ep.start_line, 0) AS startLine LIMIT 10";

    private const string ResolveFunctionOwnersPrefix =
        "MATCH (fn:Function) WHERE fn.id IN [";

    private const string ResolveFunctionOwnersSuffix =
        "] OPTIONAL MATCH (owner:Type)-[:HAS_METHOD]->(fn) " +
        "OPTIONAL MATCH (fn)-[:DECLARED_IN]->(f:File) " +
        "RETURN fn.id AS id, fn.name AS name, coalesce(owner.id, '') AS ownerId, " +
        "coalesce(owner.name, '') AS ownerName, coalesce(owner.file_path, owner.path, '') AS ownerPath, " +
        "coalesce(f.path, fn.file_path, fn.path, '') AS filePath, " +
        "coalesce(fn.start_line, 0) AS startLine, coalesce(fn.end_line, 0) AS endLine";

    private const string ExpandTypeMethodsPrefix =
        "MATCH (t:Type)-[:HAS_METHOD]->(fn:Function) WHERE t.id IN [";

    private const string ExpandTypeMethodsSuffix =
        "] OPTIONAL MATCH (fn)-[:DECLARED_IN]->(f:File) " +
        "RETURN t.id AS ownerId, t.name AS ownerName, fn.id AS id, fn.name AS name, " +
        "coalesce(f.path, fn.file_path, t.file_path, t.path, '') AS filePath, " +
        "coalesce(fn.start_line, 0) AS startLine, coalesce(fn.end_line, 0) AS endLine";

    private const string ExpandOutboundSideEffectsPrefix =
        "MATCH (fn:Function)-[r:CALLS|PUBLISHES_TO|TRIGGERS|CALLS_ENDPOINT|SERVICE_CALL]->(callee) WHERE fn.id IN [";

    private const string ExpandOutboundSideEffectsSuffix =
        "] OPTIONAL MATCH (owner:Type)-[:HAS_METHOD]->(fn) " +
        "OPTIONAL MATCH (calleeOwner:Type)-[:HAS_METHOD]->(callee) " +
        "OPTIONAL MATCH (callee)-[:DECLARED_IN]->(cf:File) " +
        "WHERE calleeOwner IS NULL OR owner IS NULL OR calleeOwner.id <> owner.id " +
        "RETURN coalesce(owner.name + '.' + fn.name, fn.name) AS callerName, " +
        "coalesce(calleeOwner.name, '') AS calleeOwner, coalesce(callee.name, '') AS calleeName, " +
        "type(r) AS relName, coalesce(callee.file_path, callee.path, cf.path, calleeOwner.file_path, calleeOwner.path, '') AS filePath, " +
        "coalesce(callee.start_line, 0) AS startLine LIMIT 60";

    private const string ExpandInboundCallersPrefix =
        "MATCH (target)<-[r:CALLS|TRIGGERS|SERVICE_CALL]-(caller) WHERE target.id IN [";

    private const string ExpandInboundCallersSuffix =
        "] OPTIONAL MATCH (targetOwner:Type)-[:HAS_METHOD]->(target) " +
        "OPTIONAL MATCH (callerOwner:Type)-[:HAS_METHOD]->(caller) " +
        "OPTIONAL MATCH (caller)-[:DECLARED_IN]->(cf:File) " +
        "WHERE callerOwner IS NULL OR targetOwner IS NULL OR callerOwner.id <> targetOwner.id " +
        "RETURN coalesce(targetOwner.name + '.' + target.name, target.name) AS calleeName, " +
        "coalesce(callerOwner.name, '') AS callerOwner, " +
        "CASE WHEN caller:Endpoint THEN coalesce(caller.http_method, 'HTTP') + ' ' + coalesce(caller.route_template, caller.route, caller.name) ELSE coalesce(caller.name, '') END AS callerName, " +
        "type(r) AS relName, coalesce(caller.file_path, caller.path, cf.path, callerOwner.file_path, callerOwner.path, '') AS filePath, " +
        "coalesce(caller.start_line, 0) AS startLine LIMIT 50";

    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "the", "and", "for", "with", "from", "into", "when", "after", "before", "should", "must",
        "ensure", "make", "sure", "all", "any", "that", "this", "add", "implement", "fix", "update",
        "support", "handle", "create", "new", "existing", "only", "also", "without", "across", "via",
        "return", "returns", "method", "function", "service", "class", "file", "code", "workspace",
        "an", "is", "are", "was", "were", "be", "been", "by", "on", "in", "to", "of", "or", "as", "at",
        "up", "out", "off", "clean", "cleanup", "properly", "associated", "related"
    };

    private static readonly HashSet<string> ActionVerbs = new(StringComparer.OrdinalIgnoreCase)
    {
        "revoke", "revoked", "delete", "deleted", "remove", "removed", "suspend", "suspended",
        "register", "upgrade", "trigger", "sync", "enable", "disable", "reset", "validate"
    };

    internal sealed record AnchorCandidate(
        string NodeId,
        string Name,
        string FullName,
        NodeKind NodeLabel,
        string RelativePath,
        int StartLine,
        double FinalScore,
        string MatchedConcept);

    public static async Task<string> BuildTaskContextYamlAsync(
        IGraphClient client,
        string query,
        int maxAnchors,
        bool includeGates,
        string? workspaceRoot,
        CancellationToken ct)
    {
        var trimmed = query?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(trimmed))
        {
            return ToonYamlSerializer.SerializeObject(new { error = "Non-empty task query is required." });
        }

        var clampedAnchors = Math.Clamp(maxAnchors, 3, 15);
        var (concepts, domainTokens) = ExtractSearchConcepts(trimmed);
        var anchors = await ResolveDiversifiedAnchorsAsync(client, trimmed, concepts, domainTokens, clampedAnchors, workspaceRoot, ct);

        var fnIds = anchors.Where(a => a.NodeLabel == NodeKind.Function).Select(a => a.NodeId).ToHashSet(StringComparer.Ordinal);
        var typeIds = anchors.Where(a => a.NodeLabel == NodeKind.Type).Select(a => a.NodeId).ToHashSet(StringComparer.Ordinal);
        var subgraphFiles = anchors.Select(a => a.RelativePath).Where(p => !string.IsNullOrEmpty(p)).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var resolvedFns = await ExpandAnchorFunctionsAndTypesAsync(client, fnIds, typeIds, subgraphFiles, workspaceRoot, ct);
        var tablesOwnership = await BuildTablesAndCanonicalOwnersAsync(client, concepts, domainTokens, resolvedFns.AllFunctionIds, subgraphFiles, workspaceRoot, ct);
        resolvedFns.AllFunctionIds.UnionWith(tablesOwnership.CanonicalOwnerMethodIds);
        var candidateServices = new HashSet<string>(resolvedFns.ServiceTypeNames, StringComparer.Ordinal);
        candidateServices.UnionWith(tablesOwnership.CanonicalOwnerNames);
        var moduleWiring = await BuildModuleWiringAndCycleSafetyAsync(client, candidateServices, subgraphFiles, workspaceRoot, ct);
        var outboundEffects = await ExpandOutboundEffectsAsync(client, resolvedFns.AllFunctionIds, subgraphFiles, workspaceRoot, ct);
        var inboundCallers = await ExpandInboundCallersAsync(client, resolvedFns.AllTargetIds, subgraphFiles, workspaceRoot, ct);
        var guardrails = includeGates && !string.IsNullOrEmpty(workspaceRoot)
            ? await BuildScopedGuardrailsAsync(client, workspaceRoot, subgraphFiles, ct)
            : null;
        var readPlan = BuildSurgicalReadPlan(anchors, resolvedFns.TypeOutlines, tablesOwnership.OwnerFiles);

        return ToonYamlSerializer.SerializeObject(new
        {
            query = trimmed,
            anchors = anchors.Select(a => new
            {
                symbol = a.FullName,
                kind = a.NodeLabel.ToString(),
                location = a.StartLine > 0 ? $"{a.RelativePath}:{a.StartLine}" : a.RelativePath,
                concept = a.MatchedConcept
            }).ToList(),
            serviceMethods = resolvedFns.TypeOutlines,
            outboundSideEffects = outboundEffects,
            inboundCallersAndRoutes = inboundCallers,
            tablesAndCanonicalOwners = tablesOwnership.Tables,
            moduleWiringAndCycleSafety = moduleWiring,
            guardrails,
            readPlan
        });
    }

    internal static (List<string> Concepts, List<string> DomainTokens) ExtractSearchConcepts(string rawQuery)
    {
        var allWords = IdentifierVectorEncoder.Tokenize(rawQuery);
        var domainTokens = allWords
            .Where(w => w.Length >= 2 && !StopWords.Contains(w))
            .Select(NormalizeSingularStem)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var nounTokens = domainTokens.Where(t => !ActionVerbs.Contains(t)).ToList();
        var concepts = new List<string>();
        for (var i = 0; i < nounTokens.Count - 1; i++)
        {
            var t1 = nounTokens[i].ToLowerInvariant();
            var t2 = nounTokens[i + 1];
            var camel = t1 + char.ToUpperInvariant(t2[0]) + (t2.Length > 1 ? t2[1..].ToLowerInvariant() : string.Empty);
            if (!concepts.Contains(camel, StringComparer.OrdinalIgnoreCase))
            {
                concepts.Add(camel);
            }
        }

        foreach (var tok in domainTokens)
        {
            if (tok.Length >= 3 && !concepts.Contains(tok, StringComparer.OrdinalIgnoreCase))
            {
                concepts.Add(tok);
            }
        }

        return (concepts, domainTokens);
    }

    private static readonly string[] NonSingularizableSuffixes = ["ss", "us", "is", "rics", "tics"];
    private const string SuffixIes = "ies";
    private const string SuffixY = "y";
    private const string SuffixS = "s";

    private static string NormalizeSingularStem(string word)
    {
        if (word.Length <= 4) return word;
        if (word.EndsWith(SuffixIes, StringComparison.OrdinalIgnoreCase) && word.Length > 5)
        {
            return word[..^3] + SuffixY;
        }
        if (word.EndsWith(SuffixS, StringComparison.OrdinalIgnoreCase) && !EndsWithAnyCaseInsensitive(word, NonSingularizableSuffixes))
        {
            return word[..^1];
        }
        return word;
    }

    private static bool EndsWithAnyCaseInsensitive(string word, string[] suffixes)
    {
        foreach (var s in suffixes)
        {
            if (word.EndsWith(s, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    private static async Task<List<AnchorCandidate>> ResolveDiversifiedAnchorsAsync(
        IGraphClient client,
        string fullQuery,
        List<string> concepts,
        List<string> domainTokens,
        int maxAnchors,
        string? workspaceRoot,
        CancellationToken ct)
    {
        var rawMatches = new Dictionary<string, (IdentifierMatch Match, string Concept)>(StringComparer.Ordinal);
        var probes = new List<string> { fullQuery };
        probes.AddRange(concepts.Take(MaxSearchProbes - 1));

        foreach (var probe in probes.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var found = await ProbeGraphSymbolsAsync(client, probe, ct);
            foreach (var m in found)
            {
                if (string.IsNullOrEmpty(m.NodeId)) continue;
                if (!rawMatches.TryGetValue(m.NodeId, out var prev) || m.Score > prev.Match.Score)
                {
                    rawMatches[m.NodeId] = (m, probe == fullQuery ? InferBestConcept(m, concepts) : probe);
                }
            }
        }

        await ProbeEndpointRoutesAsync(client, fullQuery, domainTokens, rawMatches, ct);

        var scored = rawMatches.Values
            .Select(item => ScoreCandidate(item.Match, item.Concept, concepts, domainTokens, workspaceRoot))
            .Where(c => c.FinalScore > 0.2)
            .OrderByDescending(c => c.FinalScore)
            .ToList();

        return DiversifyByConcept(scored, maxAnchors);
    }

    private static async Task<IReadOnlyList<IdentifierMatch>> ProbeGraphSymbolsAsync(
        IGraphClient client,
        string probe,
        CancellationToken ct)
    {
        if (client.SupportsIdentifierSearch)
        {
            return await client.SearchIdentifiersAsync(probe, null, SearchProbeLimit, ct);
        }

        var p = new Dictionary<string, string> { ["term"] = probe };
        var res = await client.ExecuteCypherAsync(FallbackSymbolSearchQuery, p, ct);
        return res.Rows.Select(r => new IdentifierMatch(
            r.GetStringProp(ColId),
            r.GetStringProp(ColName),
            r.GetStringProp(ColName),
            r.GetStringProp(ColNodeLabel),
            r.GetStringProp(ColFilePath),
            (int)r.GetLongProp(ColStartLine),
            0.85f)).ToList();
    }

    private static async Task ProbeEndpointRoutesAsync(
        IGraphClient client,
        string fullQuery,
        List<string> domainTokens,
        Dictionary<string, (IdentifierMatch Match, string Concept)> rawMatches,
        CancellationToken ct)
    {
        var routeTerm = fullQuery.Contains('/', StringComparison.Ordinal)
            ? ExtractRouteFragment(fullQuery)
            : domainTokens.FirstOrDefault(t => t.Length >= 4);
        if (string.IsNullOrEmpty(routeTerm))
        {
            return;
        }

        var res = await client.ExecuteCypherAsync(RouteEndpointSearchQuery, new Dictionary<string, string> { ["term"] = routeTerm }, ct);
        foreach (var r in res.Rows)
        {
            var id = r.GetStringProp(ColId);
            if (string.IsNullOrEmpty(id)) continue;
            var epMatch = new IdentifierMatch(id, r.GetStringProp(ColName), r.GetStringProp(ColName), NodeKind.Endpoint, r.GetStringProp(ColFilePath), (int)r.GetLongProp(ColStartLine), 1.1f);
            rawMatches.TryAdd(id, (epMatch, routeTerm));

            var handlerId = r.GetStringProp(ColHandlerId);
            if (!string.IsNullOrEmpty(handlerId))
            {
                var hMatch = new IdentifierMatch(handlerId, r.GetStringProp(ColHandlerName), r.GetStringProp(ColHandlerName), NodeKind.Function, r.GetStringProp(ColFilePath), (int)r.GetLongProp(ColStartLine), 1.05f);
                rawMatches.TryAdd(handlerId, (hMatch, routeTerm));
            }
        }
    }

    private static string ExtractRouteFragment(string query)
    {
        foreach (var part in query.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (part.Contains('/', StringComparison.Ordinal))
            {
                var trimmed = part.Trim('`', '\'', '"', ',', '.', '(', ')');
                var colonIdx = trimmed.IndexOf(':');
                if (colonIdx > 1) trimmed = trimmed[..colonIdx];
                return trimmed.Trim('/');
            }
        }
        return string.Empty;
    }

    private static string InferBestConcept(IdentifierMatch m, List<string> concepts)
    {
        foreach (var c in concepts)
        {
            if (m.Name.Contains(c, StringComparison.OrdinalIgnoreCase) || m.FullName.Contains(c, StringComparison.OrdinalIgnoreCase))
            {
                return c;
            }
        }
        return concepts.FirstOrDefault() ?? m.Name;
    }
}
