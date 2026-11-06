using System.Text.Json;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Parser.Syntax;

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
        Relationship targetRel,
        Relationship contributingRel,
        int cap = DefaultEvidenceCap) =>
        RecordContributingEdge(targetRel, contributingRel.From, contributingRel.Kind, contributingRel.To, cap);

    public static void RecordContributingEdge(
        Relationship targetRel,
        string fromId,
        string kind,
        string toId,
        int cap = DefaultEvidenceCap)
    {
        if (string.IsNullOrWhiteSpace(fromId) || string.IsNullOrWhiteSpace(kind) || string.IsNullOrWhiteSpace(toId))
        {
            return;
        }

        if (targetRel.HasMaterializedProperties)
        {
            RecordContributingEdge(targetRel.Properties, fromId, kind, toId, cap);
            return;
        }

        var edgeIdentity = FormatEdgeIdentity(fromId, kind, toId);
        targetRel.EvidenceIds ??= new List<string>(4);
        if (targetRel.EvidenceIds.Contains(edgeIdentity, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        var totalCount = Math.Max(targetRel.EvidenceIds.Count, targetRel.EvidenceCount) + 1;
        if (targetRel.EvidenceIds.Count < cap)
        {
            targetRel.EvidenceIds.Add(edgeIdentity);
        }

        targetRel.EvidenceCount = totalCount;
        if (totalCount > cap)
        {
            targetRel.EvidenceTruncated = true;
        }
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
        Relationship target,
        Relationship? source,
        int cap = DefaultEvidenceCap)
    {
        if (source is null) return;
        if (target.HasMaterializedProperties || source.HasMaterializedProperties)
        {
            MergeProvenance(target.Properties, source.Properties, cap);
            return;
        }

        if (source.EvidenceIds is not { Count: > 0 }) return;
        target.EvidenceIds ??= new List<string>(source.EvidenceIds.Count);
        var targetTotal = Math.Max(target.EvidenceIds.Count, target.EvidenceCount);
        var sourceTotal = Math.Max(source.EvidenceIds.Count, source.EvidenceCount);
        var added = 0;

        foreach (var id in source.EvidenceIds)
        {
            if (target.EvidenceIds.Contains(id, StringComparer.OrdinalIgnoreCase)) continue;
            added++;
            if (target.EvidenceIds.Count < cap)
            {
                target.EvidenceIds.Add(id);
            }
        }

        var extraBeyondListed = Math.Max(0, sourceTotal - source.EvidenceIds.Count);
        var newTotal = targetTotal + added + extraBeyondListed;
        target.EvidenceCount = newTotal;
        if (newTotal > cap || source.EvidenceTruncated)
        {
            target.EvidenceTruncated = true;
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

    public static Dictionary<string, string>? BuildCallSiteExtensions(
        int line,
        int column,
        CallSiteExecutionContext context = default,
        string? argFlow = null)
    {
        if (line <= 0) return null;
        var col = Math.Max(1, column);
        var dict = new Dictionary<string, string>
        {
            ["line"] = line.ToString(),
            ["column"] = col.ToString(),
            ["call_sites"] = $"[\"{line}:{col}\"]",
            ["call_count"] = "1"
        };

        if (context.Flags.HasFlag(CallExecutionFlags.InLoop))
        {
            dict["in_loop"] = "true";
        }
        if (context.LoopDepth > 0)
        {
            dict["loop_depth"] = context.LoopDepth.ToString();
        }
        if (context.Flags.HasFlag(CallExecutionFlags.IsConditional))
        {
            dict["is_conditional"] = "true";
        }
        if (context.Flags.HasFlag(CallExecutionFlags.InTryBlock))
        {
            dict["in_try"] = "true";
        }
        if (context.Flags.HasFlag(CallExecutionFlags.InCatchBlock))
        {
            dict["in_catch"] = "true";
        }
        if (context.Flags.HasFlag(CallExecutionFlags.InFinallyBlock))
        {
            dict["in_finally"] = "true";
        }
        if (context.Flags.HasFlag(CallExecutionFlags.InLockBlock))
        {
            dict["in_lock"] = "true";
        }
        if (context.Flags.HasFlag(CallExecutionFlags.IsAwaited))
        {
            dict["is_awaited"] = "true";
        }
        if (!string.IsNullOrEmpty(argFlow))
        {
            dict["arg_flow"] = argFlow;
        }

        return dict;
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

    public static void MergeRelationships(Relationship target, Relationship source, int cap = DefaultEvidenceCap)
    {
        if (!source.HasTelemetry) return;

        // Fast-path when neither side has materialized a raw Dictionary<string, object>
        if (!target.HasMaterializedProperties && !source.HasMaterializedProperties)
        {
            // 1. Merge Call Sites
            if (source.Line > 0)
            {
                if (target.Line <= 0)
                {
                    target.Line = source.Line;
                    target.Column = source.Column;
                    target.CallCount = Math.Max(1, source.CallCount);
                    target.CallContext = source.CallContext;
                    target.ArgFlow ??= source.ArgFlow;
                    if (source.AdditionalCallSites is { Count: > 0 })
                    {
                        target.AdditionalCallSites = [.. source.AdditionalCallSites];
                    }
                }
                else
                {
                    AddCallSiteCoord(target, source.Line, Math.Max(1, source.Column), cap);
                    if (source.AdditionalCallSites != null)
                    {
                        foreach (var (l, c) in source.AdditionalCallSites)
                        {
                            AddCallSiteCoord(target, l, c, cap);
                        }
                    }
                    target.CallContext = new CallSiteExecutionContext(
                        target.CallContext.Flags | source.CallContext.Flags,
                        Math.Max(target.CallContext.LoopDepth, source.CallContext.LoopDepth));
                    target.ArgFlow ??= source.ArgFlow;
                }
            }

            // 2. Merge Evidence IDs
            if (source.EvidenceIds is { Count: > 0 })
            {
                target.EvidenceIds ??= new List<string>(source.EvidenceIds.Count);
                var added = 0;
                foreach (var id in source.EvidenceIds)
                {
                    if (target.EvidenceIds.Contains(id, StringComparer.OrdinalIgnoreCase)) continue;
                    added++;
                    if (target.EvidenceIds.Count < cap)
                    {
                        target.EvidenceIds.Add(id);
                    }
                }
                target.EvidenceCount = Math.Max(target.EvidenceIds.Count, target.EvidenceCount + added);
                if (target.EvidenceCount > cap || source.EvidenceTruncated)
                {
                    target.EvidenceTruncated = true;
                }
            }

            if (target.Hops == 0 && source.Hops > 0) target.Hops = source.Hops;
            target.TargetSinkClassification ??= source.TargetSinkClassification;
            if (target.LineEnd == 0 && source.LineEnd > 0) target.LineEnd = source.LineEnd;
            target.FilePath ??= source.FilePath;
            target.Operation ??= source.Operation;
            target.EvidenceKind ??= source.EvidenceKind;
            target.DependencyType ??= source.DependencyType;
            target.Category ??= source.Category;
            target.IsSemantic ??= source.IsSemantic;
            if (!target.IsCanonical && source.IsCanonical) target.IsCanonical = true;
            if (!target.IsSemanticLifted && source.IsSemanticLifted) target.IsSemanticLifted = true;
            if (!target.IsExternal && source.IsExternal) target.IsExternal = true;
            target.IsInternal ??= source.IsInternal;
            if (!target.IsOrm && source.IsOrm) target.IsOrm = true;
            if (!target.FromConfig && source.FromConfig) target.FromConfig = true;
            target.Scope ??= source.Scope;
            target.BrokerType ??= source.BrokerType;
            target.MessagingDirection ??= source.MessagingDirection;
            target.ViaLibrary ??= source.ViaLibrary;
            target.CallChain ??= source.CallChain;
            target.ConfigKey ??= source.ConfigKey;
            target.Url ??= source.Url;
            target.ServiceName ??= source.ServiceName;
            target.ServiceKey ??= source.ServiceKey;
            target.ResourceRole ??= source.ResourceRole;
            target.Asset ??= source.Asset;
            target.TargetFile ??= source.TargetFile;
            target.Via ??= source.Via;
            target.Provider ??= source.Provider;
            target.Schema ??= source.Schema;
            target.Engine ??= source.Engine;
            target.DbType ??= source.DbType;
            if (!target.IsImplicitSchema && source.IsImplicitSchema) target.IsImplicitSchema = true;
            target.Protocols ??= source.Protocols;
            if (source.CustomProperties is { Count: > 0 })
            {
                target.CustomProperties ??= new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var (k, v) in source.CustomProperties)
                {
                    target.CustomProperties.TryAdd(k, v);
                }
            }
            return;
        }

        MergeEdgeProperties(target.Properties, source.Properties, cap);
    }

    private static void AddCallSiteCoord(Relationship target, int line, int col, int cap)
    {
        var tCol = Math.Max(1, target.Column);
        if (target.Line == line && tCol == col) return;
        if (target.AdditionalCallSites != null)
        {
            for (var i = 0; i < target.AdditionalCallSites.Count; i++)
            {
                if (target.AdditionalCallSites[i].Line == line && target.AdditionalCallSites[i].Column == col)
                    return;
            }
        }

        target.CallCount = Math.Max(1, target.CallCount) + 1;
        var currentTotalSites = 1 + (target.AdditionalCallSites?.Count ?? 0);
        if (currentTotalSites < cap)
        {
            target.AdditionalCallSites ??= new List<(int Line, int Column)>(4);
            target.AdditionalCallSites.Add((line, col));
        }
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

