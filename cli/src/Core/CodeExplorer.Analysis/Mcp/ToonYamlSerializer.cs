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
    public static string SerializeObject<T>(T value)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(value);
        return SerializeYaml(CypherValue.TryParseJsonText(json));
    }

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

    private static bool IsEmptyYamlValue(CypherValue val)
    {
        return val.Kind switch
        {
            CypherValueKind.Null => true,
            CypherValueKind.String => string.IsNullOrEmpty(val.AsString()),
            CypherValueKind.Array => val.AsArray().Count == 0 || val.AsArray().All(IsEmptyYamlValue),
            CypherValueKind.Object => val.AsObject().Count == 0 || val.AsObject().All(kv => IsEmptyYamlValue(kv.Value)),
            _ => false
        };
    }

    private static bool IsScalarYamlArray(IReadOnlyList<CypherValue> items)
    {
        for (var i = 0; i < items.Count; i++)
        {
            if (items[i].Kind is CypherValueKind.Object or CypherValueKind.Array)
            {
                return false;
            }
        }
        return true;
    }

    private static string FormatInlineScalarArray(IReadOnlyList<CypherValue> items)
    {
        var parts = new List<string>(items.Count);
        foreach (var item in items)
        {
            if (!IsEmptyYamlValue(item))
            {
                parts.Add(FormatYamlPrimitive(item));
            }
        }
        return $"[{string.Join(", ", parts)}]";
    }

    private static void WriteYamlElement(StringBuilder sb, CypherValue element, int indentLevel, bool isListItem)
    {
        var indent = new string(' ', indentLevel);

        switch (element.Kind)
        {
            case CypherValueKind.Object:
                var nonEmptyProps = element.AsObject().Where(p => !IsEmptyYamlValue(p.Value)).ToList();
                if (nonEmptyProps.Count == 0)
                {
                    sb.AppendLine($"{(isListItem ? "" : indent)}{{}}");
                    return;
                }

                var isFirstProp = true;
                foreach (var prop in nonEmptyProps)
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
                        var arrItems = prop.Value.AsArray();
                        if (IsScalarYamlArray(arrItems))
                        {
                            sb.AppendLine($"{prefix}{EscapeYamlKey(prop.Key)}: {FormatInlineScalarArray(arrItems)}");
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
                var items = element.AsArray().Where(i => !IsEmptyYamlValue(i)).ToList();
                if (items.Count == 0)
                {
                    sb.AppendLine($"{indent}[]");
                    return;
                }

                if (IsScalarYamlArray(items))
                {
                    sb.AppendLine($"{indent}{FormatInlineScalarArray(items)}");
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

    /// <summary>
    /// Parses a YAML or JSON string into a <see cref="CypherValue"/>.
    /// Supports standard YAML block mappings, sequences of objects/scalars, inline flow collections, and JSON fallback.
    /// </summary>
    public static CypherValue ParseYamlOrJson(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return CypherValue.Null;
        var trimmed = input.Trim();

        if (trimmed.StartsWith('{') || trimmed.StartsWith('['))
        {
            var jsonParsed = CypherValue.TryParseJsonText(trimmed);
            if (!jsonParsed.IsNull) return jsonParsed;
        }

        var lines = trimmed.Replace("\r\n", "\n").Split('\n');
        var nonEmptyLines = lines
            .Select(l => l.TrimEnd())
            .Where(l => !string.IsNullOrWhiteSpace(l) && !l.TrimStart().StartsWith('#'))
            .ToList();

        if (nonEmptyLines.Count == 0) return CypherValue.Null;

        if (nonEmptyLines[0].TrimStart().StartsWith("- ") || nonEmptyLines[0].Trim() == "-")
        {
            return ParseYamlSequence(nonEmptyLines);
        }

        return ParseYamlObject(nonEmptyLines);
    }

    private static CypherValue ParseYamlSequence(List<string> lines)
    {
        var items = new List<CypherValue>();
        List<string>? currentItemLines = null;

        foreach (var line in lines)
        {
            var t = line.TrimStart();
            var indent = line.Length - t.Length;
            if (indent <= 2 && (t.StartsWith("- ") || t == "-"))
            {
                if (currentItemLines is { Count: > 0 })
                {
                    items.Add(FinalizeSequenceItem(currentItemLines));
                }
                currentItemLines = [];
                var afterDash = t.Length > 2 ? t[2..].Trim() : string.Empty;
                if (!string.IsNullOrEmpty(afterDash))
                {
                    currentItemLines.Add(afterDash);
                }
            }
            else if (currentItemLines != null)
            {
                currentItemLines.Add(t);
            }
        }

        if (currentItemLines is { Count: > 0 })
        {
            items.Add(FinalizeSequenceItem(currentItemLines));
        }

        return CypherValue.FromArray(items);
    }

    private static CypherValue FinalizeSequenceItem(List<string> itemLines)
    {
        if (itemLines.Count == 1 && !itemLines[0].Contains(':'))
        {
            return ParseYamlScalar(itemLines[0]);
        }
        return ParseYamlObject(itemLines);
    }

    private static CypherValue ParseYamlObject(List<string> lines)
    {
        var props = new List<KeyValuePair<string, CypherValue>>();
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i].Trim();
            var colonIdx = line.IndexOf(':');
            if (colonIdx <= 0) continue;

            var key = UnquoteYaml(line[..colonIdx].Trim());
            var valPart = line[(colonIdx + 1)..].Trim();

            if (!string.IsNullOrEmpty(valPart))
            {
                props.Add(new KeyValuePair<string, CypherValue>(key, ParseYamlScalarOrFlow(valPart)));
            }
            else
            {
                var baseIndent = lines[i].Length - lines[i].TrimStart().Length;
                var childLines = new List<string>();
                while (i + 1 < lines.Count)
                {
                    var nextIndent = lines[i + 1].Length - lines[i + 1].TrimStart().Length;
                    if (nextIndent <= baseIndent) break;
                    childLines.Add(lines[++i]);
                }

                if (childLines.Count > 0)
                {
                    var childVal = childLines[0].TrimStart().StartsWith('-')
                        ? ParseYamlSequence(childLines)
                        : ParseYamlObject(childLines);
                    props.Add(new KeyValuePair<string, CypherValue>(key, childVal));
                }
            }
        }
        return CypherValue.FromObject(props);
    }

    private static CypherValue ParseYamlScalarOrFlow(string text)
    {
        if (text.StartsWith('[') && text.EndsWith(']'))
        {
            var jsonTry = CypherValue.TryParseJsonText(text);
            if (!jsonTry.IsNull) return jsonTry;
            var inner = text[1..^1].Trim();
            if (string.IsNullOrEmpty(inner)) return CypherValue.FromArray([]);
            var parts = inner.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return CypherValue.FromArray(parts.Select(ParseYamlScalar).ToList());
        }
        if (text.StartsWith('{') && text.EndsWith('}'))
        {
            var jsonTry = CypherValue.TryParseJsonText(text);
            if (!jsonTry.IsNull) return jsonTry;
            var inner = text[1..^1].Trim();
            if (string.IsNullOrEmpty(inner)) return CypherValue.FromObject([]);
            var parts = inner.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return ParseYamlObject(parts.ToList());
        }
        return ParseYamlScalar(text);
    }

    private static CypherValue ParseYamlScalar(string raw)
    {
        var s = raw.Trim();
        if (s is "null" or "~" or "") return CypherValue.Null;
        if (bool.TryParse(s, out var b)) return CypherValue.FromBool(b);
        if (long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l)) return CypherValue.FromInt64(l);
        if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)) return CypherValue.FromFloat64(d);
        return CypherValue.FromString(UnquoteYaml(s));
    }

    private static string UnquoteYaml(string s)
    {
        if (s.Length >= 2 && ((s[0] == '"' && s[^1] == '"') || (s[0] == '\'' && s[^1] == '\'')))
        {
            return s[1..^1].Replace("\\\"", "\"").Replace("\\\\", "\\");
        }
        return s;
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
