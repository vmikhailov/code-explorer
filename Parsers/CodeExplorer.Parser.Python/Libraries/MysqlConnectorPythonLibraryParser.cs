using CodeExplorer.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.Python.Libraries;

public class MysqlConnectorPythonLibraryParser : ILibraryParser
{
    public string Name => "MysqlConnectorPythonLibraryParser";
    public string Category => "database";
    public IEnumerable<string> SupportedLibraries => ["mysql-connector-python"];

    public string? MapNodeType(Node node, ParsingContext ctx) => throw new NotImplementedException();
    public string? ExtractIdentifier(Node node, ParsingContext ctx) => throw new NotImplementedException();
    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx) => throw new NotImplementedException();
}
