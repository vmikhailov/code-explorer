using System.Globalization;
using System.Text;

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
        return SerializeYaml(CypherValue.TryParseJsonText(json));
    }

    public static string SerializeYaml(CypherQueryResult result)
    {
        return SerializeYaml(ResultToValue(result));
    }

    public static string SerializeYaml(CypherValue element)
    {
        var sb = new StringBuilder();
        WriteYamlElement(sb, element, 0, false);
        return sb.ToString().TrimEnd();
    }

    public static string SerializeToon(string json)
    {
        return SerializeToon(CypherValue.TryParseJsonText(json));
    }

    public static string SerializeToon(CypherQueryResult result)
    {
        return SerializeToon(ResultToValue(result));
    }

    public static string SerializeToon(CypherValue element)
    {
        var sb = new StringBuilder();
        WriteToonElement(sb, element, 0, null);
        return sb.ToString().TrimEnd();
    }

    private static CypherValue ResultToValue(CypherQueryResult result)
    {
        if (result.Count == 0) return CypherValue.FromArray([]);
        var list = new List<CypherValue>(result.Count);
        foreach (var row in result.Rows)
        {
            var props = new List<KeyValuePair<string, CypherValue>>(row.FieldCount);
            var count = Math.Min(row.Columns.Count, row.Values.Count);
            for (var i = 0; i < count; i++)
            {
                props.Add(new KeyValuePair<string, CypherValue>(row.Columns[i], row.Values[i]));
            }
            list.Add(CypherValue.FromObject(props));
        }
        return CypherValue.FromArray(list);
    }

    #region YAML Implementation

    private static void WriteYamlElement(StringBuilder sb, CypherValue element, int indentLevel, bool isListItem)
    {
        var indent = new string(' ', indentLevel);

        switch (element.Kind)
        {
            case CypherValueKind.Object:
                var isFirstProp = true;
                foreach (var prop in element.AsObject())
                {
                    var prefix = (isFirstProp && isListItem) ? "" : indent;
                    isFirstProp = false;

                    if (prop.Value.Kind == CypherValueKind.Object)
                    {
                        sb.AppendLine($"{prefix}{EscapeYamlKey(prop.Key)}:");
                        WriteYamlElement(sb, prop.Value, indentLevel + 2, false);
                    }
                    else if (prop.Value.Kind == CypherValueKind.Array)
                    {
                        if (prop.Value.AsArray().Count == 0)
                        {
                            sb.AppendLine($"{prefix}{EscapeYamlKey(prop.Key)}: []");
                        }
                        else
                        {
                            sb.AppendLine($"{prefix}{EscapeYamlKey(prop.Key)}:");
                            WriteYamlElement(sb, prop.Value, indentLevel + 2, false);
                        }
                    }
                    else
                    {
                        sb.AppendLine($"{prefix}{EscapeYamlKey(prop.Key)}: {FormatYamlPrimitive(prop.Value)}");
                    }
                }
                break;

            case CypherValueKind.Array:
                var items = element.AsArray();
                if (items.Count == 0)
                {
                    sb.AppendLine($"{indent}[]");
                    return;
                }

                foreach (var item in items)
                {
                    if (item.Kind == CypherValueKind.Object)
                    {
                        sb.Append($"{indent}- ");
                        WriteYamlElement(sb, item, indentLevel + 2, true);
                    }
                    else if (item.Kind == CypherValueKind.Array)
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

    private static string FormatYamlPrimitive(CypherValue elem)
    {
        return elem.Kind switch
        {
            CypherValueKind.Null => "null",
            CypherValueKind.Boolean => elem.AsBool() ? "true" : "false",
            CypherValueKind.Int64 => elem.AsInt64().ToString(CultureInfo.InvariantCulture),
            CypherValueKind.Float64 => elem.AsDouble().ToString("G", CultureInfo.InvariantCulture),
            CypherValueKind.String => FormatYamlString(elem.AsString()),
            _ => elem.ToJsonString()
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

    private static void WriteToonElement(StringBuilder sb, CypherValue element, int indentLevel, string? propertyName)
    {
        var indent = new string(' ', indentLevel);

        if (element.Kind == CypherValueKind.Array)
        {
            var items = element.AsArray();
            if (items.Count == 0)
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
                foreach (var row in items)
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
            foreach (var item in items)
            {
                if (item.Kind == CypherValueKind.Object)
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

        if (element.Kind == CypherValueKind.Object)
        {
            if (propertyName != null)
            {
                sb.AppendLine($"{indent}{propertyName}:");
                indent = new string(' ', indentLevel + 2);
                indentLevel += 2;
            }

            foreach (var prop in element.AsObject())
            {
                if (prop.Value.Kind is CypherValueKind.Object or CypherValueKind.Array)
                {
                    WriteToonElement(sb, prop.Value, indentLevel, prop.Key);
                }
                else
                {
                    sb.AppendLine($"{indent}{prop.Key}: {FormatToonScalar(prop.Value)}");
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

    private static bool IsUniformObjectArray(CypherValue arrayElem, out List<string> fieldNames)
    {
        fieldNames = [];
        var allObjects = true;
        var first = true;

        foreach (var item in arrayElem.AsArray())
        {
            if (item.Kind != CypherValueKind.Object)
            {
                allObjects = false;
                break;
            }

            if (first)
            {
                foreach (var prop in item.AsObject())
                {
                    // Only scalar fields can be flattened into TOON tabular format
                    if (prop.Value.Kind is CypherValueKind.Object or CypherValueKind.Array)
                    {
                        allObjects = false;
                        break;
                    }
                    fieldNames.Add(prop.Key);
                }
                first = false;
                if (!allObjects || fieldNames.Count == 0) return false;
            }
        }

        return allObjects && fieldNames.Count > 0;
    }

    private static string FormatToonScalar(CypherValue elem)
    {
        return elem.Kind switch
        {
            CypherValueKind.Null => "-",
            CypherValueKind.Boolean => elem.AsBool() ? "true" : "false",
            CypherValueKind.Int64 => elem.AsInt64().ToString(CultureInfo.InvariantCulture),
            CypherValueKind.Float64 => elem.AsDouble().ToString("G", CultureInfo.InvariantCulture),
            CypherValueKind.String => EscapeToonCsvValue(elem.AsString()),
            _ => EscapeToonCsvValue(elem.ToJsonString())
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
