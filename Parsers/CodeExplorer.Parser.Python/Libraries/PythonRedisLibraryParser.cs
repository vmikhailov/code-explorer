using CodeExplorer.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.Python.Libraries;

public class PythonRedisLibraryParser : ILibraryParser
{
    public string Name => "PythonRedisLibraryParser";
    public string LibraryType => "db:keyvalue";
    public string LibraryName => "Redis";
    public string LibraryId => "redis";
    public bool Supports(string libraryName) => ILibraryParser.IsLibraryMatch(libraryName, "redis");

    public string? MapNodeType(Node node, ParsingContext ctx) => throw new NotImplementedException();
    public string? ExtractIdentifier(Node node, ParsingContext ctx) => throw new NotImplementedException();
    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx) => throw new NotImplementedException();
}
