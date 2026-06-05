using CodeExplorer.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.Python.Libraries;

public class PythonRedisLibraryParser : ILibraryParser
{
    public string Type => "db:keyvalue";
    public string Name => "Redis";
    public string Id => "redis";
    public IReadOnlyList<string> SupportedPatterns => ["redis"];

    public string? MapNodeType(Node node, ParsingContext ctx) => throw new NotImplementedException();
    public string? ExtractIdentifier(Node node, ParsingContext ctx) => throw new NotImplementedException();
    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx) => throw new NotImplementedException();
}
