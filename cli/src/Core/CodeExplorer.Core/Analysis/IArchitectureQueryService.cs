using CodeExplorer.Core.Protocol;

namespace CodeExplorer.Core.Analysis;

/// <summary>
/// Single unified entry point for all architectural graph queries across REST, WebSocket, and MCP protocols.
/// </summary>
public interface IArchitectureQueryService
{
    /// <summary>
    /// Executes a standardized single-query architectural projection (C1 System Context, C2 Service Flow, C3 Component).
    /// </summary>
    Task<GraphDataDto> GetViewAsync(ArchitectureViewRequest request, CancellationToken ct = default);

    /// <summary>
    /// Retrieves the system architecture graph with materialized macro-relationships.
    /// </summary>
    Task<GraphDataDto> GetArchitectureGraphAsync(string? projectFilter = null, CancellationToken ct = default);

    /// <summary>
    /// Retrieves the 3-column dependency flow / neighborhood graph for a specific project.
    /// </summary>
    Task<GraphDataDto> GetProjectNeighborhoodAsync(string? projectName = null, CancellationToken ct = default);

    /// <summary>
    /// Retrieves all project names in the workspace.
    /// </summary>
    Task<List<string>> GetAllProjectsAsync(CancellationToken ct = default);

    /// <summary>
    /// Retrieves metadata and aggregate node/relationship statistics.
    /// </summary>
    Task<MetadataResponseDto> GetMetadataAsync(CancellationToken ct = default);

    /// <summary>
    /// Retrieves paginated nodes matching a kind or search query.
    /// </summary>
    Task<NodesResponseDto> GetNodesAsync(string? kind = null, int offset = 0, int limit = 50, string? search = null, CancellationToken ct = default);
}
