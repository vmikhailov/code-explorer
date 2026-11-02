using System.Text.Json.Serialization;
using CodeExplorer.Core.Common.Relationships;
using CodeExplorer.Cypher.Common;

namespace CodeExplorer.Core.Database;

public record Relationship
{
    public string From { get; init; }
    public string To { get; init; }
    public string Kind { get; init; }
    public Dictionary<string, object> Properties { get; init; }

    [JsonIgnore]
    public RelationshipKind RelationshipKind => EnumExtensions.ParseRelationshipKind(Kind);

    [JsonConstructor]
    public Relationship(string from, string to, string kind, Dictionary<string, object> properties)
    {
        From = from;
        To = to;
        Kind = kind;
        Properties = properties;
    }

    public Relationship(string from, string to, string kind)
        : this(from, to, kind, new Dictionary<string, object>())
    {
    }

    public Relationship(string from, string to, RelationshipKind kind, Dictionary<string, object> properties)
        : this(from, to, kind.ToCypherType(), properties)
    {
    }

    public Relationship(string from, string to, RelationshipKind kind)
        : this(from, to, kind.ToCypherType(), new Dictionary<string, object>())
    {
    }

    public static Relationship FromRelationship(IOntologyRelationship rel)
    {
        var properties = new Dictionary<string, object>();
        if (rel.Extensions != null)
        {
            foreach (var kvp in rel.Extensions)
            {
                properties[kvp.Key] = kvp.Value;
            }
        }

        return new Relationship(rel.From, rel.To, rel.Kind, properties);
    }
}
