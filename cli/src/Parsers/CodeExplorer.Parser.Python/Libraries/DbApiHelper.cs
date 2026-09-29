using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.Python.Libraries;

public static class DbApiHelper
{
    private static readonly HashSet<string> ExecMethods = new(StringComparer.Ordinal)
    {
        "execute", "executemany"
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
                    sqlText = PythonAstHelper.ResolveStringOrVariable(args[0]);
                }
                return true;
            }
        }
        return false;
    }
}
