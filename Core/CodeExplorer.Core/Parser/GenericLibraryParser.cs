using CodeExplorer.Common;
using TreeSitter;

namespace CodeExplorer.Core.Parser;

public class GenericLibraryParser : ILibraryParser
{
    public string Name { get; }
    public IReadOnlyList<string> SupportedPatterns { get; }
    public bool IsImplemented => false;

    public string LibraryType { get; }
    public string LibraryName { get; }
    public string LibraryId { get; }
    public bool IsBuiltIn { get; }

    public GenericLibraryParser(
        string name,
        string libraryType,
        IReadOnlyList<string> supportedPatterns,
        string? libraryName = null,
        string? libraryId = null,
        bool isBuiltIn = false)
    {
        Name = name;
        LibraryType = libraryType;
        SupportedPatterns = supportedPatterns ?? Array.Empty<string>();
        LibraryName = libraryName ?? name;
        IsBuiltIn = isBuiltIn;

        var firstLib = System.Linq.Enumerable.FirstOrDefault(SupportedPatterns);
        LibraryId = libraryId ?? (firstLib != null ? firstLib.ToLowerInvariant() : name.ToLowerInvariant());
    }

    public string? MapNodeType(Node node, ParsingContext ctx) => null;
    public string? ExtractIdentifier(Node node, ParsingContext ctx) => null;
    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx) { }
}
