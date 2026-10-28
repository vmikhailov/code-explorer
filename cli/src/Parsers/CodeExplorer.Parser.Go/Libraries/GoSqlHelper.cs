using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.Go.Libraries;

public static class GoSqlHelper
{
    private static readonly HashSet<string> SqlMethods = new(StringComparer.Ordinal)
    {
        "Query", "QueryRow", "Exec",
        "QueryContext", "QueryRowContext", "ExecContext"
    };

    public static bool IsSqlCall(Node node, out string? sqlText, out string? methodName)
    {
        sqlText = null;
        methodName = null;
        if (!node.Is(TreeSitterSyntax.Go.CallExpression)) return false;

        var func = node.GetFunctionNode();
        if (!func.IsValid()) return false;

        var funcText = func.Text;
        foreach (var m in SqlMethods)
        {
            if (funcText.EndsWith("." + m, StringComparison.Ordinal) || funcText == m)
            {
                methodName = m;
                var args = GoAstHelper.GetCallArguments(node);
                for (var i = 0; i < Math.Min(args.Count, 3); i++)
                {
                    var candidate = ResolveSqlCandidate(args[i]);
                    if (!string.IsNullOrEmpty(candidate))
                    {
                        if (NestedSqlParser.TryParseSql(candidate, out _, out _))
                        {
                            sqlText = candidate;
                            return true;
                        }
                        if (sqlText == null && !candidate.Equals("ctx", StringComparison.OrdinalIgnoreCase) && !candidate.Equals("context.Background()", StringComparison.OrdinalIgnoreCase) && !candidate.Equals("context.TODO()", StringComparison.OrdinalIgnoreCase))
                        {
                            sqlText = candidate;
                        }
                    }
                }
                return true;
            }
        }
        return false;
    }

    private static string? ResolveSqlCandidate(Node node)
    {
        if (!node.IsValid()) return null;
        if (node.IsAny(TreeSitterSyntax.Go.InterpretedStringLiteral, TreeSitterSyntax.Go.RawStringLiteral, TreeSitterSyntax.Go.StringLiteral))
        {
            return node.Text.Trim('"', '`');
        }
        return GoAstHelper.ResolveStringOrVariable(node);
    }
}
