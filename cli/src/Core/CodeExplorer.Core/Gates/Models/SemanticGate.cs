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
/// Threshold configuration for warning and error classification.
/// </summary>
public record GateThresholdSettings
{
    public int? Warning { get; init; }
    public int? Error { get; init; }
}

/// <summary>
/// Represents a Semantic Invariant Gate (SIG) specification.
/// </summary>
public record SemanticGate
{
    public required string Id { get; init; }
    public GateCategory Category { get; init; } = GateCategory.Architectural;

    /// <summary>
    /// Target platform or language ecosystem (e.g. "agnostic", "dotnet", "typescript", "python", "go", "java").
    /// </summary>
    public string Platform { get; init; } = "agnostic";

    public InvariantScope Scope { get; init; } = InvariantScope.Module;
    public InvariantDimension Dimension { get; init; } = InvariantDimension.Boundaries;
    public required string Name { get; init; }
    public GateSeverity Severity { get; init; } = GateSeverity.Error;
    public string? Description { get; init; }
    public string? Remediation { get; init; }

    /// <summary>
    /// Parameterized violation message template (e.g. "Async function '{fn.name}' invokes blocking call: '{target.name}'").
    /// Supports standard fields ({file}, {line}, {symbol}, {target}) and node properties ({node.prop}).
    /// </summary>
    public string? Message { get; init; }

    /// <summary>
    /// Alias for Message for template clarity.
    /// </summary>
    public string? MessageTemplate => Message;

    /// <summary>
    /// Optional thresholds for classifying infractions into warnings vs blocking errors.
    /// </summary>
    public GateThresholdSettings? Thresholds { get; init; }

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
