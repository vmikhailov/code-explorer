namespace CodeExplorer.Core.Gates.Models;

/// <summary>
/// An individual violation produced by a Semantic Invariant Gate.
/// </summary>
public record GateViolation
{
    public required string GateId { get; init; }
    public string? File { get; init; }
    public int Line { get; init; }
    public int Column { get; init; }
    public string? Symbol { get; init; }
    public string? Target { get; init; }
    public string? Message { get; init; }

    /// <summary>
    /// Line-shift immune fingerprint: SHA256(GateId + NormalizedFilePath + SymbolName + Target).
    /// </summary>
    public required string Fingerprint { get; init; }

    /// <summary>
    /// Two-dimensional source location (file:line:col or file:line or file).
    /// </summary>
    public string Location
    {
        get
        {
            var clean = CleanFilePath(File);
            if (string.IsNullOrEmpty(clean) || clean == "-") return "-";
            return Line > 0
                ? (Column > 0 ? $"{clean}:{Line}:{Column}" : $"{clean}:{Line}")
                : clean;
        }
    }

    /// <summary>
    /// Strips internal symbol prefixes ('ws:sym:') and AST qualifiers (':Function:', ':Type:', etc.)
    /// to yield an actual, clean relative file path.
    /// </summary>
    public static string CleanFilePath(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "-";

        var s = raw.Trim();
        if (s.StartsWith("ws:sym:", StringComparison.OrdinalIgnoreCase))
        {
            s = s["ws:sym:".Length..];
        }

        var colonIdx = s.IndexOf(':');
        if (colonIdx > 0)
        {
            var rest = s[colonIdx..];
            if (rest.StartsWith(":Function:", StringComparison.OrdinalIgnoreCase) ||
                rest.StartsWith(":Type:", StringComparison.OrdinalIgnoreCase) ||
                rest.StartsWith(":Class:", StringComparison.OrdinalIgnoreCase) ||
                rest.StartsWith(":Method:", StringComparison.OrdinalIgnoreCase) ||
                rest.StartsWith(":Member:", StringComparison.OrdinalIgnoreCase) ||
                rest.StartsWith(":Interface:", StringComparison.OrdinalIgnoreCase) ||
                rest.StartsWith(":Enum:", StringComparison.OrdinalIgnoreCase) ||
                rest.StartsWith(":Struct:", StringComparison.OrdinalIgnoreCase) ||
                rest.StartsWith(":Record:", StringComparison.OrdinalIgnoreCase) ||
                rest.StartsWith(":Property:", StringComparison.OrdinalIgnoreCase) ||
                rest.StartsWith(":Field:", StringComparison.OrdinalIgnoreCase) ||
                rest.StartsWith(":Constructor:", StringComparison.OrdinalIgnoreCase))
            {
                s = s[..colonIdx];
            }
        }

        return s.Replace('\\', '/');
    }
}

/// <summary>
/// Baseline entry representing an acknowledged legacy violation.
/// </summary>
public record GateBaselineEntry
{
    public required string Fingerprint { get; init; }
    public string? File { get; init; }
    public int Line { get; init; }
    public int Column { get; init; }
    public string? Symbol { get; init; }
    public string? Target { get; init; }
    public DateTimeOffset? AcknowledgedAt { get; init; }
    public string? Reason { get; init; }
}

/// <summary>
/// Baseline snapshot storing tolerated legacy debt for a gate.
/// </summary>
public record GateBaseline
{
    public required string GateId { get; init; }
    public int AllowedViolations { get; init; }
    public List<GateBaselineEntry> Entries { get; init; } = [];
}
