using System.Text.RegularExpressions;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.Python.Libraries;

public static class DbApiHelper
{
    private static readonly HashSet<string> ExecMethods = new(StringComparer.Ordinal)
    {
        "execute", "executemany", "executescript", "execute_sql", "raw", "fetch", "fetchrow", "fetchval"
    };

    public static bool IsDbApiCall(Node node, out string? sqlText, out string? methodName)
    {
        sqlText = null;
        methodName = null;

        if (PythonAstHelper.TryGetMemberAccess(node, out _, out methodName))
        {
            if (methodName != null && ExecMethods.Contains(methodName))
            {
                var args = PythonAstHelper.GetCallArguments(node);
                if (args.Count > 0)
                {
                    sqlText = PythonAstHelper.ResolveSqlStringOrVariable(args[0]);
                }
                return true;
            }
        }
        return false;
    }

    public static string FormatQueryIdentifier(string providerPrefix, string? sqlText, string? methodName)
    {
        if (!string.IsNullOrEmpty(sqlText))
        {
            var clean = Regex.Replace(NestedSqlParser.CleanQueryText(sqlText), @"\s+", " ").Trim();
            if (clean.Length > 160)
            {
                clean = clean[..157] + "...";
            }
            if (NestedSqlParser.TryParseSql(sqlText, out var firstWord, out _))
            {
                return $"{firstWord} Query: {clean}";
            }
            return $"{providerPrefix}: {clean}";
        }
        return $"{providerPrefix}: {methodName ?? "execute"}";
    }

    public static void EnrichDbApiSymbol(Node node, SyntacticSymbol symbol)
    {
        if (IsDbApiCall(node, out var sqlText, out _) && !string.IsNullOrEmpty(sqlText))
        {
            symbol.Text = sqlText;
        }
    }
}
