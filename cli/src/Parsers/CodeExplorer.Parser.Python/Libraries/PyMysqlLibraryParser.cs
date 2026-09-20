using CodeExplorer.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.Python.Libraries;

public class PyMysqlLibraryParser : ILibraryParser
{
    public string Type => "db:relational";
    public string Name => "MySQL";
    public string Id => "mysql";
    public IReadOnlyList<string> SupportedPatterns => ["pymysql"];

    public string? MapNodeType(Node node, ParsingContext ctx) => throw new NotImplementedException();
    public string? ExtractIdentifier(Node node, ParsingContext ctx) => throw new NotImplementedException();
    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx) => throw new NotImplementedException();
}
