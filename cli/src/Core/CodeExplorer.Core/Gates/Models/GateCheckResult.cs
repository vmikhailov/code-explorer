namespace CodeExplorer.Core.Gates.Models;

/// <summary>
/// Status of a Semantic Invariant Gate evaluation.
/// </summary>
public enum GateCheckStatus
{
    /// <summary>
    /// Zero violations detected.
    /// </summary>
    Passed = 0,

    /// <summary>
    /// Violations exist, but all are recognized within the tolerated baseline.
    /// </summary>
    PassedTolerated = 1,

    /// <summary>
    /// New un-baselined violations detected.
    /// </summary>
    FailedNew = 2,

    /// <summary>
    /// Gate failed execution due to query syntax or engine error.
    /// </summary>
    Error = 3
}

/// <summary>
/// Result of evaluating a Semantic Invariant Gate.
/// </summary>
public record GateCheckResult
{
    public required SemanticGate Gate { get; init; }
    public required GateCheckStatus Status { get; init; }
    public List<GateViolation> Violations { get; init; } = [];
    public List<GateViolation> NewViolations { get; init; } = [];
    public List<GateViolation> ToleratedViolations { get; init; } = [];
    public int BaselineCount { get; init; }
    public bool CanRatchetDown => Violations.Count < BaselineCount;
    public TimeSpan Duration { get; init; }
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Comprehensive summary of evaluating a suite of Semantic Invariant Gates.
/// </summary>
public record GateSuiteResult
{
    public required IReadOnlyList<GateCheckResult> Results { get; init; }
    public int TotalGates => Results.Count;
    public int PassedCount => Results.Count(r => r.Status is GateCheckStatus.Passed or GateCheckStatus.PassedTolerated);
    public int FailedCount => Results.Count(r => r.Status is GateCheckStatus.FailedNew or GateCheckStatus.Error);
    public int NewViolationsCount => Results.Sum(r => r.NewViolations.Count);
    public int ToleratedViolationsCount => Results.Sum(r => r.ToleratedViolations.Count);
    public int RatchetOpportunities => Results.Count(r => r.CanRatchetDown);
    public bool IsSuccess => FailedCount == 0;
    public TimeSpan TotalDuration { get; init; }
}
