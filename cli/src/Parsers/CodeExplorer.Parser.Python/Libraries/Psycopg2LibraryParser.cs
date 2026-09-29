using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.Python.Libraries;

public class Psycopg2LibraryParser : ISemanticExtension
{
    public string Type => "db:relational";
    public string Name => "PostgreSQL";
    public string Id => "postgres";
    public IReadOnlyList<string> SupportedPatterns => ["psycopg2", "psycopg", "psycopg2.*", "psycopg.*"];
    public bool IsImplemented => true;

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (DbApiHelper.IsDbApiCall(node, out _, out _))
        {
            return OntologyConstants.NodeLabels.Query;
        }
        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx)
    {
        if (DbApiHelper.IsDbApiCall(node, out var sqlText, out var method))
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
            return $"PostgreSQL: {method ?? "execute"}";
        }
        return null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (DbApiHelper.IsDbApiCall(node, out var sqlText, out _))
        {
            if (!string.IsNullOrEmpty(sqlText))
            {
                NestedSqlParser.TryDetectSqlDependencies(sqlText, scopeSymbolId, references);
            }
            references.Add(new Reference(scopeSymbolId, "postgres", OntologyConstants.Relationships.UsesDb));
        }
    }
}
