using System;
using System.Collections.Generic;
using System.Linq;
using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public class RedisLibraryParser : ILibraryParser
{
    public string Name => "RedisLibraryParser";

    public string Category => "database";

    public IEnumerable<string> SupportedLibraries => new[] { "redis", "ioredis" };

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
            var func = node.GetChildForField("function");
            if (func == null || (func.Id == IntPtr.Zero && node.Children.Count > 0)) func = node.Children[0];
            if (func != null && func.Type == "member_expression")
            {
                var obj = func.GetChildForField("object");
                var prop = func.GetChildForField("property");
                if (obj != null && prop != null)
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
        if (node.Type != "call_expression") return false;

        var func = node.GetChildForField("function");
        if (func == null || (func.Id == IntPtr.Zero && node.Children.Count > 0)) func = node.Children[0];
        if (func == null || func.Id == IntPtr.Zero) return false;

        if (func.Type == "member_expression")
        {
            var prop = func.GetChildForField("property");
            if (prop != null && prop.Id != IntPtr.Zero)
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
