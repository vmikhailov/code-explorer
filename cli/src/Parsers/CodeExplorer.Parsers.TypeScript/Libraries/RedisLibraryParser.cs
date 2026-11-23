using System.Collections.Frozen;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parsers.TypeScript.Libraries;

public class RedisLibraryParser : DeclarativeSemanticExtension
{
    public RedisLibraryParser()
    {
        Type = SemanticExtensionType.KeyValueDb;
        Name = "Redis";
        Id = "redis";
        SupportedPatterns = ["redis", "ioredis"];
        LibraryRole = LibraryRole.OrmOrDatabase;
        CustomAstRules =
        [
            new CustomAstRule
            {
                NodeTypes = new[] { TreeSitterSyntax.TypeScript.CallExpression }.ToFrozenSet(),
                MapNodeType = static (node, ctx) => IsRedisCall(node) ? OntologyConstants.NodeLabels.Query : null,
                ExtractIdentifier = static (node, _, _, _) =>
                {
                    if (!IsRedisCall(node)) return null;
                    var func = node.GetFunctionNode();
                    if (func.IsValid() && func.Is(TreeSitterSyntax.TypeScript.MemberExpression))
                    {
                        var obj = func.GetField(TreeSitterSyntax.Fields.Object);
                        var prop = func.GetField(TreeSitterSyntax.Fields.Property);
                        if (obj.IsValid() && prop.IsValid())
                        {
                            return $"Redis: {obj.Text}.{prop.Text}";
                        }
                    }
                    return "Redis Command";
                }
            }
        ];
    }

    private static readonly FrozenSet<string> UnambiguousRedisMethods = new[]
    {
        "hget", "hset", "hdel", "hmget", "hmset", "hgetall",
        "sadd", "srem", "sismember", "smembers",
        "lpush", "rpush", "lpop", "rpop", "lrange",
        "zadd", "zrem", "zrange",
        "incr", "decr", "incrby", "decrby",
        "setex", "psetex", "setnx", "mget", "mset"
    }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> ReceiverScopedRedisMethods = new[]
    {
        "get", "set", "del", "exists", "expire", "ttl", "publish", "subscribe"
    }.ToFrozenSet(StringComparer.Ordinal);

    private const string ClientReceiverToken = "client";
    private const string ImportStatementType = "import_statement";
    private static readonly string[] RedisReceiverTokens = ["redis", "cache", "ioredis", "valkey", "kv"];
    private static readonly string[] RedisModuleTokens = ["'redis'", "\"redis\"", "'ioredis'", "\"ioredis\"", "/redis'", "/redis\""];

    private static bool IsRedisCall(Node node)
    {
        if (!node.Is(TreeSitterSyntax.TypeScript.CallExpression)) return false;

        var func = node.GetFunctionNode();
        if (!func.IsValid()) return false;

        if (func.Is(TreeSitterSyntax.TypeScript.MemberExpression))
        {
            var prop = func.GetField(TreeSitterSyntax.Fields.Property);
            if (prop.IsValid())
            {
                var propName = prop.Text;
                if (UnambiguousRedisMethods.Contains(propName))
                {
                    return true;
                }
                if (ReceiverScopedRedisMethods.Contains(propName))
                {
                    var obj = func.GetField(TreeSitterSyntax.Fields.Object);
                    if (!obj.IsValid()) return false;
                    var recvSpan = obj.AsSpan();
                    if (IsLikelyRedisReceiver(recvSpan)) return true;
                    if (recvSpan.Contains(ClientReceiverToken.AsSpan(), StringComparison.OrdinalIgnoreCase) &&
                        HasRedisImportInFile(node))
                    {
                        return true;
                    }
                }
            }
        }
        return false;
    }

    private static bool HasRedisImportInFile(Node node)
    {
        var root = node;
        while (root.Parent.IsValid())
        {
            root = root.Parent;
        }
        for (var i = 0; i < root.Children.Count; i++)
        {
            var child = root.Children[i];
            if (!child.Is(ImportStatementType)) continue;
            var span = child.AsSpan();
            for (var k = 0; k < RedisModuleTokens.Length; k++)
            {
                if (span.Contains(RedisModuleTokens[k].AsSpan(), StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }
        return false;
    }

    private static bool IsLikelyRedisReceiver(ReadOnlySpan<char> receiverSpan)
    {
        for (var i = 0; i < RedisReceiverTokens.Length; i++)
        {
            if (receiverSpan.Contains(RedisReceiverTokens[i].AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }
}
