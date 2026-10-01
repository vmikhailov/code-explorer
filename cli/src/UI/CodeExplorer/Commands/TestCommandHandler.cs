using System.Text.Json;
using CodeExplorer.Core.Analysis.Testing;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Database;
using CodeExplorer.Options;

namespace CodeExplorer.Commands;

public static class TestCommandHandler
{
    public static async Task<int> HandleAsync(TestOptions opts)
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

        // Check if user requested coverage subcommand via 'ce test coverage'
        if (string.Equals(opts.Subcommand, "coverage", StringComparison.OrdinalIgnoreCase))
        {
            var covOpts = new CoverageOptions
            {
                Project = opts.Project,
                UncoveredOnly = opts.UncoveredOnly,
                CoveredOnly = opts.CoveredOnly,
                Threshold = opts.Threshold,
                Format = opts.Format,
                Dir = opts.Dir
            };
            return await CoverageCommandHandler.HandleAsync(covOpts);
        }

        await using var client = new SqliteGraphClient(ws.DbPath);
        var service = new TestIntelligenceService(client);

        var changedFiles = !string.IsNullOrWhiteSpace(opts.Files)
            ? opts.Files.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries)
                .Select(f => f.Trim())
                .ToList()
            : null;

        var symbolNames = !string.IsNullOrWhiteSpace(opts.Symbols)
            ? opts.Symbols.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .ToList()
            : null;

        var gitBase = opts.GitBase;
        if (string.IsNullOrWhiteSpace(gitBase) && (opts.Git || (changedFiles == null && opts.Diff == null && symbolNames == null)))
        {
            gitBase = "HEAD";
        }

        var request = new TestImpactRequest(
            ChangedFiles: changedFiles,
            GitDiff: opts.Diff,
            GitBase: gitBase,
            SymbolNames: symbolNames,
            MaxDepth: opts.MaxDepth,
            WorkspaceRoot: ws.RootDirectory
        );

        var report = await service.AnalyzeImpactAsync(request);

        switch (opts.Format.ToLowerInvariant())
        {
            case "list":
            case "tests":
                foreach (var t in report.AffectedTestMethods)
                {
                    Console.WriteLine(t.TestMethodName);
                }
                break;

            case "commands":
            case "cmd":
                if (report.RunnerCommands.Count > 0)
                {
                    foreach (var (fw, cmd) in report.RunnerCommands)
                    {
                        Console.WriteLine($"# [{fw}]");
                        Console.WriteLine(cmd);
                    }
                }
                else
                {
                    Console.WriteLine("# No affected tests found to run.");
                }
                break;

            case "json":
                Console.WriteLine(JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
                break;

            case "markdown":
            default:
                Console.WriteLine(TestIntelligenceService.FormatImpactMarkdown(report));
                break;
        }

        return 0;
    }
}
