using CodeExplorer.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.CSharp.Libraries;

public class EfCoreLibraryParser : ILibraryParser
{
    public string Name => "EfCoreLibraryParser";
    public string LibraryType => "db:relational";
    public string LibraryName => "Microsoft.EntityFrameworkCore";
    public string LibraryId => "microsoft.entityframeworkcore";
    public bool Supports(string libraryName) => ILibraryParser.IsLibraryMatch(libraryName, "Microsoft.EntityFrameworkCore");

    public string? MapNodeType(Node node, ParsingContext ctx) => throw new NotImplementedException();
    public string? ExtractIdentifier(Node node, ParsingContext ctx) => throw new NotImplementedException();
    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx) => throw new NotImplementedException();
}
