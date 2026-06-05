using CodeExplorer.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.Go.Libraries;

public class LibPqLibraryParser : ILibraryParser
{
    public string Name => "LibPqLibraryParser";
    public string LibraryType => "db:relational";
    public string LibraryName => "PostgreSQL";
    public string LibraryId => "postgres";
    public System.Collections.Generic.IReadOnlyList<string> SupportedPatterns => ["github.com/lib/pq"];

    public string? MapNodeType(Node node, ParsingContext ctx) => throw new NotImplementedException();
    public string? ExtractIdentifier(Node node, ParsingContext ctx) => throw new NotImplementedException();
    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx) => throw new NotImplementedException();
}
