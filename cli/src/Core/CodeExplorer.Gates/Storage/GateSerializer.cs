using System.Text.Json;
using CodeExplorer.Core.Gates.Models;
using YamlDotNet.Core;
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
        .WithTypeConverter(new LenientGateEnumYamlConverter())
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

        var category = raw.Category ?? GateCategory.Architectural;
        var platform = !string.IsNullOrWhiteSpace(raw.Platform) ? raw.Platform.Trim().ToLowerInvariant() : "agnostic";
        var scope = raw.Scope ?? InvariantScope.Module;
        var dim = raw.Dimension ?? InvariantDimension.Boundaries;
        var sev = raw.Severity ?? GateSeverity.Error;
        var message = raw.Message?.Trim() ?? raw.MessageTemplate?.Trim();
        var target = raw.Target?.Trim() ?? raw.TargetTemplate?.Trim();
        GateThresholdSettings? thresholds = null;
        if (raw.Thresholds != null && (raw.Thresholds.Warning.HasValue || raw.Thresholds.Error.HasValue))
        {
            thresholds = new GateThresholdSettings
            {
                Warning = raw.Thresholds.Warning,
                Error = raw.Thresholds.Error
            };
        }

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
            Message = message,
            Target = target,
            Thresholds = thresholds,
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
            Category = gate.Category,
            Platform = gate.Platform,
            Scope = gate.Scope,
            Dimension = gate.Dimension,
            Name = gate.Name,
            Severity = gate.Severity,
            Description = gate.Description,
            Remediation = gate.Remediation,
            Message = gate.Message,
            Target = gate.Target,
            Thresholds = gate.Thresholds != null
                ? new RawThresholdDefinition { Warning = gate.Thresholds.Warning, Error = gate.Thresholds.Error }
                : null,
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
        public GateCategory? Category { get; set; }
        public string? Platform { get; set; }
        public InvariantScope? Scope { get; set; }
        public InvariantDimension? Dimension { get; set; }
        public string? Name { get; set; }
        public GateSeverity? Severity { get; set; }
        public string? Description { get; set; }
        public string? Remediation { get; set; }
        public string? Message { get; set; }
        public string? MessageTemplate { get; set; }
        public string? Target { get; set; }
        public string? TargetTemplate { get; set; }
        public RawThresholdDefinition? Thresholds { get; set; }
        public string? Query { get; set; }
        public RawRatchetDefinition? Ratchet { get; set; }
        public Dictionary<string, string>? Parameters { get; set; }
    }


    private class RawRatchetDefinition
    {
        public bool Enabled { get; set; } = true;
        public string? Baseline { get; set; }
    }

    private class RawThresholdDefinition
    {
        public int? Warning { get; set; }
        public int? Error { get; set; }
    }

    private sealed class LenientGateEnumYamlConverter : IYamlTypeConverter
    {
        public bool Accepts(Type type)
        {
            var t = Nullable.GetUnderlyingType(type) ?? type;
            return t == typeof(GateCategory) || t == typeof(InvariantScope) || t == typeof(InvariantDimension) || t == typeof(GateSeverity);
        }

        public object? ReadYaml(YamlDotNet.Core.IParser parser, Type type, ObjectDeserializer rootDeserializer)
        {
            var scalar = parser.Consume<YamlDotNet.Core.Events.Scalar>().Value;
            if (string.IsNullOrWhiteSpace(scalar)) return null;
            var t = Nullable.GetUnderlyingType(type) ?? type;
            var s = scalar.Trim().ToLowerInvariant();

            if (t == typeof(GateCategory))
            {
                return s is "platform" or "rules" or "framework" or "runtime" or "language"
                    ? GateCategory.Platform
                    : GateCategory.Architectural;
            }
            if (t == typeof(InvariantScope))
            {
                return s switch
                {
                    "platform" or "workspace" or "system" => InvariantScope.System,
                    "service" => InvariantScope.Service,
                    "module" or "project" => InvariantScope.Module,
                    "class" or "type" or "file" => InvariantScope.Class,
                    "method" or "function" or "member" => InvariantScope.Method,
                    _ => Enum.TryParse<InvariantScope>(scalar, true, out var sc) ? sc : InvariantScope.Module
                };
            }
            if (t == typeof(InvariantDimension))
            {
                return s switch
                {
                    "contract" or "contracts" => InvariantDimension.Contract,
                    "rules" or "rule" or "implementation" => InvariantDimension.Rules,
                    "resources" or "resource" => InvariantDimension.Resources,
                    "boundaries" or "boundary" or "architecture" => InvariantDimension.Boundaries,
                    _ => Enum.TryParse<InvariantDimension>(scalar, true, out var dm) ? dm : InvariantDimension.Boundaries
                };
            }
            if (t == typeof(GateSeverity))
            {
                return s switch
                {
                    "warning" or "warn" => GateSeverity.Warning,
                    "info" or "information" => GateSeverity.Info,
                    _ => GateSeverity.Error
                };
            }
            return null;
        }

        public void WriteYaml(YamlDotNet.Core.IEmitter emitter, object? value, Type type, ObjectSerializer serializer)
        {
            emitter.Emit(new YamlDotNet.Core.Events.Scalar(value?.ToString() ?? string.Empty));
        }
    }
}
