using CodeExplorer.Core.Gates.Models;

namespace CodeExplorer.Core.Gates.Storage;

/// <summary>
/// Interface for discovering and managing Semantic Invariant Gates and their debt baselines.
/// </summary>
public interface IGateRepository
{
    /// <summary>
    /// Gets all gates (built-in and workspace-specific).
    /// </summary>
    Task<IReadOnlyList<SemanticGate>> GetAllGatesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds a gate by its unique identifier (e.g. "SIG-P-B-001").
    /// </summary>
    Task<SemanticGate?> GetGateByIdAsync(string gateId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads the debt baseline snapshot for a given gate. Returns null if no baseline exists.
    /// </summary>
    Task<GateBaseline?> LoadBaselineAsync(SemanticGate gate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves or updates the debt baseline snapshot for a given gate.
    /// </summary>
    Task SaveBaselineAsync(GateBaseline baseline, SemanticGate gate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Initializes default starter gates in the workspace (.codeexplorer/gates).
    /// </summary>
    Task<int> InitStarterGatesAsync(bool overwrite = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves or updates a gate YAML definition in the workspace (.codeexplorer/gates).
    /// Returns the workspace-relative path of the saved gate file.
    /// </summary>
    Task<string> SaveGateAsync(SemanticGate gate, string? targetPath = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a workspace gate YAML file. Returns true if found and deleted.
    /// </summary>
    Task<bool> DeleteGateAsync(string gateId, CancellationToken cancellationToken = default);
}
