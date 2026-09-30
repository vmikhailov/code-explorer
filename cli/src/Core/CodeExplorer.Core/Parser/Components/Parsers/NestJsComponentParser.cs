namespace CodeExplorer.Core.Parser.Components.Parsers;

/// <summary>
/// Component parser for NestJS applications.
/// Identifies whether the NestJS service functions as a Scheduler (@nestjs/schedule),
/// Queue Worker (@nestjs/bull, @nestjs/bullmq), or Web Service (@nestjs/core).
/// </summary>
public class NestJsComponentParser : IComponentLibraryParser
{
    public string Id => "nestjs";
    public string Name => "NestJS";

    public bool CanHandle(ProjectContext context)
    {
        return context.Dependencies.Any(d =>
            d.StartsWith("@nestjs/", StringComparison.OrdinalIgnoreCase) ||
            d.Equals("nestjs", StringComparison.OrdinalIgnoreCase));
    }

    public ComponentAnalysisResult AnalyzeManifest(ProjectContext context)
    {
        var hasSchedule = context.Dependencies.Any(d =>
            d.Equals("@nestjs/schedule", StringComparison.OrdinalIgnoreCase));

        var hasQueue = context.Dependencies.Any(d =>
            d.Equals("@nestjs/bull", StringComparison.OrdinalIgnoreCase) ||
            d.Equals("@nestjs/bullmq", StringComparison.OrdinalIgnoreCase));

        var hasMicroservices = context.Dependencies.Any(d =>
            d.Equals("@nestjs/microservices", StringComparison.OrdinalIgnoreCase));

        var capabilities = ComponentCapabilities.None;
        var metadata = new Dictionary<string, string>();

        if (hasSchedule)
        {
            capabilities |= ComponentCapabilities.Scheduler;
            metadata["has_schedule"] = "true";
            metadata["is_scheduler"] = "true";
        }

        if (hasQueue)
        {
            capabilities |= ComponentCapabilities.QueueWorker;
            metadata["has_queue_worker"] = "true";
            metadata["is_queue_worker"] = "true";
        }

        if (hasMicroservices)
        {
            metadata["has_microservices"] = "true";
        }

        // NestJS core provides HTTP endpoints
        capabilities |= ComponentCapabilities.HttpEndpoints;

        var role = LibraryRole.WebService;
        if (hasSchedule)
        {
            role = LibraryRole.Scheduler;
        }
        else if (hasQueue)
        {
            role = LibraryRole.WorkerService;
        }

        return new ComponentAnalysisResult
        {
            ComponentId = Id,
            ComponentName = Name,
            Role = role,
            Capabilities = capabilities,
            Metadata = metadata
        };
    }
}
