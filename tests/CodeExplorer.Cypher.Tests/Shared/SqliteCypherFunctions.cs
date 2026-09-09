using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;

namespace CodeExplorer.Cypher.Tests.Shared;

public static class SqliteCypherFunctions
{
    public static void Register(SqliteConnection conn)
    {
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

        // 5. List functions
        conn.CreateFunction("head", (string? jsonList) =>
        {
            if (string.IsNullOrEmpty(jsonList)) return null;
            try
            {
                using var doc = JsonDocument.Parse(jsonList);
                if (doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() > 0)
                {
                    return doc.RootElement[0].ToString();
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
                using var doc = JsonDocument.Parse(jsonList);
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    var len = doc.RootElement.GetArrayLength();
                    if (len > 0) return doc.RootElement[len - 1].ToString();
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
                using var doc = JsonDocument.Parse(jsonList);
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    var items = doc.RootElement.EnumerateArray().Skip(1).Select(x => x.ToString()).ToList();
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
