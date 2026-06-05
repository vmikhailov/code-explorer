using CodeExplorer.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.Python.Libraries;

public class ElasticsearchPythonLibraryParser : ILibraryParser
{
    public string Type => "db:search";
    public string Name => "Elasticsearch";
    public string Id => "elasticsearch";
    public IReadOnlyList<string> SupportedPatterns => ["elasticsearch"];

    public string? MapNodeType(Node node, ParsingContext ctx) => throw new NotImplementedException();
    public string? ExtractIdentifier(Node node, ParsingContext ctx) => throw new NotImplementedException();
    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx) => throw new NotImplementedException();
}
