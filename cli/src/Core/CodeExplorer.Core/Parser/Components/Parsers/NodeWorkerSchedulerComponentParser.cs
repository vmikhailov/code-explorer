namespace CodeExplorer.Core.Parser.Components.Parsers;

/// <summary>
/// Component parser for Node.js queue workers and job schedulers
/// (BullMQ, Bull, amqplib, KafkaJS, node-cron, agenda, etc.).
/// </summary>
public class NodeWorkerSchedulerComponentParser : IComponentLibraryParser
{
    public string Id => "node-worker-scheduler";
    public string Name => "Node Worker/Scheduler";

    private static readonly HashSet<string> SchedulerPackages = new(StringComparer.OrdinalIgnoreCase)
    {
        "node-cron", "cron", "agenda", "bree", "node-schedule"
    };

    private static readonly HashSet<string> WorkerPackages = new(StringComparer.OrdinalIgnoreCase)
    {
        "bullmq", "bull", "amqplib", "kafkajs", "@cloudflare/workers-types", "wrangler", "rhea"
    };

    public bool CanHandle(ProjectContext context)
    {
        return context.Dependencies.Any(d => SchedulerPackages.Contains(d) || WorkerPackages.Contains(d));
    }

    public ComponentAnalysisResult AnalyzeManifest(ProjectContext context)
    {
        var hasScheduler = context.Dependencies.Any(d => SchedulerPackages.Contains(d));
        var hasWorker = context.Dependencies.Any(d => WorkerPackages.Contains(d));

        var capabilities = ComponentCapabilities.None;
        var metadata = new Dictionary<string, string>();

        if (hasScheduler)
        {
            capabilities |= ComponentCapabilities.Scheduler;
            metadata["is_scheduler"] = "true";
        }

        if (hasWorker)
        {
            capabilities |= ComponentCapabilities.QueueWorker;
            metadata["is_queue_worker"] = "true";
        }

        var role = hasScheduler ? LibraryRole.Scheduler : LibraryRole.WorkerService;

        return new ComponentAnalysisResult
        {
            ComponentId = Id,
            ComponentName = hasScheduler ? "Node Scheduler" : "Node Worker",
            Role = role,
            Capabilities = capabilities,
            Metadata = metadata
        };
    }
}
