using System.Buffers;
using System.Buffers.Text;
using System.Text;
using System.Text.Json;

namespace CodeExplorer.Core.Database;

public readonly partial struct CypherValue
{
    public static CypherValue FromSqliteObject(object? val) => CypherValueParser.FromSqliteObject(val);
    public static CypherValue TryParseJsonText(string json) => CypherValueParser.TryParseJsonText(json);
}

internal static class CypherValueParser
{
    public static CypherValue FromSqliteObject(object? val)
    {
        return val switch
        {
            null or DBNull => CypherValue.Null,
            CypherValue cv => cv,
            bool b => CypherValue.FromBool(b),
            long l => CypherValue.FromInt64(l),
            int i => CypherValue.FromInt64(i),
            short s => CypherValue.FromInt64(s),
            byte b8 => CypherValue.FromInt64(b8),
            double d => CypherValue.FromFloat64(d),
            float f => CypherValue.FromFloat64(f),
            decimal dec => CypherValue.FromFloat64((double)dec),
            byte[] bytes => ReadFromJsonb(bytes),
            string str => FromPotentialJsonString(str),
            _ => CypherValue.FromString(val.ToString())
        };
    }

    public static CypherValue FromPotentialJsonString(string? str)
    {
        if (str is null) return CypherValue.Null;
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
        return CypherValue.FromString(str);
    }

    public static CypherValue TryParseJsonText(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return CypherValue.Null;
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
            if (!reader.Read()) return CypherValue.Null;
            return ReadFromJsonReader(ref reader);
        }
        catch (JsonException)
        {
            return CypherValue.Null;
        }
    }

    public static CypherValue ReadFromJsonReader(ref Utf8JsonReader reader)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Null:
                return CypherValue.Null;
            case JsonTokenType.True:
                return CypherValue.FromBool(true);
            case JsonTokenType.False:
                return CypherValue.FromBool(false);
            case JsonTokenType.Number:
                if (reader.TryGetInt64(out var l)) return CypherValue.FromInt64(l);
                if (reader.TryGetDouble(out var d)) return CypherValue.FromFloat64(d);
                return CypherValue.Null;
            case JsonTokenType.String:
                return CypherValue.FromString(reader.GetString());
            case JsonTokenType.StartArray:
            {
                var list = new List<CypherValue>();
                while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                {
                    list.Add(ReadFromJsonReader(ref reader));
                }
                return CypherValue.FromArray(list);
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
                return CypherValue.FromObject(props);
            }
            default:
                return CypherValue.Null;
        }
    }

    public static CypherValue ReadFromJsonb(ReadOnlySpan<byte> jsonb)
    {
        if (jsonb.IsEmpty) return CypherValue.Null;
        var cursor = 0;
        return ReadJsonbElement(jsonb, ref cursor);
    }

    private static CypherValue ReadJsonbElement(ReadOnlySpan<byte> buffer, ref int cursor)
    {
        if (!SqliteJsonbReader.TryReadElement(buffer, ref cursor, out var elemType, out var payload))
        {
            return CypherValue.Null;
        }

        return elemType switch
        {
            0 => CypherValue.Null,
            1 => CypherValue.FromBool(true),
            2 => CypherValue.FromBool(false),
            3 or 4 => Utf8Parser.TryParse(payload, out long l, out _) ? CypherValue.FromInt64(l) : CypherValue.FromString(Encoding.UTF8.GetString(payload)),
            5 or 6 => Utf8Parser.TryParse(payload, out double d, out _) ? CypherValue.FromFloat64(d) : CypherValue.FromString(Encoding.UTF8.GetString(payload)),
            7 or 8 or 9 or 10 => CypherValue.FromString(payload.IsEmpty ? string.Empty : Encoding.UTF8.GetString(payload)),
            11 => ReadJsonbArray(payload),
            12 => ReadJsonbObject(payload),
            _ => CypherValue.Null
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
        return CypherValue.FromArray(list);
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
        return CypherValue.FromObject(props);
    }
}
