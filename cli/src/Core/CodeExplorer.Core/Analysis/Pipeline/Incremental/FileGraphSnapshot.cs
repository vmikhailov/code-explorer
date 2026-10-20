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
    public static FileGraphSnapshot Empty(string relativePath) =>
        new(relativePath, "", DateTime.MinValue, new Dictionary<string, SymbolFootprint>());
}
