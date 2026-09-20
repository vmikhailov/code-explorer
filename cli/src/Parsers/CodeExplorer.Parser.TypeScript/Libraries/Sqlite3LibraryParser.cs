using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public class Sqlite3LibraryParser : ILibraryParser
{
    public string Type => "db:relational";
    public string Name => "SQLite";
    public string Id => "sqlite";
    public IReadOnlyList<string> SupportedPatterns => ["sqlite3", "better-sqlite3", "@types/better-sqlite3"];
    public bool IsImplemented => true;

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (IsSqliteQuery(node))
        {
            return OntologyConstants.NodeLabels.Query;
        }
        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx)
    {
        if (IsSqliteQuery(node))
        {
            var sqlText = ExtractSqlArgument(node);
            if (!string.IsNullOrEmpty(sqlText))
            {
                var clean = NestedSqlParser.CleanQueryText(sqlText);
                if (NestedSqlParser.TryParseSql(sqlText, out var firstWord, out _))
                {
                    return $"{firstWord} Query: {clean}";
                }
                return $"SQLite Query: {clean}";
            }
            return "SQLite Query";
        }
        return null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (IsSqliteQuery(node))
        {
            var sqlText = ExtractSqlArgument(node);
            if (!string.IsNullOrEmpty(sqlText))
            {
                NestedSqlParser.TryDetectSqlDependencies(sqlText, scopeSymbolId, references);
            }
        }
    }

    private static bool IsSqliteQuery(Node node)
    {
        if (!node.Is(TreeSitterSyntax.TypeScript.CallExpression)) return false;

        if (AstHelper.TryGetMemberAccess(node, out _, out var propName))
        {
            return propName is "prepare" or "run" or "all" or "get" or "exec" or "each";
        }
        return false;
    }

    private static string? ExtractSqlArgument(Node node)
    {
        return AstHelper.ExtractFirstStringArgument(node);
    }
}
