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

    public Task<NodesResponseDto> GetNodesAsync(string? kind = null, int offset = 0, int limit = 50, string? search = null, CancellationToken ct = default)
    {
        return _viewEngine.GetNodesAsync(kind, offset, limit, search, ct);
    }
}
