using System.Buffers;
using System.Buffers.Text;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CodeExplorer.Cypher.Linq;

namespace CodeExplorer.Core.Database;

public enum CypherValueKind : byte
{
    Null = 0,
    Boolean = 1,
    Int64 = 2,
    Float64 = 3,
    String = 4,
    Array = 5,
    Object = 6
}

public readonly record struct CypherParam(string Name, object? Value);

[JsonConverter(typeof(CypherValueJsonConverter))]
public readonly struct CypherValue
{
    private const string TrueLiteral = "true";
    private const string FalseLiteral = "false";
    private const string OneLiteral = "1";
    private const string ZeroLiteral = "0";
    private const string NullLiteral = "null";

    private readonly long _int64;
    private readonly double _float64;
    private readonly object? _refValue;

    public CypherValueKind Kind { get; }

    private CypherValue(CypherValueKind kind, long int64, double float64, object? refValue)
    {
        Kind = kind;
        _int64 = int64;
        _float64 = float64;
        _refValue = refValue;
    }

    public static CypherValue Null => default;
    public static CypherValue FromBool(bool value) => new(CypherValueKind.Boolean, value ? 1L : 0L, 0d, null);
    public static CypherValue FromInt64(long value) => new(CypherValueKind.Int64, value, 0d, null);
    public static CypherValue FromFloat64(double value) => new(CypherValueKind.Float64, 0L, value, null);

    public static CypherValue FromString(string? value) =>
        value is null ? Null : new(CypherValueKind.String, 0L, 0d, value);

    public static CypherValue FromArray(IReadOnlyList<CypherValue> items) =>
        new(CypherValueKind.Array, 0L, 0d, items);

    public static CypherValue FromObject(IReadOnlyList<KeyValuePair<string, CypherValue>> properties) =>
        new(CypherValueKind.Object, 0L, 0d, properties);

    public bool IsNull => Kind == CypherValueKind.Null;

    public bool AsBool(bool fallback = false)
    {
        return Kind switch
        {
            CypherValueKind.Boolean => _int64 != 0,
            CypherValueKind.Int64 => _int64 != 0,
            CypherValueKind.String when _refValue is string s => ParseBoolString(s, fallback),
            _ => fallback
        };
    }

    public int? AsNullableInt32()
    {
        return Kind switch
        {
            CypherValueKind.Int64 => (int)_int64,
            CypherValueKind.Float64 => (int)_float64,
            CypherValueKind.String when _refValue is string s && int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => null
        };
    }

    public int AsInt32(int fallback = 0) => AsNullableInt32() ?? fallback;

    public long? AsNullableInt64()
    {
        return Kind switch
        {
            CypherValueKind.Int64 => _int64,
            CypherValueKind.Float64 => (long)_float64,
            CypherValueKind.String when _refValue is string s && long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => null
        };
    }

    public long AsInt64(long fallback = 0L) => AsNullableInt64() ?? fallback;

    public double? AsNullableDouble()
    {
        return Kind switch
        {
            CypherValueKind.Float64 => _float64,
            CypherValueKind.Int64 => _int64,
            CypherValueKind.String when _refValue is string s && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => null
        };
    }

    public double AsDouble(double fallback = 0d) => AsNullableDouble() ?? fallback;

    public string? AsNullableString()
    {
        return Kind switch
        {
            CypherValueKind.Null => null,
            CypherValueKind.String => _refValue as string,
            CypherValueKind.Boolean => _int64 != 0 ? TrueLiteral : FalseLiteral,
            CypherValueKind.Int64 => _int64.ToString(CultureInfo.InvariantCulture),
            CypherValueKind.Float64 => _float64.ToString(CultureInfo.InvariantCulture),
            _ => ToJsonString()
        };
    }

    public string AsString(string fallback = "") => AsNullableString() ?? fallback;

    public IReadOnlyList<CypherValue> AsArray() =>
        Kind == CypherValueKind.Array && _refValue is IReadOnlyList<CypherValue> list ? list : [];

    public IReadOnlyList<KeyValuePair<string, CypherValue>> AsObject() =>
        Kind == CypherValueKind.Object && _refValue is IReadOnlyList<KeyValuePair<string, CypherValue>> props ? props : [];

    public IReadOnlyList<string> AsStringList()
    {
        if (Kind == CypherValueKind.Array && _refValue is IReadOnlyList<CypherValue> items)
        {
            var result = new List<string>(items.Count);
            for (var i = 0; i < items.Count; i++)
            {
                var s = items[i].AsNullableString();
                if (!string.IsNullOrEmpty(s)) result.Add(s);
            }
            return result;
        }

        if (Kind == CypherValueKind.String && _refValue is string raw && raw.StartsWith('['))
        {
            var parsed = TryParseJsonText(raw);
            if (parsed.Kind == CypherValueKind.Array)
            {
                return parsed.AsStringList();
            }
        }

        return [];
    }

    public bool TryGetProperty(string propName, out CypherValue value)
    {
        if (Kind == CypherValueKind.Object && _refValue is IReadOnlyList<KeyValuePair<string, CypherValue>> props)
        {
            for (var i = 0; i < props.Count; i++)
            {
                if (string.Equals(props[i].Key, propName, StringComparison.OrdinalIgnoreCase))
                {
                    value = props[i].Value;
                    return true;
                }
            }
        }
        value = default;
        return false;
    }

    public string GetStringProp(string propName, string fallback = "") =>
        TryGetProperty(propName, out var v) && !v.IsNull ? v.AsString(fallback) : fallback;

    public int? GetNullableIntProp(string propName) =>
        TryGetProperty(propName, out var v) ? v.AsNullableInt32() : null;

    public int GetIntProp(string propName, int fallback = 0) =>
        GetNullableIntProp(propName) ?? fallback;

    public long GetLongProp(string propName, long fallback = 0L) =>
        TryGetProperty(propName, out var v) ? v.AsInt64(fallback) : fallback;

    public bool GetBoolProp(string propName, bool fallback = false) =>
        TryGetProperty(propName, out var v) ? v.AsBool(fallback) : fallback;

    public Dictionary<string, string> ExtractStringMap()
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (Kind == CypherValueKind.Object && _refValue is IReadOnlyList<KeyValuePair<string, CypherValue>> props)
        {
            for (var i = 0; i < props.Count; i++)
            {
                var valStr = props[i].Value.AsNullableString();
                if (valStr is not null)
                {
                    result[props[i].Key] = valStr;
                }
            }
        }
        else if (Kind == CypherValueKind.String && _refValue is string raw && raw.StartsWith('{'))
        {
            var parsed = TryParseJsonText(raw);
            if (parsed.Kind == CypherValueKind.Object)
            {
                return parsed.ExtractStringMap();
            }
        }
        return result;
    }

    public static CypherValue FromSqliteObject(object? val)
    {
        return val switch
        {
            null or DBNull => Null,
            CypherValue cv => cv,
            bool b => FromBool(b),
            long l => FromInt64(l),
            int i => FromInt64(i),
            short s => FromInt64(s),
            byte b8 => FromInt64(b8),
            double d => FromFloat64(d),
            float f => FromFloat64(f),
            decimal dec => FromFloat64((double)dec),
            byte[] bytes => ReadFromJsonb(bytes),
            string str => FromPotentialJsonString(str),
            _ => FromString(val.ToString())
        };
    }

    public static CypherValue FromPotentialJsonString(string? str)
    {
        if (str is null) return Null;
        if (str.Length >= 2)
        {
            var first = str[0];
            var last = str[^1];
            if ((first == '{' && last == '}') || (first == '[' && last == ']'))
            {
                var parsed = TryParseJsonText(str);
                if (!parsed.IsNull) return parsed;
            }
        }
        return FromString(str);
    }

    public static CypherValue TryParseJsonText(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return Null;
        var bytes = Encoding.UTF8.GetBytes(json);
        return TryParseUtf8Json(bytes);
    }

    public static CypherValue TryParseUtf8Json(ReadOnlySpan<byte> utf8Json)
    {
        try
        {
            var reader = new Utf8JsonReader(utf8Json, new JsonReaderOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip
            });
            if (!reader.Read()) return Null;
            return ReadFromJsonReader(ref reader);
        }
        catch (JsonException)
        {
            return Null;
        }
    }

    public static CypherValue ReadFromJsonReader(ref Utf8JsonReader reader)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Null:
                return Null;
            case JsonTokenType.True:
                return FromBool(true);
            case JsonTokenType.False:
                return FromBool(false);
            case JsonTokenType.Number:
                if (reader.TryGetInt64(out var l)) return FromInt64(l);
                if (reader.TryGetDouble(out var d)) return FromFloat64(d);
                return Null;
            case JsonTokenType.String:
                return FromString(reader.GetString());
            case JsonTokenType.StartArray:
            {
                var list = new List<CypherValue>();
                while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                {
                    list.Add(ReadFromJsonReader(ref reader));
                }
                return FromArray(list);
            }
            case JsonTokenType.StartObject:
            {
                var props = new List<KeyValuePair<string, CypherValue>>();
                while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                {
                    if (reader.TokenType != JsonTokenType.PropertyName) continue;
                    var key = reader.GetString() ?? string.Empty;
                    if (!reader.Read()) break;
                    var val = ReadFromJsonReader(ref reader);
                    props.Add(new KeyValuePair<string, CypherValue>(key, val));
                }
                return FromObject(props);
            }
            default:
                return Null;
        }
    }

    public static CypherValue ReadFromJsonb(ReadOnlySpan<byte> jsonb)
    {
        if (jsonb.IsEmpty) return Null;
        var cursor = 0;
        return ReadJsonbElement(jsonb, ref cursor);
    }

    private static CypherValue ReadJsonbElement(ReadOnlySpan<byte> buffer, ref int cursor)
    {
        if (!SqliteJsonbReader.TryReadElement(buffer, ref cursor, out var elemType, out var payload))
        {
            return Null;
        }

        return elemType switch
        {
            0 => Null,
            1 => FromBool(true),
            2 => FromBool(false),
            3 or 4 => Utf8Parser.TryParse(payload, out long l, out _) ? FromInt64(l) : FromString(Encoding.UTF8.GetString(payload)),
            5 or 6 => Utf8Parser.TryParse(payload, out double d, out _) ? FromFloat64(d) : FromString(Encoding.UTF8.GetString(payload)),
            7 or 8 or 9 or 10 => FromString(payload.IsEmpty ? string.Empty : Encoding.UTF8.GetString(payload)),
            11 => ReadJsonbArray(payload),
            12 => ReadJsonbObject(payload),
            _ => Null
        };
    }

    private static CypherValue ReadJsonbArray(ReadOnlySpan<byte> payload)
    {
        var list = new List<CypherValue>();
        var cursor = 0;
        while (cursor < payload.Length)
        {
            list.Add(ReadJsonbElement(payload, ref cursor));
        }
        return FromArray(list);
    }

    private static CypherValue ReadJsonbObject(ReadOnlySpan<byte> payload)
    {
        var props = new List<KeyValuePair<string, CypherValue>>();
        var cursor = 0;
        while (cursor < payload.Length)
        {
            if (!SqliteJsonbReader.TryReadElement(payload, ref cursor, out _, out var keySpan)) break;
            var key = keySpan.IsEmpty ? string.Empty : Encoding.UTF8.GetString(keySpan);
            var val = ReadJsonbElement(payload, ref cursor);
            props.Add(new KeyValuePair<string, CypherValue>(key, val));
        }
        return FromObject(props);
    }

    public void WriteTo(Utf8JsonWriter writer)
    {
        switch (Kind)
        {
            case CypherValueKind.Null:
                writer.WriteNullValue();
                break;
            case CypherValueKind.Boolean:
                writer.WriteBooleanValue(_int64 != 0);
                break;
            case CypherValueKind.Int64:
                writer.WriteNumberValue(_int64);
                break;
            case CypherValueKind.Float64:
                writer.WriteNumberValue(_float64);
                break;
            case CypherValueKind.String:
                writer.WriteStringValue(_refValue as string);
                break;
            case CypherValueKind.Array:
                writer.WriteStartArray();
                var arr = AsArray();
                for (var i = 0; i < arr.Count; i++)
                {
                    arr[i].WriteTo(writer);
                }
                writer.WriteEndArray();
                break;
            case CypherValueKind.Object:
                writer.WriteStartObject();
                var obj = AsObject();
                for (var i = 0; i < obj.Count; i++)
                {
                    writer.WritePropertyName(obj[i].Key);
                    obj[i].Value.WriteTo(writer);
                }
                writer.WriteEndObject();
                break;
        }
    }

    public string ToJsonString(bool indented = false)
    {
        var buffer = new ArrayBufferWriter<byte>(256);
        using var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = indented });
        WriteTo(writer);
        writer.Flush();
        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    public override string ToString() => AsNullableString() ?? NullLiteral;

    private static bool ParseBoolString(string s, bool fallback)
    {
        if (string.Equals(s, TrueLiteral, StringComparison.OrdinalIgnoreCase) || s == OneLiteral) return true;
        if (string.Equals(s, FalseLiteral, StringComparison.OrdinalIgnoreCase) || s == ZeroLiteral) return false;
        return fallback;
    }
}

public sealed class CypherValueJsonConverter : JsonConverter<CypherValue>
{
    public override CypherValue Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        CypherValue.ReadFromJsonReader(ref reader);

    public override void Write(Utf8JsonWriter writer, CypherValue value, JsonSerializerOptions options) =>
        value.WriteTo(writer);
}

[JsonConverter(typeof(CypherRowJsonConverter))]
public readonly struct CypherRow
{
    private const string DefaultPropsColumn = "properties";

    private readonly IReadOnlyList<string> _columns;
    private readonly CypherValue[] _values;

    public CypherRow(IReadOnlyList<string> columns, CypherValue[] values)
    {
        _columns = columns ?? [];
        _values = values ?? [];
    }

    public IReadOnlyList<string> Columns => _columns ?? [];
    public IReadOnlyList<CypherValue> Values => _values ?? [];
    public int FieldCount => _values?.Length ?? 0;

    public CypherValue this[int index] =>
        _values is not null && (uint)index < (uint)_values.Length ? _values[index] : CypherValue.Null;

    public CypherValue this[string columnName] =>
        TryGetProperty(columnName, out var val) ? val : CypherValue.Null;

    public bool TryGetProperty(string columnName, out CypherValue value)
    {
        if (_columns is not null && _values is not null)
        {
            var count = Math.Min(_columns.Count, _values.Length);
            for (var i = 0; i < count; i++)
            {
                if (string.Equals(_columns[i], columnName, StringComparison.OrdinalIgnoreCase))
                {
                    value = _values[i];
                    return true;
                }
            }
        }
        value = CypherValue.Null;
        return false;
    }

    public string GetStringProp(string columnName, string fallback = "") =>
        TryGetProperty(columnName, out var v) && !v.IsNull ? v.AsString(fallback) : fallback;

    public int? GetNullableIntProp(string columnName) =>
        TryGetProperty(columnName, out var v) ? v.AsNullableInt32() : null;

    public int GetIntProp(string columnName, int fallback = 0) =>
        GetNullableIntProp(columnName) ?? fallback;

    public long GetLongProp(string columnName, long fallback = 0L) =>
        TryGetProperty(columnName, out var v) ? v.AsInt64(fallback) : fallback;

    public double GetDoubleProp(string columnName, double fallback = 0d) =>
        TryGetProperty(columnName, out var v) ? v.AsDouble(fallback) : fallback;

    public bool GetBoolProp(string columnName, bool fallback = false) =>
        TryGetProperty(columnName, out var v) ? v.AsBool(fallback) : fallback;

    public IReadOnlyList<string> GetStringListProp(string columnName) =>
        TryGetProperty(columnName, out var v) ? v.AsStringList() : [];

    public Dictionary<string, string> ExtractProperties(string propName = DefaultPropsColumn) =>
        TryGetProperty(propName, out var v) ? v.ExtractStringMap() : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public void WriteTo(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        if (_columns is not null && _values is not null)
        {
            var count = Math.Min(_columns.Count, _values.Length);
            for (var i = 0; i < count; i++)
            {
                writer.WritePropertyName(_columns[i]);
                _values[i].WriteTo(writer);
            }
        }
        writer.WriteEndObject();
    }
}

public sealed class CypherRowJsonConverter : JsonConverter<CypherRow>
{
    public override CypherRow Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var val = CypherValue.ReadFromJsonReader(ref reader);
        if (val.Kind == CypherValueKind.Object)
        {
            var props = val.AsObject();
            var cols = new string[props.Count];
            var vals = new CypherValue[props.Count];
            for (var i = 0; i < props.Count; i++)
            {
                cols[i] = props[i].Key;
                vals[i] = props[i].Value;
            }
            return new CypherRow(cols, vals);
        }
        return default;
    }

    public override void Write(Utf8JsonWriter writer, CypherRow value, JsonSerializerOptions options) =>
        value.WriteTo(writer);
}

public sealed class CypherQueryResult
{
    public static readonly CypherQueryResult Empty = new([], []);

    public IReadOnlyList<string> Columns { get; }
    public IReadOnlyList<CypherRow> Rows { get; }
    public int Count => Rows.Count;

    public CypherQueryResult(IReadOnlyList<string> columns, IReadOnlyList<CypherRow> rows)
    {
        Columns = columns ?? [];
        Rows = rows ?? [];
    }

    public string ToJson(bool indented = true)
    {
        var buffer = new ArrayBufferWriter<byte>(512);
        using var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = indented });
        WriteRowsTo(writer);
        writer.Flush();
        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    public void WriteRowsTo(Utf8JsonWriter writer)
    {
        writer.WriteStartArray();
        for (var i = 0; i < Rows.Count; i++)
        {
            Rows[i].WriteTo(writer);
        }
        writer.WriteEndArray();
    }

    public static CypherQueryResult FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return Empty;
        var root = CypherValue.TryParseJsonText(json);
        if (root.Kind != CypherValueKind.Array) return Empty;

        var items = root.AsArray();
        if (items.Count == 0) return Empty;

        var columns = new List<string>();
        var colSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var rows = new List<CypherRow>(items.Count);

        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            if (item.Kind == CypherValueKind.Object)
            {
                var props = item.AsObject();
                var rowCols = new string[props.Count];
                var rowVals = new CypherValue[props.Count];
                for (var p = 0; p < props.Count; p++)
                {
                    var k = props[p].Key;
                    rowCols[p] = k;
                    rowVals[p] = props[p].Value;
                    if (colSet.Add(k))
                    {
                        columns.Add(k);
                    }
                }
                rows.Add(new CypherRow(rowCols, rowVals));
            }
        }

        return new CypherQueryResult(columns, rows);
    }
}
