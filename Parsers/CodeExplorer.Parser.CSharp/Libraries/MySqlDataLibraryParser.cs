using CodeExplorer.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.CSharp.Libraries;

public class MySqlDataLibraryParser : ILibraryParser
{
    public string Name => "MySqlDataLibraryParser";
    public string LibraryType => "db:relational";
    public string LibraryName => "MySQL";
    public string LibraryId => "mysql";
    public bool Supports(string libraryName) => ILibraryParser.IsLibraryMatch(libraryName, "MySql.Data");

    public string? MapNodeType(Node node, ParsingContext ctx) => throw new NotImplementedException();
    public string? ExtractIdentifier(Node node, ParsingContext ctx) => throw new NotImplementedException();
    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx) => throw new NotImplementedException();
}
