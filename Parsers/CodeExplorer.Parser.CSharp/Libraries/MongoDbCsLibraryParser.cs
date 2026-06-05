using CodeExplorer.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.CSharp.Libraries;

public class MongoDbCsLibraryParser : ILibraryParser
{
    public string Name => "MongoDbCsLibraryParser";
    public string LibraryType => "db:document";
    public string LibraryName => "MongoDB";
    public string LibraryId => "mongodb";
    public bool Supports(string libraryName) => ILibraryParser.IsLibraryMatch(libraryName, "MongoDB.Driver");

    public string? MapNodeType(Node node, ParsingContext ctx) => throw new NotImplementedException();
    public string? ExtractIdentifier(Node node, ParsingContext ctx) => throw new NotImplementedException();
    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx) => throw new NotImplementedException();
}
