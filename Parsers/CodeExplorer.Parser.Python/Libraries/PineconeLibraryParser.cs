using CodeExplorer.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.Python.Libraries;

public class PineconeLibraryParser : ILibraryParser
{
    public string Name => "PineconeLibraryParser";
    public string LibraryType => "db:vector";
    public string LibraryName => "Pinecone";
    public string LibraryId => "pinecone";
    public bool Supports(string libraryName) => ILibraryParser.IsLibraryMatch(libraryName, "pinecone-client");

    public string? MapNodeType(Node node, ParsingContext ctx) => throw new NotImplementedException();
    public string? ExtractIdentifier(Node node, ParsingContext ctx) => throw new NotImplementedException();
    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx) => throw new NotImplementedException();
}
