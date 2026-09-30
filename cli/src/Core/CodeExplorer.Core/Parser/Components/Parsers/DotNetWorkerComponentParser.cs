namespace CodeExplorer.Core.Parser.Components.Parsers;

/// <summary>
/// Component parser for .NET message queues and asynchronous workers (MassTransit, RabbitMQ, Kafka).
/// </summary>
public class DotNetWorkerComponentParser : IComponentLibraryParser
{
    public string Id => "dotnet-worker";
    public string Name => "MassTransit / Message Worker";

    public bool CanHandle(ProjectContext context)
    {
        return context.Dependencies.Any(d =>
            d.StartsWith("MassTransit", StringComparison.OrdinalIgnoreCase) ||
            d.StartsWith("RabbitMQ.Client", StringComparison.OrdinalIgnoreCase) ||
            d.StartsWith("Confluent.Kafka", StringComparison.OrdinalIgnoreCase));
    }

    public ComponentAnalysisResult AnalyzeManifest(ProjectContext context)
    {
        return new ComponentAnalysisResult
        {
            ComponentId = Id,
            ComponentName = Name,
            Role = LibraryRole.WorkerService,
            Capabilities = ComponentCapabilities.QueueWorker,
            Metadata = new Dictionary<string, string>
            {
                ["is_queue_worker"] = "true"
            }
        };
    }
}
