using CodeExplorer.Common;
using TreeSitter;

namespace CodeExplorer.Core.Parser;

public class GenericLibraryParser : ILibraryParser
{
    public string Name { get; }
    public IReadOnlyList<string> SupportedPatterns { get; }
    public bool IsImplemented => false;

    public string Type { get; }
    public string Id { get; }
    public bool IsBuiltIn { get; }

    public GenericLibraryParser(
        string id,
        string name,
        string libraryType,
        IReadOnlyList<string> supportedPatterns,
        bool isBuiltIn = false)
    {
        Id = id;
        Name = name;
        Type = libraryType;
        SupportedPatterns = supportedPatterns ?? Array.Empty<string>();
        IsBuiltIn = isBuiltIn;
    }

    public string? MapNodeType(Node node, ParsingContext ctx) => null;
    public string? ExtractIdentifier(Node node, ParsingContext ctx) => null;

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
    }
}
