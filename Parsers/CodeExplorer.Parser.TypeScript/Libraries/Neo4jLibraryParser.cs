using CodeExplorer.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public class Neo4jLibraryParser : ILibraryParser
{
    public string Type => "db:graph";
    public string Name => "Neo4j";
    public string Id => "neo4j";
    public IReadOnlyList<string> SupportedPatterns => ["neo4j-driver"];

    public string? MapNodeType(Node node, ParsingContext ctx) => throw new NotImplementedException();
    public string? ExtractIdentifier(Node node, ParsingContext ctx) => throw new NotImplementedException();
    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx) => throw new NotImplementedException();
}
