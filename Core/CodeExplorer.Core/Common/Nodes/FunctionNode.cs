using System.Text.Json.Serialization;

namespace CodeExplorer.Common;

public record FunctionNode(
    [property: JsonIgnore] string Id,
    string Name,
    string Symbol,
    string FilePath,
    int StartLine,
    int EndLine,
    int StartCol,
    int EndCol,
    [property: JsonIgnore] Dictionary<string, string>? Extensions = null
) : IOntologyNode
{
    [JsonIgnore]
    public string Kind => OntologyConstants.NodeLabels.Function;
}
