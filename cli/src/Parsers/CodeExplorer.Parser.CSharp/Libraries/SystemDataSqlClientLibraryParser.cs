using CodeExplorer.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.CSharp.Libraries;

public class SystemDataSqlClientLibraryParser : ILibraryParser
{
    public string Type => "db:relational";
    public string Name => "SQL Server";
    public string Id => "mssql";
    public IReadOnlyList<string> SupportedPatterns => ["System.Data.SqlClient"];

    public string? MapNodeType(Node node, ParsingContext ctx) => throw new NotImplementedException();
    public string? ExtractIdentifier(Node node, ParsingContext ctx) => throw new NotImplementedException();
    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx) => throw new NotImplementedException();
}
