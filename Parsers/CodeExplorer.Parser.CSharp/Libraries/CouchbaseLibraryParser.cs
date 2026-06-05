using CodeExplorer.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.CSharp.Libraries;

public class CouchbaseLibraryParser : ILibraryParser
{
    public string Name => "CouchbaseLibraryParser";
    public string LibraryType => "db:document";
    public string LibraryName => "Couchbase";
    public string LibraryId => "couchbase";
    public IEnumerable<string> SupportedLibraries => ["Couchbase"];

    public string? MapNodeType(Node node, ParsingContext ctx) => throw new NotImplementedException();
    public string? ExtractIdentifier(Node node, ParsingContext ctx) => throw new NotImplementedException();
    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx) => throw new NotImplementedException();
}
