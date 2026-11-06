using CodeExplorer.Cypher.Common;
using System.Text.Json.Serialization;

namespace CodeExplorer.Core.Common.Nodes.Layer4_Semantic;

[OntologyNode(
    label: OntologyConstants.NodeLabels.PeerMesh,
    idScheme: "{workspaceId}:mesh:peer-network",
    purpose: "Represents a symmetric peer-to-peer cluster or collective topology.",
    layer: OntologyConstants.Layers.Semantic,
    icon: "share-alt",
    order: 14,
    pluralLabel: "Peer Meshes"
)]
public record PeerMeshNode(
    string Id,
    [property: OntologyProperty("The name of the peer mesh.")] string Name,
    [property: OntologyProperty("The path of the folder or file relative to its parent container.")] string Path,
    [property: JsonPropertyName("member_count"), OntologyProperty("The number of configured peer members, when known.")] int? MemberCount,
    [property: OntologyProperty("The detected communication protocols.")] string Protocols,
    Dictionary<string, string>? CustomProperties = null
) : CompositeNode(Id, CustomProperties)
{
    [JsonPropertyName("topology")]
    public string? Topology { get; set; }

    [JsonPropertyName("detection_signals")]
    public string? DetectionSignals { get; set; }

    [JsonIgnore]
    public override NodeKind Kind => NodeKind.PeerMesh;
}
