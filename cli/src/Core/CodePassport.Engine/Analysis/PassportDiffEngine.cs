using CodePassport.Engine.Classification;
using CodePassport.Engine.Models;

namespace CodePassport.Engine.Analysis;

/// <summary>
/// Computes behavioral capability drift, taint flow drift, and autonomous sink drift
/// between two package versions:
/// Delta(v_N-1, v_N) = Cap(v_N) &amp; ~Cap(v_N-1), plus new source-to-sink taint flows
/// and new hardcoded/autonomous sink targets (solving the trojanized-SDK / axios problem).
/// </summary>
public static class PassportDiffEngine
{
    public static PassportDiffResult Compare(PackagePassport fromPassport, PackagePassport toPassport)
    {
        var fromMask = (CapabilityFlags)fromPassport.CapabilityMask;
        var toMask = (CapabilityFlags)toPassport.CapabilityMask;

        var addedMask = toMask & ~fromMask;
        var removedMask = fromMask & ~toMask;

        var addedCaps = CapabilityCatalog.FormatCapabilities(addedMask);
        var removedCaps = CapabilityCatalog.FormatCapabilities(removedMask);

        var newHooks = new List<string>();
        foreach (var (hookName, hookCmd) in toPassport.InstallHooks)
        {
            if (!fromPassport.InstallHooks.TryGetValue(hookName, out var prevCmd) ||
                !string.Equals(prevCmd, hookCmd, StringComparison.Ordinal))
            {
                newHooks.Add($"{hookName}: {hookCmd}");
            }
        }

        var prevToxicIds = new HashSet<string>(fromPassport.ToxicCombinations.Select(t => t.Id), StringComparer.Ordinal);
        var newToxic = toPassport.ToxicCombinations
            .Where(t => !prevToxicIds.Contains(t.Id))
            .ToList();

        // Detect new semantic taint flows (Source -> Sink) introduced in v_N
        var prevFlows = new HashSet<string>(
            (fromPassport.TaintFlows ?? []).Select(f => $"{f.SourceCapability}->{f.SinkCapability}:{f.SourceExpression}"),
            StringComparer.Ordinal);
        var newTaintFlows = (toPassport.TaintFlows ?? [])
            .Where(f => !prevFlows.Contains($"{f.SourceCapability}->{f.SinkCapability}:{f.SourceExpression}"))
            .ToList();

        // Detect new autonomous (non-caller-parameterized) sink targets (e.g., new hardcoded URL in an HTTP library)
        var prevTargets = new HashSet<string>(fromPassport.AutonomousTargetLiterals ?? [], StringComparer.Ordinal);
        var newAutonomousTargets = (toPassport.AutonomousTargetLiterals ?? [])
            .Where(t => !prevTargets.Contains(t))
            .ToList();

        // Detect capabilities newly moved into <module_init> (top-level import-time execution)
        var prevInitCaps = new HashSet<string>(fromPassport.ModuleInitCapabilities, StringComparer.Ordinal);
        var newModuleInitCaps = toPassport.ModuleInitCapabilities
            .Where(c => !prevInitCaps.Contains(c))
            .ToList();

        // Detect new illegitimate dangerous calls via upward argument provenance verification
        var prevIllegitimateKeys = new HashSet<string>(
            (fromPassport.DangerousCalls ?? [])
                .Where(dc => !dc.IsLegitimate)
                .Select(dc => $"{dc.SinkTarget}:{dc.IllegitimacyReason}"),
            StringComparer.Ordinal);

        var newIllegitimateCalls = (toPassport.DangerousCalls ?? [])
            .Where(dc => !dc.IsLegitimate && !prevIllegitimateKeys.Contains($"{dc.SinkTarget}:{dc.IllegitimacyReason}"))
            .ToList();

        var addedCapSet = new HashSet<string>(addedCaps, StringComparer.Ordinal);
        var newEvidence = toPassport.Evidence
            .Where(e => addedCapSet.Contains(e.Capability) || e.Provenance is SinkInputProvenance.SensitiveSourceTaint or SinkInputProvenance.DecodedConstant)
            .ToList();

        var verdict = EvaluateDiffVerdict(
            fromPassport.Tier,
            toPassport.Tier,
            addedMask,
            newHooks.Count > 0,
            newToxic.Count > 0,
            newTaintFlows.Count > 0,
            newAutonomousTargets.Count > 0,
            newIllegitimateCalls.Count > 0);

        return new PassportDiffResult(
            Ecosystem: toPassport.Ecosystem,
            PackageName: toPassport.PackageName,
            FromVersion: fromPassport.Version,
            ToVersion: toPassport.Version,
            FromTier: fromPassport.Tier,
            ToTier: toPassport.Tier,
            Verdict: verdict,
            AddedCapabilityMask: (ulong)addedMask,
            RemovedCapabilityMask: (ulong)removedMask,
            AddedCapabilities: addedCaps,
            RemovedCapabilities: removedCaps,
            NewInstallHooks: newHooks,
            NewToxicCombinations: newToxic,
            NewEvidence: newEvidence,
            NewTaintFlows: newTaintFlows,
            NewAutonomousTargets: newAutonomousTargets,
            NewModuleInitCapabilities: newModuleInitCaps,
            NewIllegitimateCalls: newIllegitimateCalls);
    }

    private static RiskVerdict EvaluateDiffVerdict(
        BehavioralTier fromTier,
        BehavioralTier toTier,
        CapabilityFlags addedMask,
        bool hasNewInstallHooks,
        bool hasNewToxicCombinations,
        bool hasNewTaintFlows,
        bool hasNewAutonomousTargets,
        bool hasNewIllegitimateCalls)
    {
        if (hasNewToxicCombinations || hasNewInstallHooks || hasNewTaintFlows || hasNewAutonomousTargets || hasNewIllegitimateCalls)
        {
            return RiskVerdict.CriticalDrift;
        }

        const CapabilityFlags alwaysCriticalAdded =
            CapabilityFlags.DistShadowCapability |
            CapabilityFlags.ProcShellDropper |
            CapabilityFlags.NetRawIp |
            CapabilityFlags.NetExfilHost |
            CapabilityFlags.FsSensitivePath |
            CapabilityFlags.EnvDumpAll |
            CapabilityFlags.CryptoObfuscated |
            CapabilityFlags.CodeMonkeyPatch;

        if ((addedMask & alwaysCriticalAdded) != 0)
        {
            return RiskVerdict.CriticalDrift;
        }

        if (fromTier <= BehavioralTier.Tier1LocalPassive && toTier >= BehavioralTier.Tier2NetworkClient)
        {
            return RiskVerdict.CriticalDrift;
        }
        if (fromTier == BehavioralTier.Tier2NetworkClient && toTier == BehavioralTier.Tier3SystemPrivileged)
        {
            return RiskVerdict.CriticalDrift;
        }

        if (addedMask != CapabilityFlags.None || toTier > fromTier)
        {
            return RiskVerdict.Expansion;
        }

        return RiskVerdict.Safe;
    }
}
