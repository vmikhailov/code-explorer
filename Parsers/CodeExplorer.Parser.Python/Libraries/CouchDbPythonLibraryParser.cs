using CodeExplorer.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.Python.Libraries;

public class CouchDbPythonLibraryParser : ILibraryParser
{
    public string Name => "CouchDbPythonLibraryParser";
    public string LibraryType => "db:document";
    public string LibraryName => "CouchDB";
    public string LibraryId => "couchdb";
    public bool Supports(string libraryName) => ILibraryParser.IsLibraryMatch(libraryName, "couchdb");

    public string? MapNodeType(Node node, ParsingContext ctx) => throw new NotImplementedException();
    public string? ExtractIdentifier(Node node, ParsingContext ctx) => throw new NotImplementedException();
    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx) => throw new NotImplementedException();
}
