using System.Text.Json;
using System.Text.Json.Serialization;
using CodeExplorer.Core.Gates.Engine;
using CodeExplorer.Core.Gates.Models;
using CodeExplorer.Options;

namespace CodeExplorer.Commands;

public partial class GateCommandHandler
{
    private const string FormatSarif = "sarif";
    private const string FormatGithub = "github";
    private const string SarifSchemaUrl = "https://json.schemastore.org/sarif-2.1.0.json";
    private const string SarifVersion = "2.1.0";
    private const string SarifToolName = "CodeExplorer";
    private const string SarifInfoUri = "https://code-explorer.app";
    private const string SarifSrcRootBaseId = "%SRCROOT%";
    private const string SarifFingerprintKey = "codeExplorerViolationHash/v1";
    private const string LevelError = "error";
    private const string LevelWarning = "warning";
    private const string DashPlaceholder = "-";

    private static readonly JsonSerializerOptions SarifJsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static bool IsCiFormat(string? format) =>
        string.Equals(format, FormatSarif, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(format, FormatGithub, StringComparison.OrdinalIgnoreCase);

    private static int RenderCiFormat(IReadOnlyList<GateCheckResult> results, GateOptions opts, int exitCode)
    {
        if (string.Equals(opts.Format, FormatSarif, StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine(BuildSarifReport(results, opts));
            return exitCode;
        }

        WriteGithubAnnotations(results, opts);
        return exitCode;
    }

    private static string BuildSarifReport(IReadOnlyList<GateCheckResult> results, GateOptions opts)
    {
        var rules = results
            .Select(r => r.Gate)
            .DistinctBy(g => g.Id, StringComparer.OrdinalIgnoreCase)
            .Select(BuildSarifRule)
            .ToList();

        var sarifResults = new List<object>();
        foreach (var r in results)
        {
            foreach (var (v, isTol) in CollectCiViolations(r, opts))
            {
                sarifResults.Add(BuildSarifResultItem(r, v, isTol));
            }
        }

        var payload = new Dictionary<string, object>
        {
            ["$schema"] = SarifSchemaUrl,
            ["version"] = SarifVersion,
            ["runs"] = new[]
            {
                new
                {
                    tool = new
                    {
                        driver = new
                        {
                            name = SarifToolName,
                            informationUri = SarifInfoUri,
                            rules
                        }
                    },
                    results = sarifResults
                }
            }
        };

        return JsonSerializer.Serialize(payload, SarifJsonOptions);
    }

    private static object BuildSarifRule(SemanticGate gate)
    {
        var defaultLevel = gate.Severity == GateSeverity.Warning ? LevelWarning : LevelError;
        var fullDesc = !string.IsNullOrWhiteSpace(gate.Description) ? gate.Description : gate.Name;
        object? helpObj = !string.IsNullOrWhiteSpace(gate.Remediation) ? new { text = gate.Remediation } : null;

        return new
        {
            id = gate.Id,
            name = gate.Name,
            shortDescription = new { text = gate.Name },
            fullDescription = new { text = fullDesc },
            help = helpObj,
            defaultConfiguration = new { level = defaultLevel },
            properties = new
            {
                category = gate.Category.ToString(),
                platform = gate.Platform,
                scope = gate.Scope.ToString(),
                dimension = gate.Dimension.ToString()
            }
        };
    }

    private static object BuildSarifResultItem(GateCheckResult checkResult, GateViolation v, bool isTolerated)
    {
        var level = ResolveCiLevel(checkResult, isTolerated);
        var uri = NormalizeRelativeUri(v.File);
        object[]? locations = null;
        if (!string.IsNullOrEmpty(uri))
        {
            object? region = v.Line > 0
                ? new { startLine = v.Line, startColumn = Math.Max(1, v.Column) }
                : null;
            locations =
            [
                new
                {
                    physicalLocation = new
                    {
                        artifactLocation = new { uri, uriBaseId = SarifSrcRootBaseId },
                        region
                    }
                }
            ];
        }

        object[]? suppressions = isTolerated
            ? [new { kind = "external", justification = "Tolerated by technical debt ratchet baseline." }]
            : null;

        return new
        {
            ruleId = checkResult.Gate.Id,
            level,
            message = new { text = FormatCiViolationMessage(checkResult.Gate, v) },
            locations,
            partialFingerprints = new Dictionary<string, string>
            {
                [SarifFingerprintKey] = v.Fingerprint
            },
            suppressions
        };
    }

    private static void WriteGithubAnnotations(IReadOnlyList<GateCheckResult> results, GateOptions opts)
    {
        foreach (var r in results)
        {
            var title = EscapeGithubProperty($"{r.Gate.Id} ({r.Gate.Name})");
            foreach (var (v, isTol) in CollectCiViolations(r, opts))
            {
                var level = ResolveCiLevel(r, isTol);
                var props = new List<string>(4);
                var uri = NormalizeRelativeUri(v.File);
                if (!string.IsNullOrEmpty(uri))
                {
                    props.Add($"file={EscapeGithubProperty(uri)}");
                }
                if (v.Line > 0)
                {
                    props.Add($"line={v.Line}");
                    props.Add($"col={Math.Max(1, v.Column)}");
                }
                props.Add($"title={title}");

                var message = EscapeGithubData(FormatCiViolationMessage(r.Gate, v));
                Console.WriteLine($"::{level} {string.Join(',', props)}::{message}");
            }
        }
    }

    private static List<(GateViolation Violation, bool IsTolerated)> CollectCiViolations(GateCheckResult r, GateOptions opts)
    {
        var includeTolerated = opts.All || opts.OnlyTolerated;
        var filtered = FilterViolations(r, opts);
        if (!includeTolerated)
        {
            filtered = filtered.Where(x => !x.IsTolerated).ToList();
        }
        if (opts.Offset > 0)
        {
            filtered = filtered.Skip(opts.Offset).ToList();
        }
        return filtered;
    }

    private static string ResolveCiLevel(GateCheckResult r, bool isTolerated)
    {
        if (isTolerated || r.Status == GateCheckStatus.Warning || r.Gate.Severity == GateSeverity.Warning)
        {
            return LevelWarning;
        }
        return LevelError;
    }

    private static string FormatCiViolationMessage(SemanticGate gate, GateViolation v)
    {
        if (!string.IsNullOrWhiteSpace(v.Message))
        {
            return !string.IsNullOrWhiteSpace(v.Symbol) && !v.Message.Contains(v.Symbol, StringComparison.Ordinal)
                ? $"{v.Symbol}: {v.Message}"
                : v.Message;
        }

        if (!string.IsNullOrWhiteSpace(v.Symbol) && !string.IsNullOrWhiteSpace(v.Target))
        {
            return $"{v.Symbol} -> {v.Target} violates {gate.Id} ({gate.Name})";
        }

        if (!string.IsNullOrWhiteSpace(v.Symbol))
        {
            return $"{v.Symbol} violates {gate.Id} ({gate.Name})";
        }

        return $"Violation of {gate.Id}: {gate.Name}";
    }

    private static string? NormalizeRelativeUri(string? rawFile)
    {
        var cleaned = GateViolation.CleanFilePath(rawFile);
        if (string.IsNullOrWhiteSpace(cleaned) || cleaned == DashPlaceholder)
        {
            return null;
        }

        var normalized = cleaned.Replace('\\', '/').Trim();
        while (normalized.StartsWith("./", StringComparison.Ordinal))
        {
            normalized = normalized[2..];
        }
        return normalized.TrimStart('/');
    }

    private static string EscapeGithubData(string value) =>
        value
            .Replace("%", "%25", StringComparison.Ordinal)
            .Replace("\r", "%0D", StringComparison.Ordinal)
            .Replace("\n", "%0A", StringComparison.Ordinal);

    private static string EscapeGithubProperty(string value) =>
        EscapeGithubData(value)
            .Replace(":", "%3A", StringComparison.Ordinal)
            .Replace(",", "%2C", StringComparison.Ordinal);
}
