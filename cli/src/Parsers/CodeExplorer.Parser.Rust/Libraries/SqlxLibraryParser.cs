using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.Rust.Libraries;

/// <summary>
/// Semantic extension for the SQLx database library in Rust.
/// Detects compile-time macros (sqlx::query!, sqlx::query_as!, sqlx::query_scalar!),
/// dynamic query calls (sqlx::query(...)), and pool connections (PgPool, MySqlPool, SqlitePool).
/// Emits Query, Table, and Database nodes with QUERIES_DB and USES_DB relationships.
/// </summary>
public class SqlxLibraryParser : ISemanticExtension
{
    public string Id => "sqlx";
    public string Name => "SQLx";
    public string Type => "db:relational";
    public bool IsImplemented => true;
    public LibraryRole LibraryRole => LibraryRole.OrmOrDatabase;
    public IReadOnlyList<string> SupportedPatterns => ["sqlx*"];

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (IsSqlQuery(node, out _, out _))
        {
            return OntologyConstants.NodeLabels.Query;
        }

        if (IsDatabasePool(node, out _))
        {
            return OntologyConstants.NodeLabels.Database;
        }

        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx) => ExtractIdentifier(node, ctx, null, null);
    public string? ExtractIdentifier(Node node, ParsingContext ctx, string? projectName) => ExtractIdentifier(node, ctx, projectName, null);

    public string? ExtractIdentifier(Node node, ParsingContext ctx, string? projectName, string? relativePath)
    {
        if (IsSqlQuery(node, out var sqlText, out var primaryTable))
        {
            var clean = NestedSqlParser.CleanQueryText(sqlText);
            if (NestedSqlParser.TryParseSql(sqlText, out var firstWord, out _))
            {
                return $"{firstWord} Query: {clean}";
            }
            return $"Query: {clean}";
        }

        if (IsDatabasePool(node, out var dbEngine))
        {
            return dbEngine;
        }

        return null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (IsSqlQuery(node, out var sqlText, out var primaryTable))
        {
            NestedSqlParser.TryDetectSqlDependencies(sqlText, scopeSymbolId, references);

            if (!string.IsNullOrEmpty(primaryTable))
            {
                references.Add(new Reference(scopeSymbolId, primaryTable, OntologyConstants.Relationships.QueriesDb));
                references.Add(new Reference(scopeSymbolId, primaryTable, OntologyConstants.Relationships.UsesDb));
            }
        }
        else if (IsDatabasePool(node, out var dbEngine))
        {
            references.Add(new Reference(scopeSymbolId, dbEngine, OntologyConstants.Relationships.UsesDb));
        }
    }

    public static bool IsSqlQuery(Node node, out string sqlText, out string? primaryTable)
    {
        sqlText = string.Empty;
        primaryTable = null;

        var isCall = node.Type == TreeSitterSyntax.Rust.CallExpression;
        var isMacro = node.Type == TreeSitterSyntax.Rust.MacroInvocation;

        if (!isCall && !isMacro)
        {
            return false;
        }

        if (!RustAstHelper.GetMethodCallInfo(node, out _, out var methodName, out var args))
        {
            return false;
        }

        if (string.IsNullOrEmpty(methodName)) return false;

        var isSqlxCall = methodName.StartsWith("sqlx::query", StringComparison.OrdinalIgnoreCase) ||
                         methodName.Equals("query!", StringComparison.OrdinalIgnoreCase) ||
                         methodName.Equals("query_as!", StringComparison.OrdinalIgnoreCase) ||
                         methodName.Equals("query_scalar!", StringComparison.OrdinalIgnoreCase) ||
                         methodName.Equals("query", StringComparison.OrdinalIgnoreCase) ||
                         methodName.Equals("query_as", StringComparison.OrdinalIgnoreCase) ||
                         methodName.Equals("query_scalar", StringComparison.OrdinalIgnoreCase);

        if (!isSqlxCall) return false;

        foreach (var arg in args)
        {
            var candidate = RustAstHelper.ExtractStringLiteral(arg);
            if (!string.IsNullOrEmpty(candidate) && NestedSqlParser.TryParseSql(candidate, out _, out primaryTable))
            {
                sqlText = candidate;
                return true;
            }
        }

        return false;
    }

    public static bool IsDatabasePool(Node node, out string dbEngine)
    {
        dbEngine = string.Empty;

        if (node.Type != TreeSitterSyntax.Rust.CallExpression)
        {
            return false;
        }

        if (!RustAstHelper.GetMethodCallInfo(node, out _, out var methodName, out _))
        {
            return false;
        }

        if (string.IsNullOrEmpty(methodName)) return false;

        var text = node.Text;
        if (text.Contains("PgPool", StringComparison.OrdinalIgnoreCase) || text.Contains("postgres", StringComparison.OrdinalIgnoreCase))
        {
            dbEngine = "postgres";
            return true;
        }
        if (text.Contains("MySqlPool", StringComparison.OrdinalIgnoreCase) || text.Contains("mysql", StringComparison.OrdinalIgnoreCase))
        {
            dbEngine = "mysql";
            return true;
        }
        if (text.Contains("SqlitePool", StringComparison.OrdinalIgnoreCase) || text.Contains("sqlite", StringComparison.OrdinalIgnoreCase))
        {
            dbEngine = "sqlite";
            return true;
        }

        return false;
    }
}
