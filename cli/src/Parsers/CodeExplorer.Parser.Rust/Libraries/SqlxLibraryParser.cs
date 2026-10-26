using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using CodeExplorer.Core.Parser.Syntax;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

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

        var isSqlxCall = methodName.StartWithIC("sqlx::query") ||
                         methodName.Equals("query!", StringComparison.OrdinalIgnoreCase) ||
                         methodName.Equals("query_as!", StringComparison.OrdinalIgnoreCase) ||
                         methodName.Equals("query_scalar!", StringComparison.OrdinalIgnoreCase) ||
                         methodName.Equals("query", StringComparison.OrdinalIgnoreCase) ||
                         methodName.Equals("query_as", StringComparison.OrdinalIgnoreCase) ||
                         methodName.Equals("query_scalar", StringComparison.OrdinalIgnoreCase) ||
                         methodName.Equals("execute", StringComparison.OrdinalIgnoreCase);

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

        // Check PostgreSQL pool
        if (methodName.Contains("PgPoolOptions::new", StringComparison.OrdinalIgnoreCase) ||
            methodName.Contains("PgPool::connect", StringComparison.OrdinalIgnoreCase) ||
            (text.Contains("PgPoolOptions", StringComparison.OrdinalIgnoreCase) && (methodName.Equals("new", StringComparison.OrdinalIgnoreCase) || methodName.Equals("connect", StringComparison.OrdinalIgnoreCase) || methodName.Equals("connect_lazy", StringComparison.OrdinalIgnoreCase) || methodName.Equals("connect_with", StringComparison.OrdinalIgnoreCase))) ||
            (text.Contains("PgPool::connect", StringComparison.OrdinalIgnoreCase)))
        {
            dbEngine = "postgres";
            return true;
        }

        // Check MySQL pool
        if (methodName.Contains("MySqlPoolOptions::new", StringComparison.OrdinalIgnoreCase) ||
            methodName.Contains("MySqlPool::connect", StringComparison.OrdinalIgnoreCase) ||
            (text.Contains("MySqlPoolOptions", StringComparison.OrdinalIgnoreCase) && (methodName.Equals("new", StringComparison.OrdinalIgnoreCase) || methodName.Equals("connect", StringComparison.OrdinalIgnoreCase) || methodName.Equals("connect_lazy", StringComparison.OrdinalIgnoreCase) || methodName.Equals("connect_with", StringComparison.OrdinalIgnoreCase))) ||
            (text.Contains("MySqlPool::connect", StringComparison.OrdinalIgnoreCase)))
        {
            dbEngine = "mysql";
            return true;
        }

        // Check SQLite pool
        if (methodName.Contains("SqlitePoolOptions::new", StringComparison.OrdinalIgnoreCase) ||
            methodName.Contains("SqlitePool::connect", StringComparison.OrdinalIgnoreCase) ||
            (text.Contains("SqlitePoolOptions", StringComparison.OrdinalIgnoreCase) && (methodName.Equals("new", StringComparison.OrdinalIgnoreCase) || methodName.Equals("connect", StringComparison.OrdinalIgnoreCase) || methodName.Equals("connect_lazy", StringComparison.OrdinalIgnoreCase) || methodName.Equals("connect_with", StringComparison.OrdinalIgnoreCase))) ||
            (text.Contains("SqlitePool::connect", StringComparison.OrdinalIgnoreCase)))
        {
            dbEngine = "sqlite";
            return true;
        }

        return false;
    }
}
