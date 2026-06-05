using CodeExplorer.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.CSharp.Libraries;

public class ElasticsearchNetLibraryParser : ILibraryParser
{
    public string Name => "ElasticsearchNetLibraryParser";
    public string LibraryType => "db:search";
    public string LibraryName => "Elasticsearch";
    public string LibraryId => "elasticsearch";
    public System.Collections.Generic.IReadOnlyList<string> SupportedPatterns => ["Elasticsearch.Net"];

    public string? MapNodeType(Node node, ParsingContext ctx) => throw new NotImplementedException();
    public string? ExtractIdentifier(Node node, ParsingContext ctx) => throw new NotImplementedException();
    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx) => throw new NotImplementedException();
}
