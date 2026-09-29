using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.Go.Libraries;

public class GoRedisLegacyLibraryParser : ISemanticExtension
{
    public string Type => "db:keyvalue";
    public string Name => "Redis (go-redis v8)";
    public string Id => "go-redis-v8";
    public IReadOnlyList<string> SupportedPatterns => ["github.com/go-redis/redis/v8", "github.com/go-redis/redis"];
    public bool IsImplemented => true;

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (GoRedisLibraryParser.IsRedisCall(node, out _, out _))
        {
            return OntologyConstants.NodeLabels.Query;
        }
        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx)
    {
        if (GoRedisLibraryParser.IsRedisCall(node, out var method, out _))
        {
            return $"Redis: {method}";
        }
        return null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (GoRedisLibraryParser.IsRedisCall(node, out var method, out var target))
        {
            if (method == "Publish")
            {
                var channel = !string.IsNullOrEmpty(target) ? "redis:" + target : "redis";
                references.Add(new Reference(scopeSymbolId, channel, OntologyConstants.Relationships.PublishesTo));
            }
            else if (method == "Subscribe")
            {
                var channel = !string.IsNullOrEmpty(target) ? "redis:" + target : "redis";
                references.Add(new Reference(scopeSymbolId, channel, OntologyConstants.Relationships.SubscribesTo));
            }
            else
            {
                references.Add(new Reference(scopeSymbolId, "redis", OntologyConstants.Relationships.UsesDb));
            }
        }
    }
}
