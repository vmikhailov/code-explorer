using System.Text.Json.Serialization;
using CodeExplorer.Cypher.Common;

namespace CodeExplorer.Core.Common.Relationships;

public interface IOntologyRelationship
{
    string From { get; }
    string To { get; }
    RelationshipKind Kind { get; }
    RelationshipKind RelationshipKind => Kind;
    Dictionary<string, string>? CustomProperties { get; }
}

public abstract record OntologyRelationship(
    [property: JsonIgnore] string From,
    [property: JsonIgnore] string To,
    [property: JsonIgnore] Dictionary<string, string>? CustomProperties = null
) : IOntologyRelationship
{
    [JsonIgnore]
    public abstract RelationshipKind Kind { get; }

    [JsonIgnore]
    public int Line { get; init; }

    [JsonIgnore]
    public int LineEnd { get; init; }

    [JsonIgnore]
    public int Column { get; init; }

    [JsonIgnore]
    public string? FilePath { get; init; }

    [JsonIgnore]
    public string? Operation { get; init; }

    [JsonIgnore]
    public string? EvidenceKind { get; init; }

    [JsonIgnore]
    public string? DependencyType { get; init; }

    [JsonIgnore]
    public string? Category { get; init; }

    [JsonIgnore]
    public bool? IsSemantic { get; init; }

    [JsonIgnore]
    public bool IsCanonical { get; init; }

    [JsonIgnore]
    public bool IsSemanticLifted { get; init; }

    [JsonIgnore]
    public bool IsExternal { get; init; }

    [JsonIgnore]
    public bool? IsInternal { get; init; }

    [JsonIgnore]
    public bool IsOrm { get; init; }

    [JsonIgnore]
    public string? Scope { get; init; }

    [JsonIgnore]
    public string? BrokerType { get; init; }

    [JsonIgnore]
    public string? MessagingDirection { get; init; }

    [JsonIgnore]
    public string? ViaLibrary { get; init; }

    [JsonIgnore]
    public string? CallChain { get; init; }

    [JsonIgnore]
    public string? ConfigKey { get; init; }

    [JsonIgnore]
    public string? Url { get; init; }

    [JsonIgnore]
    public string? ServiceName { get; init; }

    [JsonIgnore]
    public string? ServiceKey { get; init; }

    [JsonIgnore]
    public string? ResourceRole { get; init; }

    [JsonIgnore]
    public string? Asset { get; init; }

    [JsonIgnore]
    public string? TargetFile { get; init; }

    [JsonIgnore]
    public string? Via { get; init; }

    [JsonIgnore]
    public string? Provider { get; init; }

    [JsonIgnore]
    public string? Schema { get; init; }

    [JsonIgnore]
    public bool IsImplicitSchema { get; init; }

    [JsonIgnore]
    public string? Protocols { get; init; }

    [JsonIgnore]
    public string? ReferenceExpression { get; init; }

    [JsonIgnore]
    public string? OutputName { get; init; }

    [JsonIgnore]
    public bool IsInferred { get; init; }
}
