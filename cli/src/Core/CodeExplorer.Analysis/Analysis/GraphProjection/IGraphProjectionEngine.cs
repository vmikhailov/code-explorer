using CodeExplorer.Core.Protocol;
using CodeExplorer.Cypher.Common;

namespace CodeExplorer.Core.Analysis.GraphProjection;

/// <summary>
/// Target container abstraction for projection levels (C2: Project/Service, C3: Type/Class).
/// </summary>
public enum ProjectionLevel
{
    /// <summary>C2: Cross-project / service level (containers are Projects/Services/Databases).</summary>
    Container = 2,

    /// <summary>C3: Internal project level (containers are Types/Classes/Interfaces and Tables/Topics).</summary>
    Component = 3
}

/// <summary>
/// Configuration request for graph projection.
/// </summary>
public sealed record GraphProjectionRequest
{
    /// <summary>Projection level (C2 Container or C3 Component).</summary>
    public ProjectionLevel Level { get; init; } = ProjectionLevel.Component;

    /// <summary>Scope identifier (e.g. Project ID or workspace path).</summary>
    public string? Scope { get; init; }

    /// <summary>Whether to include external resources (Databases, Tables, Topics, ExternalServices).</summary>
    public bool IncludeResources { get; init; } = true;

    /// <summary>Whether to exclude test files and test classes.</summary>
    public bool ExcludeTests { get; init; } = true;

    /// <summary>Active filter presets (e.g. hideUtilities, hideModels, cyclesOnly).</summary>
    public IReadOnlyDictionary<string, bool> Presets { get; init; } = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Contract for projecting a code property graph into an architecture map view.
/// </summary>
public interface IGraphProjectionEngine
{
    /// <summary>
    /// Projects ontology nodes and edges into a unified <see cref="GraphDataDto"/>.
    /// </summary>
    Task<GraphDataDto> ProjectGraphAsync(GraphProjectionRequest request, CancellationToken ct = default);
}
