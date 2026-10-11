namespace CodeExplorer.Core.Gates.Models;

/// <summary>
/// Configuration for monotonic technical debt ratchet.
/// </summary>
public record GateRatchetSettings
{
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// Relative path to baseline JSON file from workspace root, or null for default path.
    /// </summary>
    public string? Baseline { get; init; }
}

/// <summary>
/// Represents a Semantic Invariant Gate (SIG) specification.
/// </summary>
public record SemanticGate
{
    public required string Id { get; init; }
    public InvariantScope Scope { get; init; } = InvariantScope.Module;
    public InvariantDimension Dimension { get; init; } = InvariantDimension.Boundaries;
    public required string Name { get; init; }
    public GateSeverity Severity { get; init; } = GateSeverity.Error;
    public string? Description { get; init; }
    public string? Remediation { get; init; }

    /// <summary>
    /// OpenCypher query following the Zero-Match Contract.
    /// Returns 0 rows if invariant holds; returns violating rows if violated.
    /// Standard return columns: file, line, symbol, target, message.
    /// </summary>
    public required string Query { get; init; }

    public GateRatchetSettings Ratchet { get; init; } = new();

    public Dictionary<string, string> Parameters { get; init; } = new();

    /// <summary>
    /// Source of this gate definition (e.g. "BuiltIn", or file path).
    /// </summary>
    public string Source { get; init; } = "BuiltIn";

    /// <summary>
    /// FOIM Coordinate representation: e.g. [P, B]
    /// </summary>
    public string CoordinateCode => $"[{Scope.ToString()[0]}, {Dimension.ToString()[0]}]";
}
