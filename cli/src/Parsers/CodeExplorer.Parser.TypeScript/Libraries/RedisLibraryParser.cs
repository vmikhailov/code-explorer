using System.Collections.Frozen;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.TypeScript.Libraries;

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
