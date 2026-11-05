using System.Text.Json;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Protocol;

namespace CodeExplorer.Core.Analysis.GraphProjection;

/// <summary>
/// Evidence record for a lifted relationship between two containers.
/// </summary>
public sealed record LiftedEdgeEvidence(
    string SourceSymbol,
    string TargetSymbol,
    string? SourceFile,
    string? TargetFile,
    int? Line,
    string Kind);

/// <summary>
/// Result of edge lifting between target containers.
/// </summary>
public sealed class LiftedEdgeResult
{
    public required string SourceId { get; init; }
    public required string TargetId { get; init; }
    public required string Kind { get; init; }
    public int Weight { get; set; } = 0;
    public HashSet<string> SourceFiles { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<LiftedEdgeEvidence> Samples { get; } = [];
}

/// <summary>
/// Universal edge lifting engine that lifts member/leaf relationships up to target view containers.
/// </summary>
public static class UniversalCypherEdgeLifter
{
    public const int MaxSamplesPerEdge = 10;

    /// <summary>
    /// Lifts low-level edges in memory given a mapping from leaf symbol IDs / file paths to container IDs.
    /// </summary>
    public static List<GraphEdgeDto> LiftInMemory(
        IEnumerable<(string SourceLeafId, string TargetLeafId, string Kind, string? SourceFile, string? TargetFile, int? Line)> rawEdges,
        Func<string, string?> leafToContainerResolver)
    {
        var aggregated = new Dictionary<(string SourceContainer, string TargetContainer, string Kind), LiftedEdgeResult>();

        foreach (var (srcLeaf, tgtLeaf, kind, srcFile, tgtFile, line) in rawEdges)
        {
            var srcContainer = leafToContainerResolver(srcLeaf);
            var tgtContainer = leafToContainerResolver(tgtLeaf);

            if (string.IsNullOrEmpty(srcContainer) || string.IsNullOrEmpty(tgtContainer))
                continue;

            if (srcContainer.Equals(tgtContainer, StringComparison.OrdinalIgnoreCase))
                continue;

            var key = (srcContainer, tgtContainer, kind);
            if (!aggregated.TryGetValue(key, out var item))
            {
                item = new LiftedEdgeResult
                {
                    SourceId = srcContainer,
                    TargetId = tgtContainer,
                    Kind = kind
                };
                aggregated[key] = item;
            }

            item.Weight++;
            if (!string.IsNullOrEmpty(srcFile))
            {
                item.SourceFiles.Add(srcFile);
            }

            if (item.Samples.Count < MaxSamplesPerEdge)
            {
                item.Samples.Add(new LiftedEdgeEvidence(srcLeaf, tgtLeaf, srcFile, tgtFile, line, kind));
            }
        }

        var result = new List<GraphEdgeDto>(aggregated.Count);
        foreach (var ((src, tgt, kind), edge) in aggregated)
        {
            var props = new Dictionary<string, string>
            {
                ["count"] = edge.Weight.ToString(),
                ["distinct_files"] = edge.SourceFiles.Count.ToString(),
                ["label"] = kind,
                ["kind"] = kind
            };

            if (edge.Samples.Count > 0)
            {
                props["samples"] = JsonSerializer.Serialize(edge.Samples);
            }

            result.Add(new GraphEdgeDto
            {
                Id = $"{src}->{tgt}:{kind}",
                Source = src,
                Target = tgt,
                Kind = kind,
                Properties = props
            });
        }

        return result;
    }

    /// <summary>
    /// Executes a 2-level Cypher query lifting leaf relationships to container nodes.
    /// </summary>
    public static async Task<List<GraphEdgeDto>> LiftViaCypherAsync(
        IGraphClient db,
        string containerLabel,
        IReadOnlyList<string> relationshipKinds,
        string? scopeWhereClause = null,
        CancellationToken ct = default)
    {
        var relTypes = string.Join("|", relationshipKinds);
        var where = string.IsNullOrWhiteSpace(scopeWhereClause) ? "" : $"AND ({scopeWhereClause})";

        var query = $@"
            MATCH (c1:{containerLabel})-[:CONTAINS*0..2]->(u)-[r:{relTypes}]->(v)<-[:CONTAINS*0..2]-(c2:{containerLabel})
            WHERE c1 <> c2 {where}
            RETURN c1.id AS source, c2.id AS target, type(r) AS kind, count(r) AS weight
        ";

        var json = await db.ExecuteQueryAsync(query, null, ct);
        using var doc = JsonDocument.Parse(json);

        var list = new List<GraphEdgeDto>();
        foreach (var elem in doc.RootElement.EnumerateArray())
        {
            var src = elem.GetStringProp("source");
            var tgt = elem.GetStringProp("target");
            var kind = elem.GetStringProp("kind");
            var weight = elem.GetNullableIntProp("weight") ?? 1;

            if (string.IsNullOrEmpty(src) || string.IsNullOrEmpty(tgt)) continue;

            list.Add(new GraphEdgeDto
            {
                Id = $"{src}->{tgt}:{kind}",
                Source = src,
                Target = tgt,
                Kind = kind,
                Properties = new Dictionary<string, string>
                {
                    ["count"] = weight.ToString(),
                    ["label"] = kind,
                    ["kind"] = kind
                }
            });
        }

        return list;
    }
}
