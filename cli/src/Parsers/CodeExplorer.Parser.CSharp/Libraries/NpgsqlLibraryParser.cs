using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.CSharp.Libraries;

public class NpgsqlLibraryParser : ISemanticExtension
{
    public string Type => "db:relational";
    public string Name => "PostgreSQL";
    public string Id => "postgres";
    public IReadOnlyList<string> SupportedPatterns => ["Npgsql", "Npgsql.*"];
    public bool IsImplemented => true;

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (AdoNetCommandHelper.IsAdoNetCall(node, "NpgsqlCommand", out _, out _))
        {
            return OntologyConstants.NodeLabels.Query;
        }
        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx)
    {
        if (AdoNetCommandHelper.IsAdoNetCall(node, "NpgsqlCommand", out var sqlText, out var method))
        {
            if (!string.IsNullOrEmpty(sqlText))
            {
                var clean = NestedSqlParser.CleanQueryText(sqlText);
                if (NestedSqlParser.TryParseSql(sqlText, out var firstWord, out _))
                {
                    return $"{firstWord} Query: {clean}";
                }
                return $"PostgreSQL: {clean}";
            }
            return $"PostgreSQL: {method ?? "Command"}";
        }
        return null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (AdoNetCommandHelper.IsAdoNetCall(node, "NpgsqlCommand", out var sqlText, out _))
        {
            if (!string.IsNullOrEmpty(sqlText))
            {
                NestedSqlParser.TryDetectSqlDependencies(sqlText, scopeSymbolId, references);
            }
        }
    }
}
