using System.Text.Json;

namespace CodeExplorer.Core.Common;

public static class JsonElementExtensions
{
    public static string GetStringProp(this JsonElement elem, string propName, string fallback = "")
    {
        return elem.TryGetProperty(propName, out var v) && v.ValueKind == JsonValueKind.String
            ? (v.GetString() ?? fallback)
            : fallback;
    }

    public static int? GetNullableIntProp(this JsonElement elem, string propName)
    {
        if (elem.TryGetProperty(propName, out var v))
        {
            if (v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var num)) return num;
            if (v.ValueKind == JsonValueKind.String && int.TryParse(v.GetString(), out var parsed)) return parsed;
        }
        return null;
    }

    public static Dictionary<string, string> ExtractProperties(this JsonElement elem, string propName = "properties")
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!elem.TryGetProperty(propName, out var pElem)) return result;

        if (pElem.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in pElem.EnumerateObject())
            {
                result[prop.Name] = prop.Value.ToString();
            }
        }
        else if (pElem.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(pElem.GetString()))
        {
            try
            {
                using var innerDoc = JsonDocument.Parse(pElem.GetString()!);

                if (innerDoc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in innerDoc.RootElement.EnumerateObject())
                    {
                        result[prop.Name] = prop.Value.ToString();
                    }
                }
            }
            catch { }
        }

        return result;
    }
}
