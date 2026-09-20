using CodeExplorer.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.Go.Libraries;

public class GoSqlite3LibraryParser : ILibraryParser
{
    public string Type => "db:relational";
    public string Name => "SQLite";
    public string Id => "sqlite";
    public IReadOnlyList<string> SupportedPatterns => ["github.com/mattn/go-sqlite3"];

    public string? MapNodeType(Node node, ParsingContext ctx) => throw new NotImplementedException();
    public string? ExtractIdentifier(Node node, ParsingContext ctx) => throw new NotImplementedException();
    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx) => throw new NotImplementedException();
}
