using System.Text.Json;
using System.Text.Json.Serialization;
using CodePassport.Cli.Options;
using CodePassport.Engine.Analysis;
using CodePassport.Engine.Models;

namespace CodePassport.Cli.Commands;

public static class InspectCommandHandler
{
    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static async Task<int> HandleAsync(InspectOptions options)
    {
        var generator = new PassportGenerator();
        var passport = await generator.GenerateAsync(options.Path);

        if (!string.IsNullOrWhiteSpace(options.OutputPath))
        {
            var outFullPath = Path.GetFullPath(options.OutputPath);
            var json = JsonSerializer.Serialize(passport, JsonOptions);
            await File.WriteAllTextAsync(outFullPath, json);
        }

        if (options.Json)
        {
            Console.WriteLine(JsonSerializer.Serialize(passport, JsonOptions));
            return 0;
        }

        RenderPassportSummary(passport);
        return 0;
    }

    public static void RenderPassportSummary(PackagePassport p)
    {
        Console.WriteLine($"=== CodePassport: {p.Ecosystem}:{p.PackageName}@{p.Version} ===");
        Console.WriteLine($"  Behavioral Tier : {FormatTier(p.Tier)}");
        Console.WriteLine($"  Risk Verdict    : {p.Verdict}");
        Console.WriteLine($"  Bitmask (64-bit): 0x{p.CapabilityMask:X11}");
        Console.WriteLine($"  Dimensions (10D): {(p.ActiveDimensions.Count > 0 ? string.Join(", ", p.ActiveDimensions) : "NONE (Pure)")}");
        Console.WriteLine($"  Capabilities    : {(p.ActiveCapabilities.Count > 0 ? string.Join(", ", p.ActiveCapabilities) : "NONE")}");

        if (p.InstallHooks.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("  [!] Install-Time Lifecycle Hooks:");
            foreach (var (hook, cmd) in p.InstallHooks)
            {
                Console.WriteLine($"      - {hook}: {cmd}");
            }
        }

        if (p.ToxicCombinations.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("  [!!!] Toxic Combinations Detected:");
            foreach (var tc in p.ToxicCombinations)
            {
                Console.WriteLine($"      * [{tc.Id}] {tc.Archetype}: {tc.Description}");
                Console.WriteLine($"        Matched: {string.Join(" + ", tc.MatchedCapabilities)}");
            }
        }

        if (p.TaintFlows is { Count: > 0 })
        {
            Console.WriteLine();
            Console.WriteLine("  [!!!] Semantic Taint Flows (Source -> Sink):");
            foreach (var tf in p.TaintFlows)
            {
                Console.WriteLine($"      * [{tf.SourceCapability}] {tf.SourceExpression} ==> [{tf.SinkCapability}] {tf.SinkExpression} ({tf.FilePath}:{tf.Line} in {tf.EnclosingSymbol})");
            }
        }

        if (p.AutonomousTargetLiterals is { Count: > 0 })
        {
            Console.WriteLine();
            Console.WriteLine("  [!] Autonomous / Hardcoded Sink Targets:");
            foreach (var target in p.AutonomousTargetLiterals)
            {
                Console.WriteLine($"      - {target}");
            }
        }

        if (p.DangerousCalls is { Count: > 0 })
        {
            Console.WriteLine();
            Console.WriteLine($"  Dangerous Calls Map & Upward Argument Provenance ({p.DangerousCalls.Count}):");
            foreach (var dc in p.DangerousCalls)
            {
                var status = dc.IsLegitimate ? "[LEGITIMATE: Caller-Driven]" : $"[ILLEGITIMATE: {dc.IllegitimacyReason}]";
                Console.WriteLine($"      * {dc.SinkTarget} [{dc.Capability}] at {dc.FilePath}:{dc.Line} (in {dc.EnclosingFunction}) -> {status}");
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

        if (p.Exports.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine($"  Exported Symbol Contracts ({p.Exports.Count}):");
            foreach (var (_, exp) in p.Exports.Take(25))
            {
                var caps = exp.Capabilities.Count > 0 ? string.Join(", ", exp.Capabilities) : "PURE";
                var paramSummary = exp.ParameterFlows is { Count: > 0 }
                    ? $" | ParamFlows: {string.Join(", ", exp.ParameterFlows.Select(pf => $"param[{pf.ParameterIndex}]({pf.ParameterName})->{pf.SinkCapability}"))}"
                    : string.Empty;
                Console.WriteLine($"      - {exp.Name} ({exp.FilePath}:{exp.Line}) -> [{caps}]{paramSummary}");
            }
        }

        if (p.Evidence.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine($"  Capability Evidence ({p.Evidence.Count}):");
            foreach (var ev in p.Evidence.Take(25))
            {
                var minBadge = ev.IsMinified ? " [MINIFIED/DIST]" : string.Empty;
                Console.WriteLine($"      - [{ev.Capability}] {ev.FilePath}:{ev.Line} ({ev.SymbolName}) -> {ev.TargetExpression}{minBadge}");
            }
        }
    }

    internal static string FormatTier(BehavioralTier tier) => tier switch
    {
        BehavioralTier.Tier0Pure => "Tier 0 (Pure Computation)",
        BehavioralTier.Tier1LocalPassive => "Tier 1 (Local Passive)",
        BehavioralTier.Tier2NetworkClient => "Tier 2 (Network / Client SDK)",
        BehavioralTier.Tier3SystemPrivileged => "Tier 3 (System & Privileged Execution)",
        _ => tier.ToString()
    };
}
