using CodeExplorer.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.Python.Libraries;

public class SqlAlchemyLibraryParser : ILibraryParser
{
    public string Name => "SqlAlchemyLibraryParser";
    public string LibraryType => "db:relational";
    public string LibraryName => "SQLAlchemy";
    public string LibraryId => "sqlalchemy";
    public bool Supports(string libraryName) => ILibraryParser.IsLibraryMatch(libraryName, "sqlalchemy");

    public string? MapNodeType(Node node, ParsingContext ctx) => throw new NotImplementedException();
    public string? ExtractIdentifier(Node node, ParsingContext ctx) => throw new NotImplementedException();
    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx) => throw new NotImplementedException();
}
