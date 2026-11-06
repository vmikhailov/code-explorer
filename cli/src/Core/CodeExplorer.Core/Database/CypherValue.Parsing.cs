using System.Buffers;
using System.Buffers.Text;
using System.Text;
using System.Text.Json;

namespace CodeExplorer.Core.Database;

public readonly partial struct CypherValue
{
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
}
