using CodeExplorer.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.Go.Libraries;

public class GoRedisLegacyLibraryParser : ILibraryParser
{
    public string Name => "GoRedisLegacyLibraryParser";
    public string Category => "database";
    public IEnumerable<string> SupportedLibraries => ["github.com/go-redis/redis"];

    public string? MapNodeType(Node node, ParsingContext ctx) => throw new NotImplementedException();
    public string? ExtractIdentifier(Node node, ParsingContext ctx) => throw new NotImplementedException();
    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx) => throw new NotImplementedException();
}
