using CodeExplorer.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public class SequelizeLibraryParser : ILibraryParser
{
    public string Name => "SequelizeLibraryParser";
    public string LibraryType => "db:relational";
    public string LibraryName => "Sequelize";
    public string LibraryId => "sequelize";
    public bool Supports(string libraryName) => ILibraryParser.IsLibraryMatch(libraryName, "sequelize");

    public string? MapNodeType(Node node, ParsingContext ctx) => throw new NotImplementedException();
    public string? ExtractIdentifier(Node node, ParsingContext ctx) => throw new NotImplementedException();
    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx) => throw new NotImplementedException();
}
