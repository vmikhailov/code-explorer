using CodeExplorer.Core.Gates.Models;

namespace CodeExplorer.Core.Gates.Engine;

/// <summary>
/// Service managing monotonic technical debt ratchets and baseline comparisons.
/// Enforces: N_{t+1} &lt;= N_t (debt can strictly only decrease or stay equal).
/// </summary>
public static class RatchetService
{
    public static (GateCheckStatus Status, List<GateViolation> NewViolations, List<GateViolation> ToleratedViolations, int BaselineCount)
        Evaluate(SemanticGate gate, IReadOnlyList<GateViolation> violations, GateBaseline? baseline)
    {
        if (!gate.Ratchet.Enabled || baseline == null)
        {
            if (violations.Count == 0)
            {
                return (GateCheckStatus.Passed, [], [], 0);
            }

            return (GateCheckStatus.FailedNew, violations.ToList(), [], 0);
        }

        var baselineFps = baseline.Entries
            .Select(e => e.Fingerprint)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var tolerated = new List<GateViolation>();
        var newViolations = new List<GateViolation>();

        foreach (var v in violations)
        {
            if (baselineFps.Contains(v.Fingerprint))
            {
                tolerated.Add(v);
            }
            else
            {
                newViolations.Add(v);
            }
        }

        if (newViolations.Count > 0)
        {
            return (GateCheckStatus.FailedNew, newViolations, tolerated, baseline.AllowedViolations);
        }

        if (violations.Count == 0)
        {
            return (GateCheckStatus.Passed, [], [], baseline.AllowedViolations);
        }

        return (GateCheckStatus.PassedTolerated, [], tolerated, baseline.AllowedViolations);
    }

    public static GateBaseline CreateUpdatedBaseline(
        SemanticGate gate,
        IReadOnlyList<GateViolation> currentViolations,
        GateBaseline? existingBaseline = null)
    {
        var existingByFp = new Dictionary<string, GateBaselineEntry>(StringComparer.OrdinalIgnoreCase);
        if (existingBaseline?.Entries != null)
        {
            foreach (var e in existingBaseline.Entries)
            {
                existingByFp.TryAdd(e.Fingerprint, e);
            }
        }

        var now = DateTimeOffset.UtcNow;
        var entries = new List<GateBaselineEntry>();
        var seenFps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var v in currentViolations)
        {
            if (!seenFps.Add(v.Fingerprint))
            {
                continue;
            }

            if (existingByFp.TryGetValue(v.Fingerprint, out var prev))
            {
                entries.Add(prev);
            }
            else
            {
                entries.Add(new GateBaselineEntry
                {
                    Fingerprint = v.Fingerprint,
                    File = v.File,
                    Line = v.Line,
                    Column = v.Column,
                    Symbol = v.Symbol,
                    Target = v.Target,
                    AcknowledgedAt = now,
                    Reason = $"Baseline snapshot captured at {now:yyyy-MM-dd HH:mm:ss}"
                });
            }
        }

        return new GateBaseline
        {
            GateId = gate.Id,
            AllowedViolations = currentViolations.Count,
            Entries = entries
        };
    }
}
