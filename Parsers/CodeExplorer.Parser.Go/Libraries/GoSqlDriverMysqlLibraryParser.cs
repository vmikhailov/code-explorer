using CodeExplorer.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.Go.Libraries;

public class GoSqlDriverMysqlLibraryParser : ILibraryParser
{
    public string Name => "GoSqlDriverMysqlLibraryParser";
    public string LibraryType => "db:relational";
    public string LibraryName => "MySQL";
    public string LibraryId => "mysql";
    public IEnumerable<string> SupportedLibraries => ["github.com/go-sql-driver/mysql"];

    public string? MapNodeType(Node node, ParsingContext ctx) => throw new NotImplementedException();
    public string? ExtractIdentifier(Node node, ParsingContext ctx) => throw new NotImplementedException();
    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx) => throw new NotImplementedException();
}
