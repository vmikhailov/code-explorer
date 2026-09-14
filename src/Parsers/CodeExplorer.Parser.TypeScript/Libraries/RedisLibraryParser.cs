using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public class RedisLibraryParser : ILibraryParser
{
    public string Type => "db:keyvalue";

    public string Name => "Redis";

    public string Id => "redis";

    public IReadOnlyList<string> SupportedPatterns => ["redis", "ioredis"];

    public bool IsImplemented => true;

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (IsRedisCall(node))
        {
            return OntologyConstants.NodeLabels.Query;
        }
        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx)
    {
        if (IsRedisCall(node))
        {
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
        return null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        // Redis commands represent database queries
    }

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
                return propName is "get" or "set" or "del" or "exists" or "incr" or "decr"
                                   or "hget" or "hset" or "hdel" or "sadd" or "srem" or "sismember"
                                   or "lpush" or "rpush" or "lpop" or "rpop" or "publish" or "subscribe";
            }
        }
        return false;
    }
}
