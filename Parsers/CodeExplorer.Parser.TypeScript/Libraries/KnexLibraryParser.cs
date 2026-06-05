using CodeExplorer.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public class KnexLibraryParser : ILibraryParser
{
    public string Name => "KnexLibraryParser";
    public string LibraryType => "db:relational";
    public string LibraryName => "Knex";
    public string LibraryId => "knex";
    public bool Supports(string libraryName) => ILibraryParser.IsLibraryMatch(libraryName, "knex");

    public string? MapNodeType(Node node, ParsingContext ctx) => throw new NotImplementedException();
    public string? ExtractIdentifier(Node node, ParsingContext ctx) => throw new NotImplementedException();
    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx) => throw new NotImplementedException();
}
