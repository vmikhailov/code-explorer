using System.Collections.Frozen;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.Go.Libraries;

public class GoRedisLegacyLibraryParser : DeclarativeSemanticExtension
{
    private static readonly HashSet<string> RedisConstructorMethods = new(StringComparer.Ordinal)
    {
        "NewClient", "NewFailoverClient", "NewClusterClient", "NewUniversalClient", "NewRing"
    };

    public GoRedisLegacyLibraryParser()
    {
        Type = SemanticExtensionType.KeyValueDb;
        Name = "Redis (go-redis v8)";
        Id = "go-redis-v8";
        SupportedPatterns = ["github.com/go-redis/redis/v8*", "github.com/go-redis/redis*", "gopkg.in/redis*"];
        LibraryRole = LibraryRole.OrmOrDatabase;
        CustomAstRules =
        [
            new CustomAstRule
            {
                NodeTypes = new[] { TreeSitterSyntax.Go.CallExpression }.ToFrozenSet(),
                MapNodeType = static (node, ctx) => (GoRedisLibraryParser.IsRedisCall(node, out var method, out _) && !RedisConstructorMethods.Contains(method!)) ? OntologyConstants.NodeLabels.Query : null,
                ExtractIdentifier = static (node, _, _, _) => (GoRedisLibraryParser.IsRedisCall(node, out var method, out _) && !RedisConstructorMethods.Contains(method!)) ? $"Redis: {method}" : null,
                CollectReferences = CollectRedisLegacyReferences
            }
        ];
    }

    private const string RedisDbId = "redis";

    private static void CollectRedisLegacyReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (GoRedisLibraryParser.IsRedisCall(node, out var method, out var target))
        {
            if (method == "Publish")
            {
                var channel = !string.IsNullOrEmpty(target) ? "redis:" + target : RedisDbId;
                references.Add(new Reference(scopeSymbolId, channel, OntologyConstants.Relationships.PublishesTo));
            }
            else if (method is "Subscribe" or "PSubscribe")
            {
                var channel = !string.IsNullOrEmpty(target) ? "redis:" + target : RedisDbId;
                references.Add(new Reference(scopeSymbolId, channel, OntologyConstants.Relationships.SubscribesTo));
            }
            else
            {
                references.Add(new Reference(scopeSymbolId, RedisDbId, OntologyConstants.Relationships.UsesDb));
            }
        }
    }
}
