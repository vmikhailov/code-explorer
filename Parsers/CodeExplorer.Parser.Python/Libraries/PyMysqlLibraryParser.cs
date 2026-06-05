using CodeExplorer.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.Python.Libraries;

public class PyMysqlLibraryParser : ILibraryParser
{
    public string Name => "PyMysqlLibraryParser";
    public string LibraryType => "db:relational";
    public string LibraryName => "MySQL";
    public string LibraryId => "mysql";
    public bool Supports(string libraryName) => ILibraryParser.IsLibraryMatch(libraryName, "pymysql");

    public string? MapNodeType(Node node, ParsingContext ctx) => throw new NotImplementedException();
    public string? ExtractIdentifier(Node node, ParsingContext ctx) => throw new NotImplementedException();
    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx) => throw new NotImplementedException();
}
