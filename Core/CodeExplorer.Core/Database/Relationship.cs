using System.Text.Json;
using System.Text.Json.Serialization;
using CodeExplorer.Common;

namespace CodeExplorer.Database;

public record Relationship(string From, string To, string Kind, Dictionary<string, object> Properties)
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static Relationship FromRelationship(IOntologyRelationship rel)
    {
        var json = JsonSerializer.Serialize(rel, rel.GetType(), SerializerOptions);
        var properties = JsonSerializer.Deserialize<Dictionary<string, object>>(json, SerializerOptions) 
            ?? new Dictionary<string, object>();

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
