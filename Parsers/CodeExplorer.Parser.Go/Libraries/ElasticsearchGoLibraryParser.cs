using CodeExplorer.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.Go.Libraries;

public class ElasticsearchGoLibraryParser : ILibraryParser
{
    public string Name => "ElasticsearchGoLibraryParser";
    public string LibraryType => "db:search";
    public string LibraryName => "Elasticsearch";
    public string LibraryId => "elasticsearch";
    public System.Collections.Generic.IReadOnlyList<string> SupportedPatterns => ["github.com/elastic/go-elasticsearch"];

    public string? MapNodeType(Node node, ParsingContext ctx) => throw new NotImplementedException();
    public string? ExtractIdentifier(Node node, ParsingContext ctx) => throw new NotImplementedException();
    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx) => throw new NotImplementedException();
}
