using CodeExplorer.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.Python.Libraries;

public class PeeweeLibraryParser : ILibraryParser
{
    public string Name => "PeeweeLibraryParser";
    public string LibraryType => "db:relational";
    public string LibraryName => "Peewee";
    public string LibraryId => "peewee";
    public bool Supports(string libraryName) => ILibraryParser.IsLibraryMatch(libraryName, "peewee");

    public string? MapNodeType(Node node, ParsingContext ctx) => throw new NotImplementedException();
    public string? ExtractIdentifier(Node node, ParsingContext ctx) => throw new NotImplementedException();
    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx) => throw new NotImplementedException();
}
