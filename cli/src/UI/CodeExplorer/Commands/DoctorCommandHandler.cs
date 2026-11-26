using System.Text;
using System.Text.Json;
using CodeExplorer.Core.Analysis;
using CodeExplorer.Core.Common;
using CodeExplorer.Options;

namespace CodeExplorer.Commands;

public static class DoctorCommandHandler
{
    private const string FormatText = "text";
    private const string FormatMarkdown = "markdown";
    private const string FormatMd = "md";
    private const string FormatJson = "json";
    private const string FormatYaml = "yaml";

    private static readonly JsonSerializerOptions PrettyJsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public static async Task<int> HandleAsync(DoctorOptions opts)
    {
        try
        {
            var ws = WorkspaceLocator.FindOrThrow(opts.Dir);
            if (!File.Exists(ws.DbPath))
            {
                Console.Error.WriteLine("Workspace database not found. Run 'ce scan' first.");
                return 1;
            }

            await using var client = new SqliteGraphClient(ws.DbPath);

            var includeIssues = !opts.GapsOnly || opts.IssuesOnly;
            var includeGaps = !opts.IssuesOnly || opts.GapsOnly;
            var anonymize = opts.Anonymize || opts.Report;
            var format = !string.IsNullOrWhiteSpace(opts.Format)
                ? opts.Format.Trim().ToLowerInvariant()
                : (opts.Report ? FormatMarkdown : FormatText);

            var service = new WorkspaceDoctorService();
            var report = await service.AnalyzeAsync(client, ws.RootDirectory, new WorkspaceDoctorOptions
            {
                IncludeIssues = includeIssues,
                IncludeGaps = includeGaps,
                Anonymize = anonymize,
                MaxSamples = Math.Max(1, opts.Limit),
                QueryTimeoutSeconds = opts.TimeoutSeconds
            });

            var rendered = format switch
            {
                FormatJson => JsonSerializer.Serialize(report, PrettyJsonOptions),
                FormatYaml => ToonYamlSerializer.SerializeObject(report),
                FormatMarkdown or FormatMd => FormatAsMarkdown(report),
                _ => FormatAsText(report)
            };

            if (!string.IsNullOrWhiteSpace(opts.OutputPath))
            {
                var fullOut = Path.GetFullPath(opts.OutputPath);
                var outDir = Path.GetDirectoryName(fullOut);
                if (!string.IsNullOrEmpty(outDir) && !Directory.Exists(outDir))
                {
                    Directory.CreateDirectory(outDir);
                }
                await File.WriteAllTextAsync(fullOut, rendered);
                Console.WriteLine($"Doctor report written to {fullOut}");
            }
            else
            {
                Console.WriteLine(rendered);
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error running doctor diagnostics: {ex.Message}");
            return 1;
        }
    }

    private const string SeparatorLine = "------------------------------------------------------------------------";

    public static string FormatAsText(WorkspaceDoctorReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"CodeExplorer Doctor (v{report.CodeExplorerVersion}){(report.Anonymized ? " [anonymized]" : "")}");
        sb.AppendLine(SeparatorLine);

        var h = report.Health;
        var langs = h.LanguagesDetected.Count > 0 ? string.Join(", ", h.LanguagesDetected) : "none";
        sb.AppendLine("1. Workspace & Index Health");
        sb.AppendLine($"   Schema:      v{h.SchemaVersion} / v{h.CurrentSchemaVersion}{(h.IsSchemaOutdated ? " [OUTDATED]" : " [OK]")}");
        sb.AppendLine($"   Graph:       {h.TotalNodes:N0} nodes, {h.TotalRelationships:N0} relationships, {h.ProjectCount} project(s)");
        sb.AppendLine($"   Languages:   {langs}");
        if (h.KeyNodeCounts.Count > 0)
        {
            var summary = string.Join(", ", h.KeyNodeCounts.Select(kv => $"{kv.Key}={kv.Value}"));
            sb.AppendLine($"   Key nodes:   {summary}");
        }

        if (report.Issues != null)
        {
            sb.AppendLine();
            sb.AppendLine($"2. Parsing & Scan Issues ({report.Issues.TotalCount} total)");
            if (report.Issues.TotalCount == 0)
            {
                sb.AppendLine("   No scan issues recorded.");
            }
            else
            {
                foreach (var g in report.Issues.Groups)
                {
                    sb.AppendLine($"   - [{g.Severity}] {g.Stage} / {g.IssueCode} ({g.Language}): {g.Count}");
                }
                if (report.Issues.Samples.Count > 0)
                {
                    sb.AppendLine("   Samples:");
                    foreach (var s in report.Issues.Samples)
                    {
                        var loc = s.Line > 0 ? $"{s.Path}:{s.Line}" : s.Path;
                        sb.AppendLine($"     * {loc} [{s.IssueCode}] {s.Message}");
                    }
                }
            }
        }

        if (report.Gaps != null)
        {
            var g = report.Gaps;
            sb.AppendLine();
            sb.AppendLine("3. Parser & Framework Coverage Gaps");

            sb.AppendLine($"   Unhandled file extensions ({g.UnhandledExtensions.Count}):");
            if (g.UnhandledExtensions.Count == 0)
            {
                sb.AppendLine("     (none)");
            }
            else
            {
                foreach (var ext in g.UnhandledExtensions)
                {
                    sb.AppendLine($"     - {ext.Extension,-14} ({ext.FileCount} file(s))");
                }
            }

            sb.AppendLine($"   Unhandled XML dialects ({g.UnhandledXmlDialects.Count}):");
            if (g.UnhandledXmlDialects.Count == 0)
            {
                sb.AppendLine("     (none)");
            }
            else
            {
                foreach (var x in g.UnhandledXmlDialects)
                {
                    sb.AppendLine($"     - <{x.RootElement}> xmlns={x.NamespaceUri} ({x.FileCount} file(s))");
                }
            }

            sb.AppendLine($"   Unmatched framework/library packages ({g.UnmatchedPackages.Count} prioritized, {g.OtherUnmatchedPackageCount} other):");
            if (g.UnmatchedPackages.Count == 0)
            {
                sb.AppendLine("     (none)");
            }
            else
            {
                foreach (var p in g.UnmatchedPackages)
                {
                    sb.AppendLine($"     - [{p.Role}] {p.Name} ({p.Ecosystem})");
                }
            }

            sb.AppendLine($"   Coverage anomalies ({g.CoverageAnomalies.Count}):");
            if (g.CoverageAnomalies.Count == 0)
            {
                sb.AppendLine("     (none)");
            }
            else
            {
                foreach (var a in g.CoverageAnomalies)
                {
                    sb.AppendLine($"     - [{a.Kind}] {a.ProjectLabel} ({a.Language}): {a.Detail}");
                }
            }
        }

        return sb.ToString().TrimEnd();
    }

    public static string FormatAsMarkdown(WorkspaceDoctorReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"## CodeExplorer Doctor Report (`v{report.CodeExplorerVersion}`)");
        if (report.Anonymized)
        {
            sb.AppendLine("> Privacy-safe anonymized report (no source code, file paths, or internal project names).");
        }
        sb.AppendLine();

        var h = report.Health;
        sb.AppendLine("### Index Health");
        sb.AppendLine($"- **Schema**: `v{h.SchemaVersion}` / `v{h.CurrentSchemaVersion}`{(h.IsSchemaOutdated ? " (outdated)" : "")}");
        sb.AppendLine($"- **Graph**: `{h.TotalNodes:N0}` nodes, `{h.TotalRelationships:N0}` relationships, `{h.ProjectCount}` project(s)");
        sb.AppendLine($"- **Languages**: {(h.LanguagesDetected.Count > 0 ? string.Join(", ", h.LanguagesDetected.Select(l => $"`{l}`")) : "_none_")}");
        if (h.KeyNodeCounts.Count > 0)
        {
            sb.AppendLine($"- **Key Nodes**: {string.Join(", ", h.KeyNodeCounts.Select(kv => $"`{kv.Key}`: {kv.Value}"))}");
        }

        if (report.Issues != null)
        {
            sb.AppendLine();
            sb.AppendLine($"### Parsing & Scan Issues (`{report.Issues.TotalCount}` total)");
            if (report.Issues.Groups.Count == 0)
            {
                sb.AppendLine("_No scan issues recorded._");
            }
            else
            {
                sb.AppendLine("| Stage | Category | Language | Severity | Count |");
                sb.AppendLine("| :--- | :--- | :--- | :--- | ---: |");
                foreach (var grp in report.Issues.Groups)
                {
                    sb.AppendLine($"| `{grp.Stage}` | `{grp.IssueCode}` | `{grp.Language}` | `{grp.Severity}` | {grp.Count} |");
                }
            }
        }

        if (report.Gaps != null)
        {
            var g = report.Gaps;
            sb.AppendLine();
            sb.AppendLine("### Parser & Stack Coverage Gaps");

            if (g.UnhandledExtensions.Count > 0)
            {
                sb.AppendLine("#### Unhandled File Extensions");
                foreach (var ext in g.UnhandledExtensions)
                {
                    sb.AppendLine($"- `{ext.Extension}`: {ext.FileCount} file(s)");
                }
                sb.AppendLine();
            }

            if (g.UnhandledXmlDialects.Count > 0)
            {
                sb.AppendLine("#### Unhandled XML Dialects");
                foreach (var x in g.UnhandledXmlDialects)
                {
                    sb.AppendLine($"- `<{x.RootElement}>` (`{x.NamespaceUri}`): {x.FileCount} file(s)");
                }
                sb.AppendLine();
            }

            if (g.UnmatchedPackages.Count > 0)
            {
                sb.AppendLine($"#### Unmatched Framework / Library Packages (`+{g.OtherUnmatchedPackageCount}` other)");
                foreach (var p in g.UnmatchedPackages)
                {
                    sb.AppendLine($"- `{p.Name}` ({p.Ecosystem}) — *{p.Role}*");
                }
                sb.AppendLine();
            }

            if (g.CoverageAnomalies.Count > 0)
            {
                sb.AppendLine("#### Coverage Anomalies");
                foreach (var a in g.CoverageAnomalies)
                {
                    sb.AppendLine($"- **`{a.Kind}`** (`{a.ProjectLabel}`, `{a.Language}`): {a.Detail}");
                }
            }

            if (g.UnhandledExtensions.Count == 0 &&
                g.UnhandledXmlDialects.Count == 0 &&
                g.UnmatchedPackages.Count == 0 &&
                g.CoverageAnomalies.Count == 0)
            {
                sb.AppendLine("_No parser or framework coverage gaps detected._");
            }
        }

        return sb.ToString().TrimEnd();
    }
}
