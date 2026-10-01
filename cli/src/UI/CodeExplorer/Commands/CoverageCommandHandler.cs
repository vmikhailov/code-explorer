using System.Text.Json;
using CodeExplorer.Core.Analysis.Testing;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Database;
using CodeExplorer.Options;

namespace CodeExplorer.Commands;

public class CoverageCommandHandler : BaseCommandHandler
{
    public static async Task<int> HandleAsync(CoverageOptions opts)
    {
        var ws = EnsureInitialized(opts.Dir, requireIndexed: true);
        if (ws == null) return 1;

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

        if (IsJsonFormat(opts.Format))
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
