using CodeExplorer.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.Python.Libraries;

public class PythonSqlite3LibraryParser : ILibraryParser
{
    public string Name => "PythonSqlite3LibraryParser";
    public string LibraryType => "db:relational";
    public string LibraryName => "SQLite";
    public string LibraryId => "sqlite";
    public bool Supports(string libraryName) => ILibraryParser.IsLibraryMatch(libraryName, "sqlite3");

    public string? MapNodeType(Node node, ParsingContext ctx) => throw new NotImplementedException();
    public string? ExtractIdentifier(Node node, ParsingContext ctx) => throw new NotImplementedException();
    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx) => throw new NotImplementedException();
}
