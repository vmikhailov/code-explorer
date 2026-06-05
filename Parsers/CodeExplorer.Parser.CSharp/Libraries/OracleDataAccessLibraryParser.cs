using CodeExplorer.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.CSharp.Libraries;

public class OracleDataAccessLibraryParser : ILibraryParser
{
    public string Type => "db:relational";
    public string Name => "Oracle";
    public string Id => "oracle";
    public IReadOnlyList<string> SupportedPatterns => ["Oracle.ManagedDataAccess"];

    public string? MapNodeType(Node node, ParsingContext ctx) => throw new NotImplementedException();
    public string? ExtractIdentifier(Node node, ParsingContext ctx) => throw new NotImplementedException();
    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx) => throw new NotImplementedException();
}
