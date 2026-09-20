using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.Java.Libraries;

public class JdbcTemplateLibraryParser : ILibraryParser
{
    public string Type => "db:relational";
    public string Name => "Spring JdbcTemplate / JDBC";
    public string Id => "jdbc";

    public IReadOnlyList<string> SupportedPatterns =>
    [
        "org.springframework.jdbc",
        "java.sql",
        "javax.sql"
    ];

    public bool IsImplemented => true;

    private static readonly HashSet<string> JdbcMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "query", "queryForObject", "queryForList", "queryForRowSet",
        "update", "batchUpdate", "execute", "prepareStatement", "prepareCall"
    };

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (IsJdbcCall(node))
        {
            return OntologyConstants.NodeLabels.Query;
        }
        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx)
    {
        if (IsJdbcCall(node))
        {
            var sqlText = ExtractSqlText(node);
            if (!string.IsNullOrEmpty(sqlText))
            {
                var clean = NestedSqlParser.CleanQueryText(sqlText);
                if (NestedSqlParser.TryParseSql(sqlText, out var firstWord, out _))
                {
                    return $"{firstWord} Query: {clean}";
                }
                return $"JDBC Query: {clean}";
            }
            return "JDBC Query";
        }
        return null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (IsJdbcCall(node))
        {
            var sqlText = ExtractSqlText(node);
            if (!string.IsNullOrEmpty(sqlText))
            {
                NestedSqlParser.TryDetectSqlDependencies(sqlText, scopeSymbolId, references);
            }
        }
    }

    public static bool IsJdbcCall(Node node)
    {
        if (!node.Is(TreeSitterSyntax.Java.MethodInvocation)) return false;

        var name = node.GetChildFieldText(TreeSitterSyntax.Fields.Name);
        if (!string.IsNullOrEmpty(name) && JdbcMethods.Contains(name))
        {
            var sql = ExtractSqlText(node);
            return !string.IsNullOrEmpty(sql);
        }

        return false;
    }

    public static string? ExtractSqlText(Node node)
    {
        var argList = node.FindChildOfType(TreeSitterSyntax.Java.ArgumentList);
        if (argList.IsValid() && argList.Children.Count > 0)
        {
            var firstArg = argList.Children.FirstOrDefault(c => c.IsAny(TreeSitterSyntax.Java.StringLiteral, TreeSitterSyntax.Java.TextBlock));
            if (firstArg.IsValid())
            {
                return firstArg.Text.Trim('"').Trim();
            }
        }
        return null;
    }
}
