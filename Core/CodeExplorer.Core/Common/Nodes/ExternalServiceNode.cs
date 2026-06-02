using System.Text.Json.Serialization;

namespace CodeExplorer.Common;

public record ExternalServiceNode(
    string Id,
    string Name,
    string Protocol,
    string DomainOrService,
    Dictionary<string, string>? Extensions = null
) : CompositeNode(Id, Extensions)
{
    [JsonIgnore]
    public override string Kind => OntologyConstants.NodeLabels.ExternalService;
}
