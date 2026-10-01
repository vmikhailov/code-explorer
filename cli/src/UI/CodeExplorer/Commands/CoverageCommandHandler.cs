using System.Text.Json;
using CodeExplorer.Core.Analysis.Testing;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Database;
using CodeExplorer.Options;

namespace CodeExplorer.Commands;

public static class CoverageCommandHandler
{
    public static async Task<int> HandleAsync(CoverageOptions opts)
    {
        var targetDir = Path.GetFullPath(opts.Dir ?? Directory.GetCurrentDirectory());
        var ws = WorkspaceLocator.Find(targetDir);
        if (ws == null)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Error: No CodeExplorer workspace found at '{targetDir}'. Run 'ce init' and 'ce index' first.");
            Console.ResetColor();
            return 1;
        }

        await using var client = new SqliteGraphClient(ws.DbPath);
        var service = new TestIntelligenceService(client);

        var status = opts.UncoveredOnly
            ? "uncovered"
            : (opts.CoveredOnly ? "covered" : "all");

        var filter = new TestCoverageFilter(
            Project: opts.Project,
            PathPrefix: opts.PathPrefix,
            Status: status
        );

        var report = await service.AnalyzeCoverageAsync(filter);

        if (string.Equals(opts.Format, "json", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine(JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        }
        else
        {
            Console.WriteLine(TestIntelligenceService.FormatCoverageMarkdown(report));
        }

        // CI threshold check
        if (opts.Threshold.HasValue)
        {
            if (report.Summary.MethodCoveragePercentage < opts.Threshold.Value)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\n[FAIL] Method coverage {report.Summary.MethodCoveragePercentage:F1}% is below the required threshold of {opts.Threshold.Value:F1}%.");
                Console.ResetColor();
                return 1;
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"\n[PASS] Method coverage {report.Summary.MethodCoveragePercentage:F1}% meets the required threshold of {opts.Threshold.Value:F1}%.");
                Console.ResetColor();
            }
        }

        return 0;
    }
}
