using CodeExplorer.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.Go.Libraries;

public class MongoGoLibraryParser : ILibraryParser
{
    public string Name => "MongoGoLibraryParser";
    public string LibraryType => "db:document";
    public string LibraryName => "MongoDB";
    public string LibraryId => "mongodb";
    public bool Supports(string libraryName) => ILibraryParser.IsLibraryMatch(libraryName, "go.mongodb.org/mongo-driver");

    public string? MapNodeType(Node node, ParsingContext ctx) => throw new NotImplementedException();
    public string? ExtractIdentifier(Node node, ParsingContext ctx) => throw new NotImplementedException();
    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx) => throw new NotImplementedException();
}
