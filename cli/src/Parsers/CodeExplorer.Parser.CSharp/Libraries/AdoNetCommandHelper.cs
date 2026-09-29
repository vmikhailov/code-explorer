using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.CSharp.Libraries;

public static class AdoNetCommandHelper
{
    private static readonly HashSet<string> ExecutionMethods = new(StringComparer.Ordinal)
    {
        "ExecuteReader", "ExecuteReaderAsync",
        "ExecuteNonQuery", "ExecuteNonQueryAsync",
        "ExecuteScalar", "ExecuteScalarAsync"
    };

    public static bool IsAdoNetCall(Node node, string commandTypeName, out string? sqlText, out string? methodName)
    {
        sqlText = null;
        methodName = null;

        // 1. new SqlCommand("SELECT ...", conn)
        if (node.Is(TreeSitterSyntax.CSharp.ObjectCreationExpression))
        {
            var typeNode = node.GetField(TreeSitterSyntax.Fields.Type);
            if (typeNode.IsValid() && IsMatchingType(typeNode.Text, commandTypeName))
            {
                methodName = commandTypeName;
                sqlText = ExtractSqlFromArguments(node);
                return true;
            }
        }

        // 2. cmd.CommandText = "SELECT ..."
        if (node.Is(TreeSitterSyntax.Common.AssignmentExpression))
        {
            var left = node.GetField(TreeSitterSyntax.Fields.Left);
            if (left.IsValid() && left.Is(TreeSitterSyntax.CSharp.MemberAccessExpression))
            {
                var prop = left.GetChildFieldText(TreeSitterSyntax.Fields.Name);
                if (prop == "CommandText")
                {
                    methodName = "CommandText";
                    var right = node.GetField(TreeSitterSyntax.Fields.Right);
                    if (right.IsValid())
                    {
                        sqlText = ResolveSqlString(right);
                        return true;
                    }
                }
            }
        }

        // 3. cmd.ExecuteReader(), etc.
        if (node.Is(TreeSitterSyntax.CSharp.InvocationExpression))
        {
            var func = node.GetFunctionNode();
            if (func.Is(TreeSitterSyntax.CSharp.MemberAccessExpression))
            {
                var name = func.GetChildFieldText(TreeSitterSyntax.Fields.Name);
                if (!string.IsNullOrEmpty(name) && ExecutionMethods.Contains(name))
                {
                    methodName = name;
                    sqlText = ExtractSqlFromArguments(node);
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsMatchingType(string actualType, string expectedType)
    {
        return actualType == expectedType || actualType.EndsWith("." + expectedType, StringComparison.Ordinal);
    }

    public static string? ExtractSqlFromArguments(Node node)
    {
        var argList = node.FindChildOfType(TreeSitterSyntax.Common.ArgumentList);
        if (argList.IsValid() && argList.Children.Count > 1)
        {
            var arg = argList.FindChildOfType(TreeSitterSyntax.CSharp.Argument);
            if (arg.IsValid())
            {
                var valNode = arg.GetField(TreeSitterSyntax.Fields.Expression)
                              ?? arg.Children.FirstOrDefault(c => c.IsValid() && c.Type != ",");
                if (valNode.IsValid())
                {
                    return ResolveSqlString(valNode);
                }
            }
        }
        return null;
    }

    public static string? ResolveSqlString(Node valNode)
    {
        if (AstValueResolver.TryResolveExpression(valNode, null, null, out var resolved) && !string.IsNullOrWhiteSpace(resolved))
        {
            return resolved;
        }

        if (ConstantRegistry.TryResolve(null, valNode.Text.Trim(), out var constVal) && !string.IsNullOrWhiteSpace(constVal))
        {
            return constVal;
        }

        if (valNode.Type.Contains("string") || valNode.Is(TreeSitterSyntax.CSharp.InterpolatedStringExpression))
        {
            var rawText = CSharpFileVisitor.ExtractFullStringText(valNode).Trim('"');
            if (!string.IsNullOrWhiteSpace(rawText))
            {
                return rawText;
            }
        }

        return null;
    }
}
