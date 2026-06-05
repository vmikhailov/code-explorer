using CodeExplorer.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.CSharp.Libraries;

public class SystemDataSqlClientLibraryParser : ILibraryParser
{
    public string Name => "SystemDataSqlClientLibraryParser";
    public string LibraryType => "db:relational";
    public string LibraryName => "SQL Server";
    public string LibraryId => "mssql";
    public bool Supports(string libraryName) => ILibraryParser.IsLibraryMatch(libraryName, "System.Data.SqlClient");

    public string? MapNodeType(Node node, ParsingContext ctx) => throw new NotImplementedException();
    public string? ExtractIdentifier(Node node, ParsingContext ctx) => throw new NotImplementedException();
    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx) => throw new NotImplementedException();
}
