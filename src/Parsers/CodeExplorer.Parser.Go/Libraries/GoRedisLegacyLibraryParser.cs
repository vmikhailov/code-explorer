using CodeExplorer.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.Go.Libraries;

public class GoRedisLegacyLibraryParser : ILibraryParser
{
    public string Type => "db:keyvalue";
    public string Name => "Redis";
    public string Id => "redis";
    public IReadOnlyList<string> SupportedPatterns => ["github.com/go-redis/redis"];

    public string? MapNodeType(Node node, ParsingContext ctx) => throw new NotImplementedException();
    public string? ExtractIdentifier(Node node, ParsingContext ctx) => throw new NotImplementedException();
    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx) => throw new NotImplementedException();
}
