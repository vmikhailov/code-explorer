using System.Text.Json;
using CodeExplorer.Core.Gates.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace CodeExplorer.Core.Gates.Storage;

/// <summary>
/// Handles YAML serialization/deserialization for Semantic Gates and JSON for Baselines.
/// </summary>
public static class GateSerializer
{
    private static readonly IDeserializer YamlDeserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    private static readonly ISerializer YamlSerializer = new SerializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
        .Build();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public static SemanticGate DeserializeGate(string yamlContent, string source = "Workspace")
    {
        var raw = YamlDeserializer.Deserialize<RawGateDefinition>(yamlContent);
        if (string.IsNullOrWhiteSpace(raw.Id))
        {
            throw new InvalidOperationException("Gate definition must contain an 'id' field.");
        }
        if (string.IsNullOrWhiteSpace(raw.Query))
        {
            throw new InvalidOperationException($"Gate '{raw.Id}' must contain a 'query' field.");
        }

        var category = Enum.TryParse<GateCategory>(raw.Category, true, out var cat) ? cat : GateCategory.Architectural;
        var platform = !string.IsNullOrWhiteSpace(raw.Platform) ? raw.Platform.Trim().ToLowerInvariant() : "agnostic";
        var scope = Enum.TryParse<InvariantScope>(raw.Scope, true, out var sc) ? sc : InvariantScope.Module;
        var dim = Enum.TryParse<InvariantDimension>(raw.Dimension, true, out var dm) ? dm : InvariantDimension.Boundaries;
        var sev = Enum.TryParse<GateSeverity>(raw.Severity, true, out var sv) ? sv : GateSeverity.Error;

        return new SemanticGate
        {
            Id = raw.Id.Trim(),
            Category = category,
            Platform = platform,
            Scope = scope,
            Dimension = dim,
            Name = !string.IsNullOrWhiteSpace(raw.Name) ? raw.Name.Trim() : raw.Id.Trim(),
            Severity = sev,
            Description = raw.Description?.Trim(),
            Remediation = raw.Remediation?.Trim(),
            Query = raw.Query.Trim(),
            Ratchet = new GateRatchetSettings
            {
                Enabled = raw.Ratchet?.Enabled ?? true,
                Baseline = raw.Ratchet?.Baseline
            },
            Parameters = raw.Parameters ?? new Dictionary<string, string>(),
            Source = source
        };
    }

    public static string SerializeGate(SemanticGate gate)
    {
        var raw = new RawGateDefinition
        {
            Id = gate.Id,
            Category = gate.Category.ToString(),
            Platform = gate.Platform,
            Scope = gate.Scope.ToString(),
            Dimension = gate.Dimension.ToString(),
            Name = gate.Name,
            Severity = gate.Severity.ToString(),
            Description = gate.Description,
            Remediation = gate.Remediation,
            Query = gate.Query,
            Ratchet = new RawRatchetDefinition
            {
                Enabled = gate.Ratchet.Enabled,
                Baseline = gate.Ratchet.Baseline
            },
            Parameters = gate.Parameters.Count > 0 ? gate.Parameters : null
        };

        return YamlSerializer.Serialize(raw);
    }

    public static GateBaseline DeserializeBaseline(string jsonContent)
    {
        return JsonSerializer.Deserialize<GateBaseline>(jsonContent, JsonOptions)
               ?? throw new InvalidOperationException("Failed to deserialize gate baseline JSON.");
    }

    public static string SerializeBaseline(GateBaseline baseline)
    {
        return JsonSerializer.Serialize(baseline, JsonOptions);
    }

    private class RawGateDefinition
    {
        public string? Id { get; set; }
        public string? Category { get; set; }
        public string? Platform { get; set; }
        public string? Scope { get; set; }
        public string? Dimension { get; set; }
        public string? Name { get; set; }
        public string? Severity { get; set; }
        public string? Description { get; set; }
        public string? Remediation { get; set; }
        public string? Query { get; set; }
        public RawRatchetDefinition? Ratchet { get; set; }
        public Dictionary<string, string>? Parameters { get; set; }
    }

    private class RawRatchetDefinition
    {
        public bool Enabled { get; set; } = true;
        public string? Baseline { get; set; }
    }
}
