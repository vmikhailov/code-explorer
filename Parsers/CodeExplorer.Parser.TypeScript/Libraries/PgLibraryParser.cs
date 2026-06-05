using CodeExplorer.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public class PgLibraryParser : ILibraryParser
{
    public string Name => "PgLibraryParser";
    public string LibraryType => "db:relational";
    public string LibraryName => "PostgreSQL";
    public string LibraryId => "postgres";
    public System.Collections.Generic.IReadOnlyList<string> SupportedPatterns => ["pg"];

    public string? MapNodeType(Node node, ParsingContext ctx) => throw new NotImplementedException();
    public string? ExtractIdentifier(Node node, ParsingContext ctx) => throw new NotImplementedException();
    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx) => throw new NotImplementedException();
}
