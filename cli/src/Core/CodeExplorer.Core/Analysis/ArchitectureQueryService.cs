using CodeExplorer.Core.Database;
using CodeExplorer.Core.Protocol;

namespace CodeExplorer.Core.Analysis;

/// <summary>
/// Default implementation of <see cref="IArchitectureQueryService"/> delegating to
/// <see cref="ArchitectureViewEngine"/> and optimized graph projection converters.
/// </summary>
public class ArchitectureQueryService(IGraphClient db) : IArchitectureQueryService
{
    private readonly ArchitectureViewEngine _viewEngine = new(db);

    public Task<GraphDataDto> GetViewAsync(ArchitectureViewRequest request, CancellationToken ct = default)
    {
        return _viewEngine.GetViewAsync(request, ct);
    }

    public Task<GraphDataDto> GetArchitectureGraphAsync(string? projectFilter = null, CancellationToken ct = default)
    {
        return _viewEngine.GetSystemContextViewAsync(includeLibraries: true, projectFilter, ct);
    }

    public Task<GraphDataDto> GetProjectNeighborhoodAsync(string? projectName = null, CancellationToken ct = default)
    {
        return _viewEngine.GetServiceFlowViewAsync(projectName, includeLibraries: true, ct);
    }

    public Task<List<string>> GetAllProjectsAsync(CancellationToken ct = default)
    {
        return _viewEngine.GetAllProjectsAsync(ct);
    }

    public Task<MetadataResponseDto> GetMetadataAsync(CancellationToken ct = default)
    {
        return _viewEngine.GetMetadataAsync(ct);
    }

    public Task<NodesResponseDto> GetNodesAsync(string? kind = null, int offset = 0, int limit = 50, string? search = null, string? service = null, CancellationToken ct = default)
    {
        return _viewEngine.GetNodesAsync(kind, offset, limit, search, service, ct);
    }

    public Task<OntologyLayersResponseDto> GetOntologyLayersAsync(CancellationToken ct = default)
    {
        return _viewEngine.GetOntologyLayersAsync(ct);
    }

    public Task<List<ServiceSummaryDto>> GetServicesOntologySummaryAsync(CancellationToken ct = default)
    {
        return _viewEngine.GetServicesOntologySummaryAsync(ct);
    }

    public Task<ServiceOntologyDetailsDto> GetServiceCapabilitiesAsync(string serviceName, CancellationToken ct = default)
    {
        return _viewEngine.GetServiceCapabilitiesAsync(serviceName, ct);
    }

    public Task<DomainArchitectureDto> GetDomainArchitectureAsync(bool includeLibraries = true, CancellationToken ct = default)
    {
        return _viewEngine.GetDomainArchitectureAsync(includeLibraries, ct);
    }

    public Task<ServiceContractDto> GetServiceContractsAsync(string serviceName, string direction = "all", CancellationToken ct = default)
    {
        return _viewEngine.GetServiceContractsAsync(serviceName, direction, ct);
    }

    public Task<CrossServiceFlowDto> TraceCrossServiceFlowAsync(string startService, string? entryPoint = null, int maxDepth = 3, CancellationToken ct = default)
    {
        return _viewEngine.TraceCrossServiceFlowAsync(startService, entryPoint, maxDepth, ct);
    }
}
