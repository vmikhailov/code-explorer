using CodeExplorer.Core.Analysis;
using CodeExplorer.Cypher.Common;

namespace CodeExplorer.Core.Mcp;

internal static partial class McpContextPackBuilder
{
    private static readonly string[] CoreRoleSuffixes = ["Service", "Controller", "Resolver", "Manager", "Processor", "Job", "Handler"];
    private static readonly string[] DataModelSuffixes = ["DTO", "Dto", "Input", "Args", "Exception", "Entity", "Module"];

    internal sealed record ExpandedFunctionsResult(
        HashSet<string> AllFunctionIds,
        HashSet<string> AllTargetIds,
        List<object> TypeOutlines);

    private static AnchorCandidate ScoreCandidate(
        IdentifierMatch m,
        string concept,
        List<string> concepts,
        List<string> domainTokens,
        string? workspaceRoot)
    {
        var relPath = McpResultFormatter.CleanRelativePath(m.Path, workspaceRoot);
        var cleanFull = McpResultFormatter.CleanSymbolDisplay(m.FullName, m.Name);
        double score = m.Score;

        foreach (var tok in domainTokens)
        {
            if (cleanFull.Contains(tok, StringComparison.OrdinalIgnoreCase)) score += 0.9;
            else if (relPath.Contains(tok, StringComparison.OrdinalIgnoreCase)) score += 0.4;
        }

        foreach (var comp in concepts)
        {
            if (comp.Length >= 5 && cleanFull.Contains(comp, StringComparison.OrdinalIgnoreCase))
            {
                score += 2.2;
            }
        }

        score += ComputeArchitecturalRoleBonus(m.Kind, m.Name, cleanFull, relPath);
        return new AnchorCandidate(m.NodeId, m.Name, cleanFull, m.Kind, relPath, m.StartLine, Math.Round(score, 3), concept);
    }

    private static double ComputeArchitecturalRoleBonus(NodeKind kind, string name, string fullName, string relPath)
    {
        if (IsNoiseOrTestPath(relPath, name))
        {
            return -6.0;
        }

        double bonus = kind switch
        {
            NodeKind.Endpoint => 2.5,
            NodeKind.Table or NodeKind.Topic => 2.2,
            NodeKind.Type => 1.8,
            NodeKind.Function => 1.5,
            NodeKind.Member => -1.5,
            _ => 0.0
        };

        if (EndsWithAny(name, CoreRoleSuffixes)
            || ContainsArchitecturalOwner(fullName))
        {
            bonus += 1.8;
        }
        else if (EndsWithAny(name, DataModelSuffixes))
        {
            bonus -= 0.9;
        }

        return bonus;
    }

    private static bool ContainsArchitecturalOwner(string fullName) =>
        fullName.Contains("Service.", StringComparison.Ordinal)
        || fullName.Contains("Controller.", StringComparison.Ordinal)
        || fullName.Contains("Resolver.", StringComparison.Ordinal)
        || fullName.Contains("Processor.", StringComparison.Ordinal);

    internal static bool IsNoiseOrTestPath(string path, string name) =>
        path.Contains(".spec.", StringComparison.OrdinalIgnoreCase)
        || path.Contains("integration-spec.", StringComparison.OrdinalIgnoreCase)
        || path.Contains(".test.", StringComparison.OrdinalIgnoreCase)
        || path.Contains("__tests__", StringComparison.OrdinalIgnoreCase)
        || path.Contains("__mocks__", StringComparison.OrdinalIgnoreCase)
        || path.Contains("migrations", StringComparison.OrdinalIgnoreCase)
        || path.Contains("/seeds/", StringComparison.OrdinalIgnoreCase)
        || path.Contains("/generated/", StringComparison.OrdinalIgnoreCase)
        || path.EndsWith(".graphql", StringComparison.OrdinalIgnoreCase)
        || name.StartsWith("Jest:", StringComparison.Ordinal)
        || name.StartsWith("Vitest:", StringComparison.Ordinal);

    private static bool EndsWithAny(string value, params string[] suffixes)
    {
        foreach (var s in suffixes)
        {
            if (value.EndsWith(s, StringComparison.Ordinal)) return true;
        }
        return false;
    }

    private static List<AnchorCandidate> DiversifyByConcept(List<AnchorCandidate> sorted, int maxAnchors)
    {
        var selected = new List<AnchorCandidate>(maxAnchors);
        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        var perConceptCount = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var c in sorted)
        {
            if (selected.Count >= maxAnchors) break;
            var count = perConceptCount.GetValueOrDefault(c.MatchedConcept, 0);
            if (count >= 2) continue;
            if (seenIds.Add(c.NodeId))
            {
                selected.Add(c);
                perConceptCount[c.MatchedConcept] = count + 1;
            }
        }

        foreach (var c in sorted)
        {
            if (selected.Count >= maxAnchors) break;
            if (seenIds.Add(c.NodeId))
            {
                selected.Add(c);
            }
        }

        return selected;
    }

    private static async Task<ExpandedFunctionsResult> ExpandAnchorFunctionsAndTypesAsync(
        IGraphClient client,
        HashSet<string> fnIds,
        HashSet<string> typeIds,
        HashSet<string> subgraphFiles,
        string? workspaceRoot,
        CancellationToken ct)
    {
        var allFnIds = new HashSet<string>(fnIds, StringComparer.Ordinal);
        var allTypeIds = new HashSet<string>(typeIds, StringComparer.Ordinal);

        if (fnIds.Count > 0)
        {
            var ownerRes = await client.ExecuteCypherAsync(ResolveFunctionOwnersPrefix + BuildIdList(fnIds) + ResolveFunctionOwnersSuffix, null, ct);
            foreach (var r in ownerRes.Rows)
            {
                var oid = r.GetStringProp(ColOwnerId);
                if (!string.IsNullOrEmpty(oid)) allTypeIds.Add(oid);
                var fp = McpResultFormatter.CleanRelativePath(r.GetStringProp(ColFilePath), workspaceRoot);
                if (!string.IsNullOrEmpty(fp)) subgraphFiles.Add(fp);
            }
        }

        var outlines = new List<object>();
        if (allTypeIds.Count > 0)
        {
            var methodsRes = await client.ExecuteCypherAsync(ExpandTypeMethodsPrefix + BuildIdList(allTypeIds) + ExpandTypeMethodsSuffix, null, ct);
            foreach (var grp in methodsRes.Rows.GroupBy(r => r.GetStringProp(ColOwnerName), StringComparer.Ordinal))
            {
                if (string.IsNullOrEmpty(grp.Key)) continue;
                var firstPath = McpResultFormatter.CleanRelativePath(grp.First().GetStringProp(ColFilePath), workspaceRoot);
                if (!string.IsNullOrEmpty(firstPath)) subgraphFiles.Add(firstPath);

                var methods = grp
                    .Where(m => !string.Equals(m.GetStringProp(ColName), "constructor", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(m => m.GetLongProp(ColStartLine))
                    .Take(MaxMethodsPerType)
                    .Select(m =>
                    {
                        var mid = m.GetStringProp(ColId);
                        if (!string.IsNullOrEmpty(mid)) allFnIds.Add(mid);
                        var sl = m.GetLongProp(ColStartLine);
                        var el = m.GetLongProp(ColEndLine);
                        return el > sl ? $"{m.GetStringProp(ColName)} (L{sl}-L{el})" : $"{m.GetStringProp(ColName)} (L{sl})";
                    })
                    .ToList();

                outlines.Add(new { serviceOrClass = grp.Key, filePath = firstPath, methods });
            }
        }

        var allTargets = new HashSet<string>(allFnIds, StringComparer.Ordinal);
        allTargets.UnionWith(allTypeIds);
        return new ExpandedFunctionsResult(allFnIds, allTargets, outlines);
    }

    private static async Task<List<string>> ExpandOutboundEffectsAsync(
        IGraphClient client,
        HashSet<string> allFnIds,
        HashSet<string> subgraphFiles,
        string? workspaceRoot,
        CancellationToken ct)
    {
        if (allFnIds.Count == 0) return [];
        var res = await client.ExecuteCypherAsync(ExpandOutboundSideEffectsPrefix + BuildIdList(allFnIds.Take(40)) + ExpandOutboundSideEffectsSuffix, null, ct);
        var items = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var r in res.Rows)
        {
            var caller = r.GetStringProp(ColCallerName);
            var calleeOwner = r.GetStringProp(ColCalleeOwner);
            var callee = r.GetStringProp(ColCalleeName);
            if (string.IsNullOrEmpty(callee) || string.IsNullOrEmpty(calleeOwner)) continue;
            if (callee is "constructor" or "log" or "warn" or "error" or "debug") continue;

            var fp = McpResultFormatter.CleanRelativePath(r.GetStringProp(ColFilePath), workspaceRoot);
            if (IsNoiseOrTestPath(fp, callee)) continue;
            if (!string.IsNullOrEmpty(fp)) subgraphFiles.Add(fp);

            var targetDisplay = !string.IsNullOrEmpty(calleeOwner) ? $"{calleeOwner}.{callee}" : callee;
            var line = r.GetLongProp(ColStartLine);
            var loc = !string.IsNullOrEmpty(fp) && line > 0 ? $" ({fp}:{line})" : string.Empty;
            var edge = $"{caller} -> {targetDisplay}{loc}";
            if (seen.Add(edge))
            {
                items.Add(edge);
                if (items.Count >= MaxSideEffectsCap) break;
            }
        }

        return items;
    }

    private static async Task<List<string>> ExpandInboundCallersAsync(
        IGraphClient client,
        HashSet<string> targetIds,
        HashSet<string> subgraphFiles,
        string? workspaceRoot,
        CancellationToken ct)
    {
        if (targetIds.Count == 0) return [];
        var res = await client.ExecuteCypherAsync(ExpandInboundCallersPrefix + BuildIdList(targetIds.Take(35)) + ExpandInboundCallersSuffix, null, ct);
        var items = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var r in res.Rows)
        {
            var callerOwner = r.GetStringProp(ColCallerOwner);
            var caller = r.GetStringProp(ColCallerName);
            var callee = r.GetStringProp(ColCalleeName);
            if (string.IsNullOrEmpty(caller)) continue;

            var fp = McpResultFormatter.CleanRelativePath(r.GetStringProp(ColFilePath), workspaceRoot);
            if (IsNoiseOrTestPath(fp, caller)) continue;
            if (!string.IsNullOrEmpty(fp)) subgraphFiles.Add(fp);

            var callerDisplay = !string.IsNullOrEmpty(callerOwner) ? $"{callerOwner}.{caller}" : caller;
            var line = r.GetLongProp(ColStartLine);
            var loc = !string.IsNullOrEmpty(fp) && line > 0 ? $" ({fp}:{line})" : string.Empty;
            var entry = $"{callerDisplay} -> {callee}{loc}";
            if (seen.Add(entry))
            {
                items.Add(entry);
                if (items.Count >= MaxInboundCallersCap) break;
            }
        }

        return items;
    }

    private static List<object> BuildSurgicalReadPlan(
        List<AnchorCandidate> anchors,
        List<object> typeOutlines,
        List<(string Path, string Reason)> ownerFiles)
    {
        var plan = new List<object>();
        var seenFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (path, reason) in ownerFiles)
        {
            if (!string.IsNullOrEmpty(path) && seenFiles.Add(path))
            {
                plan.Add(new { filePath = path, startLine = 1, reason });
                if (plan.Count >= MaxReadPlanCap) return plan;
            }
        }

        foreach (var a in anchors)
        {
            if (!string.IsNullOrEmpty(a.RelativePath) && seenFiles.Add(a.RelativePath))
            {
                plan.Add(new
                {
                    filePath = a.RelativePath,
                    startLine = Math.Max(1, a.StartLine),
                    reason = $"Anchor {a.NodeLabel}: {a.FullName}"
                });
                if (plan.Count >= MaxReadPlanCap) break;
            }
        }

        return plan;
    }

    internal static string BuildIdList(IEnumerable<string> ids) =>
        string.Join(CommaSep, ids.Select(id => $"'{id.Replace(SingleQuote, EscapedSingleQuote)}'"));
}
