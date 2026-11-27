using CodeExplorer.Core.Gates.Engine;
using CodeExplorer.Core.Gates.Storage;

namespace CodeExplorer.Core.Mcp;

internal static partial class McpContextPackBuilder
{
    private const int MaxContextTablesCap = 4;
    private const int MaxOwnerMethodsCap = 7;
    private const int MaxOtherAccessorsCap = 3;
    private const int MaxLegacyTrapsCap = 6;
    private const string ColTableName = "tableName";
    private const string ColAccessKind = "accessKind";
    private const string AccessEntity = "ENTITY";
    private const string AccessWriter = "WRITER";
    private const string AccessReader = "READER";

    private const string FindTablesByTermQuery =
        "MATCH (t:Table) WHERE t.name IS NOT NULL AND toLower(t.name) CONTAINS toLower($term) RETURN t.id AS id LIMIT 6";

    private const string FindTablesTouchedByFunctionsPrefix =
        "MATCH (fn:Function)-[:READS_FROM|WRITES_TO]->(t:Table) WHERE fn.id IN [";

    private const string FindTablesTouchedByFunctionsSuffix =
        "] RETURN DISTINCT t.id AS id LIMIT 8";

    internal sealed record TablesOwnershipResult(
        List<object> Tables,
        List<(string Path, string Reason)> OwnerFiles,
        HashSet<string> CanonicalOwnerMethodIds,
        HashSet<string> CanonicalOwnerNames);

    private static async Task<TablesOwnershipResult> BuildTablesAndCanonicalOwnersAsync(
        IGraphClient client,
        List<string> concepts,
        List<string> domainTokens,
        HashSet<string> allFnIds,
        HashSet<string> subgraphFiles,
        string? workspaceRoot,
        CancellationToken ct)
    {
        var tableIds = new HashSet<string>(StringComparer.Ordinal);
        if (allFnIds.Count > 0)
        {
            var touched = await client.ExecuteCypherAsync(FindTablesTouchedByFunctionsPrefix + BuildIdList(allFnIds.Take(40)) + FindTablesTouchedByFunctionsSuffix, null, ct);
            foreach (var r in touched.Rows)
            {
                var id = r.GetStringProp(ColId);
                if (!string.IsNullOrEmpty(id)) tableIds.Add(id);
            }
        }

        foreach (var term in concepts.Concat(domainTokens.Where(t => t.Length >= 4)).Distinct(StringComparer.OrdinalIgnoreCase).Take(6))
        {
            if (tableIds.Count >= MaxContextTablesCap) break;
            var res = await client.ExecuteCypherAsync(FindTablesByTermQuery, new Dictionary<string, string> { ["term"] = term }, ct);
            foreach (var r in res.Rows)
            {
                var id = r.GetStringProp(ColId);
                if (!string.IsNullOrEmpty(id)) tableIds.Add(id);
            }
        }

        if (tableIds.Count == 0)
        {
            return new TablesOwnershipResult([], [], [], []);
        }

        var idLiteral = BuildIdList(tableIds.Take(MaxContextTablesCap));
        var lineageRes = await client.ExecuteCypherAsync(BuildTableOwnershipUnionQuery(idLiteral), null, ct);

        var tables = new List<object>();
        var ownerFiles = new List<(string Path, string Reason)>();
        var ownerMethodIds = new HashSet<string>(StringComparer.Ordinal);
        var ownerNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (var grp in lineageRes.Rows.GroupBy(r => r.GetStringProp(ColTableName), StringComparer.Ordinal))
        {
            if (string.IsNullOrEmpty(grp.Key)) continue;
            var summary = SummarizeSingleTableOwnership(grp.Key, grp.ToList(), subgraphFiles, ownerFiles, ownerMethodIds, ownerNames, workspaceRoot);
            if (summary != null)
            {
                tables.Add(summary);
                if (tables.Count >= MaxContextTablesCap) break;
            }
        }

        return new TablesOwnershipResult(tables, ownerFiles, ownerMethodIds, ownerNames);
    }

    private static string BuildTableOwnershipUnionQuery(string idLiteral)
    {
        var anchor = $"MATCH (t:Table) WHERE t.id IN [{idLiteral}] ";
        return anchor + "OPTIONAL MATCH (t)-[:IMPLEMENTED_BY]->(e:Type) " +
               "RETURN t.name AS tableName, 'ENTITY' AS accessKind, '' AS id, coalesce(e.name, '') AS name, '' AS ownerName, coalesce(e.file_path, e.path, '') AS filePath, 0 AS startLine UNION " +
               anchor + "OPTIONAL MATCH (t)<-[r:WRITES_TO]-(fn:Function) OPTIONAL MATCH (fn)<-[:HAS_METHOD]-(owner:Type) " +
               "RETURN t.name AS tableName, 'WRITER' AS accessKind, coalesce(fn.id, '') AS id, coalesce(fn.name, '') AS name, coalesce(owner.name, '') AS ownerName, coalesce(fn.file_path, fn.path, r.file_path, '') AS filePath, coalesce(fn.start_line, 0) AS startLine UNION " +
               anchor + "OPTIONAL MATCH (t)<-[r:READS_FROM]-(fn:Function) OPTIONAL MATCH (fn)<-[:HAS_METHOD]-(owner:Type) " +
               "RETURN t.name AS tableName, 'READER' AS accessKind, coalesce(fn.id, '') AS id, coalesce(fn.name, '') AS name, coalesce(owner.name, '') AS ownerName, coalesce(fn.file_path, fn.path, r.file_path, '') AS filePath, coalesce(fn.start_line, 0) AS startLine";
    }

    private static object? SummarizeSingleTableOwnership(
        string tableName,
        List<CypherRow> rows,
        HashSet<string> subgraphFiles,
        List<(string Path, string Reason)> ownerFiles,
        HashSet<string> ownerMethodIds,
        HashSet<string> ownerNames,
        string? workspaceRoot)
    {
        var entityPath = rows
            .Where(r => r.GetStringProp(ColAccessKind) == AccessEntity && !string.IsNullOrEmpty(r.GetStringProp(ColName)))
            .Select(r => McpResultFormatter.CleanRelativePath(r.GetStringProp(ColFilePath), workspaceRoot))
            .FirstOrDefault() ?? string.Empty;
        var entityDir = GetParentModulePrefix(entityPath);

        var accessGroups = rows
            .Where(r => r.GetStringProp(ColAccessKind) is AccessWriter or AccessReader)
            .Select(r => (
                Access: r.GetStringProp(ColAccessKind),
                FnId: r.GetStringProp(ColId),
                FnName: r.GetStringProp(ColName),
                Owner: r.GetStringProp(ColOwnerName),
                Path: McpResultFormatter.CleanRelativePath(r.GetStringProp(ColFilePath), workspaceRoot),
                Line: r.GetLongProp(ColStartLine)))
            .Where(x => !string.IsNullOrEmpty(x.FnName) && !IsNoiseOrTestPath(x.Path, x.FnName))
            .GroupBy(x => !string.IsNullOrEmpty(x.Owner) ? x.Owner : x.Path, StringComparer.Ordinal)
            .Select(g => ScoreOwnerGroup(tableName, entityDir, g.Key, g.ToList()))
            .OrderByDescending(x => x.Score)
            .ToList();

        var canonical = accessGroups.FirstOrDefault();
        if (canonical == null || canonical.Score <= 0)
        {
            return null;
        }

        ownerNames.Add(canonical.OwnerKey);

        var canonicalLabel = !string.IsNullOrEmpty(canonical.FilePath) ? $"{canonical.OwnerKey} ({canonical.FilePath})" : canonical.OwnerKey;
        foreach (var mid in canonical.MethodIds)
        {
            ownerMethodIds.Add(mid);
        }
        if (!string.IsNullOrEmpty(canonical.FilePath))
        {
            subgraphFiles.Add(canonical.FilePath);
            ownerFiles.Add((canonical.FilePath, $"Canonical owner of table '{tableName}': {canonical.OwnerKey}"));
        }

        var others = accessGroups
            .Skip(1)
            .Take(MaxOtherAccessorsCap)
            .Select(x => !string.IsNullOrEmpty(x.FilePath) ? $"{x.OwnerKey} ({x.FilePath})" : x.OwnerKey)
            .ToList();

        return new
        {
            table = tableName,
            canonicalOwner = canonicalLabel,
            ownerMethods = canonical.Methods,
            otherDirectAccessors = others
        };
    }

    private sealed record ScoredOwnerGroup(string OwnerKey, string FilePath, int Score, List<string> Methods, List<string> MethodIds);

    private static ScoredOwnerGroup ScoreOwnerGroup(
        string tableName,
        string entityDir,
        string ownerKey,
        List<(string Access, string FnId, string FnName, string Owner, string Path, long Line)> items)
    {
        var firstPath = items.Select(i => i.Path).FirstOrDefault(p => !string.IsNullOrEmpty(p)) ?? string.Empty;
        var writes = items.Where(i => i.Access == AccessWriter).Select(i => i.FnName).Distinct(StringComparer.Ordinal).Count();
        var reads = items.Where(i => i.Access == AccessReader).Select(i => i.FnName).Distinct(StringComparer.Ordinal).Count();

        var score = writes * 4 + reads * 2;
        if (ownerKey.EndsWith("Service", StringComparison.Ordinal) || ownerKey.EndsWith("Repository", StringComparison.Ordinal))
        {
            score += 5;
        }
        if (ownerKey.EndsWith("Controller", StringComparison.Ordinal) || ownerKey.EndsWith("Resolver", StringComparison.Ordinal))
        {
            score -= 8;
        }
        if (ownerKey.Contains(tableName, StringComparison.OrdinalIgnoreCase))
        {
            score += 8;
        }
        if (!string.IsNullOrEmpty(entityDir) && firstPath.StartsWith(entityDir, StringComparison.OrdinalIgnoreCase))
        {
            score += 6;
        }

        var methods = items
            .GroupBy(i => i.FnName, StringComparer.Ordinal)
            .OrderBy(g => g.Min(x => x.Line))
            .Take(MaxOwnerMethodsCap)
            .Select(g =>
            {
                var hasW = g.Any(x => x.Access == AccessWriter);
                var hasR = g.Any(x => x.Access == AccessReader);
                var tag = hasW && hasR ? "RW" : (hasW ? "W" : "R");
                var ln = g.Min(x => x.Line);
                return ln > 0 ? $"{g.Key} [{tag}] (L{ln})" : $"{g.Key} [{tag}]";
            })
            .ToList();

        var methodIds = items.Select(i => i.FnId).Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal).ToList();
        return new ScoredOwnerGroup(ownerKey, firstPath, score, methods, methodIds);
    }

    private static string GetParentModulePrefix(string relPath)
    {
        if (string.IsNullOrEmpty(relPath)) return string.Empty;
        var norm = relPath.Replace('\\', '/');
        var idx = norm.LastIndexOf('/');
        if (idx <= 0) return string.Empty;
        var parent = norm[..idx];
        var grandIdx = parent.LastIndexOf('/');
        return grandIdx > 0 ? parent[..grandIdx] : parent;
    }

    private static async Task<object?> BuildScopedGuardrailsAsync(
        IGraphClient client,
        string workspaceRoot,
        HashSet<string> subgraphFiles,
        CancellationToken ct)
    {
        var gateRepo = new FileGateRepository(workspaceRoot);
        var gates = await gateRepo.GetAllGatesAsync(ct);
        if (gates.Count == 0)
        {
            return null;
        }

        var engine = new GateExecutionEngine(client, gateRepo);
        var suite = await engine.CheckSuiteAsync(cancellationToken: ct);

        var activeRules = suite.Results
            .Select(r => new
            {
                id = r.Gate.Id,
                status = r.Status.ToString(),
                rule = r.Gate.Name,
                remediation = r.Gate.Remediation ?? r.Gate.Description ?? string.Empty
            })
            .ToList();

        var groupedTraps = new Dictionary<(string File, string GateId), List<string>>();
        foreach (var r in suite.Results)
        {
            foreach (var tv in r.ToleratedViolations)
            {
                var rel = McpResultFormatter.CleanRelativePath(tv.File, workspaceRoot);
                if (string.IsNullOrEmpty(rel) || !subgraphFiles.Contains(rel)) continue;
                var key = (rel, r.Gate.Id);
                if (!groupedTraps.TryGetValue(key, out var syms))
                {
                    syms = [];
                    groupedTraps[key] = syms;
                }
                var sym = tv.Symbol ?? string.Empty;
                if (!string.IsNullOrEmpty(sym) && !syms.Contains(sym, StringComparer.Ordinal) && syms.Count < 4)
                {
                    syms.Add(sym);
                }
            }
        }

        var legacyTraps = groupedTraps
            .Take(MaxLegacyTrapsCap)
            .Select(kv => new
            {
                file = kv.Key.File,
                gateId = kv.Key.GateId,
                legacySymbols = kv.Value,
                note = "TOLERATED LEGACY DEBT: do NOT imitate this pattern in new/modified methods; follow activeRules remediation."
            })
            .ToList();

        return new
        {
            suiteStatus = suite.FailedCount > 0 ? "failed" : "passed",
            newViolationsCount = suite.NewViolationsCount,
            activeRules,
            legacyTrapsInSubgraphFiles = legacyTraps
        };
    }
}
