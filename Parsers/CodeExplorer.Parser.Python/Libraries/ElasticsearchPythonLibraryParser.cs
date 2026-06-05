using CodeExplorer.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.Python.Libraries;

public class ElasticsearchPythonLibraryParser : ILibraryParser
{
    public string Name => "ElasticsearchPythonLibraryParser";
    public string LibraryType => "db:search";
    public string LibraryName => "Elasticsearch";
    public string LibraryId => "elasticsearch";
    public IEnumerable<string> SupportedLibraries => ["elasticsearch"];

    public string? MapNodeType(Node node, ParsingContext ctx) => throw new NotImplementedException();
    public string? ExtractIdentifier(Node node, ParsingContext ctx) => throw new NotImplementedException();
    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx) => throw new NotImplementedException();
}
