using System.Text.Json.Serialization;

namespace CodeExplorer.Core.Analysis;

public record ConfiguredDomainDefinition(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("description")] string? Description = null,
    [property: JsonPropertyName("keywords")] List<string>? Keywords = null);
