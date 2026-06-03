using System.Text.Json;
using System.Text.Json.Serialization;
using CodeExplorer.Common;

namespace CodeExplorer.Database;

public record Node(string Id, string Kind, Dictionary<string, object> Properties)
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static Node FromNode(IOntologyNode node)
    {
        var json = JsonSerializer.Serialize(node, node.GetType(), SerializerOptions);
        var rawProperties = JsonSerializer.Deserialize<Dictionary<string, object>>(json, SerializerOptions) 
            ?? new Dictionary<string, object>();

        var properties = new Dictionary<string, object>();
        foreach (var kvp in rawProperties)
        {
            var cleanedVal = JsonHelper.ConvertJsonElement(kvp.Value);
            if (cleanedVal != null)
            {
                properties[kvp.Key] = cleanedVal;
            }
        }

        if (node.Extensions != null)
        {
            foreach (var kvp in node.Extensions)
            {
                properties[kvp.Key] = kvp.Value;
            }
        }

        return new Node(node.Id, node.Kind, properties);
    }
}
