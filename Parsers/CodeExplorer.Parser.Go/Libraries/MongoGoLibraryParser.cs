using CodeExplorer.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.Go.Libraries;

public class MongoGoLibraryParser : ILibraryParser
{
    public string Type => "db:document";
    public string Name => "MongoDB";
    public string Id => "mongodb";
    public IReadOnlyList<string> SupportedPatterns => ["go.mongodb.org/mongo-driver"];

    public string? MapNodeType(Node node, ParsingContext ctx) => throw new NotImplementedException();
    public string? ExtractIdentifier(Node node, ParsingContext ctx) => throw new NotImplementedException();
    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx) => throw new NotImplementedException();
}
