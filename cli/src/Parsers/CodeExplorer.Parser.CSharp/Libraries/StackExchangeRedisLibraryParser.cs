using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.CSharp.Libraries;

public class StackExchangeRedisLibraryParser : ISemanticExtension
{
    public string Type => "db:keyvalue";
    public string Name => "Redis";
    public string Id => "redis";
    public IReadOnlyList<string> SupportedPatterns => ["StackExchange.Redis", "StackExchange.Redis.*"];
    public bool IsImplemented => true;

    private static readonly HashSet<string> RedisMethods = new(StringComparer.Ordinal)
    {
        "StringGet", "StringGetAsync", "StringSet", "StringSetAsync",
        "KeyDelete", "KeyDeleteAsync", "KeyExists", "KeyExistsAsync",
        "StringIncrement", "StringIncrementAsync", "StringDecrement", "StringDecrementAsync",
        "HashGet", "HashGetAsync", "HashSet", "HashSetAsync", "HashDelete", "HashDeleteAsync",
        "ListLeftPush", "ListLeftPushAsync", "ListRightPop", "ListRightPopAsync",
        "Publish", "PublishAsync"
    };

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (IsRedisCall(node, out _))
        {
            return OntologyConstants.NodeLabels.Query;
        }
        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx)
    {
        if (IsRedisCall(node, out var method))
        {
            return $"Redis: {method}";
        }
        return null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (IsRedisCall(node, out var method))
        {
            if (method is "Publish" or "PublishAsync")
            {
                var channel = AdoNetCommandHelper.ExtractSqlFromArguments(node);
                var target = !string.IsNullOrEmpty(channel) ? "redis:" + channel : "redis";
                references.Add(new Reference(scopeSymbolId, target, OntologyConstants.Relationships.PublishesTo));
            }
            else
            {
                references.Add(new Reference(scopeSymbolId, "redis", OntologyConstants.Relationships.UsesDb));
            }
        }
    }

    private static bool IsRedisCall(Node node, out string? methodName)
    {
        methodName = null;
        if (!node.Is(TreeSitterSyntax.CSharp.InvocationExpression)) return false;

        var func = node.GetFunctionNode();
        if (func.Is(TreeSitterSyntax.CSharp.MemberAccessExpression))
        {
            var name = func.GetChildFieldText(TreeSitterSyntax.Fields.Name);
            if (!string.IsNullOrEmpty(name) && RedisMethods.Contains(name))
            {
                methodName = name;
                return true;
            }
        }
        return false;
    }
}
