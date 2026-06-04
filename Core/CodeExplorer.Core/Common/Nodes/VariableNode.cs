using System.Text.Json.Serialization;

namespace CodeExplorer.Core.Common.Nodes;

public record VariableNode(
    string Id,
    string Name,
    string Symbol,
    string FilePath,
    int StartLine,
    int EndLine,
    int StartCol,
    int EndCol,
    Dictionary<string, string>? Extensions = null
) : CompositeNode(Id, Extensions)
{
    [JsonIgnore]
    public override string Kind => OntologyConstants.NodeLabels.Variable;
}
