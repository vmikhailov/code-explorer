namespace CodeExplorer.Core.Parser.Incremental;

/// <summary>
/// Fine-grained semantic footprint of a symbol (class, method, endpoint, query) in a file.
/// Used by the SemanticGraphDiffer to determine whether graph relationships need to be updated.
/// </summary>
public record SymbolFootprint(
    string SymbolId,
    string QualifiedName,
    string Kind,
    string SignatureHash,
    string BodyHash,
    HashSet<string> OutgoingCalls,
    HashSet<string> OutgoingTypes,
    int StartLine,
    int EndLine,
    int StartCol = 0,
    int EndCol = 0
)
{
    public bool HasIdenticalSignatureAndCalls(SymbolFootprint other)
    {
        if (!string.Equals(SignatureHash, other.SignatureHash, StringComparison.Ordinal)) return false;
        if (!OutgoingCalls.SetEquals(other.OutgoingCalls)) return false;
        if (!OutgoingTypes.SetEquals(other.OutgoingTypes)) return false;
        return true;
    }
}
