using System.Text.Json.Serialization;

namespace CodeExplorer.Common;

public record PackageNode(
    string Id,
    string Name,
    string Version,
    string Type,
    Dictionary<string, string>? Extensions = null
) : CompositeNode(Id, Extensions)
{
    [JsonIgnore]
    public override string Kind => OntologyConstants.NodeLabels.Package;
}
