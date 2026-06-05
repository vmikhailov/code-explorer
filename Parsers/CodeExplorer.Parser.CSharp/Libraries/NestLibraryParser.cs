using CodeExplorer.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.CSharp.Libraries;

public class NestLibraryParser : ILibraryParser
{
    public string Name => "NestLibraryParser";
    public string LibraryType => "db:search";
    public string LibraryName => "Elasticsearch";
    public string LibraryId => "elasticsearch";
    public IEnumerable<string> SupportedLibraries => ["Nest"];

    public string? MapNodeType(Node node, ParsingContext ctx) => throw new NotImplementedException();
    public string? ExtractIdentifier(Node node, ParsingContext ctx) => throw new NotImplementedException();
    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx) => throw new NotImplementedException();
}
