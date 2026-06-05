using CodeExplorer.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.CSharp.Libraries;

public class ElasticsearchNetLibraryParser : ILibraryParser
{
    public string Type => "db:search";
    public string Name => "Elasticsearch";
    public string Id => "elasticsearch";
    public IReadOnlyList<string> SupportedPatterns => ["Elasticsearch.Net"];

    public string? MapNodeType(Node node, ParsingContext ctx) => throw new NotImplementedException();
    public string? ExtractIdentifier(Node node, ParsingContext ctx) => throw new NotImplementedException();
    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx) => throw new NotImplementedException();
}
