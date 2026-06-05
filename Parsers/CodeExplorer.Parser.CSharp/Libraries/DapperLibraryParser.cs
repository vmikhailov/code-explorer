using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.CSharp.Libraries;

public class DapperLibraryParser : ILibraryParser
{
    public string Name => "DapperLibraryParser";

    public string LibraryType => "db:relational";

    public string LibraryName => "Dapper";

    public string LibraryId => "dapper";

    public System.Collections.Generic.IReadOnlyList<string> SupportedPatterns => ["Dapper"];

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
        if (node.Type != "invocation_expression") return false;

        var func = node.GetChildForField("function");
        if (func == null || (func.Id == IntPtr.Zero && node.Children.Count > 0)) func = node.Children[0];
        if (func == null || func.Id == IntPtr.Zero) return false;

        if (func.Type == "member_access_expression")
        {
            var nameChild = func.GetChildForField("name");
            if (nameChild != null && nameChild.Id != IntPtr.Zero)
            {
                var methodName = nameChild.Text;
                return methodName is "Query" or "QueryAsync" or "QueryFirst" or "QueryFirstOrDefault" 
                                   or "QuerySingle" or "QuerySingleOrDefault" or "QueryMultiple" or "QueryMultipleAsync" 
                                   or "Execute" or "ExecuteAsync" or "ExecuteReader" or "ExecuteScalar";
            }
        }
        return false;
    }

    private static string? ExtractSqlArgument(Node node)
    {
        var argList = node.Children.FirstOrDefault(c => c.Type == "argument_list");
        if (argList != null && argList.Children.Count > 1)
        {
            // First argument contains the SQL string
            var arg = argList.Children.FirstOrDefault(c => c.Type == "argument");
            if (arg != null)
            {
                var valNode = arg.Children.FirstOrDefault();
                if (valNode != null)
                {
                    return valNode.Text.Trim('"');
                }
            }
        }
        return null;
    }
}
