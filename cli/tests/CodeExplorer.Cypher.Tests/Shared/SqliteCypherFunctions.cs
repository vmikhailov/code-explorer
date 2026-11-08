using System.Text.Json;
using System.Text.RegularExpressions;
using CodeExplorer.Cypher.Common;
using Microsoft.Data.Sqlite;

namespace CodeExplorer.Cypher.Tests.Shared;

public static class SqliteCypherFunctions
{
    public static void Register(SqliteConnection conn)
    {
        conn.CreateFunction<object?, string?>("ce_node_kind", static v => v switch
        {
            null => null,
            long l => GraphKindCodec.DecodeNodeKind((int)l),
            int i => GraphKindCodec.DecodeNodeKind(i),
            string s => int.TryParse(s, out var parsed) ? GraphKindCodec.DecodeNodeKind(parsed) : s,
            _ => v.ToString()
        }, isDeterministic: true);

        conn.CreateFunction<object?, int?>("ce_node_kind_id", static v => v switch
        {
            null => null,
            long l => (int)l,
            int i => i,
            string s => GraphKindCodec.EncodeNodeKind(s),
            _ => GraphKindCodec.EncodeNodeKind(v.ToString())
        }, isDeterministic: true);

        conn.CreateFunction<object?, string?>("ce_rel_kind", static v => v switch
        {
            null => null,
            long l => GraphKindCodec.DecodeRelKind((int)l),
            int i => GraphKindCodec.DecodeRelKind(i),
            string s => int.TryParse(s, out var parsed) ? GraphKindCodec.DecodeRelKind(parsed) : s,
            _ => v.ToString()
        }, isDeterministic: true);

        conn.CreateFunction<object?, int?>("ce_rel_kind_id", static v => v switch
        {
            null => null,
            long l => (int)l,
            int i => i,
            string s => GraphKindCodec.EncodeRelKind(s),
            _ => GraphKindCodec.EncodeRelKind(v.ToString())
        }, isDeterministic: true);

        // 1. Math functions
        conn.CreateFunction("power", (double x, double y) => Math.Pow(x, y));
        conn.CreateFunction("sqrt", (double x) => Math.Sqrt(x));

        // 2. Regular expressions
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

        // 3. String functions
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

        // 4. Object & Map functions
        conn.CreateFunction("properties", (string? propsJson) => propsJson ?? "{}");

        conn.CreateFunction("keys", (string? propsJson) =>
        {
            if (string.IsNullOrEmpty(propsJson)) return "[]";
            try
            {
                var map = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(propsJson);
                if (map != null)
                {
                    return JsonSerializer.Serialize(map.Keys.ToList());
                }
            }
            catch { }
            return "[]";
        });

        // 5. List functions
        conn.CreateFunction("head", (string? jsonList) =>
        {
            if (string.IsNullOrEmpty(jsonList)) return null;
            try
            {
                var arr = JsonSerializer.Deserialize<List<JsonElement>>(jsonList);
                if (arr is { Count: > 0 })
                {
                    return arr[0].ToString();
                }
            }
            catch { }
            return null;
        });

        conn.CreateFunction("last", (string? jsonList) =>
        {
            if (string.IsNullOrEmpty(jsonList)) return null;
            try
            {
                var arr = JsonSerializer.Deserialize<List<JsonElement>>(jsonList);
                if (arr is { Count: > 0 })
                {
                    return arr[^1].ToString();
                }
            }
            catch { }
            return null;
        });

        conn.CreateFunction("tail", (string? jsonList) =>
        {
            if (string.IsNullOrEmpty(jsonList)) return "[]";
            try
            {
                var arr = JsonSerializer.Deserialize<List<JsonElement>>(jsonList);
                if (arr != null)
                {
                    var items = arr.Skip(1).Select(x => x.ToString()).ToList();
                    return JsonSerializer.Serialize(items);
                }
            }
            catch { }
            return "[]";
        });

        conn.CreateFunction("range", (int start, int end, int step) =>
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
        });
    }
}
