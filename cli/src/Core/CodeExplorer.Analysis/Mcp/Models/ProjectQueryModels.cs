using System.Text.Json.Serialization;

namespace CodeExplorer.Core.Mcp.Models;

public record ProjectQueryParameter(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("type")] string Type = "string",
    [property: JsonPropertyName("required")] bool Required = false,
    [property: JsonPropertyName("description")] string? Description = null,
    [property: JsonPropertyName("defaultValue")] object? DefaultValue = null
);

public record ProjectQueryMetadata
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; init; } = string.Empty;

    [JsonPropertyName("parameters")]
    public IReadOnlyList<ProjectQueryParameter> Parameters { get; init; } = [];

    [JsonPropertyName("returns")]
    public string? Returns { get; init; }

    [JsonPropertyName("tags")]
    public IReadOnlyList<string> Tags { get; init; } = [];
}

public record ProjectQueryItem(
    string Name,
    string Description,
    IReadOnlyList<ProjectQueryParameter> Parameters,
    string? Returns,
    IReadOnlyList<string> Tags,
    string Cypher,
    string CypherPath,
    string MetadataPath
);
