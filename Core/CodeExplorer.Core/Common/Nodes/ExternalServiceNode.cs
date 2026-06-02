using System.Text.Json.Serialization;

namespace CodeExplorer.Common;

public record ExternalServiceNode(
    [property: JsonIgnore] string Id,
    string Name,
    string Protocol,
    string DomainOrService,
    [property: JsonIgnore] Dictionary<string, string>? Extensions = null
) : IOntologyNode
{
    [JsonIgnore]
    public string Kind => OntologyConstants.NodeLabels.ExternalService;
}
