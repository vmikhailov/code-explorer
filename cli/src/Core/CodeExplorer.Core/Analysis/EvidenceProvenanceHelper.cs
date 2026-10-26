using System.Text.Json;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Database;

namespace CodeExplorer.Core.Analysis;

public static class EvidenceProvenanceHelper
{
    public const int DefaultEvidenceCap = 200;

    public static string FormatEdgeIdentity(string fromId, string kind, string toId) =>
        $"{fromId}|{kind}|{toId}";

    public static bool TryParseEdgeIdentity(string identity, out (string From, string Kind, string To) edge)
    {
        edge = default;
        if (string.IsNullOrWhiteSpace(identity)) return false;
        var first = identity.IndexOf('|');
        if (first <= 0) return false;
        var second = identity.IndexOf('|', first + 1);
        if (second <= first + 1 || second >= identity.Length - 1) return false;

        edge = (identity[..first], identity[(first + 1)..second], identity[(second + 1)..]);
        return true;
    }

    public static void RecordContributingEdge(
        Dictionary<string, object> properties,
        Relationship contributingRel,
        int cap = DefaultEvidenceCap) =>
        RecordContributingEdge(properties, contributingRel.From, contributingRel.Kind, contributingRel.To, cap);

    public static void RecordContributingEdge(
        Dictionary<string, object> properties,
        string fromId,
        string kind,
        string toId,
        int cap = DefaultEvidenceCap)
    {
        if (string.IsNullOrWhiteSpace(fromId) || string.IsNullOrWhiteSpace(kind) || string.IsNullOrWhiteSpace(toId))
        {
            return;
        }

        var edgeIdentity = FormatEdgeIdentity(fromId, kind, toId);
        var ids = ParseEvidenceIds(properties);
        if (ids.Contains(edgeIdentity, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        var totalCount = GetEvidenceCount(properties, ids.Count) + 1;
        if (ids.Count < cap)
        {
            ids.Add(edgeIdentity);
        }

        properties[OntologyConstants.PropertyNames.EvidenceIds] = JsonSerializer.Serialize(ids);
        properties[OntologyConstants.PropertyNames.EvidenceCount] = totalCount;
        if (totalCount > cap)
        {
            properties[OntologyConstants.PropertyNames.EvidenceTruncated] = "true";
        }
    }

    public static void MergeProvenance(
        Dictionary<string, object> targetProperties,
        IReadOnlyDictionary<string, object>? sourceProperties,
        int cap = DefaultEvidenceCap)
    {
        if (sourceProperties is null) return;

        var targetIds = ParseEvidenceIds(targetProperties);
        var sourceIds = ParseEvidenceIds(sourceProperties);
        if (sourceIds.Count == 0) return;

        var targetTotal = GetEvidenceCount(targetProperties, targetIds.Count);
        var sourceTotal = GetEvidenceCount(sourceProperties, sourceIds.Count);
        var added = 0;

        foreach (var id in sourceIds)
        {
            if (targetIds.Contains(id, StringComparer.OrdinalIgnoreCase)) continue;
            added++;
            if (targetIds.Count < cap)
            {
                targetIds.Add(id);
            }
        }

        var extraBeyondListed = Math.Max(0, sourceTotal - sourceIds.Count);
        var newTotal = targetTotal + added + extraBeyondListed;

        targetProperties[OntologyConstants.PropertyNames.EvidenceIds] = JsonSerializer.Serialize(targetIds);
        targetProperties[OntologyConstants.PropertyNames.EvidenceCount] = newTotal;
        if (newTotal > cap ||
            (sourceProperties.TryGetValue(OntologyConstants.PropertyNames.EvidenceTruncated, out var trunc) &&
             string.Equals(trunc?.ToString(), "true", StringComparison.OrdinalIgnoreCase)))
        {
            targetProperties[OntologyConstants.PropertyNames.EvidenceTruncated] = "true";
        }
    }

    public static List<string> ParseEvidenceIds(IReadOnlyDictionary<string, object>? properties)
    {
        if (properties is null ||
            !properties.TryGetValue(OntologyConstants.PropertyNames.EvidenceIds, out var raw) ||
            raw is null)
        {
            return [];
        }

        var json = raw.ToString();
        if (string.IsNullOrWhiteSpace(json)) return [];

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static int GetEvidenceCount(IReadOnlyDictionary<string, object> properties, int fallback)
    {
        if (properties.TryGetValue(OntologyConstants.PropertyNames.EvidenceCount, out var raw) && raw is not null)
        {
            if (raw is int i) return i;
            if (int.TryParse(raw.ToString(), out var parsed)) return parsed;
        }
        return fallback;
    }

    public static Dictionary<string, string>? BuildCallSiteExtensions(int line, int column)
    {
        if (line <= 0) return null;
        var col = Math.Max(1, column);
        return new Dictionary<string, string>
        {
            ["line"] = line.ToString(),
            ["column"] = col.ToString(),
            ["call_sites"] = $"[\"{line}:{col}\"]",
            ["call_count"] = "1"
        };
    }

    public static List<string> ParseCallSites(IReadOnlyDictionary<string, object>? properties)
    {
        if (properties is null) return [];

        if (properties.TryGetValue("call_sites", out var raw) && raw is not null)
        {
            var json = raw.ToString();
            if (!string.IsNullOrWhiteSpace(json))
            {
                try
                {
                    var parsed = JsonSerializer.Deserialize<List<string>>(json);
                    if (parsed is { Count: > 0 }) return parsed;
                }
                catch
                {
                    // Fallback to scalar line/column below
                }
            }
        }

        if (TryExtractLineCol(properties, out var line, out var col))
        {
            return [$"{line}:{col}"];
        }

        return [];
    }

    public static void MergeEdgeProperties(
        Dictionary<string, object> targetProperties,
        IReadOnlyDictionary<string, object>? sourceProperties,
        int cap = DefaultEvidenceCap)
    {
        if (sourceProperties is null || sourceProperties.Count == 0) return;

        MergeProvenance(targetProperties, sourceProperties, cap);
        MergeCallSites(targetProperties, sourceProperties, cap);

        foreach (var (key, value) in sourceProperties)
        {
            if (!targetProperties.ContainsKey(key))
            {
                targetProperties[key] = value;
            }
        }
    }

    public static void MergeCallSites(
        Dictionary<string, object> targetProperties,
        IReadOnlyDictionary<string, object>? sourceProperties,
        int cap = DefaultEvidenceCap)
    {
        if (sourceProperties is null) return;

        var sourceSites = ParseCallSites(sourceProperties);
        if (sourceSites.Count == 0) return;

        var targetSites = ParseCallSites(targetProperties);
        var targetCount = GetIntProperty(targetProperties, "call_count", targetSites.Count);
        var sourceCount = GetIntProperty(sourceProperties, "call_count", sourceSites.Count);

        var added = 0;
        foreach (var site in sourceSites)
        {
            if (targetSites.Contains(site, StringComparer.Ordinal)) continue;
            added++;
            if (targetSites.Count < cap)
            {
                targetSites.Add(site);
            }
        }

        var extraBeyondListed = Math.Max(0, sourceCount - sourceSites.Count);
        var newTotal = Math.Max(targetSites.Count, targetCount + added + extraBeyondListed);

        if (targetSites.Count > 0)
        {
            if (!targetProperties.ContainsKey("line") && TryParseSite(targetSites[0], out var firstLine, out var firstCol))
            {
                targetProperties["line"] = firstLine.ToString();
                targetProperties["column"] = firstCol.ToString();
            }

            targetProperties["call_sites"] = JsonSerializer.Serialize(targetSites);
            targetProperties["call_count"] = newTotal.ToString();
        }
    }

    private static bool TryExtractLineCol(IReadOnlyDictionary<string, object> properties, out int line, out int col)
    {
        line = GetIntProperty(properties, "line", 0);
        col = GetIntProperty(properties, "column", 1);
        if (col <= 0) col = 1;
        return line > 0;
    }

    public static bool TryParseSite(string site, out int line, out int col)
    {
        line = 0;
        col = 1;
        if (string.IsNullOrWhiteSpace(site)) return false;
        var idx = site.IndexOf(':');
        if (idx <= 0)
        {
            return int.TryParse(site, out line) && line > 0;
        }

        if (!int.TryParse(site[..idx], out line) || line <= 0) return false;
        if (!int.TryParse(site[(idx + 1)..], out col) || col <= 0) col = 1;
        return true;
    }

    private static int GetIntProperty(IReadOnlyDictionary<string, object> properties, string key, int fallback)
    {
        if (properties.TryGetValue(key, out var raw) && raw is not null)
        {
            if (raw is int i) return i;
            if (int.TryParse(raw.ToString(), out var parsed)) return parsed;
        }
        return fallback;
    }
}

