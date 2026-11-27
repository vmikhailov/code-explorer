using System.Text.Json.Serialization;

namespace CodeExplorer.Core.Analysis;

public record BatchInferenceResult(
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("file_path")] string? FilePath,
    [property: JsonPropertyName("domain")] string? Domain,
    [property: JsonPropertyName("layer")] string? Layer,
    [property: JsonPropertyName("pattern")] string? Pattern,
    [property: JsonPropertyName("operation_type")] OperationType? OperationType,
    [property: JsonPropertyName("capability_tag")] string? CapabilityTag,
    [property: JsonPropertyName("intent_summary")] string? IntentSummary,
    [property: JsonPropertyName("is_pure_domain")] bool? IsPureDomain,
    [property: JsonPropertyName("target_entities")] List<string>? TargetEntities,
    [property: JsonPropertyName("emitted_events")] List<string>? EmittedEvents)
{
    public BatchInferenceResult(
        string? Id,
        string? FilePath,
        string? Domain,
        string? Layer,
        string? Pattern,
        string? OperationType,
        string? CapabilityTag,
        string? IntentSummary,
        bool? IsPureDomain,
        List<string>? TargetEntities,
        List<string>? EmittedEvents)
        : this(
            Id,
            FilePath,
            Domain,
            Layer,
            Pattern,
            TaxonomyEnumExtendedExtensions.ParseNullableOperationType(OperationType),
            CapabilityTag,
            IntentSummary,
            IsPureDomain,
            TargetEntities,
            EmittedEvents)
    {
    }
}
