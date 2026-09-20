using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public class Mysql2LibraryParser : ILibraryParser
{
    public string Type => "db:relational";
    public string Name => "MySQL";
    public string Id => "mysql";
    public IReadOnlyList<string> SupportedPatterns => ["mysql2", "mysql", "@types/mysql", "@types/mysql2"];
    public bool IsImplemented => true;

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (IsMysqlQuery(node))
        {
            return OntologyConstants.NodeLabels.Query;
        }
        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx)
    {
        if (IsMysqlQuery(node))
        {
            var sqlText = ExtractSqlArgument(node);
            if (!string.IsNullOrEmpty(sqlText))
            {
                var clean = NestedSqlParser.CleanQueryText(sqlText);
                if (NestedSqlParser.TryParseSql(sqlText, out var firstWord, out _))
                {
                    return $"{firstWord} Query: {clean}";
                }
                return $"MySQL Query: {clean}";
            }
            return "MySQL Query";
        }
        return null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (IsMysqlQuery(node))
        {
            var sqlText = ExtractSqlArgument(node);
            if (!string.IsNullOrEmpty(sqlText))
            {
                NestedSqlParser.TryDetectSqlDependencies(sqlText, scopeSymbolId, references);
            }
        }
    }

    private static bool IsMysqlQuery(Node node)
    {
        if (!node.Is(TreeSitterSyntax.TypeScript.CallExpression)) return false;

        if (AstHelper.TryGetMemberAccess(node, out _, out var propName))
        {
            return propName is "query" or "execute";
        }
        return false;
    }

    private static string? ExtractSqlArgument(Node node)
    {
        return AstHelper.ExtractFirstStringArgument(node);
    }
}
