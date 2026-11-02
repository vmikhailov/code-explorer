namespace CodeExplorer.Core.Gates.Models;

/// <summary>
/// Granularity level (scope) of the architectural invariant according to the
/// Fractal Orthogonal Invariant Matrix (FOIM).
/// </summary>
public enum InvariantScope
{
    Method = 1,
    Class = 2,
    Module = 3,
    Service = 4,
    System = 5
}
