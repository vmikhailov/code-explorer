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
}
