using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.CSharp.Libraries;

public class SystemDataSqlClientLibraryParser : ISemanticExtension
{
    public string Type => "db:relational";
    public string Name => "SQL Server (Legacy)";
    public string Id => "mssql-legacy";
    public IReadOnlyList<string> SupportedPatterns => ["System.Data.SqlClient", "System.Data.SqlClient.*"];
    public bool IsImplemented => true;

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (AdoNetCommandHelper.IsAdoNetCall(node, "SqlCommand", out _, out _))
        {
            return OntologyConstants.NodeLabels.Query;
        }
        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx)
    {
        if (AdoNetCommandHelper.IsAdoNetCall(node, "SqlCommand", out var sqlText, out var method))
        {
            if (!string.IsNullOrEmpty(sqlText))
            {
                var clean = NestedSqlParser.CleanQueryText(sqlText);
                if (NestedSqlParser.TryParseSql(sqlText, out var firstWord, out _))
                {
                    return $"{firstWord} Query: {clean}";
                }
                return $"SQL Server: {clean}";
            }
            return $"SQL Server: {method ?? "Command"}";
        }
        return null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (AdoNetCommandHelper.IsAdoNetCall(node, "SqlCommand", out var sqlText, out _))
        {
            if (!string.IsNullOrEmpty(sqlText))
            {
                NestedSqlParser.TryDetectSqlDependencies(sqlText, scopeSymbolId, references);
            }
        }
    }
}
