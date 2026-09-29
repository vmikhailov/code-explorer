using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.Python.Libraries;

public class PythonRedisLibraryParser : ISemanticExtension
{
    public string Type => "db:keyvalue";
    public string Name => "Redis";
    public string Id => "redis";
    public IReadOnlyList<string> SupportedPatterns => ["redis", "redis.*", "aioredis", "aioredis.*"];
    public bool IsImplemented => true;

    private static readonly HashSet<string> RedisMethods = new(StringComparer.Ordinal)
    {
        "get", "set", "delete", "mget", "mset", "incr", "decr", "exists",
        "hget", "hset", "hdel", "hgetall",
        "lpush", "rpush", "lpop", "rpop",
        "publish", "subscribe"
    };

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (IsRedisCall(node, out _, out _))
        {
            return OntologyConstants.NodeLabels.Query;
        }
        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx)
    {
        if (IsRedisCall(node, out var method, out _))
        {
            return $"Redis: {method}";
        }
        return null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (IsRedisCall(node, out var method, out var target))
        {
            if (method == "publish")
            {
                var channel = !string.IsNullOrEmpty(target) ? "redis:" + target : "redis";
                references.Add(new Reference(scopeSymbolId, channel, OntologyConstants.Relationships.PublishesTo));
            }
            else if (method == "subscribe")
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

    private static bool IsRedisCall(Node node, out string? methodName, out string? targetInfo)
    {
        methodName = null;
        targetInfo = null;

        if (PythonAstHelper.TryGetMemberAccess(node, out _, out methodName))
        {
            if (methodName != null && RedisMethods.Contains(methodName))
            {
                var args = PythonAstHelper.GetCallArguments(node);
                if (args.Count > 0)
                {
                    targetInfo = PythonAstHelper.ResolveStringOrVariable(args[0]);
                }
                return true;
            }
        }

        return false;
    }
}
