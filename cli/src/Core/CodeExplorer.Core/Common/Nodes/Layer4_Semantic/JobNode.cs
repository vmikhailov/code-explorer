using System.Text.Json.Serialization;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;

namespace CodeExplorer.Core.Common.Nodes.Layer4_Semantic;

[OntologyNode(
    label: OntologyConstants.NodeLabels.Job,
    idScheme: "{workspaceId}:job:{subKind}:{jobName}",
    purpose: "Represents a scheduled cron task, recurring background worker, queued job, or durable workflow/activity.",
    layer: OntologyConstants.Layers.Semantic,
    icon: "clock",
    order: 14,
    pluralLabel: "Scheduled Jobs & Workflows"
)]
[OntologyEdge<FunctionNode>(OntologyConstants.Relationships.Triggers)]
[OntologyEdge<FileNode>(OntologyConstants.Relationships.DeclaredIn)]
public record JobNode(
    string Id,
    [property: OntologyProperty("The scheduled job, task, or workflow name.")] string Name,
    [property: OntologyProperty("The workspace-relative path of the declaring file.")] string Path,
    [property: JsonPropertyName("sub_kind"), OntologyProperty("The execution model of the job (cron, recurring, fire_and_forget, workflow, activity, background_worker).")] JobKind SubKind,
    Dictionary<string, string>? CustomProperties = null
) : CompositeNode(Id, CustomProperties)
{
    [JsonPropertyName("schedule_expr"), OntologyProperty("Cron expression or fixed interval specification when applicable.")]
    public string? ScheduleExpr { get; set; }

    [JsonPropertyName("queue"), OntologyProperty("Target job queue or task routing key when applicable.")]
    public string? Queue { get; set; }

    [JsonPropertyName("framework"), OntologyProperty("Detected scheduler or workflow framework (e.g. spring-scheduled, hangfire, quartz, nestjs-schedule, celery, temporal).")]
    public string? Framework { get; set; }

    [JsonPropertyName("file_path"), OntologyProperty("Workspace-relative source file declaring the job.")]
    public string? FilePath { get; set; }

    [JsonPropertyName("start_line"), OntologyProperty("1-indexed starting line number of the job handler.")]
    public int? StartLine { get; set; }

    [JsonIgnore]
    public override NodeKind Kind => NodeKind.Job;
}
