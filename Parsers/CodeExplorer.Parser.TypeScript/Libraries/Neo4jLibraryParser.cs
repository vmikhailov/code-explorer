using CodeExplorer.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public class Neo4jLibraryParser : ILibraryParser
{
    public string Name => "Neo4jLibraryParser";
    public string LibraryType => "db:graph";
    public string LibraryName => "Neo4j";
    public string LibraryId => "neo4j";
    public bool Supports(string libraryName) => ILibraryParser.IsLibraryMatch(libraryName, "neo4j-driver");

    public string? MapNodeType(Node node, ParsingContext ctx) => throw new NotImplementedException();
    public string? ExtractIdentifier(Node node, ParsingContext ctx) => throw new NotImplementedException();
    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx) => throw new NotImplementedException();
}
