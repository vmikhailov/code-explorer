namespace CodeExplorer.Core.Gates.Models;

/// <summary>
/// High-level categorization of a semantic gate:
/// Architectural (language- and runtime-agnostic structural invariants) vs
/// Platform (framework-, runtime-, or language-specific invariants).
/// </summary>
public enum GateCategory
{
    Architectural = 1,
    Platform = 2
}
