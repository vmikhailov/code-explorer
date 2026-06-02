using System.Text.Json.Serialization;

namespace CodeExplorer.Common;

public record ProcedureNode(
    string Id,
    string Name,
    string Path,
    Dictionary<string, string>? Extensions = null
) : CompositeNode(Id, Extensions)
{
    [JsonIgnore]
    public override string Kind => OntologyConstants.NodeLabels.Procedure;
}
