using System.Text.Json;
using System.Text.RegularExpressions;
using CodeExplorer.Cypher.Common;
using Microsoft.Data.Sqlite;

namespace CodeExplorer.Core.Database;

public static class SqliteCypherFunctions
{
    public static void Register(SqliteConnection conn)
    {
        RegisterMathFunctions(conn);
        RegisterRegexFunctions(conn);
        RegisterStringFunctions(conn);
        RegisterObjectFunctions(conn);
        RegisterListFunctions(conn);
        RegisterKindFunctions(conn);
    }

    private static void RegisterKindFunctions(SqliteConnection conn)
    {
        const string fnNodeKind = "ce_node_kind";
        const string fnNodeKindId = "ce_node_kind_id";
        const string fnRelKind = "ce_rel_kind";
        const string fnRelKindId = "ce_rel_kind_id";

        conn.CreateFunction<object?, string?>(fnNodeKind, static v => v switch
        {
            null => null,
            long l => GraphKindCodec.DecodeNodeKind((int)l),
            int i => GraphKindCodec.DecodeNodeKind(i),
            string s => int.TryParse(s, out var parsed) ? GraphKindCodec.DecodeNodeKind(parsed) : s,
            _ => v.ToString()
        }, isDeterministic: true);

        conn.CreateFunction<object?, int?>(fnNodeKindId, static v => v switch
        {
            null => null,
            long l => (int)l,
            int i => i,
            string s => GraphKindCodec.EncodeNodeKind(s),
            _ => GraphKindCodec.EncodeNodeKind(v.ToString())
        }, isDeterministic: true);

        conn.CreateFunction<object?, string?>(fnRelKind, static v => v switch
        {
            null => null,
            long l => GraphKindCodec.DecodeRelKind((int)l),
            int i => GraphKindCodec.DecodeRelKind(i),
            string s => int.TryParse(s, out var parsed) ? GraphKindCodec.DecodeRelKind(parsed) : s,
            _ => v.ToString()
        }, isDeterministic: true);

        conn.CreateFunction<object?, int?>(fnRelKindId, static v => v switch
        {
            null => null,
            long l => (int)l,
            int i => i,
            string s => GraphKindCodec.EncodeRelKind(s),
            _ => GraphKindCodec.EncodeRelKind(v.ToString())
        }, isDeterministic: true);
    }

    private static void RegisterMathFunctions(SqliteConnection conn)
    {
        conn.CreateFunction("power", (double x, double y) => Math.Pow(x, y));
        conn.CreateFunction("sqrt", (double x) => Math.Sqrt(x));
    }

    private static void RegisterRegexFunctions(SqliteConnection conn)
    {
        conn.CreateFunction("regexp", (string pattern, string input) =>
        {
            if (string.IsNullOrEmpty(pattern) || string.IsNullOrEmpty(input)) return 0;
            try
            {
                return Regex.IsMatch(input, pattern) ? 1 : 0;
            }
            catch
            {
                return 0;
            }
        });
    }

    private static void RegisterStringFunctions(SqliteConnection conn)
    {
        conn.CreateFunction("reverse", (string? s) =>
        {
            if (string.IsNullOrEmpty(s)) return s;
            var arr = s.ToCharArray();
            Array.Reverse(arr);
            return new string(arr);
        });

        conn.CreateFunction("split", (string? s, string? delimiter) =>
        {
            if (s == null) return "[]";
            var delim = delimiter ?? ",";
            var parts = s.Split(delim);
            return JsonSerializer.Serialize(parts);
        });
    }

    private static void RegisterObjectFunctions(SqliteConnection conn)
    {
        conn.CreateFunction("properties", (string? propsJson) => propsJson ?? "{}");
        conn.CreateFunction("keys", (string? propsJson) =>
        {
            if (string.IsNullOrEmpty(propsJson)) return "[]";
            var parsed = CypherValue.TryParseJsonText(propsJson);
            if (parsed.Kind == CypherValueKind.Object)
            {
                var keys = parsed.AsObject().Select(p => p.Key).ToList();
                return JsonSerializer.Serialize(keys);
            }
            return "[]";
        });
    }

    private static void RegisterListFunctions(SqliteConnection conn)
    {
        conn.CreateFunction("head", (string? jsonList) => SafeListElement(jsonList, isLast: false));
        conn.CreateFunction("last", (string? jsonList) => SafeListElement(jsonList, isLast: true));
        conn.CreateFunction("tail", (string? jsonList) => SafeListTail(jsonList));
        conn.CreateFunction("range", (int start, int end, int step) => GenerateRangeJson(start, end, step));
    }

    private static string? SafeListElement(string? jsonList, bool isLast)
    {
        if (string.IsNullOrEmpty(jsonList)) return null;
        var parsed = CypherValue.TryParseJsonText(jsonList);
        if (parsed.Kind != CypherValueKind.Array) return null;

        var items = parsed.AsArray();
        if (items.Count == 0) return null;

        return (isLast ? items[^1] : items[0]).AsNullableString();
    }

    private static string SafeListTail(string? jsonList)
    {
        if (string.IsNullOrEmpty(jsonList)) return "[]";
        var parsed = CypherValue.TryParseJsonText(jsonList);
        if (parsed.Kind == CypherValueKind.Array)
        {
            var items = parsed.AsArray().Skip(1).Select(x => x.AsString()).ToList();
            return JsonSerializer.Serialize(items);
        }
        return "[]";
    }

    private static string GenerateRangeJson(int start, int end, int step)
    {
        if (step == 0) step = 1;
        var list = new List<int>();
        if (step > 0)
        {
            for (int i = start; i <= end; i += step) list.Add(i);
        }
        else
        {
            for (int i = start; i >= end; i += step) list.Add(i);
        }
        return JsonSerializer.Serialize(list);
    }
}
