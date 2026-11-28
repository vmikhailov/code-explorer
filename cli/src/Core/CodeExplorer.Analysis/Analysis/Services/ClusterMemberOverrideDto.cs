using System.Text.Json.Serialization;

namespace CodeExplorer.Core.Analysis;

public sealed class ClusterMemberOverrideDto
{
    [JsonPropertyName("cluster")]
    public string Cluster { get; set; } = string.Empty;

    [JsonPropertyName("context")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Context { get; set; }
}
