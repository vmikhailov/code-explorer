using System.Text.Json.Serialization;

namespace CodeExplorer.Core.Conventions.Models;

/// <summary>
/// Strongly-typed configuration model for .codeexplorer/conventions.json.
/// </summary>
public sealed class WorkspaceConventionsConfig
{
    [JsonPropertyName("topics")]
    public Dictionary<string, string>? Topics { get; set; }

    [JsonPropertyName("service_prefixes")]
    public List<string>? ServicePrefixes { get; set; }

    [JsonPropertyName("route_functions")]
    public List<string>? RouteFunctions { get; set; }

    [JsonPropertyName("database_aliases")]
    public Dictionary<string, string>? DatabaseAliases { get; set; }

    [JsonPropertyName("service_aliases")]
    public Dictionary<string, string>? ServiceAliases { get; set; }

    [JsonPropertyName("garbage_hosts")]
    public List<string>? GarbageHosts { get; set; }

    [JsonPropertyName("placeholder_names")]
    public List<string>? PlaceholderNames { get; set; }

    [JsonPropertyName("domains")]
    public object? DomainsElement { get; set; }

    [JsonPropertyName("overrides")]
    public Dictionary<string, object>? Overrides { get; set; }
}

/// <summary>
/// Domain definition item within domains.json.
/// </summary>
public sealed class DomainDefinitionEntry
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("icon")]
    public string? Icon { get; set; }

    [JsonPropertyName("projects")]
    public List<string>? Projects { get; set; }

    [JsonPropertyName("services")]
    public List<string>? Services { get; set; }

    [JsonPropertyName("patterns")]
    public List<string>? Patterns { get; set; }
}
