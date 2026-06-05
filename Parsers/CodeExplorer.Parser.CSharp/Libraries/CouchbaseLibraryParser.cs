using CodeExplorer.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.CSharp.Libraries;

public class CouchbaseLibraryParser : ILibraryParser
{
    public string Type => "db:document";
    public string Name => "Couchbase";
    public string Id => "couchbase";
    public IReadOnlyList<string> SupportedPatterns => ["Couchbase"];

    public string? MapNodeType(Node node, ParsingContext ctx) => throw new NotImplementedException();
    public string? ExtractIdentifier(Node node, ParsingContext ctx) => throw new NotImplementedException();
    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx) => throw new NotImplementedException();
}
