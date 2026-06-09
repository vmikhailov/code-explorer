using CodeExplorer.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.Python.Libraries;

public class CouchDbPythonLibraryParser : ILibraryParser
{
    public string Type => "db:document";
    public string Name => "CouchDB";
    public string Id => "couchdb";
    public IReadOnlyList<string> SupportedPatterns => ["couchdb"];

    public string? MapNodeType(Node node, ParsingContext ctx) => throw new NotImplementedException();
    public string? ExtractIdentifier(Node node, ParsingContext ctx) => throw new NotImplementedException();
    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx) => throw new NotImplementedException();
}
