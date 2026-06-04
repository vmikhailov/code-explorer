using System.Text.Json.Serialization;

namespace CodeExplorer.Core.Common.Nodes;

public record PackageNode(
    string Id,
    string Name,
    string Version,
    string Type,
    string Path,
    Dictionary<string, string>? Extensions = null
) : CompositeNode(Id, Extensions)
{
    [JsonIgnore]
    public override string Kind => OntologyConstants.NodeLabels.Package;
}
