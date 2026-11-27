using System.Text.Json.Serialization;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;

namespace CodeExplorer.Core.Common.Nodes.Layer4_Semantic;

[OntologyNode(
    label: OntologyConstants.NodeLabels.Procedure,
    idScheme: "{workspaceId}:proc:{procedureName}",
    purpose: "Represents a stored procedure in a database.",
    layer: OntologyConstants.Layers.Semantic,
    icon: "database",
    order: 23,
    pluralLabel: "Stored Procedures"
)]
[OntologyEdge<QueryNode>(OntologyConstants.Relationships.Contains)]
[OntologyEdge<FunctionNode>(OntologyConstants.Relationships.AccessedBy)]
[OntologyEdge<CallArgumentNode>(OntologyConstants.Relationships.TargetedBy)]
[OntologyEdge<TableNode>(OntologyConstants.Relationships.ReadsFrom)]
[OntologyEdge<TableNode>(OntologyConstants.Relationships.WritesTo)]
[OntologyEdge<ProcedureNode>(OntologyConstants.Relationships.Calls)]
public record ProcedureNode(
    string Id,
    string Name,
    string Path,
    Dictionary<string, string>? CustomProperties = null
) : CompositeNode(Id, CustomProperties)
{
    [JsonPropertyName("engine"), OntologyProperty("Database engine hosting this routine (e.g. postgresql, sqlserver, mysql).")]
    public string? Engine { get; set; }

    [JsonPropertyName("schema"), OntologyProperty("Database schema qualifying this routine (e.g. dbo, public).")]
    public string? Schema { get; set; }

    [JsonPropertyName("sub_kind"), OntologyProperty("Specific database routine kind (procedure, function, trigger).")]
    public string? SubKind { get; set; }

    [JsonPropertyName("trigger_table"), OntologyProperty("Target table name that fires this trigger, if sub_kind is trigger.")]
    public string? TriggerTable { get; set; }

    [JsonPropertyName("trigger_event"), OntologyProperty("Trigger timing and event (e.g. AFTER INSERT, BEFORE UPDATE).")]
    public string? TriggerEvent { get; set; }

    [JsonIgnore]
    public override NodeKind Kind => NodeKind.Procedure;
}
