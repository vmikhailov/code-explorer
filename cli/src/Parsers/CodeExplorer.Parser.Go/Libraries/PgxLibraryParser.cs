using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.Go.Libraries;

public class PgxLibraryParser : ISemanticExtension
{
    public string Type => "db:relational";
    public string Name => "PostgreSQL (pgx)";
    public string Id => "postgres-pgx";
    public IReadOnlyList<string> SupportedPatterns => ["github.com/jackc/pgx*"];
    public bool IsImplemented => true;
    public LibraryRole LibraryRole => LibraryRole.OrmOrDatabase;

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (GoSqlHelper.IsSqlCall(node, out _, out _))
        {
            return OntologyConstants.NodeLabels.Query;
        }
        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx)
    {
        if (GoSqlHelper.IsSqlCall(node, out var sqlText, out var method))
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
            return $"PostgreSQL: {method ?? "Query"}";
        }
        return null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (GoSqlHelper.IsSqlCall(node, out var sqlText, out _))
        {
            if (!string.IsNullOrEmpty(sqlText))
            {
                NestedSqlParser.TryDetectSqlDependencies(sqlText, scopeSymbolId, references);
            }
            references.Add(new Reference(scopeSymbolId, "postgres", OntologyConstants.Relationships.UsesDb));
        }
    }
}
