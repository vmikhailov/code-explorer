using CodeExplorer.Core.Analysis.Testing;
using CodeExplorer.Options;

namespace CodeExplorer.Commands;

public class TestCommandHandler : BaseCommandHandler
{
    private const string SubcommandCoverage = "coverage";
    private const string SubcommandImpact = "impact";

    public static async Task<int> HandleAsync(TestOptions opts)
    {
        var ws = EnsureInitialized(opts.Dir, requireIndexed: true);
        if (ws == null) return 1;

        var hasImpactSelector = !string.IsNullOrWhiteSpace(opts.Files)
            || opts.Git
            || !string.IsNullOrWhiteSpace(opts.GitBase)
            || !string.IsNullOrWhiteSpace(opts.Diff)
            || !string.IsNullOrWhiteSpace(opts.Symbols);
        var hasCoverageSelector = !string.IsNullOrWhiteSpace(opts.CoverageStatus)
            || !string.IsNullOrWhiteSpace(opts.Project)
            || !string.IsNullOrWhiteSpace(opts.PathPrefix)
            || opts.UncoveredOnly
            || opts.CoveredOnly
            || opts.Threshold.HasValue;

        if (IsOneOf(opts.Subcommand, SubcommandCoverage)
            || (IsOneOf(opts.Subcommand, SubcommandImpact) && !hasImpactSelector && hasCoverageSelector))
        {
            var covOpts = new CoverageOptions
            {
                Project = opts.Project,
                PathPrefix = opts.PathPrefix,
                Status = opts.CoverageStatus,
                Limit = opts.Limit,
                UncoveredOnly = opts.UncoveredOnly,
                CoveredOnly = opts.CoveredOnly,
                Threshold = opts.Threshold,
                Dir = opts.Dir
            };
            return await CoverageCommandHandler.HandleAsync(covOpts);
        }

        await using var client = new SqliteGraphClient(ws.DbPath);
        var service = new TestIntelligenceService(client);

        var changedFiles = ParseList(opts.Files);
        var symbolNames = ParseList(opts.Symbols);

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
                var sb = new System.Text.StringBuilder();
                if (report.Groups is { Count: > 0 })
                {
                    foreach (var g in report.Groups)
                    {
                        if (g.AllTestsAffected && !string.IsNullOrEmpty(g.ClassName))
                        {
                            sb.AppendLine(g.ClassName);
                        }
                        else
                        {
                            foreach (var m in g.Methods)
                            {
                                sb.AppendLine(m.TestMethodName);
                            }
                        }
                    }
                }
                else
                {
                    foreach (var t in report.AffectedTestMethods)
                    {
                        sb.AppendLine(t.TestMethodName);
                    }
                }
                Console.Write(sb.ToString());
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

            default:
                Console.WriteLine(ToonYamlSerializer.SerializeObject(report));
                break;
        }

        return 0;
    }
}
