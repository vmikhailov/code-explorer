using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.CSharp.Libraries;

public class OracleDataAccessLibraryParser : ISemanticExtension
{
    public string Type => "db:relational";
    public string Name => "Oracle";
    public string Id => "oracle";
    public IReadOnlyList<string> SupportedPatterns => ["Oracle.ManagedDataAccess", "Oracle.ManagedDataAccess.*", "Oracle.DataAccess", "Oracle.DataAccess.*"];
    public bool IsImplemented => true;

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (AdoNetCommandHelper.IsAdoNetCall(node, "OracleCommand", out _, out _))
        {
            return OntologyConstants.NodeLabels.Query;
        }
        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx)
    {
        if (AdoNetCommandHelper.IsAdoNetCall(node, "OracleCommand", out var sqlText, out var method))
        {
            if (!string.IsNullOrEmpty(sqlText))
            {
                var clean = NestedSqlParser.CleanQueryText(sqlText);
                if (NestedSqlParser.TryParseSql(sqlText, out var firstWord, out _))
                {
                    return $"{firstWord} Query: {clean}";
                }
                return $"Oracle: {clean}";
            }
            return $"Oracle: {method ?? "Command"}";
        }
        return null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (AdoNetCommandHelper.IsAdoNetCall(node, "OracleCommand", out var sqlText, out _))
        {
            if (!string.IsNullOrEmpty(sqlText))
            {
                NestedSqlParser.TryDetectSqlDependencies(sqlText, scopeSymbolId, references);
            }
        }
    }
}
