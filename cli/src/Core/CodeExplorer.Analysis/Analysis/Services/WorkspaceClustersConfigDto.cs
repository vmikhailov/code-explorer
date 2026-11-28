using System.Text.Json.Serialization;

namespace CodeExplorer.Core.Analysis;

public sealed class WorkspaceClustersConfigDto
{
    [JsonPropertyName("clusters")]
    public List<ClusterDefinitionDto> Clusters { get; set; } = [];

    [JsonPropertyName("overrides")]
    public Dictionary<string, ClusterMemberOverrideDto> Overrides { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
