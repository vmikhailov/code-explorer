using System.Text;
using System.Text.Json;

namespace CodeExplorer.Core.Mcp;

/// <summary>
/// Provides token-efficient serialization to YAML and TOON (Token-Oriented Object Notation).
/// TOON combines YAML-like indentation for objects with CSV-like tabular layout for uniform collections,
/// reducing LLM prompt token consumption by 30-60% compared to JSON.
/// </summary>
public static class ToonYamlSerializer
{
    public static string SerializeYaml(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return SerializeYaml(doc.RootElement);
    }

    public static string SerializeYaml(JsonElement element)
    {
        var sb = new StringBuilder();
        WriteYamlElement(sb, element, 0, false);
        return sb.ToString().TrimEnd();
    }

    public static string SerializeToon(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return SerializeToon(doc.RootElement);
    }

    public static string SerializeToon(JsonElement element)
    {
        var sb = new StringBuilder();
        WriteToonElement(sb, element, 0, null);
        return sb.ToString().TrimEnd();
    }

    #region YAML Implementation

    private static void WriteYamlElement(StringBuilder sb, JsonElement element, int indentLevel, bool isListItem)
    {
        var indent = new string(' ', indentLevel);

        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                var isFirstProp = true;
                foreach (var prop in element.EnumerateObject())
                {
                    var prefix = (isFirstProp && isListItem) ? "" : indent;
                    isFirstProp = false;

                    if (prop.Value.ValueKind == JsonValueKind.Object)
                    {
                        sb.AppendLine($"{prefix}{EscapeYamlKey(prop.Name)}:");
                        WriteYamlElement(sb, prop.Value, indentLevel + 2, false);
                    }
                    else if (prop.Value.ValueKind == JsonValueKind.Array)
                    {
                        if (prop.Value.GetArrayLength() == 0)
                        {
                            sb.AppendLine($"{prefix}{EscapeYamlKey(prop.Name)}: []");
                        }
                        else
                        {
                            sb.AppendLine($"{prefix}{EscapeYamlKey(prop.Name)}:");
                            WriteYamlElement(sb, prop.Value, indentLevel + 2, false);
                        }
                    }
                    else
                    {
                        sb.AppendLine($"{prefix}{EscapeYamlKey(prop.Name)}: {FormatYamlPrimitive(prop.Value)}");
                    }
                }
                break;

            case JsonValueKind.Array:
                if (element.GetArrayLength() == 0)
                {
                    sb.AppendLine($"{indent}[]");
                    return;
                }

                foreach (var item in element.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.Object)
                    {
                        sb.Append($"{indent}- ");
                        WriteYamlElement(sb, item, indentLevel + 2, true);
                    }
                    else if (item.ValueKind == JsonValueKind.Array)
                    {
                        sb.AppendLine($"{indent}-");
                        WriteYamlElement(sb, item, indentLevel + 2, false);
                    }
                    else
                    {
                        sb.AppendLine($"{indent}- {FormatYamlPrimitive(item)}");
                    }
                }
                break;

            default:
                sb.AppendLine($"{indent}{FormatYamlPrimitive(element)}");
                break;
        }
    }

    private static string EscapeYamlKey(string key)
    {
        if (string.IsNullOrEmpty(key)) return "\"\"";
        if (key.Contains(':') || key.Contains('#') || key.Contains(' ') || key.StartsWith('-'))
        {
            return $"\"{key.Replace("\"", "\\\"")}\"";
        }
        return key;
    }

    private static string FormatYamlPrimitive(JsonElement elem)
    {
        return elem.ValueKind switch
        {
            JsonValueKind.Null => "null",
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Number => elem.GetRawText(),
            JsonValueKind.String => FormatYamlString(elem.GetString() ?? ""),
            _ => elem.GetRawText()
        };
    }

    private static string FormatYamlString(string str)
    {
        if (string.IsNullOrEmpty(str)) return "\"\"";
        if (str.Equals("true", StringComparison.OrdinalIgnoreCase) ||
            str.Equals("false", StringComparison.OrdinalIgnoreCase) ||
            str.Equals("null", StringComparison.OrdinalIgnoreCase) ||
            str.Contains(':') || str.Contains('#') || str.Contains('\n') || str.Contains('"') ||
            str.StartsWith(' ') || str.EndsWith(' ') || str.StartsWith('-') || str.StartsWith('[') || str.StartsWith('{'))
        {
            return $"\"{str.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "")}\"";
        }
        return str;
    }

    #endregion

    #region TOON Implementation

    private static void WriteToonElement(StringBuilder sb, JsonElement element, int indentLevel, string? propertyName)
    {
        var indent = new string(' ', indentLevel);

        if (element.ValueKind == JsonValueKind.Array)
        {
            var length = element.GetArrayLength();
            if (length == 0)
            {
                if (propertyName != null) sb.AppendLine($"{indent}{propertyName}: []");
                else sb.AppendLine($"{indent}[]");
                return;
            }

            // Check if array is uniform tabular collection of objects
            if (IsUniformObjectArray(element, out var fieldNames))
            {
                var fieldsHeader = string.Join(",", fieldNames);
                var headerPrefix = propertyName != null ? $"{propertyName}" : "";
                sb.AppendLine($"{indent}{headerPrefix}[{fieldsHeader}]:");

                var rowIndent = new string(' ', indentLevel + 2);
                foreach (var row in element.EnumerateArray())
                {
                    var values = new List<string>(fieldNames.Count);
                    foreach (var f in fieldNames)
                    {
                        if (row.TryGetProperty(f, out var propVal))
                        {
                            values.Add(FormatToonScalar(propVal));
                        }
                        else
                        {
                            values.Add("");
                        }
                    }
                    sb.AppendLine($"{rowIndent}{string.Join(",", values)}");
                }
                return;
            }

            // Non-uniform or primitive array
            if (propertyName != null)
            {
                sb.AppendLine($"{indent}{propertyName}:");
            }
            var itemIndent = new string(' ', propertyName != null ? indentLevel + 2 : indentLevel);
            foreach (var item in element.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Object)
                {
                    WriteToonElement(sb, item, itemIndent.Length, null);
                }
                else
                {
                    sb.AppendLine($"{itemIndent}- {FormatToonScalar(item)}");
                }
            }
            return;
        }

        if (element.ValueKind == JsonValueKind.Object)
        {
            if (propertyName != null)
            {
                sb.AppendLine($"{indent}{propertyName}:");
                indent = new string(' ', indentLevel + 2);
                indentLevel += 2;
            }

            foreach (var prop in element.EnumerateObject())
            {
                if (prop.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
                {
                    WriteToonElement(sb, prop.Value, indentLevel, prop.Name);
                }
                else
                {
                    sb.AppendLine($"{indent}{prop.Name}: {FormatToonScalar(prop.Value)}");
                }
            }
            return;
        }

        // Single primitive
        var scalarVal = FormatToonScalar(element);
        if (propertyName != null)
        {
            sb.AppendLine($"{indent}{propertyName}: {scalarVal}");
        }
        else
        {
            sb.AppendLine($"{indent}{scalarVal}");
        }
    }

    private static bool IsUniformObjectArray(JsonElement arrayElem, out List<string> fieldNames)
    {
        fieldNames = [];
        var allObjects = true;
        var first = true;

        foreach (var item in arrayElem.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                allObjects = false;
                break;
            }

            if (first)
            {
                foreach (var prop in item.EnumerateObject())
                {
                    // Only scalar fields can be flattened into TOON tabular format
                    if (prop.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
                    {
                        allObjects = false;
                        break;
                    }
                    fieldNames.Add(prop.Name);
                }
                first = false;
                if (!allObjects || fieldNames.Count == 0) return false;
            }
        }

        return allObjects && fieldNames.Count > 0;
    }

    private static string FormatToonScalar(JsonElement elem)
    {
        return elem.ValueKind switch
        {
            JsonValueKind.Null => "-",
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Number => elem.GetRawText(),
            JsonValueKind.String => EscapeToonCsvValue(elem.GetString() ?? ""),
            _ => EscapeToonCsvValue(elem.GetRawText())
        };
    }

    private static string EscapeToonCsvValue(string value)
    {
        if (string.IsNullOrEmpty(value)) return "-";
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r') || value.StartsWith(' ') || value.EndsWith(' '))
        {
            return $"\"{value.Replace("\"", "\"\"").Replace("\r\n", " ").Replace("\n", " ")}\"";
        }
        return value;
    }

    #endregion
}
