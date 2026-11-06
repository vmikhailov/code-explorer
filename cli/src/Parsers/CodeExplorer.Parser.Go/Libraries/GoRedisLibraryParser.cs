using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.Go.Libraries;

public class GoRedisLibraryParser : ISemanticExtension
{
    public SemanticExtensionType Type => SemanticExtensionType.KeyValueDb;
    public string Name => "Redis (go-redis)";
    public string Id => "go-redis";
    public IReadOnlyList<string> SupportedPatterns =>
    [
        "github.com/redis/go-redis/v9*",
        "github.com/redis/go-redis*",
        "github.com/go-redis/cache/v9*",
        "github.com/go-redis/cache*",
        "github.com/gomodule/redigo*",
        "github.com/garyburd/redigo*"
    ];
    public bool IsImplemented => true;
    public LibraryRole LibraryRole => LibraryRole.OrmOrDatabase;
    private static readonly char[] QuoteSeparators = ['"', '`'];

    private static readonly HashSet<string> RedisConstructorMethods = new(StringComparer.Ordinal)
    {
        "NewClient", "NewFailoverClient", "NewClusterClient", "NewUniversalClient", "NewRing"
    };

    private static readonly HashSet<string> RedisMethods = new(StringComparer.Ordinal)
    {
        "Get", "Set", "SetNX", "SetEX", "Del", "Delete", "Exists", "Incr", "IncrBy", "Decr", "DecrBy",
        "Expire", "ExpireAt", "TTL", "Persist", "Type", "Rename", "RenameNX",
        "HGet", "HSet", "HDel", "HExists", "HGetAll", "HKeys", "HVals", "HIncrBy", "HMGet", "HMSet",
        "LPush", "RPush", "LPop", "RPop", "LRange", "LLen", "LIndex", "LRem",
        "SAdd", "SRem", "SMembers", "SIsMember", "SCard", "SPop",
        "ZAdd", "ZRem", "ZRange", "ZRevRange", "ZRangeByScore", "ZCard", "ZScore", "ZIncrBy",
        "Publish", "Subscribe", "PSubscribe",
        "Ping", "Do", "Process", "Pipeline", "TxPipeline", "Pipelined", "TxPipelined", "Watch", "Once",
        "FlushDB", "FlushAll", "MGet", "MSet", "Keys", "Scan",
        "NewClient", "NewFailoverClient", "NewClusterClient", "NewUniversalClient", "NewRing"
    };

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (IsRedisCall(node, out var method, out _) && !RedisConstructorMethods.Contains(method!))
        {
            return OntologyConstants.NodeLabels.Query;
        }
        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx)
    {
        if (IsRedisCall(node, out var method, out var target) && !RedisConstructorMethods.Contains(method!))
        {
            if (method == "Do" && !string.IsNullOrEmpty(target))
            {
                return $"Redis: {target}";
            }
            return $"Redis: {method}";
        }
        return null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (IsRedisCall(node, out var method, out var target))
        {
            if (method == "Publish")
            {
                var channel = !string.IsNullOrEmpty(target) ? "redis:" + target : "redis";
                references.Add(new Reference(scopeSymbolId, channel, OntologyConstants.Relationships.PublishesTo));
            }
            else if (method is "Subscribe" or "PSubscribe")
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

    public static bool IsRedisCall(Node node, out string? methodName, out string? targetInfo)
    {
        methodName = null;
        targetInfo = null;

        if (!node.Is(TreeSitterSyntax.Go.CallExpression)) return false;

        var func = node.GetFunctionNode();
        if (!func.IsValid()) return false;

        var funcText = func.Text;
        foreach (var m in RedisMethods)
        {
            if (funcText.EndsWith("." + m, StringComparison.Ordinal) || funcText == m)
            {
                methodName = m;
                var args = GoAstHelper.GetCallArguments(node);
                if (m == "Do" && args.Count > 0)
                {
                    targetInfo = GoAstHelper.ResolveStringOrVariable(args[0])?.Trim(QuoteSeparators);
                    return true;
                }
                if (args.Count > 1)
                {
                    targetInfo = GoAstHelper.ResolveStringOrVariable(args[1]);
                }
                else if (args.Count > 0)
                {
                    targetInfo = GoAstHelper.ResolveStringOrVariable(args[0]);
                }
                return true;
            }
        }

        return false;
    }
}
