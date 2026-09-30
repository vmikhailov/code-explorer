namespace CodeExplorer.Core.Parser.Components.Parsers;

/// <summary>
/// Component parser for .NET background schedulers (Hangfire, Quartz.NET).
/// </summary>
public class DotNetSchedulerComponentParser : IComponentLibraryParser
{
    public string Id => "dotnet-scheduler";
    public string Name => "Hangfire / Quartz Scheduler";

    public bool CanHandle(ProjectContext context)
    {
        return context.Dependencies.Any(d =>
            d.StartsWith("Hangfire", StringComparison.OrdinalIgnoreCase) ||
            d.StartsWith("Quartz", StringComparison.OrdinalIgnoreCase));
    }

    public ComponentAnalysisResult AnalyzeManifest(ProjectContext context)
    {
        return new ComponentAnalysisResult
        {
            ComponentId = Id,
            ComponentName = Name,
            Role = LibraryRole.Scheduler,
            Capabilities = ComponentCapabilities.Scheduler,
            Metadata = new Dictionary<string, string>
            {
                ["is_scheduler"] = "true"
            }
        };
    }
}
