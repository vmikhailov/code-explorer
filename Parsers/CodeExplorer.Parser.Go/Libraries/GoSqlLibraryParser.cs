using CodeExplorer.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.Go.Libraries;

public class GoSqlLibraryParser : ILibraryParser
{
    public string Name => "GoSqlLibraryParser";
    public string LibraryType => "db:relational";
    public string LibraryName => "SQL";
    public string LibraryId => "sql";
    public IEnumerable<string> SupportedLibraries => ["database/sql"];

    public string? MapNodeType(Node node, ParsingContext ctx) => throw new NotImplementedException();
    public string? ExtractIdentifier(Node node, ParsingContext ctx) => throw new NotImplementedException();
    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx) => throw new NotImplementedException();
}
