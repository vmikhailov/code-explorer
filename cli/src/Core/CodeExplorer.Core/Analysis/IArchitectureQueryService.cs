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
    /// Retrieves paginated nodes matching a kind or search query, optionally scoped to a service.
    /// </summary>
    Task<NodesResponseDto> GetNodesAsync(string? kind = null, int offset = 0, int limit = 50, string? search = null, string? service = null, CancellationToken ct = default);

    /// <summary>
    /// Retrieves pre-calculated 5-layer ontology structure with categories, icons, and exact counts.
    /// </summary>
    Task<OntologyLayersResponseDto> GetOntologyLayersAsync(CancellationToken ct = default);

    /// <summary>
    /// Retrieves ontology services summary list with component counts (endpoints, dbs, topics, external APIs).
    /// </summary>
    Task<List<ServiceSummaryDto>> GetServicesOntologySummaryAsync(CancellationToken ct = default);

    /// <summary>
    /// Retrieves full ontology capability groups (endpoints, dbs, topics, external) for a specific service.
    /// </summary>
    Task<ServiceOntologyDetailsDto> GetServiceCapabilitiesAsync(string serviceName, CancellationToken ct = default);

    /// <summary>
    /// Retrieves synthesized domain architecture (Bounded Contexts) with macro-edges and statistics.
    /// </summary>
    Task<DomainArchitectureDto> GetDomainArchitectureAsync(bool includeLibraries = true, CancellationToken ct = default);

    /// <summary>
    /// Retrieves ingress and egress communication contracts for a given service.
    /// </summary>
    Task<ServiceContractDto> GetServiceContractsAsync(string serviceName, string direction = "all", CancellationToken ct = default);

    /// <summary>
    /// Traces end-to-end execution flow across services starting from a service or entry point.
    /// </summary>
    Task<CrossServiceFlowDto> TraceCrossServiceFlowAsync(string startService, string? entryPoint = null, int maxDepth = 3, CancellationToken ct = default);
}

