using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.Rust.Libraries;

/// <summary>
/// Semantic extension for the SQLx database library in Rust.
/// Detects compile-time macros (sqlx::query!, sqlx::query_as!, sqlx::query_scalar!),
/// dynamic query calls (sqlx::query(...)), and pool connections (PgPool, MySqlPool, SqlitePool).
/// Emits Query, Table, and Database nodes with QUERIES_DB and USES_DB relationships.
/// </summary>
public class SqlxLibraryParser : DeclarativeSemanticExtension
{
    public SqlxLibraryParser()
    {
        Id = "sqlx";
        Name = "SQLx";
        Type = SemanticExtensionType.RelationalDb;
        LibraryRole = LibraryRole.OrmOrDatabase;
        SupportedPatterns = ["sqlx*"];
        CustomAstRules =
        [
            new CustomAstRule
            {
                NodeTypes = System.Collections.Frozen.FrozenSet.ToFrozenSet(
                [
                    TreeSitterSyntax.Rust.CallExpression,
                    TreeSitterSyntax.Rust.MacroInvocation,
                    TreeSitterSyntax.Rust.UseDeclaration,
                    TreeSitterSyntax.Rust.TypeIdentifier,
                    TreeSitterSyntax.Rust.ScopedTypeIdentifier
                ]),
                MapNodeType = (node, ctx) => IsSqlQuery(node, out _, out _) ? OntologyConstants.NodeLabels.Query : null,
                ExtractIdentifier = (node, _, _, _) => ExtractIdentifierCore(node),
                CollectReferences = CollectReferencesCore
            }
        ];
    }

    private static string? ExtractIdentifierCore(Node node)
    {
        if (IsSqlQuery(node, out var sqlText, out _))
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

    private static void CollectReferencesCore(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
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

        var isCall = node.Is(TreeSitterSyntax.Rust.CallExpression);
        var isMacro = node.Is(TreeSitterSyntax.Rust.MacroInvocation);

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

        if (node.IsAny(TreeSitterSyntax.Rust.UseDeclaration, TreeSitterSyntax.Rust.TypeIdentifier, TreeSitterSyntax.Rust.ScopedTypeIdentifier))
        {
            var span = node.TextSpan;
            if (span.Contains("PgPool", StringComparison.OrdinalIgnoreCase) ||
                span.Contains("sqlx::postgres", StringComparison.OrdinalIgnoreCase))
            {
                dbEngine = "postgres";
                return true;
            }
            if (span.Contains("MySqlPool", StringComparison.OrdinalIgnoreCase) ||
                span.Contains("sqlx::mysql", StringComparison.OrdinalIgnoreCase))
            {
                dbEngine = "mysql";
                return true;
            }
            if (span.Contains("SqlitePool", StringComparison.OrdinalIgnoreCase) ||
                span.Contains("sqlx::sqlite", StringComparison.OrdinalIgnoreCase))
            {
                dbEngine = "sqlite";
                return true;
            }
            return false;
        }

        if (!node.Is(TreeSitterSyntax.Rust.CallExpression))
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
