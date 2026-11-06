using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.CSharp.Libraries;

public class StackExchangeRedisLibraryParser : ISemanticExtension
{
    public SemanticExtensionType Type => SemanticExtensionType.KeyValueDb;
    public string Name => "Redis";
    public string Id => "redis";
    public IReadOnlyList<string> SupportedPatterns =>
    [
        "StackExchange.Redis",
        "StackExchange.Redis.*",
        "Microsoft.Extensions.Caching.StackExchangeRedis",
        "Microsoft.Extensions.Caching.StackExchangeRedis.*",
        "Microsoft.Extensions.Caching.Distributed"
    ];
    public bool IsImplemented => true;
    public LibraryRole LibraryRole => LibraryRole.OrmOrDatabase;

    private static readonly HashSet<string> RedisMethods = new(StringComparer.Ordinal)
    {
        "StringGet", "StringGetAsync", "StringSet", "StringSetAsync",
        "KeyDelete", "KeyDeleteAsync", "KeyExists", "KeyExistsAsync",
        "StringIncrement", "StringIncrementAsync", "StringDecrement", "StringDecrementAsync",
        "HashGet", "HashGetAsync", "HashSet", "HashSetAsync", "HashDelete", "HashDeleteAsync",
        "ListLeftPush", "ListLeftPushAsync", "ListRightPop", "ListRightPopAsync",
        "Publish", "PublishAsync",
        "AddStackExchangeRedisCache",
        "Get", "GetAsync", "Set", "SetAsync", "Refresh", "RefreshAsync", "Remove", "RemoveAsync"
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
                if (name is "Get" or "GetAsync" or "Set" or "SetAsync" or "Refresh" or "RefreshAsync" or "Remove" or "RemoveAsync")
                {
                    var expr = func.GetChildFieldText(TreeSitterSyntax.Fields.Expression);
                    if (string.IsNullOrEmpty(expr) ||
                        (!expr.Contains("cache", StringComparison.OrdinalIgnoreCase) &&
                         !expr.Contains("redis", StringComparison.OrdinalIgnoreCase) &&
                         !expr.Contains("store", StringComparison.OrdinalIgnoreCase)))
                    {
                        return false;
                    }
                }
                methodName = name;
                return true;
            }
        }
        return false;
    }
}
