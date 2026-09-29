using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

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
                var isContext = m.EndsWith("Context", StringComparison.Ordinal);
                var sqlArgIdx = isContext ? 1 : 0;
                if (args.Count > sqlArgIdx)
                {
                    sqlText = GoAstHelper.ResolveStringOrVariable(args[sqlArgIdx]);
                }
                return true;
            }
        }
        return false;
    }
}
