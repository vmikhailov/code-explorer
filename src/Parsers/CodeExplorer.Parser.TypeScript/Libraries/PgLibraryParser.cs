using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public class PgLibraryParser : ILibraryParser
{
    public string Type => "db:relational";
    public string Name => "PostgreSQL";
    public string Id => "postgres";
    public IReadOnlyList<string> SupportedPatterns => ["pg", "pg-promise", "@types/pg"];
    public bool IsImplemented => true;

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (IsPgQuery(node))
        {
            return OntologyConstants.NodeLabels.Query;
        }
        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx)
    {
        if (IsPgQuery(node))
        {
            var sqlText = ExtractSqlArgument(node);
            if (!string.IsNullOrEmpty(sqlText))
            {
                var clean = NestedSqlParser.CleanQueryText(sqlText);
                if (NestedSqlParser.TryParseSql(sqlText, out var firstWord, out _))
                {
                    return $"{firstWord} Query: {clean}";
                }
                return $"PostgreSQL Query: {clean}";
            }
            return "PostgreSQL Query";
        }
        return null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (IsPgQuery(node))
        {
            var sqlText = ExtractSqlArgument(node);
            if (!string.IsNullOrEmpty(sqlText))
            {
                NestedSqlParser.TryDetectSqlDependencies(sqlText, scopeSymbolId, references);
            }
        }
    }

    private static bool IsPgQuery(Node node)
    {
        if (!node.Is(TreeSitterSyntax.TypeScript.CallExpression)) return false;

        if (AstHelper.TryGetMemberAccess(node, out _, out var propName))
        {
            return propName is "query" or "any" or "one" or "oneOrNone" or "many" or "none" or "multi";
        }
        return false;
    }

    private static string? ExtractSqlArgument(Node node)
    {
        return AstHelper.ExtractFirstStringArgument(node);
    }
}
