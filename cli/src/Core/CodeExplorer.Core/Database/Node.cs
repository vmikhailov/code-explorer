using System.Text.Json;
using System.Text.Json.Serialization;
using CodeExplorer.Core.Common.Nodes;

namespace CodeExplorer.Core.Database;

public record Node(string Id, string Kind, Dictionary<string, object> Properties)
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
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
                if (int.TryParse(kvp.Value, out var intVal) &&
                    (kvp.Key.EndsWith("_line") || kvp.Key.EndsWith("_col") || kvp.Key is "line" or "column" or "arg_index" or "start_line" or "end_line" or "stage_order" or "loc" or "line_count"))
                {
                    properties[kvp.Key] = intVal;
                }
                else if (long.TryParse(kvp.Value, out var longVal) && kvp.Key is "size_bytes")
                {
                    properties[kvp.Key] = longVal;
                }
                else if (bool.TryParse(kvp.Value, out var boolVal) && kvp.Key is "is_test" or "is_library")
                {
                    properties[kvp.Key] = boolVal;
                }
                else
                {
                    properties[kvp.Key] = kvp.Value;
                }
            }
        }

        return new Node(node.Id, node.Kind, properties);
    }
}
