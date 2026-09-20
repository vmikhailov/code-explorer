using CodeExplorer.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.Python.Libraries;

public class SqlAlchemyLibraryParser : ILibraryParser
{
    public string Type => "db:relational";
    public string Name => "SQLAlchemy";
    public string Id => "sqlalchemy";
    public IReadOnlyList<string> SupportedPatterns => ["sqlalchemy"];

    public string? MapNodeType(Node node, ParsingContext ctx) => throw new NotImplementedException();
    public string? ExtractIdentifier(Node node, ParsingContext ctx) => throw new NotImplementedException();

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx) =>
        throw new NotImplementedException();
}
