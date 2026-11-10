using CodeExplorer.Cypher.Common;

namespace CodeExplorer.Core.Parser;

public partial class PostIndexAnalyzer
{
    private const string SymUrnToken = ":sym:";
    private const string SymbolUrnToken = ":symbol:";
    private const string ShortExtServiceUrnToken = ":es:";
    private const string ExtServiceUrnToken = ":externalservice:";
    private const string ResExtServiceUrnToken = ":res:service:external:";
    private const string CloudUrnToken = ":cloud:";

    private static void MaterializeServiceCalledByEdges(
        IReadOnlyList<Relationship> allRelationships,
        List<Relationship> materializedRels)
    {
        var seenCalledBy = new HashSet<(string From, string To)>();
        foreach (var rel in allRelationships)
        {
            if (rel.Kind == RelationshipKind.CalledBy)
            {
                seenCalledBy.Add((rel.From, rel.To));
            }
        }

        var serviceCallEdges = materializedRels
            .Where(r => r.Kind == RelationshipKind.ServiceCall && r.EvidenceIds is { Count: > 0 })
            .ToList();

        foreach (var scRel in serviceCallEdges)
        {
            var targetId = scRel.To;
            if (string.IsNullOrEmpty(targetId) || IsExternalOrCloudUrn(targetId))
            {
                continue;
            }

            foreach (var entry in scRel.EvidenceIds!)
            {
                if (!EvidenceProvenanceHelper.TryParseEdgeIdentity(entry, out var contrib))
                {
                    continue;
                }

                TryAddServiceCalledByEdge(targetId, contrib.From, materializedRels, seenCalledBy);
                TryAddServiceCalledByEdge(targetId, contrib.To, materializedRels, seenCalledBy);
            }
        }
    }

    private static void TryAddServiceCalledByEdge(
        string targetServiceId,
        string candidateCallerId,
        List<Relationship> materializedRels,
        HashSet<(string From, string To)> seenCalledBy)
    {
        if (string.IsNullOrEmpty(candidateCallerId) || !IsSymbolUrn(candidateCallerId))
        {
            return;
        }

        if (!seenCalledBy.Add((targetServiceId, candidateCallerId)))
        {
            return;
        }

        materializedRels.Add(new Relationship(targetServiceId, candidateCallerId, RelationshipKind.CalledBy)
        {
            IsSemantic = true
        });
    }

    private static bool IsSymbolUrn(string id) =>
        id.Contains(SymUrnToken, StringComparison.OrdinalIgnoreCase) ||
        id.Contains(SymbolUrnToken, StringComparison.OrdinalIgnoreCase);

    private static bool IsExternalOrCloudUrn(string id) =>
        id.Contains(ShortExtServiceUrnToken, StringComparison.OrdinalIgnoreCase) ||
        id.Contains(ExtServiceUrnToken, StringComparison.OrdinalIgnoreCase) ||
        id.Contains(ResExtServiceUrnToken, StringComparison.OrdinalIgnoreCase) ||
        id.Contains(CloudUrnToken, StringComparison.OrdinalIgnoreCase);
}
