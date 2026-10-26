using CodeExplorer.Common;
using CodeExplorer.Core.Common;

namespace CodeExplorer.Core.Parser.Syntax;

/// <summary>
/// Evaluates compile-time string/constant expressions across a <see cref="SyntaxDocument"/>,
/// populating <see cref="ExprSyntax.ResolvedValue"/>, <see cref="MemberDeclSyntax.ResolvedValue"/>,
/// <see cref="VariableDeclSyntax.ResolvedValue"/>, and attaching embedded <see cref="SqlStmtSyntax"/> nodes
/// to <see cref="ExprSyntax.EmbeddedSyntax"/> and <see cref="SyntaxDocument.AllSqlStatements"/>.
/// </summary>
public static class SyntaxValueEvaluator
{
    public static void EvaluateDocument(SyntaxDocument? doc, string? projectName = null)
    {
        if (doc == null) return;

        var isFrontend = WorkspaceConventions.IsFrontendFilePath(doc.RelativePath);
        var contextKey = !string.IsNullOrEmpty(projectName) ? projectName : doc.FilePath;

        // 1. Evaluate MemberDeclSyntax initializers
        for (int i = 0; i < doc.AllMembers.Count; i++)
        {
            var member = doc.AllMembers[i];
            if (member.Initializer != null)
            {
                if (AstValueResolver.TryResolveString(member.Initializer, contextKey, out var val) && !string.IsNullOrEmpty(val))
                {
                    member.ResolvedValue = val;
                    member.Initializer.ResolvedValue = val;
                }
            }
        }

        // 2. Evaluate VariableDeclSyntax initializers
        for (int i = 0; i < doc.AllVariables.Count; i++)
        {
            var variable = doc.AllVariables[i];
            if (variable.Initializer != null)
            {
                if (AstValueResolver.TryResolveString(variable.Initializer, contextKey, out var val) && !string.IsNullOrEmpty(val))
                {
                    variable.ResolvedValue = val;
                    variable.Initializer.ResolvedValue = val;
                }
            }
            else if (variable.ResolvedValue == null && AstValueResolver.TryResolveString(variable, contextKey, out var declVal) && !string.IsNullOrEmpty(declVal))
            {
                variable.ResolvedValue = declVal;
            }
        }

        // 3. Walk the syntax tree to pre-populate ExprSyntax.ResolvedValue and detect embedded SQL statements
        var embeddedSqlList = doc.AllSqlStatements.Count > 0
            ? new List<SqlStmtSyntax>(doc.AllSqlStatements)
            : null;

        WalkAndEvaluate(doc.RootNode, doc, contextKey, isFrontend, ref embeddedSqlList);

        if (embeddedSqlList != null)
        {
            doc.AllSqlStatements = embeddedSqlList;
        }
    }

    private static void WalkAndEvaluate(
        SyntaxNode node,
        SyntaxDocument doc,
        string? contextKey,
        bool isFrontend,
        ref List<SqlStmtSyntax>? embeddedSqlList)
    {
        if (node is ExprSyntax expr)
        {
            if (expr.ResolvedValue == null && ShouldPreEvaluateExpression(expr))
            {
                if (AstValueResolver.TryResolveString(expr, contextKey, out var resolved) && !string.IsNullOrEmpty(resolved))
                {
                    expr.ResolvedValue = resolved;
                }
            }

            if (!isFrontend && expr.EmbeddedSyntax == null)
            {
                var candidateSql = expr.ResolvedValue;
                if (string.IsNullOrEmpty(candidateSql) && expr is LiteralExprSyntax { LiteralKind: LiteralSyntaxKind.String or LiteralSyntaxKind.RawString })
                {
                    candidateSql = AstValueResolver.Unquote(expr.Text);
                }

                if (!string.IsNullOrEmpty(candidateSql) && NestedSqlParser.TryParseSql(candidateSql, out var firstWord, out _))
                {
                    var sqlStmt = CreateEmbeddedSqlNode(expr, doc, candidateSql, firstWord ?? "SQL");
                    expr.EmbeddedSyntax = sqlStmt;
                    embeddedSqlList ??= [];
                    embeddedSqlList.Add(sqlStmt);
                }
            }
        }

        for (int i = 0; i < node.Children.Count; i++)
        {
            WalkAndEvaluate(node.Children[i], doc, contextKey, isFrontend, ref embeddedSqlList);
        }
    }

    private static bool ShouldPreEvaluateExpression(ExprSyntax expr) =>
        expr is LiteralExprSyntax { LiteralKind: LiteralSyntaxKind.String or LiteralSyntaxKind.RawString }
            or InterpolatedStringExprSyntax
            or BinaryExprSyntax
            or CallExprSyntax;

    private static SqlStmtSyntax CreateEmbeddedSqlNode(ExprSyntax hostExpr, SyntaxDocument doc, string sqlText, string firstWord)
    {
        var upper = firstWord.ToUpperInvariant();
        var opKind = upper switch
        {
            "SELECT" or "WITH" => SqlOperationKind.Select,
            "INSERT" => SqlOperationKind.Insert,
            "UPDATE" => SqlOperationKind.Update,
            "DELETE" => SqlOperationKind.Delete,
            "MERGE" or "REPLACE" => SqlOperationKind.Merge,
            "CREATE" => SqlOperationKind.CreateTable,
            "ALTER" => SqlOperationKind.AlterTable,
            "DROP" or "TRUNCATE" => SqlOperationKind.DropTable,
            "EXEC" or "EXECUTE" or "CALL" => SqlOperationKind.Exec,
            _ => SqlOperationKind.Unknown
        };

        var refs = new List<Reference>();
        NestedSqlParser.TryDetectSqlDependencies(sqlText, "", refs);

        var tables = new List<SqlTableRefSyntax>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in refs)
        {
            if (!string.IsNullOrEmpty(r.TargetName) && seen.Add(r.TargetName))
            {
                string? schema = null;
                var tableName = r.TargetName;
                var dotIdx = tableName.IndexOf('.');
                if (dotIdx > 0)
                {
                    schema = tableName[..dotIdx];
                    tableName = tableName[(dotIdx + 1)..];
                }
                tables.Add(new SqlTableRefSyntax(
                    tableName,
                    schema,
                    IsWriteTarget: opKind is SqlOperationKind.Insert or SqlOperationKind.Update or SqlOperationKind.Delete or SqlOperationKind.Merge));
            }
        }

        return new SqlStmtSyntax
        {
            NodeId = hostExpr.NodeId,
            Span = hostExpr.Span,
            RawGrammarKind = "embedded_sql",
            FieldRole = "embedded_sql",
            IsNamed = true,
            Document = doc,
            Parent = hostExpr,
            Operation = opKind,
            OperationVerb = upper,
            Tables = tables,
            RawSql = NestedSqlParser.CleanQueryText(sqlText)
        };
    }
}

