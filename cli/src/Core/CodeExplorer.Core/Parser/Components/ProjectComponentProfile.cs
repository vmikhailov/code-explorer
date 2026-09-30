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
    public IReadOnlyList<LibraryRole> SecondaryRoles { get; init; } = [];
    public IReadOnlyList<LibraryRole> AllRoles { get; init; } = [];
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

        // Collect all detected candidate roles
        var allRoles = new HashSet<LibraryRole>();
        foreach (var c in components)
        {
            if (c.Role is not LibraryRole.General and not LibraryRole.Utility)
            {
                allRoles.Add(c.Role);
            }
        }

        if (combinedCapabilities.HasFlag(ComponentCapabilities.HttpEndpoints)) allRoles.Add(LibraryRole.WebService);
        if (combinedCapabilities.HasFlag(ComponentCapabilities.Scheduler)) allRoles.Add(LibraryRole.Scheduler);
        if (combinedCapabilities.HasFlag(ComponentCapabilities.QueueWorker)) allRoles.Add(LibraryRole.WorkerService);
        if (combinedCapabilities.HasFlag(ComponentCapabilities.FrontendApp)) allRoles.Add(LibraryRole.FrontendFramework);
        if (combinedCapabilities.HasFlag(ComponentCapabilities.UiLibrary)) allRoles.Add(LibraryRole.UiComponentLibrary);
        if (combinedCapabilities.HasFlag(ComponentCapabilities.CliTool) || isCliManifest) allRoles.Add(LibraryRole.CliFramework);
        if (combinedCapabilities.HasFlag(ComponentCapabilities.ApiGateway)) allRoles.Add(LibraryRole.ApiGateway);
        if (combinedCapabilities.HasFlag(ComponentCapabilities.EgressClient)) allRoles.Add(LibraryRole.EgressClient);
        if (combinedCapabilities.HasFlag(ComponentCapabilities.TestRunner) || isTestManifest) allRoles.Add(LibraryRole.TestFramework);
        if (combinedCapabilities.HasFlag(ComponentCapabilities.SharedLibrary) || isLibraryManifest) allRoles.Add(LibraryRole.SharedLibrary);

        // Check if project is explicitly declared as a dedicated worker or scheduler
        var projName = (context.ProjectName ?? "").ToLowerInvariant();
        var dirName = Path.GetFileName(context.DirectoryPath ?? "").ToLowerInvariant();
        var isExplicitWorkerOrScheduler = isWorkerManifest ||
                                          context.ManifestProperties.GetValueOrDefault("framework_type") == "worker" ||
                                          projName.EndsWith("-worker") || projName.EndsWith(".worker") || projName.EndsWith("_worker") ||
                                          projName.EndsWith("-scheduler") || projName.EndsWith(".scheduler") || projName.EndsWith("_scheduler") ||
                                          projName.EndsWith("-consumer") || projName.EndsWith("_consumer") ||
                                          dirName.EndsWith("-worker") || dirName.EndsWith("-scheduler") || dirName.EndsWith("-consumer");

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
        else if (combinedCapabilities.HasFlag(ComponentCapabilities.HttpEndpoints) &&
                 (combinedCapabilities.HasFlag(ComponentCapabilities.Scheduler) || combinedCapabilities.HasFlag(ComponentCapabilities.QueueWorker)))
        {
            if (isExplicitWorkerOrScheduler)
            {
                dominantRole = combinedCapabilities.HasFlag(ComponentCapabilities.Scheduler)
                    ? LibraryRole.Scheduler
                    : LibraryRole.WorkerService;
            }
            else
            {
                // Full API microservice with background cron or queue consumption
                dominantRole = LibraryRole.WebService;
            }
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
            var firstRole = allRoles.FirstOrDefault(r => r is not LibraryRole.General and not LibraryRole.Utility);
            dominantRole = firstRole != LibraryRole.General ? firstRole : LibraryRole.General;
        }

        if (dominantRole != LibraryRole.General)
        {
            allRoles.Add(dominantRole);
        }

        var secondaryRoles = allRoles.Where(r => r != dominantRole).Distinct().ToList();

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
            SecondaryRoles = secondaryRoles,
            AllRoles = allRoles.ToList(),
            IsLibrary = isLib,
            EntryPoints = entryPoints.Distinct().ToList(),
            Endpoints = endpoints.Distinct().ToList(),
            Schedules = schedules.Distinct().ToList(),
            Tests = tests.Distinct().ToList(),
            Metadata = metadata
        };
    }
}
