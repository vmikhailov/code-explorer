using CodeExplorer.Core.Parser.Components;

namespace CodeExplorer.Parser.CSharp.Components;

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
            d.StartWithIC("Hangfire") ||
            d.StartWithIC("Quartz"));
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
