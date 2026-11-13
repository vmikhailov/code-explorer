using System.Text;

namespace CodeExplorer.Core.Parser.Incremental;

/// <summary>
/// In-memory semantic snapshot of an indexed file's symbols and declarations.
/// </summary>
public record FileGraphSnapshot(
    string RelativePath,
    string ContentHash,
    DateTime LastModifiedUtc,
    IReadOnlyDictionary<string, SymbolFootprint> Symbols
)
{
    public string? PrecomputedStructuralHash { get; init; }

    public static FileGraphSnapshot Empty(string relativePath) =>
        new(relativePath, "", DateTime.MinValue, new Dictionary<string, SymbolFootprint>());

    public static FileGraphSnapshot FromStructuralHash(string relativePath, string contentHash, DateTime lastModifiedUtc, string structuralHash) =>
        new(relativePath, contentHash, lastModifiedUtc, new Dictionary<string, SymbolFootprint>())
        {
            PrecomputedStructuralHash = structuralHash
        };

    /// <summary>
    /// Computes a canonical 64-char SHA-256 hash over all structural symbol signatures, outgoing calls, and outgoing types,
    /// invariant to line/column shifts and internal method body logic changes.
    /// </summary>
    public string ComputeStructuralHash()
    {
        if (!string.IsNullOrEmpty(PrecomputedStructuralHash))
        {
            return PrecomputedStructuralHash;
        }

        if (Symbols.Count == 0)
        {
            return HashUtility.ComputeSha256(string.Empty);
        }

        var sb = new StringBuilder(Symbols.Count * 96);
        foreach (var (id, sym) in Symbols.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            sb.Append(id).Append('|').Append(sym.SignatureHash).Append('|');
            if (sym.OutgoingCalls.Count > 0)
            {
                foreach (var c in sym.OutgoingCalls.OrderBy(x => x, StringComparer.Ordinal))
                {
                    sb.Append(c).Append(',');
                }
            }
            sb.Append('|');
            if (sym.OutgoingTypes.Count > 0)
            {
                foreach (var t in sym.OutgoingTypes.OrderBy(x => x, StringComparer.Ordinal))
                {
                    sb.Append(t).Append(',');
                }
            }
            sb.Append('\n');
        }

        return HashUtility.ComputeSha256(sb.ToString());
    }
}
