namespace CodeExplorer.Core.Mcp;

internal static class McpImpactFormatter
{
    private const int MaxImpactRowsCap = 100;
    private const int MaxCallersRowsCap = 150;
    private const string ColId = "id";
    private const string ColDependentType = "dependentType";
    private const string ColDependentName = "dependentName";
    private const string ColDependentId = "dependentId";
    private const string ColRelationship = "relationship";
    private const string ColFilePath = "filePath";
    private const string ColTargetName = "targetName";
    private const string ColCallerName = "callerName";
    private const string ColRelKind = "relKind";
    private const string ColLocation = "location";
    private const string SingleQuote = "'";
    private const string EscapedSingleQuote = "''";

    private const string ExactTargetIdsQuery =
        "MATCH (target) WHERE (target:Type OR target:Function OR target:Project OR target:ExternalService OR target:Table OR target:Topic) " +
        "AND (target.name = $symbolName OR target.id = $symbolName OR target.symbol = $symbolName " +
        "OR target.graphql_field = $symbolName OR target.graphql_operation = $symbolName " +
        "OR target.name = ($symbolName + '.resolve')) " +
        "RETURN target.id AS id LIMIT 25";

    private const string FallbackContainsTargetIdsQuery =
        "MATCH (target) WHERE (target:Type OR target:Function OR target:Table OR target:Topic) " +
        "AND (target.symbol ENDS WITH $symbolName OR target.name CONTAINS $symbolName) " +
        "RETURN target.id AS id LIMIT 12";

    private const string DirectInboundPrefix =
        "MATCH (target)<-[r:USES_TYPE|OF_TYPE|CALLS|SERVICE_CALL|CALLS_ENDPOINT|IMPLEMENTS|INHERITS_FROM|PUBLISHES_TO|SUBSCRIBES_TO|TRIGGERS|QUERIED_BY|PERSISTED_IN|READS_FROM|WRITES_TO]-(dependent) " +
        "WHERE target.id IN [";

    private const string DirectInboundSuffix =
        "] OPTIONAL MATCH (dependent)<-[:HAS_METHOD|HAS_MEMBER]-(owner:Type) " +
        "OPTIONAL MATCH (dependent)-[:DECLARED_IN]->(f:File) " +
        "RETURN dependent.id AS dependentId, " +
        "CASE WHEN dependent:Member AND owner IS NOT NULL THEN 'Class' " +
        "WHEN dependent:Type THEN (CASE WHEN dependent.kind = 'class' THEN 'Class' WHEN dependent.kind = 'interface' THEN 'Interface' ELSE coalesce(dependent.kind, 'Type') END) " +
        "WHEN dependent:Function THEN 'Function' WHEN dependent:Project THEN 'Service' ELSE labels(dependent)[0] END AS dependentType, " +
        "CASE WHEN dependent:Member AND owner IS NOT NULL THEN owner.name " +
        "WHEN dependent:Function AND owner IS NOT NULL THEN owner.name + '.' + dependent.name ELSE dependent.name END AS dependentName, " +
        "CASE WHEN dependent:Member AND owner IS NOT NULL THEN 'INJECTS_TYPE' ELSE type(r) END AS relationship, " +
        "CASE WHEN coalesce(dependent.path, dependent.file_path, f.path, owner.file_path) IS NOT NULL AND dependent.start_line IS NOT NULL " +
        "THEN coalesce(dependent.path, dependent.file_path, f.path, owner.file_path) + ':' + toString(dependent.start_line) " +
        "ELSE coalesce(dependent.path, dependent.file_path, f.path, owner.file_path, '') END AS filePath";

    private const string TypeMethodCallersPrefix =
        "MATCH (target:Type)-[:HAS_METHOD]->(fn:Function)<-[:CALLS]-(caller:Function) " +
        "WHERE target.id IN [";

    private const string TypeMethodCallersSuffix =
        "] AND NOT (target)-[:HAS_METHOD]->(caller) " +
        "OPTIONAL MATCH (caller)<-[:HAS_METHOD]-(callerOwner:Type) " +
        "OPTIONAL MATCH (caller)-[:DECLARED_IN]->(f:File) " +
        "RETURN caller.id AS dependentId, 'Function' AS dependentType, " +
        "CASE WHEN callerOwner IS NOT NULL THEN callerOwner.name + '.' + caller.name ELSE caller.name END AS dependentName, " +
        "'CALLS_METHOD' AS relationship, " +
        "CASE WHEN coalesce(caller.path, caller.file_path, f.path) IS NOT NULL AND caller.start_line IS NOT NULL " +
        "THEN coalesce(caller.path, caller.file_path, f.path) + ':' + toString(caller.start_line) " +
        "ELSE coalesce(caller.path, caller.file_path, f.path, '') END AS filePath";

    private const string SecondHopCallersPrefix =
        "MATCH (hop1:Function)<-[:CALLS]-(hop2:Function) WHERE hop1.id IN [";

    private const string SecondHopCallersSuffix =
        "] OPTIONAL MATCH (hop2)<-[:HAS_METHOD]-(hop2Owner:Type) " +
        "OPTIONAL MATCH (hop2)-[:DECLARED_IN]->(f:File) " +
        "RETURN hop2.id AS dependentId, 'Function' AS dependentType, " +
        "CASE WHEN hop2Owner IS NOT NULL THEN hop2Owner.name + '.' + hop2.name ELSE hop2.name END AS dependentName, " +
        "'TRANSITIVE_CALLS' AS relationship, " +
        "CASE WHEN coalesce(hop2.path, hop2.file_path, f.path) IS NOT NULL AND hop2.start_line IS NOT NULL " +
        "THEN coalesce(hop2.path, hop2.file_path, f.path) + ':' + toString(hop2.start_line) " +
        "ELSE coalesce(hop2.path, hop2.file_path, f.path, '') END AS filePath LIMIT 60";

    private static readonly string[] ImpactColumns = ["dependentType", "dependentName", "relationship", "filePath"];

    public static async Task<string> AnalyzeCodeImpactYamlAsync(
        IGraphClient client,
        string symbolName,
        string? workspaceRoot,
        CancellationToken ct)
    {
        var targetIds = await ResolveImpactTargetIdsAsync(client, symbolName, ct);
        if (targetIds.Count == 0)
        {
            return "[]";
        }

        var idLiteral = BuildInListLiteral(targetIds);
        var directRes = await client.ExecuteCypherAsync(DirectInboundPrefix + idLiteral + DirectInboundSuffix, null, ct);
        var methodRes = await client.ExecuteCypherAsync(TypeMethodCallersPrefix + idLiteral + TypeMethodCallersSuffix, null, ct);

        var hop1FnIds = directRes.Rows.Concat(methodRes.Rows)
            .Where(r => r.GetStringProp(ColDependentType) == "Function")
            .Select(r => r.GetStringProp(ColDependentId))
            .Where(id => !string.IsNullOrEmpty(id) && !targetIds.Contains(id))
            .Distinct(StringComparer.Ordinal)
            .Take(25)
            .ToList();

        var hop2Rows = Array.Empty<CypherRow>();
        if (hop1FnIds.Count > 0)
        {
            var hop2Res = await client.ExecuteCypherAsync(SecondHopCallersPrefix + BuildInListLiteral(hop1FnIds) + SecondHopCallersSuffix, null, ct);
            hop2Rows = hop2Res.Rows.ToArray();
        }

        return FormatImpactRows(directRes.Rows.Concat(methodRes.Rows).Concat(hop2Rows), targetIds, workspaceRoot);
    }

    private static async Task<HashSet<string>> ResolveImpactTargetIdsAsync(
        IGraphClient client,
        string symbolName,
        CancellationToken ct)
    {
        var p = new Dictionary<string, string> { ["symbolName"] = symbolName };
        var exactRes = await client.ExecuteCypherAsync(ExactTargetIdsQuery, p, ct);
        var ids = exactRes.Rows.Select(r => r.GetStringProp(ColId)).Where(s => !string.IsNullOrEmpty(s)).ToHashSet(StringComparer.Ordinal);
        if (ids.Count > 0)
        {
            return ids;
        }

        if (client.SupportsIdentifierSearch)
        {
            try
            {
                var resolved = await client.ResolveIdentifierAsync(symbolName, cancellationToken: ct);
                if (resolved is { Score: >= 0.70f } && !string.IsNullOrEmpty(resolved.NodeId))
                {
                    ids.Add(resolved.NodeId);
                    return ids;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Fall back to bounded substring lookup when vector/trigram resolution fails
            }
        }

        var fallbackRes = await client.ExecuteCypherAsync(FallbackContainsTargetIdsQuery, p, ct);
        foreach (var r in fallbackRes.Rows)
        {
            var id = r.GetStringProp(ColId);
            if (!string.IsNullOrEmpty(id)) ids.Add(id);
        }
        return ids;
    }

    private static string FormatImpactRows(
        IEnumerable<CypherRow> rawRows,
        HashSet<string> targetIds,
        string? workspaceRoot)
    {
        var seen = new HashSet<(string Type, string Name, string Loc)>();
        var items = new List<(bool IsTransitive, bool IsTest, string Type, string Name, string Rel, string Loc)>();

        foreach (var r in rawRows)
        {
            var depId = r.GetStringProp(ColDependentId);
            if (!string.IsNullOrEmpty(depId) && targetIds.Contains(depId))
            {
                continue;
            }

            var depType = r.GetStringProp(ColDependentType);
            var depName = r.GetStringProp(ColDependentName);
            if (string.IsNullOrEmpty(depName)) continue;
            var rel = r.GetStringProp(ColRelationship);
            var loc = NormalizeLocationWithLine(r.GetStringProp(ColFilePath), workspaceRoot);

            if (!seen.Add((depType, depName, loc)))
            {
                continue;
            }

            var isTransitive = rel == "TRANSITIVE_CALLS";
            var isTest = loc.Contains(".spec.", StringComparison.OrdinalIgnoreCase)
                || loc.Contains(".test.", StringComparison.OrdinalIgnoreCase)
                || depName.StartsWith("Jest:", StringComparison.Ordinal);
            items.Add((isTransitive, isTest, depType, depName, rel, loc));
        }

        var sorted = items
            .OrderBy(i => i.IsTransitive ? 1 : 0)
            .ThenBy(i => i.IsTest ? 1 : 0)
            .ThenBy(i => i.Type, StringComparer.Ordinal)
            .ThenBy(i => i.Name, StringComparer.Ordinal)
            .ToList();

        var shown = sorted
            .Take(MaxImpactRowsCap)
            .Select(i => new CypherRow(ImpactColumns,
            [
                CypherValue.FromString(i.Type),
                CypherValue.FromString(i.Name),
                CypherValue.FromString(i.Rel),
                CypherValue.FromString(i.Loc)
            ]))
            .ToList();

        var yaml = ToonYamlSerializer.SerializeYaml(new CypherQueryResult(ImpactColumns, shown));
        return sorted.Count > MaxImpactRowsCap
            ? $"totalDependents: {sorted.Count}\nmoreDependents: {sorted.Count - shown.Count}\n{yaml}"
            : yaml;
    }

    public static string FormatCallersResult(CypherQueryResult res, string? workspaceRoot)
    {
        if (res.Count == 0)
        {
            return "[]";
        }

        var cols = res.Columns as string[] ?? res.Columns.ToArray();
        var locIdx = Array.IndexOf(cols, ColLocation);
        var seen = new HashSet<(string Target, string Caller, string Rel, string Loc)>();
        var deduped = new List<CypherRow>(res.Count);

        foreach (var row in res.Rows)
        {
            var rawLoc = row.GetStringProp(ColLocation);
            var normLoc = NormalizeLocationWithLine(rawLoc, workspaceRoot);
            var key = (
                row.GetStringProp(ColTargetName),
                row.GetStringProp(ColCallerName),
                row.GetStringProp(ColRelKind),
                normLoc);

            if (!seen.Add(key))
            {
                continue;
            }

            if (locIdx >= 0 && normLoc != rawLoc)
            {
                var vals = row.Values.ToArray();
                if (locIdx < vals.Length) vals[locIdx] = CypherValue.FromString(normLoc);
                deduped.Add(new CypherRow(cols, vals));
            }
            else
            {
                deduped.Add(row);
            }
        }

        var shown = deduped.Take(MaxCallersRowsCap).ToList();
        var yaml = ToonYamlSerializer.SerializeYaml(new CypherQueryResult(cols, shown));
        return deduped.Count > MaxCallersRowsCap
            ? $"totalCallers: {deduped.Count}\nmoreCallers: {deduped.Count - shown.Count}\n{yaml}"
            : yaml;
    }

    private static string NormalizeLocationWithLine(string? rawLoc, string? workspaceRoot)
    {
        if (string.IsNullOrWhiteSpace(rawLoc))
        {
            return string.Empty;
        }

        var lastColon = rawLoc.LastIndexOf(':');
        if (lastColon > 2 && int.TryParse(rawLoc[(lastColon + 1)..], out var lineNum))
        {
            var cleanFile = McpResultFormatter.CleanRelativePath(rawLoc[..lastColon], workspaceRoot);
            return string.IsNullOrEmpty(cleanFile) ? string.Empty : $"{cleanFile}:{lineNum}";
        }

        return McpResultFormatter.CleanRelativePath(rawLoc, workspaceRoot);
    }

    private static string BuildInListLiteral(IEnumerable<string> ids) =>
        string.Join(", ", ids.Select(id => SingleQuote + id.Replace(SingleQuote, EscapedSingleQuote) + SingleQuote));
}
