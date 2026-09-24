using CodeExplorer.Core.Analysis;
using CodeExplorer.Core.Database;

namespace CodeExplorer.Core.Protocol;

/// <summary>
/// Facade forwarding directly to <see cref="ArchitectureViewEngine"/>.
/// </summary>
public static class GraphDataConverter
{
    public static Task<GraphDataDto> GetArchitectureGraphAsync(IGraphClient client, string? projectFilter = null, CancellationToken ct = default)
        => new ArchitectureViewEngine(client).GetSystemContextViewAsync(includeLibraries: true, projectFilter, ct);

    public static Task<GraphDataDto> GetProjectNeighborhoodAsync(IGraphClient client, string? projectName, CancellationToken ct = default)
        => new ArchitectureViewEngine(client).GetServiceFlowViewAsync(projectName, includeLibraries: true, ct);

    public static Task<List<string>> GetAllProjectsAsync(IGraphClient client, CancellationToken ct = default)
        => new ArchitectureViewEngine(client).GetAllProjectsAsync(ct);

    public static Task<Dictionary<string, string>> GetProjectPathsMapAsync(IGraphClient client, CancellationToken ct = default)
        => new ArchitectureViewEngine(client).GetProjectPathsMapAsync(ct);

    public static Task<MetadataResponseDto> GetMetadataAsync(IGraphClient client, CancellationToken ct = default)
        => new ArchitectureViewEngine(client).GetMetadataAsync(ct);

    public static Task<NodesResponseDto> GetNodesAsync(IGraphClient client, string? kind = null, int offset = 0, int limit = 50, string? search = null, CancellationToken ct = default)
        => new ArchitectureViewEngine(client).GetNodesAsync(kind, offset, limit, search, ct);
}
