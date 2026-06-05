using CodeExplorer.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.CSharp.Libraries;

public class StackExchangeRedisLibraryParser : ILibraryParser
{
    public string Name => "StackExchangeRedisLibraryParser";
    public string LibraryType => "db:keyvalue";
    public string LibraryName => "Redis";
    public string LibraryId => "redis";
    public bool Supports(string libraryName) => ILibraryParser.IsLibraryMatch(libraryName, "StackExchange.Redis");

    public string? MapNodeType(Node node, ParsingContext ctx) => throw new NotImplementedException();
    public string? ExtractIdentifier(Node node, ParsingContext ctx) => throw new NotImplementedException();
    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx) => throw new NotImplementedException();
}
