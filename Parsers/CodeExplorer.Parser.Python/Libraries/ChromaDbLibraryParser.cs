using CodeExplorer.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.Python.Libraries;

public class ChromaDbLibraryParser : ILibraryParser
{
    public string Name => "ChromaDbLibraryParser";
    public string LibraryType => "db:vector";
    public string LibraryName => "Chroma";
    public string LibraryId => "chroma";
    public IEnumerable<string> SupportedLibraries => ["chromadb"];

    public string? MapNodeType(Node node, ParsingContext ctx) => throw new NotImplementedException();
    public string? ExtractIdentifier(Node node, ParsingContext ctx) => throw new NotImplementedException();
    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx) => throw new NotImplementedException();
}
