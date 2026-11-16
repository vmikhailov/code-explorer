using System.Text;
using CodeExplorer.Core.Gates.Models;

namespace CodeExplorer.Core.Mcp;

internal static class McpGateFormatter
{
    private const int DefaultSummaryPreviewCap = 5;
    private const string StatusPassed = "passed";
    private const string StatusFailed = "failed";
    private const string StatusWarning = "warning";
    private const string TagNew = "NEW";
    private const string TagTolerated = "TOL";
    private const string UnknownGroupKey = "(unknown)";
    private const string GroupByCallee = "callee";
    private const string GroupByTarget = "target";
    private const string GroupBySymbol = "symbol";

    private static readonly string[] SuiteGateColumns = ["id", "status", "severity", "newViolations", "tolerated", "name", "description", "remediation"];
    private static readonly string[] ViolationColumns = ["location", "symbol", "target", "message"];
    private static readonly string[] ViolationWithStatusColumns = ["status", "location", "symbol", "target", "message"];
    private static readonly string[] GroupColumns = ["key", "count"];
    private static readonly string[] SuiteGroupColumns = ["key", "count", "gates"];

    public static string FormatSuiteResult(
        GateSuiteResult suite,
        bool includeTolerated,
        int limit,
        string? fileFilter,
        string? groupBy)
    {
        var effectiveLimit = Math.Clamp(limit, 1, 500);
        if (!string.IsNullOrWhiteSpace(groupBy))
        {
            return FormatSuiteGrouped(suite, includeTolerated, effectiveLimit, fileFilter, groupBy.Trim().ToLowerInvariant());
        }

        var sb = new StringBuilder();
        AppendSuiteSummaryHeader(sb, suite);
        AppendSuiteGatesTable(sb, suite.Results);
        AppendSuiteActionableDetails(sb, suite, includeTolerated, effectiveLimit, fileFilter);
        return sb.ToString().TrimEnd();
    }

    public static string FormatSingleGateResult(
        GateCheckResult result,
        bool includeTolerated,
        int limit,
        string? fileFilter,
        string? groupBy)
    {
        var effectiveLimit = Math.Clamp(limit, 1, 500);
        var toleratedSet = new HashSet<string>(result.ToleratedViolations.Select(t => t.Fingerprint), StringComparer.Ordinal);
        var candidates = FilterViolations(result.Violations, toleratedSet, includeTolerated, fileFilter);

        if (!string.IsNullOrWhiteSpace(groupBy))
        {
            return FormatSingleGateGrouped(result, candidates, toleratedSet, effectiveLimit, groupBy.Trim().ToLowerInvariant());
        }

        var sb = new StringBuilder();
        AppendSingleGateHeader(sb, result, candidates.Count);
        if (candidates.Count > 0)
        {
            var shown = candidates.Take(effectiveLimit).ToList();
            if (candidates.Count > shown.Count)
            {
                sb.AppendLine($"moreViolations: {candidates.Count - shown.Count}");
            }

            sb.AppendLine("violations:");
            AppendViolationsTable(sb, 2, shown, toleratedSet, includeTolerated);
        }

        return sb.ToString().TrimEnd();
    }

    private static void AppendSuiteSummaryHeader(StringBuilder sb, GateSuiteResult suite)
    {
        var overall = suite.FailedCount > 0
            ? StatusFailed
            : (suite.WarningCount > 0 ? StatusWarning : StatusPassed);

        sb.AppendLine($"status: {overall}");
        sb.AppendLine("summary:");
        sb.AppendLine($"  totalGates: {suite.TotalGates}");
        sb.AppendLine($"  passed: {suite.PassedCount}");
        sb.AppendLine($"  warnings: {suite.WarningCount}");
        sb.AppendLine($"  failed: {suite.FailedCount}");
        sb.AppendLine($"  newViolations: {suite.NewViolationsCount}");
        sb.AppendLine($"  toleratedViolations: {suite.ToleratedViolationsCount}");
        sb.AppendLine($"  durationMs: {(long)Math.Round(suite.TotalDuration.TotalMilliseconds)}");
    }

    private static void AppendSuiteGatesTable(StringBuilder sb, IReadOnlyList<GateCheckResult> results)
    {
        if (results.Count == 0)
        {
            sb.AppendLine("gates: []");
            return;
        }

        sb.AppendLine("gates:");
        sb.AppendLine($"  columns: [{string.Join(", ", SuiteGateColumns)}]");
        sb.AppendLine("  rows:");
        foreach (var r in results)
        {
            var cells = string.Join(", ",
                ToonYamlSerializer.FormatYamlString(r.Gate.Id),
                r.Status.ToString(),
                r.Gate.Severity.ToString(),
                r.NewViolations.Count.ToString(),
                r.ToleratedViolations.Count.ToString(),
                ToonYamlSerializer.FormatYamlString(r.Gate.Name),
                ToonYamlSerializer.FormatYamlString(r.Gate.Description ?? string.Empty),
                ToonYamlSerializer.FormatYamlString(r.Gate.Remediation ?? string.Empty));
            sb.AppendLine($"  - [{cells}]");
        }
    }

    private static void AppendSuiteActionableDetails(
        StringBuilder sb,
        GateSuiteResult suite,
        bool includeTolerated,
        int limit,
        string? fileFilter)
    {
        var explicitDetailMode = includeTolerated || !string.IsNullOrWhiteSpace(fileFilter);
        var perGateCap = explicitDetailMode ? limit : Math.Min(limit, DefaultSummaryPreviewCap);
        var detailGates = new List<(GateCheckResult Result, HashSet<string> ToleratedSet, List<GateViolation> Matches)>();

        foreach (var r in suite.Results)
        {
            var isActionable = r.Status is GateCheckStatus.FailedNew or GateCheckStatus.Error or GateCheckStatus.Warning;
            if (!explicitDetailMode && !isActionable)
            {
                continue;
            }

            var tolSet = new HashSet<string>(r.ToleratedViolations.Select(t => t.Fingerprint), StringComparer.Ordinal);
            var matches = FilterViolations(r.Violations, tolSet, includeTolerated, fileFilter);
            if (matches.Count > 0 || !string.IsNullOrWhiteSpace(r.ErrorMessage))
            {
                detailGates.Add((r, tolSet, matches));
            }
        }

        if (detailGates.Count == 0)
        {
            return;
        }

        var sectionName = explicitDetailMode ? "matchedGates" : "failedGates";
        sb.AppendLine($"{sectionName}:");
        foreach (var (res, tolSet, matches) in detailGates)
        {
            sb.AppendLine($"- id: {ToonYamlSerializer.FormatYamlString(res.Gate.Id)}");
            sb.AppendLine($"  name: {ToonYamlSerializer.FormatYamlString(res.Gate.Name)}");
            sb.AppendLine($"  status: {res.Status}");
            if (!string.IsNullOrWhiteSpace(res.ErrorMessage))
            {
                sb.AppendLine($"  error: {ToonYamlSerializer.FormatYamlString(res.ErrorMessage!)}");
            }
            if (!string.IsNullOrWhiteSpace(res.Gate.Remediation))
            {
                sb.AppendLine($"  remediation: {ToonYamlSerializer.FormatYamlString(res.Gate.Remediation!)}");
            }

            if (matches.Count == 0)
            {
                continue;
            }

            var shown = matches.Take(perGateCap).ToList();
            sb.AppendLine($"  matchingViolations: {matches.Count}");
            if (matches.Count > shown.Count)
            {
                sb.AppendLine($"  moreViolations: {matches.Count - shown.Count}");
                sb.AppendLine($"  inspectAllHint: {ToonYamlSerializer.FormatYamlString($"Call check_semantic_gates with gateId='{res.Gate.Id}' to inspect all violations.")}");
            }

            sb.AppendLine("  violations:");
            AppendViolationsTable(sb, 4, shown, tolSet, includeTolerated);
        }
    }

    private static void AppendSingleGateHeader(StringBuilder sb, GateCheckResult result, int matchingCount)
    {
        var g = result.Gate;
        sb.AppendLine($"id: {ToonYamlSerializer.FormatYamlString(g.Id)}");
        sb.AppendLine($"name: {ToonYamlSerializer.FormatYamlString(g.Name)}");
        sb.AppendLine($"status: {result.Status}");
        sb.AppendLine($"severity: {g.Severity}");
        sb.AppendLine($"category: {g.Category}");
        sb.AppendLine($"platform: {ToonYamlSerializer.FormatYamlString(g.Platform)}");
        sb.AppendLine($"coordinate: {ToonYamlSerializer.FormatYamlString(g.CoordinateCode)}");
        if (!string.IsNullOrWhiteSpace(g.Description))
        {
            sb.AppendLine($"description: {ToonYamlSerializer.FormatYamlString(g.Description!)}");
        }
        if (!string.IsNullOrWhiteSpace(g.Remediation))
        {
            sb.AppendLine($"remediation: {ToonYamlSerializer.FormatYamlString(g.Remediation!)}");
        }
        if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
        {
            sb.AppendLine($"error: {ToonYamlSerializer.FormatYamlString(result.ErrorMessage!)}");
        }

        sb.AppendLine($"baselineCount: {result.BaselineCount}");
        sb.AppendLine($"newViolations: {result.NewViolations.Count}");
        sb.AppendLine($"toleratedViolations: {result.ToleratedViolations.Count}");
        sb.AppendLine($"matchingViolations: {matchingCount}");
        if (result.CanRatchetDown)
        {
            sb.AppendLine("canRatchetDown: true");
        }
    }

    private static void AppendViolationsTable(
        StringBuilder sb,
        int indentSpaces,
        IReadOnlyList<GateViolation> violations,
        HashSet<string> toleratedSet,
        bool includeStatusColumn)
    {
        var pad = new string(' ', indentSpaces);
        var cols = includeStatusColumn ? ViolationWithStatusColumns : ViolationColumns;
        sb.AppendLine($"{pad}columns: [{string.Join(", ", cols)}]");
        sb.AppendLine($"{pad}rows:");

        foreach (var v in violations)
        {
            var loc = ToonYamlSerializer.FormatYamlString(v.Location);
            var sym = ToonYamlSerializer.FormatYamlString(v.Symbol ?? string.Empty);
            var tgt = ToonYamlSerializer.FormatYamlString(v.Target ?? string.Empty);
            var msg = ToonYamlSerializer.FormatYamlString(v.Message ?? string.Empty);

            if (includeStatusColumn)
            {
                var tag = toleratedSet.Contains(v.Fingerprint) ? TagTolerated : TagNew;
                sb.AppendLine($"{pad}- [{tag}, {loc}, {sym}, {tgt}, {msg}]");
            }
            else
            {
                sb.AppendLine($"{pad}- [{loc}, {sym}, {tgt}, {msg}]");
            }
        }
    }

    private static string FormatSingleGateGrouped(
        GateCheckResult result,
        List<GateViolation> candidates,
        HashSet<string> _,
        int limit,
        string groupBy)
    {
        var groups = candidates
            .GroupBy(v => ResolveGroupKey(v, groupBy), StringComparer.Ordinal)
            .Select(g => (Key: g.Key, Count: g.Count()))
            .OrderByDescending(g => g.Count)
            .ThenBy(g => g.Key, StringComparer.Ordinal)
            .Take(limit)
            .ToList();

        var sb = new StringBuilder();
        sb.AppendLine($"gateId: {ToonYamlSerializer.FormatYamlString(result.Gate.Id)}");
        sb.AppendLine($"name: {ToonYamlSerializer.FormatYamlString(result.Gate.Name)}");
        sb.AppendLine($"status: {result.Status}");
        sb.AppendLine($"groupBy: {ToonYamlSerializer.FormatYamlString(groupBy)}");
        sb.AppendLine($"totalViolations: {candidates.Count}");
        sb.AppendLine("groups:");
        sb.AppendLine($"  columns: [{string.Join(", ", GroupColumns)}]");
        sb.AppendLine("  rows:");
        foreach (var grp in groups)
        {
            sb.AppendLine($"  - [{ToonYamlSerializer.FormatYamlString(grp.Key)}, {grp.Count}]");
        }
        return sb.ToString().TrimEnd();
    }

    private static string FormatSuiteGrouped(
        GateSuiteResult suite,
        bool includeTolerated,
        int limit,
        string? fileFilter,
        string groupBy)
    {
        var collected = new List<(string GateId, GateViolation Violation)>();
        foreach (var r in suite.Results)
        {
            var tolSet = new HashSet<string>(r.ToleratedViolations.Select(t => t.Fingerprint), StringComparer.Ordinal);
            foreach (var v in FilterViolations(r.Violations, tolSet, includeTolerated, fileFilter))
            {
                collected.Add((r.Gate.Id, v));
            }
        }

        var groups = collected
            .GroupBy(item => ResolveGroupKey(item.Violation, groupBy), StringComparer.Ordinal)
            .Select(g => (
                Key: g.Key,
                Count: g.Count(),
                Gates: string.Join("|", g.Select(x => x.GateId).Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal))))
            .OrderByDescending(g => g.Count)
            .ThenBy(g => g.Key, StringComparer.Ordinal)
            .Take(limit)
            .ToList();

        var sb = new StringBuilder();
        AppendSuiteSummaryHeader(sb, suite);
        sb.AppendLine($"groupBy: {ToonYamlSerializer.FormatYamlString(groupBy)}");
        sb.AppendLine($"matchingViolations: {collected.Count}");
        sb.AppendLine("groups:");
        sb.AppendLine($"  columns: [{string.Join(", ", SuiteGroupColumns)}]");
        sb.AppendLine("  rows:");
        foreach (var grp in groups)
        {
            sb.AppendLine($"  - [{ToonYamlSerializer.FormatYamlString(grp.Key)}, {grp.Count}, {ToonYamlSerializer.FormatYamlString(grp.Gates)}]");
        }
        return sb.ToString().TrimEnd();
    }

    private static List<GateViolation> FilterViolations(
        IReadOnlyList<GateViolation> violations,
        HashSet<string> toleratedSet,
        bool includeTolerated,
        string? fileFilter)
    {
        var result = new List<GateViolation>();
        foreach (var v in violations)
        {
            if (!includeTolerated && toleratedSet.Contains(v.Fingerprint))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(fileFilter))
            {
                var cleanFile = GateViolation.CleanFilePath(v.File);
                if (!cleanFile.Contains(fileFilter, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
            }

            result.Add(v);
        }
        return result;
    }

    private static string ResolveGroupKey(GateViolation v, string groupBy) => groupBy switch
    {
        GroupByCallee or GroupByTarget => ExtractCalleeKey(v.Target),
        GroupBySymbol => string.IsNullOrWhiteSpace(v.Symbol) ? UnknownGroupKey : v.Symbol!,
        _ => GateViolation.CleanFilePath(v.File)
    };

    private static string ExtractCalleeKey(string? target)
    {
        if (string.IsNullOrWhiteSpace(target)) return UnknownGroupKey;
        var idx = target.IndexOf('(');
        return idx > 0 ? target[..idx].Trim() : target.Trim();
    }
}
