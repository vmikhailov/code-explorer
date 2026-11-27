using System.Text.Json.Serialization;

namespace CodeExplorer.Core.Analysis;

public record WorkspaceDomainsConfig(
    [property: JsonPropertyName("domains")] List<ConfiguredDomainDefinition> Domains);
