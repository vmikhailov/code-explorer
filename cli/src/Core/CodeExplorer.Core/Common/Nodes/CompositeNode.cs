using System.Text.Json.Serialization;
using CodeExplorer.Cypher.Common;

namespace CodeExplorer.Core.Common.Nodes;

public abstract record CompositeNode(
    [property: JsonIgnore, OntologyProperty("A unique identifier for the node.")] string Id,
    Dictionary<string, string>? CustomProperties = null
) : IOntologyNode
{
    [JsonIgnore]
    public SymbolFlags Flags { get; set; }

    [JsonIgnore]
    public Dictionary<string, string>? CustomProperties { get; set; } = CustomProperties;

    [JsonPropertyName("layer")]
    public string? Layer { get; set; }

    [JsonPropertyName("layerId")]
    public string? LayerId { get; set; }

    [JsonPropertyName("layerName")]
    public string? LayerName { get; set; }

    [JsonPropertyName("layerOrder")]
    public int? LayerOrder { get; set; }

    [JsonPropertyName("layerColor")]
    public string? LayerColor { get; set; }

    [JsonPropertyName("layerIcon")]
    public string? LayerIcon { get; set; }

    [JsonPropertyName("is_semantic_entity")]
    public bool? IsSemanticEntity { get; set; }

    [JsonPropertyName("project_id")]
    public string? ProjectId { get; set; }

    [JsonPropertyName("raw_name")]
    public string? RawName { get; set; }

    [JsonPropertyName("clean_name")]
    public string? CleanName { get; set; }

    [JsonIgnore]
    public abstract NodeKind Kind { get; }

    [JsonIgnore]
    public virtual NodeKind NodeKind => Kind;

    [JsonIgnore]
    public abstract string Path { get; init; }

    [JsonIgnore]
    public List<IOntologyNode> Children { get; } = [];

    [JsonIgnore]
    public List<Reference> References { get; } = [];
}
