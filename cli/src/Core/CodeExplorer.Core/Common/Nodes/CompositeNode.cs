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

    [JsonPropertyName("layer"), OntologyProperty("The architectural layer name.", isSystem: true)]
    public string? Layer { get; set; }

    [JsonPropertyName("layerId"), OntologyProperty("The architectural layer identifier.", isSystem: true)]
    public string? LayerId { get; set; }

    [JsonPropertyName("layerName"), OntologyProperty("The architectural layer display name.", isSystem: true)]
    public string? LayerName { get; set; }

    [JsonPropertyName("layerOrder"), OntologyProperty("The architectural layer display order.", isSystem: true)]
    public int? LayerOrder { get; set; }

    [JsonPropertyName("layerColor"), OntologyProperty("The architectural layer UI color.", isSystem: true)]
    public string? LayerColor { get; set; }

    [JsonPropertyName("layerIcon"), OntologyProperty("The architectural layer UI icon.", isSystem: true)]
    public string? LayerIcon { get; set; }

    [JsonPropertyName("is_semantic_entity"), OntologyProperty("Indicates whether the node is a semantic entity.", isSystem: true)]
    public bool? IsSemanticEntity { get; set; }

    [JsonPropertyName("project_id"), OntologyProperty("The identifier of the enclosing project.")]
    public string? ProjectId { get; set; }

    [JsonPropertyName("raw_name"), OntologyProperty("The unnormalized raw entity name.", isSystem: true)]
    public string? RawName { get; set; }

    [JsonPropertyName("clean_name"), OntologyProperty("The normalized clean entity name.", isSystem: true)]
    public string? CleanName { get; set; }

    [JsonIgnore]
    public abstract NodeKind Kind { get; }

    [JsonIgnore]
    public virtual NodeKind NodeKind => Kind;

    [JsonIgnore, OntologyProperty("The path of the folder or file relative to its parent container.")]
    public abstract string Path { get; init; }

    [JsonIgnore]
    public List<IOntologyNode> Children { get; } = [];

    [JsonIgnore]
    public List<Reference> References { get; } = [];
}
