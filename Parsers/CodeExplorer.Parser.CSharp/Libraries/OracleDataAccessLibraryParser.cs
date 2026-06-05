using CodeExplorer.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.CSharp.Libraries;

public class OracleDataAccessLibraryParser : ILibraryParser
{
    public string Name => "OracleDataAccessLibraryParser";
    public string LibraryType => "db:relational";
    public string LibraryName => "Oracle";
    public string LibraryId => "oracle";
    public IEnumerable<string> SupportedLibraries => ["Oracle.ManagedDataAccess"];

    public string? MapNodeType(Node node, ParsingContext ctx) => throw new NotImplementedException();
    public string? ExtractIdentifier(Node node, ParsingContext ctx) => throw new NotImplementedException();
    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx) => throw new NotImplementedException();
}
