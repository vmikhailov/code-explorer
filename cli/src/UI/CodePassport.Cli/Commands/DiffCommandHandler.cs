using System.Text.Json;
using CodePassport.Cli.Options;
using CodePassport.Engine.Analysis;
using CodePassport.Engine.Models;

namespace CodePassport.Cli.Commands;

public static class DiffCommandHandler
{
    public static async Task<int> HandleAsync(DiffOptions options)
    {
        var fromPassport = await LoadOrGeneratePassportAsync(options.FromPath);
        var toPassport = await LoadOrGeneratePassportAsync(options.ToPath);

        var diff = PassportDiffEngine.Compare(fromPassport, toPassport);

        if (options.Json)
        {
            Console.WriteLine(JsonSerializer.Serialize(diff, InspectCommandHandler.JsonOptions));
        }
        else
        {
            RenderDiffSummary(diff);
        }

        if (options.FailOnDrift && diff.Verdict == RiskVerdict.CriticalDrift)
        {
            return 2;
        }

        return 0;
    }

    private static async Task<PackagePassport> LoadOrGeneratePassportAsync(string targetPath)
    {
        var full = Path.GetFullPath(targetPath);
        if (File.Exists(full) && full.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            var json = await File.ReadAllTextAsync(full);
            return JsonSerializer.Deserialize<PackagePassport>(json, InspectCommandHandler.JsonOptions)
                   ?? throw new InvalidOperationException($"Failed to deserialize PackagePassport from '{full}'.");
        }

        var generator = new PassportGenerator();
        return await generator.GenerateAsync(full);
    }

    private static void RenderDiffSummary(PassportDiffResult d)
    {
        Console.WriteLine($"=== CodePassport Diff: {d.Ecosystem}:{d.PackageName} ({d.FromVersion} -> {d.ToVersion}) ===");
        Console.WriteLine($"  Tier Shift  : {InspectCommandHandler.FormatTier(d.FromTier)} -> {InspectCommandHandler.FormatTier(d.ToTier)}");
        Console.WriteLine($"  Verdict     : {d.Verdict}");
        Console.WriteLine($"  Added (+)   : {(d.AddedCapabilities.Count > 0 ? string.Join(", ", d.AddedCapabilities) : "NONE")}");
        Console.WriteLine($"  Removed (-) : {(d.RemovedCapabilities.Count > 0 ? string.Join(", ", d.RemovedCapabilities) : "NONE")}");

        if (d.NewInstallHooks.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("  [!] New Install Hooks Introduced:");
            foreach (var h in d.NewInstallHooks)
            {
                Console.WriteLine($"      + {h}");
            }
        }

        if (d.NewToxicCombinations.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("  [!!!] New Toxic Combinations Introduced:");
            foreach (var tc in d.NewToxicCombinations)
            {
                Console.WriteLine($"      * [{tc.Id}] {tc.Archetype}: {tc.Description}");
            }
        }

        if (d.NewTaintFlows is { Count: > 0 })
        {
            Console.WriteLine();
            Console.WriteLine("  [!!!] New Semantic Taint Flows Introduced:");
            foreach (var tf in d.NewTaintFlows)
            {
                Console.WriteLine($"      + [{tf.SourceCapability}] {tf.SourceExpression} ==> [{tf.SinkCapability}] {tf.SinkExpression} ({tf.FilePath}:{tf.Line})");
            }
        }

        if (d.NewAutonomousTargets is { Count: > 0 })
        {
            Console.WriteLine();
            Console.WriteLine("  [!!!] New Autonomous / Hardcoded Sink Targets Introduced:");
            foreach (var target in d.NewAutonomousTargets)
            {
                Console.WriteLine($"      + {target}");
            }
        }

        if (d.NewIllegitimateCalls is { Count: > 0 })
        {
            Console.WriteLine();
            Console.WriteLine("  [!!!] New Illegitimate Dangerous Calls (Failed Upward Argument Verification):");
            foreach (var dc in d.NewIllegitimateCalls)
            {
                Console.WriteLine($"      + {dc.SinkTarget} [{dc.Capability}] at {dc.FilePath}:{dc.Line} (in {dc.EnclosingFunction})");
                Console.WriteLine($"        Reason: {dc.IllegitimacyReason}");
                foreach (var arg in dc.Arguments)
                {
                    foreach (var orig in arg.Origins)
                    {
                        var chain = orig.CallChain is { Count: > 0 } ? string.Join(" -> ", orig.CallChain) : dc.EnclosingFunction;
                        Console.WriteLine($"          ^ arg[{arg.ArgumentIndex}]('{arg.ArgumentExpression}') <= [{orig.Provenance}] {orig.Expression} (via {chain})");
                    }
                }
            }
        }

        if (d.NewEvidence.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("  New Capability Evidence:");
            foreach (var ev in d.NewEvidence.Take(20))
            {
                Console.WriteLine($"      + [{ev.Capability}] {ev.FilePath}:{ev.Line} ({ev.SymbolName}) -> {ev.TargetExpression}");
            }
        }
    }
}
