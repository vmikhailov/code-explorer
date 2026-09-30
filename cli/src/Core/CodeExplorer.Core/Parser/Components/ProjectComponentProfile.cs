namespace CodeExplorer.Core.Parser.Components;

/// <summary>
/// Composite architectural profile of a project, aggregated across all matched component and library parsers.
/// Combines bitwise capabilities, resolves dominant role, and collects attributes.
/// </summary>
public record ProjectComponentProfile
{
    public IReadOnlyList<ComponentAnalysisResult> Components { get; init; } = [];
    public ComponentCapabilities Capabilities { get; init; } = ComponentCapabilities.None;
    public LibraryRole PrimaryRole { get; init; } = LibraryRole.General;
    public bool IsLibrary { get; init; }
    public IReadOnlyList<string> EntryPoints { get; init; } = [];
    public IReadOnlyList<string> Endpoints { get; init; } = [];
    public IReadOnlyList<string> Schedules { get; init; } = [];
    public IReadOnlyList<string> Tests { get; init; } = [];
    public Dictionary<string, string> Metadata { get; init; } = [];

    public static ProjectComponentProfile Aggregate(
        IReadOnlyList<ComponentAnalysisResult> components,
        ProjectContext context)
    {
        var combinedCapabilities = ComponentCapabilities.None;
        var entryPoints = new List<string>();
        var endpoints = new List<string>();
        var schedules = new List<string>();
        var tests = new List<string>();
        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var c in components)
        {
            combinedCapabilities |= c.Capabilities;
            entryPoints.AddRange(c.EntryPoints);
            endpoints.AddRange(c.Endpoints);
            schedules.AddRange(c.Schedules);
            tests.AddRange(c.Tests);

            foreach (var kvp in c.Metadata)
            {
                metadata[kvp.Key] = kvp.Value;
            }
        }

        // Check manifest metadata hints
        var manifestType = context.ManifestProperties.GetValueOrDefault("manifest_type")?.ToLowerInvariant();
        var isTestManifest = manifestType == "test" || context.ManifestProperties.GetValueOrDefault("is_test_project") == "true";
        var isCliManifest = manifestType == "cli" || context.ManifestProperties.GetValueOrDefault("has_cli_bin") == "true";
        var isLibraryManifest = manifestType == "library" || context.ManifestProperties.GetValueOrDefault("is_library") == "true";
        var isWorkerManifest = manifestType == "worker";

        if (isTestManifest) combinedCapabilities |= ComponentCapabilities.TestRunner;
        if (isCliManifest) combinedCapabilities |= ComponentCapabilities.CliTool;
        if (isLibraryManifest) combinedCapabilities |= ComponentCapabilities.SharedLibrary;
        if (isWorkerManifest) combinedCapabilities |= ComponentCapabilities.QueueWorker;

        // Determine dominant role by priority
        var dominantRole = LibraryRole.General;

        var hasActiveWorkload = combinedCapabilities.HasFlag(ComponentCapabilities.FrontendApp) ||
                                combinedCapabilities.HasFlag(ComponentCapabilities.CliTool) ||
                                combinedCapabilities.HasFlag(ComponentCapabilities.ApiGateway) ||
                                combinedCapabilities.HasFlag(ComponentCapabilities.Scheduler) ||
                                combinedCapabilities.HasFlag(ComponentCapabilities.QueueWorker) ||
                                combinedCapabilities.HasFlag(ComponentCapabilities.HttpEndpoints);

        if (isTestManifest || (combinedCapabilities.HasFlag(ComponentCapabilities.TestRunner) && !hasActiveWorkload))
        {
            dominantRole = LibraryRole.TestFramework;
        }
        else if (combinedCapabilities.HasFlag(ComponentCapabilities.CliTool) || isCliManifest)
        {
            dominantRole = LibraryRole.CliFramework;
        }
        else if (combinedCapabilities.HasFlag(ComponentCapabilities.FrontendApp))
        {
            dominantRole = LibraryRole.FrontendFramework;
        }
        else if (combinedCapabilities.HasFlag(ComponentCapabilities.UiLibrary))
        {
            dominantRole = LibraryRole.UiComponentLibrary;
        }
        else if (combinedCapabilities.HasFlag(ComponentCapabilities.ApiGateway))
        {
            dominantRole = LibraryRole.ApiGateway;
        }
        else if (combinedCapabilities.HasFlag(ComponentCapabilities.Scheduler))
        {
            dominantRole = LibraryRole.Scheduler;
        }
        else if (combinedCapabilities.HasFlag(ComponentCapabilities.QueueWorker))
        {
            dominantRole = LibraryRole.WorkerService;
        }
        else if (combinedCapabilities.HasFlag(ComponentCapabilities.HttpEndpoints))
        {
            dominantRole = LibraryRole.WebService;
        }
        else if (combinedCapabilities.HasFlag(ComponentCapabilities.EgressClient))
        {
            dominantRole = LibraryRole.EgressClient;
        }
        else if (combinedCapabilities.HasFlag(ComponentCapabilities.SharedLibrary) || isLibraryManifest)
        {
            dominantRole = LibraryRole.SharedLibrary;
        }
        else
        {
            // Fallback to first non-general role from components if any
            var firstRole = components.FirstOrDefault(c => c.Role is not LibraryRole.General and not LibraryRole.Utility)?.Role;
            dominantRole = firstRole ?? LibraryRole.General;
        }

        // Determine IsLibrary flag
        bool isLib;
        if (combinedCapabilities.HasFlag(ComponentCapabilities.UiLibrary))
        {
            isLib = true;
        }
        else if (hasActiveWorkload)
        {
            // Active application/service/worker workloads are not libraries
            isLib = false;
        }
        else if (isTestManifest || dominantRole == LibraryRole.TestFramework)
        {
            isLib = true; // Tests without services are classified as auxiliary/test libraries
        }
        else
        {
            isLib = isLibraryManifest || combinedCapabilities.HasFlag(ComponentCapabilities.SharedLibrary);
        }

        return new ProjectComponentProfile
        {
            Components = components,
            Capabilities = combinedCapabilities,
            PrimaryRole = dominantRole,
            IsLibrary = isLib,
            EntryPoints = entryPoints.Distinct().ToList(),
            Endpoints = endpoints.Distinct().ToList(),
            Schedules = schedules.Distinct().ToList(),
            Tests = tests.Distinct().ToList(),
            Metadata = metadata
        };
    }
}
