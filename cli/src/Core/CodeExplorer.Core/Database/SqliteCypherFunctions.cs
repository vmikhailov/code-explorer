using System.Text.Json;
using System.Text.RegularExpressions;
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
            try
            {
                using var doc = JsonDocument.Parse(propsJson);
                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    var keys = doc.RootElement.EnumerateObject().Select(p => p.Name).ToList();
                    return JsonSerializer.Serialize(keys);
                }
            }
            catch { }
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
        try
        {
            using var doc = JsonDocument.Parse(jsonList);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return null;
            var len = doc.RootElement.GetArrayLength();
            if (len == 0) return null;
            return (isLast ? doc.RootElement[len - 1] : doc.RootElement[0]).ToString();
        }
        catch { }
        return null;
    }

    private static string SafeListTail(string? jsonList)
    {
        if (string.IsNullOrEmpty(jsonList)) return "[]";
        try
        {
            using var doc = JsonDocument.Parse(jsonList);
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                var items = doc.RootElement.EnumerateArray().Skip(1).Select(x => x.ToString()).ToList();
                return JsonSerializer.Serialize(items);
            }
        }
        catch { }
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
