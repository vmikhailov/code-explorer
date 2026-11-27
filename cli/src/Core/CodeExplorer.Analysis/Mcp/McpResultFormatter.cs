using CodeExplorer.Core.Analysis.Testing;
using CodeExplorer.Core.Gates.Models;

namespace CodeExplorer.Core.Mcp;

internal static class McpResultFormatter
{
    private const string InternalSymPrefix = "ws:sym:";
    private const string KindMember = "Member";
    private const int MaxCoveringTestsPreview = 5;
    private const int MaxAffectedTestsCap = 100;

    private static readonly string[] HybridColumns = ["type", "name", "filePath", "startLine", "score"];
    private static readonly string[] SymbolMarkers =
    [
        ":Function:", ":Type:", ":Class:", ":Interface:", ":Method:", ":Member:", ":Endpoint:", ":Table:", ":Topic:"
    ];

    public static string CleanSymbolDisplay(string? rawSymbol, string? fallbackName = null)
    {
        if (string.IsNullOrWhiteSpace(rawSymbol))
        {
            return fallbackName ?? string.Empty;
        }

        var trimmed = rawSymbol.Trim();
        if (!trimmed.StartsWith(InternalSymPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return trimmed;
        }

        foreach (var marker in SymbolMarkers)
        {
            var idx = trimmed.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                var qualified = trimmed[(idx + marker.Length)..];
                return string.IsNullOrWhiteSpace(qualified) ? (fallbackName ?? string.Empty) : qualified;
            }
        }

        return fallbackName ?? string.Empty;
    }

    public static string CleanRelativePath(string? rawPath, string? workspaceRoot)
    {
        if (string.IsNullOrWhiteSpace(rawPath))
        {
            return string.Empty;
        }

        var cleaned = GateViolation.CleanFilePath(rawPath);
        if (cleaned == "-")
        {
            return string.Empty;
        }

        return IdentifierVectorEncoder.NormalizeSymbolPath(cleaned, workspaceRoot) ?? cleaned;
    }

    public static string FormatHybridMatches(
        IReadOnlyList<IdentifierMatch> matches,
        string? workspaceRoot,
        string? kindFilter,
        int limit)
    {
        if (matches.Count == 0)
        {
            return "[]";
        }

        var keepMembersAtSameLocation = string.Equals(kindFilter, KindMember, StringComparison.OrdinalIgnoreCase);
        var nonMemberSites = new HashSet<(string Path, int Line, string Name)>();
        var normalizedItems = new List<(string Kind, string Name, string Path, int Line, double Score)>(matches.Count);

        foreach (var m in matches)
        {
            var kindStr = m.Kind.ToString();
            var normPath = CleanRelativePath(m.Path, workspaceRoot);
            var rounded = Math.Round(m.Score, 3);
            normalizedItems.Add((kindStr, m.Name, normPath, m.StartLine, rounded));

            if (!string.Equals(kindStr, KindMember, StringComparison.OrdinalIgnoreCase))
            {
                nonMemberSites.Add((normPath, m.StartLine, m.Name));
            }
        }

        var rows = new List<CypherRow>();
        var seen = new HashSet<(string Kind, string Path, int Line, string Name)>();
        foreach (var item in normalizedItems)
        {
            if (!keepMembersAtSameLocation
                && string.Equals(item.Kind, KindMember, StringComparison.OrdinalIgnoreCase)
                && nonMemberSites.Contains((item.Path, item.Line, item.Name)))
            {
                continue;
            }

            if (!seen.Add((item.Kind, item.Path, item.Line, item.Name)))
            {
                continue;
            }

            rows.Add(new CypherRow(HybridColumns,
            [
                CypherValue.FromString(item.Kind),
                CypherValue.FromString(item.Name),
                CypherValue.FromString(item.Path),
                CypherValue.FromInt64(item.Line),
                CypherValue.FromFloat64(item.Score)
            ]));

            if (rows.Count >= limit)
            {
                break;
            }
        }

        return ToonYamlSerializer.SerializeYaml(new CypherQueryResult(HybridColumns, rows));
    }

    public static string FormatAffectedTests(TestImpactReport report, string? workspaceRoot)
    {
        var cleanedChanged = report.ChangedSymbols
            .Select(s => new
            {
                name = s.Name,
                kind = s.Kind.ToString(),
                filePath = CleanRelativePath(s.FilePath, workspaceRoot),
                startLine = s.StartLine,
                endLine = s.EndLine
            })
            .ToList();

        var hasNonTestProd = cleanedChanged.Any(s => !IsTestFilePath(s.filePath));
        if (hasNonTestProd)
        {
            cleanedChanged = cleanedChanged
                .Where(s => !IsTestFilePath(s.filePath) || s.kind != KindMember)
                .ToList();
        }

        var allTests = report.AffectedTestMethods;
        var shownTests = allTests
            .Take(MaxAffectedTestsCap)
            .Select(t => new
            {
                testName = t.TestMethodName,
                className = t.TestClassName,
                filePath = CleanRelativePath(t.TestFilePath, workspaceRoot),
                startLine = t.StartLine,
                framework = t.TestFramework,
                depth = t.Depth,
                reason = t.ImpactReason,
                callChain = t.CallChain
            })
            .ToList();

        var payload = new
        {
            affectedTestsCount = allTests.Count,
            moreAffectedTests = allTests.Count > shownTests.Count ? allTests.Count - shownTests.Count : 0,
            runnerCommands = report.RunnerCommands,
            changedSymbols = cleanedChanged,
            affectedTests = shownTests
        };

        return ToonYamlSerializer.SerializeObject(payload);
    }

    public static string FormatTestCoverage(TestCoverageReport report, string? workspaceRoot)
    {
        var payload = new
        {
            summary = new
            {
                totalClasses = report.Summary.TotalClasses,
                coveredClasses = report.Summary.CoveredClasses,
                uncoveredClasses = report.Summary.UncoveredClasses,
                classCoveragePct = Math.Round(report.Summary.ClassCoveragePercentage, 1),
                totalMethods = report.Summary.TotalMethods,
                coveredMethods = report.Summary.CoveredMethods,
                uncoveredMethods = report.Summary.UncoveredMethods,
                methodCoveragePct = Math.Round(report.Summary.MethodCoveragePercentage, 1)
            },
            coveredClasses = report.CoveredClasses.Select(c => new
            {
                name = c.Name,
                filePath = CleanRelativePath(c.FilePath, workspaceRoot),
                project = c.Project,
                coveredMethods = c.CoveredMethodsCount,
                totalMethods = c.TotalMethodsCount,
                coveringTests = c.TotalCoveringTests
            }).ToList(),
            uncoveredClasses = report.UncoveredClasses.Select(c => new
            {
                name = c.Name,
                filePath = CleanRelativePath(c.FilePath, workspaceRoot),
                startLine = c.StartLine,
                endLine = c.EndLine,
                project = c.Project,
                methodCount = c.MethodCount
            }).ToList(),
            coveredMethods = report.CoveredMethods.Select(m => new
            {
                name = m.Name,
                className = m.ClassName,
                filePath = CleanRelativePath(m.FilePath, workspaceRoot),
                startLine = m.StartLine,
                minDepth = m.MinCallDepth,
                coveringTestsCount = m.CoveringTests.Count,
                coveringTests = m.CoveringTests.Take(MaxCoveringTestsPreview).ToList()
            }).ToList(),
            uncoveredMethods = report.UncoveredMethods.Select(m => new
            {
                name = m.Name,
                className = m.ClassName,
                filePath = CleanRelativePath(m.FilePath, workspaceRoot),
                startLine = m.StartLine,
                endLine = m.EndLine,
                project = m.Project
            }).ToList()
        };

        return ToonYamlSerializer.SerializeObject(payload);
    }

    private static bool IsTestFilePath(string path) =>
        path.Contains(".spec.", StringComparison.OrdinalIgnoreCase)
        || path.Contains(".test.", StringComparison.OrdinalIgnoreCase)
        || path.Contains("__tests__", StringComparison.OrdinalIgnoreCase)
        || path.EndsWith("Tests.cs", StringComparison.OrdinalIgnoreCase);
}
