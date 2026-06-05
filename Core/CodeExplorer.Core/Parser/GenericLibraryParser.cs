using CodeExplorer.Common;
using TreeSitter;

namespace CodeExplorer.Core.Parser;

public class GenericLibraryParser : ILibraryParser
{
    private readonly IEnumerable<string> _supportedLibraries;
    public string Name { get; }
    public bool IsImplemented => false;

    public string LibraryType { get; }
    public string LibraryName { get; }
    public string LibraryId { get; }
    public bool IsBuiltIn { get; }

    public bool Supports(string libraryName) =>
        System.Linq.Enumerable.Any(_supportedLibraries, sl => ILibraryParser.IsLibraryMatch(libraryName, sl));

    public GenericLibraryParser(
        string name,
        string libraryType,
        IEnumerable<string> supportedLibraries,
        string? libraryName = null,
        string? libraryId = null,
        bool isBuiltIn = false)
    {
        Name = name;
        LibraryType = libraryType;
        _supportedLibraries = supportedLibraries ?? Array.Empty<string>();
        LibraryName = libraryName ?? name;
        IsBuiltIn = isBuiltIn;

        var firstLib = System.Linq.Enumerable.FirstOrDefault(_supportedLibraries);
        LibraryId = libraryId ?? (firstLib != null ? firstLib.ToLowerInvariant() : name.ToLowerInvariant());
    }

    public string? MapNodeType(Node node, ParsingContext ctx) => null;
    public string? ExtractIdentifier(Node node, ParsingContext ctx) => null;
    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx) { }
}
