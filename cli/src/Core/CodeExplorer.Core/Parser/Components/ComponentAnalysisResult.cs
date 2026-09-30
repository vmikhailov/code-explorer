namespace CodeExplorer.Core.Parser.Components;

/// <summary>
/// Analysis result emitted by a single component/library parser for a project.
/// Contains the specific role, discovered capabilities, and concrete attributes
/// (e.g. entrypoints, endpoints, schedules, test cases, metadata).
/// </summary>
public record ComponentAnalysisResult
{
    public string ComponentId { get; init; } = "";
    public string ComponentName { get; init; } = "";
    public LibraryRole Role { get; init; } = LibraryRole.General;
    public ComponentCapabilities Capabilities { get; init; } = ComponentCapabilities.None;
    public IReadOnlyList<string> EntryPoints { get; init; } = [];
    public IReadOnlyList<string> Endpoints { get; init; } = [];
    public IReadOnlyList<string> Schedules { get; init; } = [];
    public IReadOnlyList<string> Tests { get; init; } = [];
    public Dictionary<string, string> Metadata { get; init; } = [];
}
