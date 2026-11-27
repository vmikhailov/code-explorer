using System.Text.Json.Serialization;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;

namespace CodeExplorer.Core.Common.Nodes.Layer4_Semantic;

[OntologyNode(
    label: OntologyConstants.NodeLabels.Cache,
    idScheme: "{workspaceId}:cache:{engine}:{regionOrKey}",
    purpose: "Represents an in-memory, distributed, or HTTP cache region/key with distinct read, write, and invalidation semantics.",
    layer: OntologyConstants.Layers.Semantic,
    icon: "zap",
    order: 24,
    pluralLabel: "Caches & Regions"
)]
[OntologyEdge<FunctionNode>(OntologyConstants.Relationships.AccessedBy)]
[OntologyEdge<FileNode>(OntologyConstants.Relationships.DeclaredIn)]
public record CacheNode(
    string Id,
    [property: OntologyProperty("The cache key prefix, region, or named cache store.")] string Name,
    [property: OntologyProperty("The workspace-relative path of the declaring or primary accessing file.")] string Path,
    [property: JsonPropertyName("sub_kind"), OntologyProperty("The backing cache engine kind (memory, distributed, redis, memcached, hybrid, http_output).")] CacheEngine SubKind,
    Dictionary<string, string>? CustomProperties = null
) : CompositeNode(Id, CustomProperties)
{
    [JsonPropertyName("key_pattern"), OntologyProperty("Extracted cache key template or region name.")]
    public string? KeyPattern { get; set; }

    [JsonPropertyName("framework"), OntologyProperty("Detected caching library or framework (e.g. stackexchange-redis, memory-cache, spring-cache, ioredis).")]
    public string? Framework { get; set; }

    [JsonPropertyName("file_path"), OntologyProperty("Workspace-relative source file referencing or configuring the cache.")]
    public string? FilePath { get; set; }

    [JsonPropertyName("start_line"), OntologyProperty("1-indexed starting line number of the primary cache declaration or call.")]
    public int? StartLine { get; set; }

    [JsonIgnore]
    public override NodeKind Kind => NodeKind.Cache;
}
