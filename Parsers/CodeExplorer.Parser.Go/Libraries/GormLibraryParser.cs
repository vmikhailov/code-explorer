using CodeExplorer.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.Go.Libraries;

public class GormLibraryParser : ILibraryParser
{
    public string Name => "GormLibraryParser";
    public string LibraryType => "db:relational";
    public string LibraryName => "GORM";
    public string LibraryId => "gorm";
    public bool Supports(string libraryName) => ILibraryParser.IsLibraryMatch(libraryName, "gorm.io/gorm");

    public string? MapNodeType(Node node, ParsingContext ctx) => throw new NotImplementedException();
    public string? ExtractIdentifier(Node node, ParsingContext ctx) => throw new NotImplementedException();
    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx) => throw new NotImplementedException();
}
