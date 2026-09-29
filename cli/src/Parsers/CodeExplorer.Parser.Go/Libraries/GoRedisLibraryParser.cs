using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.Go.Libraries;

public class GoRedisLibraryParser : ISemanticExtension
{
    public string Type => "db:keyvalue";
    public string Name => "Redis (go-redis)";
    public string Id => "go-redis";
    public IReadOnlyList<string> SupportedPatterns => ["github.com/redis/go-redis/v9", "github.com/redis/go-redis"];
    public bool IsImplemented => true;

    private static readonly HashSet<string> RedisMethods = new(StringComparer.Ordinal)
    {
        "Get", "Set", "Del", "Exists", "Incr", "Decr",
        "HGet", "HSet", "HDel", "LPush", "RPop",
        "Publish", "Subscribe"
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
