using CodeExplorer.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public class MongodbLibraryParser : ILibraryParser
{
    public string Name => "MongodbLibraryParser";
    public string LibraryType => "db:document";
    public string LibraryName => "MongoDB";
    public string LibraryId => "mongodb";
    public IEnumerable<string> SupportedLibraries => ["mongodb"];

    public string? MapNodeType(Node node, ParsingContext ctx) => throw new NotImplementedException();
    public string? ExtractIdentifier(Node node, ParsingContext ctx) => throw new NotImplementedException();
    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx) => throw new NotImplementedException();
}
