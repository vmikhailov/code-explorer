using CodeExplorer.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public class Mysql2LibraryParser : ILibraryParser
{
    public string Name => "Mysql2LibraryParser";
    public string LibraryType => "db:relational";
    public string LibraryName => "MySQL";
    public string LibraryId => "mysql";
    public System.Collections.Generic.IReadOnlyList<string> SupportedPatterns => ["mysql2"];

    public string? MapNodeType(Node node, ParsingContext ctx) => throw new NotImplementedException();
    public string? ExtractIdentifier(Node node, ParsingContext ctx) => throw new NotImplementedException();
    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx) => throw new NotImplementedException();
}
