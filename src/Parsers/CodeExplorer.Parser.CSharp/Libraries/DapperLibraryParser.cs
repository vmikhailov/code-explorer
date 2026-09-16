using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.CSharp.Libraries;

public class DapperLibraryParser : ILibraryParser
{
    public string Type => "db:relational";

    public string Name => "Dapper";

    public string Id => "dapper";

    public IReadOnlyList<string> SupportedPatterns => ["Dapper"];

    public bool IsImplemented => true;

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (IsDapperCall(node))
        {
            return OntologyConstants.NodeLabels.Query;
        }
        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx)
    {
        if (IsDapperCall(node))
        {
            var sqlText = ExtractSqlArgument(node);
            if (!string.IsNullOrEmpty(sqlText))
            {
                var clean = NestedSqlParser.CleanQueryText(sqlText);
                if (NestedSqlParser.TryParseSql(sqlText, out var firstWord, out _))
                {
                    return $"{firstWord} Query: {clean}";
                }
                return $"Dapper Query: {clean}";
            }
            return "Dapper Query";
        }
        return null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (IsDapperCall(node))
        {
            var sqlText = ExtractSqlArgument(node);
            if (!string.IsNullOrEmpty(sqlText))
            {
                NestedSqlParser.TryDetectSqlDependencies(sqlText, scopeSymbolId, references);
            }
        }
    }

    private static bool IsDapperCall(Node node)
    {
        if (!node.Is(TreeSitterSyntax.CSharp.InvocationExpression)) return false;

        var func = node.GetFunctionNode();
        if (func.Is(TreeSitterSyntax.CSharp.MemberAccessExpression))
        {
            var methodName = func.GetChildFieldText(TreeSitterSyntax.Fields.Name);
            if (!string.IsNullOrEmpty(methodName))
            {
                return methodName is "Query" or "QueryAsync" or "QueryFirst" or "QueryFirstOrDefault"
                                   or "QuerySingle" or "QuerySingleOrDefault" or "QueryMultiple" or "QueryMultipleAsync"
                                   or "Execute" or "ExecuteAsync" or "ExecuteReader" or "ExecuteScalar";
            }
        }
        return false;
    }

    private static string? ExtractSqlArgument(Node node)
    {
        var argList = node.FindChildOfType(TreeSitterSyntax.Common.ArgumentList);
        if (argList.IsValid() && argList.Children.Count > 1)
        {
            // First argument contains the SQL string
            var arg = argList.FindChildOfType(TreeSitterSyntax.CSharp.Argument);
            if (arg.IsValid())
            {
                var valNode = arg.Children.FirstOrDefault(c => c.IsValid());
                if (valNode.IsValid())
                {
                    return CSharpFileVisitor.ExtractFullStringText(valNode).Trim('"');
                }
            }
        }
        return null;
    }
}
