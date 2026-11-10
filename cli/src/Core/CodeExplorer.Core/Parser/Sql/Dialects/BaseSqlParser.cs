using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Parser.Sql.Ast;
using CodeExplorer.Core.Parser.Sql.Tokens;
using Superpower;
using Superpower.Model;

namespace CodeExplorer.Core.Parser.Sql.Dialects;

public abstract class BaseSqlParser : ISqlDialectParser
{
    public abstract SqlDialect Dialect { get; }

    public virtual bool IsTableValuedFunction(string name)
    {
        var lower = name.ToLowerInvariant();
        return lower is "json_each" or "json_tree" or "string_split" or "openjson" or "generate_series"
            or "unnest" or "external_query" or "table_function" or "json_table"
            or "jsonb_array_elements" or "jsonb_to_recordset" or "jsonb_each" or "string_to_array"
            or "numbers" or "remote" or "cluster" or "s3" or "xmltable";
    }

    public virtual bool IsDialectKeyword(string token)
    {
        var lower = token.ToLowerInvariant();
        return lower is "only" or "final" or "sample" or "lateral" or "dual" or "system" or "as" or "using" or "exist" or "exists";
    }

    public virtual bool IsTemporaryTable(string name)
    {
        var lower = name.ToLowerInvariant();
        return lower.StartsWith('#') || lower.StartsWith('@') ||
               lower.StartsWith("temp_", StringComparison.OrdinalIgnoreCase) ||
               lower.StartsWith("tmp_", StringComparison.OrdinalIgnoreCase);
    }

    public static string CleanIdentifier(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
        var trimmed = raw.Trim().Trim('[', ']', '"', '`', '\'', '{', '}', '$', ' ');

        if ((trimmed.StartsWith("${") && trimmed.EndsWith("}")) ||
            (trimmed.StartsWith("{{") && trimmed.EndsWith("}}")) ||
            (trimmed.StartsWith("{") && trimmed.EndsWith("}")))
        {
            var inner = trimmed.Trim('$', '{', '}').Trim();
            if (ConstantRegistry.TryResolve(null, inner, out var resolved) && !string.IsNullOrWhiteSpace(resolved))
            {
                trimmed = resolved;
            }
            else
            {
                trimmed = inner;
            }
        }

        return trimmed.Trim('[', ']', '"', '`', '\'', '{', '}', '$', ' ');
    }

    public static (string? Database, string? Schema, string Table) ParseTableIdentifier(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return (null, null, string.Empty);

        var trimmed = raw.Trim().TrimEnd(';');

        // If entire token is wrapped in ${...} or {{...}} or {...}, unwrap before splitting on dot
        if ((trimmed.StartsWith("${") && trimmed.EndsWith("}")) ||
            (trimmed.StartsWith("{{") && trimmed.EndsWith("}}")) ||
            (trimmed.StartsWith("{") && trimmed.EndsWith("}")))
        {
            var inner = trimmed.Trim('$', '{', '}').Trim();
            if (ConstantRegistry.TryResolve(null, inner, out var resolved) && !string.IsNullOrWhiteSpace(resolved))
            {
                trimmed = resolved;
            }
            else
            {
                trimmed = inner;
            }
        }

        var parts = trimmed.Split('.')
            .Select(CleanIdentifier)
            .Where(p => !string.IsNullOrEmpty(p))
            .ToArray();

        if (parts.Length >= 3)
        {
            return (parts[0], parts[1], parts[2]);
        }
        if (parts.Length == 2)
        {
            return (null, parts[0], parts[1]);
        }
        return (null, null, parts.Length > 0 ? parts[0] : string.Empty);
    }

    public SqlScriptAst Parse(string sqlText)
    {
        if (TryParse(sqlText, out var script, out _))
        {
            return script!;
        }
        return new SqlScriptAst([]);
    }

    public bool TryParse(string sqlText, out SqlScriptAst? script, out string? errorMessage)
    {
        script = null;
        errorMessage = null;

        if (string.IsNullOrWhiteSpace(sqlText))
        {
            script = new SqlScriptAst([]);
            return true;
        }

        TokenList<SqlToken> tokens;
        try
        {
            tokens = SqlTokenizer.Instance.Tokenize(sqlText);
        }
        catch (Exception ex)
        {
            errorMessage = $"Tokenization failed: {ex.Message}";
            return false;
        }

        try
        {
            var statements = ParseStatements(tokens);
            script = new SqlScriptAst(statements);
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = $"Parsing failed: {ex.Message}";
            return false;
        }
    }

    protected virtual List<SqlStatementAst> ParseStatements(TokenList<SqlToken> tokens)
    {
        var statements = new List<SqlStatementAst>();
        var reader = new SqlTokenReader(tokens);

        while (!reader.IsAtEnd)
        {
            reader.SkipSemicolons();
            if (reader.IsAtEnd) break;

            if (reader.Current.ToStringValue().Equals("BEGIN", StringComparison.OrdinalIgnoreCase) ||
                reader.Current.ToStringValue().Equals("END", StringComparison.OrdinalIgnoreCase))
            {
                reader.Advance();
                continue;
            }

            var stmt = TryParseNextStatement(reader);
            if (stmt != null)
            {
                statements.Add(stmt);
            }
            else
            {
                // Advance past next semicolon or delimiter
                reader.SkipToNextStatement();
            }
        }

        return statements;
    }

    private SqlStatementAst? TryParseNextStatement(SqlTokenReader reader)
    {
        // 1. Check for WITH (CTEs)
        var ctes = new List<SqlCteAst>();
        var cteNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (reader.Match(SqlToken.With))
        {
            reader.Match(SqlToken.Recursive); // optional RECURSIVE

            do
            {
                var cte = ParseSingleCte(reader, cteNames);
                if (cte != null)
                {
                    ctes.Add(cte);
                    cteNames.Add(cte.Name);
                }
            }
            while (reader.Match(SqlToken.Comma));
        }

        // 2. Dispatch based on initial keyword
        var token = reader.Current;

        switch (token.Kind)
        {
            case SqlToken.Select:
                return ParseSelect(reader, ctes, cteNames);

            case SqlToken.Insert:
                return ParseInsert(reader, ctes, cteNames);

            case SqlToken.Update:
                return ParseUpdate(reader, ctes, cteNames);

            case SqlToken.Delete:
                return ParseDelete(reader, ctes, cteNames);

            case SqlToken.Merge:
                return ParseMerge(reader, ctes, cteNames);

            case SqlToken.Create:
                return ParseCreate(reader, ctes, cteNames);

            case SqlToken.Drop:
                return ParseDrop(reader);

            case SqlToken.Truncate:
                return ParseTruncate(reader);

            case SqlToken.Exec:
            case SqlToken.Execute:
            case SqlToken.Call:
                return ParseExec(reader);

            default:
                return null;
        }
    }

    private SqlCteAst? ParseSingleCte(SqlTokenReader reader, HashSet<string> existingCteNames)
    {
        var nameToken = reader.ReadIdentifierOrString();
        if (nameToken == null) return null;
        var cteName = CleanIdentifier(nameToken);
        var scopedCteNames = new HashSet<string>(existingCteNames, StringComparer.OrdinalIgnoreCase) { cteName };

        var columns = new List<string>();
        if (reader.Match(SqlToken.LParen))
        {
            while (!reader.IsAtEnd && !reader.Match(SqlToken.RParen))
            {
                var col = reader.ReadIdentifierOrString();
                if (!string.IsNullOrEmpty(col)) columns.Add(CleanIdentifier(col));
                reader.Match(SqlToken.Comma);
            }
        }

        if (!reader.Match(SqlToken.As)) return null;

        // Skip optional MATERIALIZED / NOT MATERIALIZED
        if (reader.Current.ToStringValue().Equals("MATERIALIZED", StringComparison.OrdinalIgnoreCase))
        {
            reader.Advance();
        }
        else if (reader.Match(SqlToken.Not))
        {
            if (reader.Current.ToStringValue().Equals("MATERIALIZED", StringComparison.OrdinalIgnoreCase))
            {
                reader.Advance();
            }
        }

        if (!reader.Match(SqlToken.LParen)) return null;

        // Parse inner CTE statement or skip to matching RParen
        var innerStmt = ParseSelect(reader, [], scopedCteNames);
        reader.SkipToMatchingRightParen();

        return new SqlCteAst(cteName, columns, innerStmt);
    }

    private SqlSelectStatementAst ParseSelect(SqlTokenReader reader, List<SqlCteAst> ctes, HashSet<string> cteNames)
    {
        reader.Match(SqlToken.Select);
        reader.Match(SqlToken.Distinct);
        reader.Match(SqlToken.All);

        var tables = new List<SqlTableRefAst>();
        var joins = new List<SqlTableRefAst>();

        // Scan until FROM, or semicolon, or end
        var depth = 0;
        while (!reader.IsAtEnd)
        {
            if (reader.Match(SqlToken.LParen))
            {
                depth++;
                continue;
            }
            if (reader.Match(SqlToken.RParen))
            {
                if (depth > 0) depth--;
                else
                {
                    reader.Backtrack();
                    break;
                }
                continue;
            }

            if (depth == 0)
            {
                if (reader.Match(SqlToken.From))
                {
                    var fromTables = ParseTableReferenceList(reader, cteNames, isWriteTarget: false);
                    tables.AddRange(fromTables);
                }
                else if (reader.Match(SqlToken.Join) ||
                         (reader.MatchAny(SqlToken.Inner, SqlToken.Left, SqlToken.Right, SqlToken.Full, SqlToken.Cross, SqlToken.Natural, SqlToken.Outer, SqlToken.StraightJoin) && reader.Match(SqlToken.Join)))
                {
                    var joinTables = ParseTableReferenceList(reader, cteNames, isWriteTarget: false);
                    joins.AddRange(joinTables);
                }
                else if (reader.MatchAny(SqlToken.Union, SqlToken.Intersect, SqlToken.Except))
                {
                    reader.Match(SqlToken.All);
                    if (reader.Current.Kind == SqlToken.Select)
                    {
                        var nextSelect = ParseSelect(reader, [], cteNames);
                        tables.AddRange(nextSelect.Tables);
                        joins.AddRange(nextSelect.Joins);
                    }
                    break;
                }
                else if (reader.Current.Kind is SqlToken.Semicolon or SqlToken.Order or SqlToken.Group or SqlToken.Having or SqlToken.Limit or SqlToken.Offset)
                {
                    break;
                }
                else
                {
                    reader.Advance();
                }
            }
            else
            {
                reader.Advance();
            }
        }

        return new SqlSelectStatementAst(tables, joins) { Ctes = ctes };
    }

    private SqlInsertStatementAst ParseInsert(SqlTokenReader reader, List<SqlCteAst> ctes, HashSet<string> cteNames)
    {
        reader.Match(SqlToken.Insert);
        reader.Match(SqlToken.Into);

        var targetTable = ParseSingleTableReference(reader, cteNames, isWriteTarget: true);

        // Optional SELECT subquery
        SqlStatementAst? sourceQuery = null;
        while (!reader.IsAtEnd && reader.Current.Kind != SqlToken.Semicolon)
        {
            if (reader.Current.Kind == SqlToken.Select)
            {
                sourceQuery = ParseSelect(reader, [], cteNames);
                break;
            }
            reader.Advance();
        }

        return new SqlInsertStatementAst(targetTable ?? new SqlTableRefAst(null, null, ""), sourceQuery) { Ctes = ctes };
    }

    private SqlUpdateStatementAst ParseUpdate(SqlTokenReader reader, List<SqlCteAst> ctes, HashSet<string> cteNames)
    {
        reader.Match(SqlToken.Update);

        var targetTable = ParseSingleTableReference(reader, cteNames, isWriteTarget: true);
        var fromTables = new List<SqlTableRefAst>();

        while (!reader.IsAtEnd && reader.Current.Kind != SqlToken.Semicolon)
        {
            if (reader.Match(SqlToken.From))
            {
                fromTables.AddRange(ParseTableReferenceList(reader, cteNames, isWriteTarget: false));
            }
            else if (reader.Match(SqlToken.Join))
            {
                fromTables.AddRange(ParseTableReferenceList(reader, cteNames, isWriteTarget: false));
            }
            else
            {
                reader.Advance();
            }
        }

        return new SqlUpdateStatementAst(targetTable ?? new SqlTableRefAst(null, null, ""), fromTables) { Ctes = ctes };
    }

    private SqlDeleteStatementAst ParseDelete(SqlTokenReader reader, List<SqlCteAst> ctes, HashSet<string> cteNames)
    {
        reader.Match(SqlToken.Delete);
        reader.Match(SqlToken.From);

        var targetTable = ParseSingleTableReference(reader, cteNames, isWriteTarget: true);
        var usingTables = new List<SqlTableRefAst>();

        while (!reader.IsAtEnd && reader.Current.Kind != SqlToken.Semicolon)
        {
            if (reader.Match(SqlToken.Using))
            {
                usingTables.AddRange(ParseTableReferenceList(reader, cteNames, isWriteTarget: false));
            }
            else if (reader.Match(SqlToken.Join))
            {
                usingTables.AddRange(ParseTableReferenceList(reader, cteNames, isWriteTarget: false));
            }
            else
            {
                reader.Advance();
            }
        }

        return new SqlDeleteStatementAst(targetTable ?? new SqlTableRefAst(null, null, ""), usingTables) { Ctes = ctes };
    }

    private SqlMergeStatementAst ParseMerge(SqlTokenReader reader, List<SqlCteAst> ctes, HashSet<string> cteNames)
    {
        reader.Match(SqlToken.Merge);
        reader.Match(SqlToken.Into);

        var targetTable = ParseSingleTableReference(reader, cteNames, isWriteTarget: true);
        SqlTableRefAst? sourceTable = null;

        while (!reader.IsAtEnd && reader.Current.Kind != SqlToken.Semicolon)
        {
            if (reader.Match(SqlToken.Using))
            {
                sourceTable = ParseSingleTableReference(reader, cteNames, isWriteTarget: false);
                break;
            }
            reader.Advance();
        }

        return new SqlMergeStatementAst(targetTable ?? new SqlTableRefAst(null, null, ""), sourceTable) { Ctes = ctes };
    }

    private SqlStatementAst? ParseCreate(SqlTokenReader reader, List<SqlCteAst> ctes, HashSet<string> cteNames)
    {
        reader.Match(SqlToken.Create);
        reader.Match(SqlToken.Or);
        reader.Match(SqlToken.Replace);

        var isTemp = reader.MatchAny(SqlToken.Temporary, SqlToken.Temp);
        reader.Match(SqlToken.External);
        reader.Match(SqlToken.Materialized);

        if (reader.Match(SqlToken.Table))
        {
            // Optional IF NOT EXISTS or IF NOT EXIST
            if (reader.Match(SqlToken.If))
            {
                reader.Match(SqlToken.Not);
                if (!reader.Match(SqlToken.Exists) && !reader.IsAtEnd && reader.Current.ToStringValue().Equals("EXIST", StringComparison.OrdinalIgnoreCase))
                {
                    reader.Advance();
                }
            }

            var table = ParseSingleTableReference(reader, cteNames, isWriteTarget: true);
            if (table == null) return null;

            if (reader.Match(SqlToken.LParen))
            {
                reader.SkipToMatchingRightParen();
            }

            return new SqlCreateTableStatementAst(table, isTemp, null) { Ctes = ctes };
        }
        else if (reader.Match(SqlToken.View))
        {
            if (reader.Match(SqlToken.If))
            {
                reader.Match(SqlToken.Not);
                if (!reader.Match(SqlToken.Exists) && !reader.IsAtEnd && reader.Current.ToStringValue().Equals("EXIST", StringComparison.OrdinalIgnoreCase))
                {
                    reader.Advance();
                }
            }

            var view = ParseSingleTableReference(reader, cteNames, isWriteTarget: true);
            if (view == null) return null;

            return new SqlCreateViewStatementAst(view, null) { Ctes = ctes };
        }
        else if (reader.MatchAny(SqlToken.Procedure, SqlToken.Proc, SqlToken.Function))
        {
            if (reader.Match(SqlToken.If))
            {
                reader.Match(SqlToken.Not);
                if (!reader.Match(SqlToken.Exists) && !reader.IsAtEnd && reader.Current.ToStringValue().Equals("EXIST", StringComparison.OrdinalIgnoreCase))
                {
                    reader.Advance();
                }
            }

            var procToken = reader.ReadMultipartIdentifier();
            if (string.IsNullOrEmpty(procToken)) return null;

            var (db, schema, procName) = ParseTableIdentifier(procToken);
            var procRef = new SqlProcRefAst(db, schema, procName);

            if (reader.Match(SqlToken.LParen))
            {
                reader.SkipToMatchingRightParen();
            }

            if (reader.Current.ToStringValue().Equals("RETURNS", StringComparison.OrdinalIgnoreCase))
            {
                reader.Advance();
                reader.ReadMultipartIdentifier();
            }

            if (reader.Match(SqlToken.As) || reader.Match(SqlToken.Is))
            {
            }

            var bodyStatements = new List<SqlStatementAst>();
            var hasBegin = false;
            var beginDepth = 0;

            while (!reader.IsAtEnd)
            {
                reader.SkipSemicolons();
                if (reader.IsAtEnd) break;

                if (reader.Current.ToStringValue().Equals("GO", StringComparison.OrdinalIgnoreCase))
                {
                    reader.Advance();
                    break;
                }

                if (reader.Current.ToStringValue().Equals("BEGIN", StringComparison.OrdinalIgnoreCase))
                {
                    hasBegin = true;
                    beginDepth++;
                    reader.Advance();
                    continue;
                }

                if (reader.Current.ToStringValue().Equals("END", StringComparison.OrdinalIgnoreCase))
                {
                    if (beginDepth > 0)
                    {
                        beginDepth--;
                        reader.Advance();
                        if (!reader.IsAtEnd && reader.Current.Kind == SqlToken.Identifier)
                        {
                            reader.Advance();
                        }
                        if (beginDepth == 0)
                        {
                            break;
                        }
                        continue;
                    }
                    else if (hasBegin)
                    {
                        reader.Advance();
                        break;
                    }
                    else
                    {
                        reader.Advance();
                        break;
                    }
                }

                var stmt = TryParseNextStatement(reader);
                if (stmt != null)
                {
                    bodyStatements.Add(stmt);
                }
                else
                {
                    reader.SkipToNextStatement();
                }
            }

            return new SqlCreateProcedureStatementAst(procRef, string.Empty, bodyStatements) { Ctes = ctes };
        }

        return null;
    }

    private SqlDropStatementAst? ParseDrop(SqlTokenReader reader)
    {
        reader.Match(SqlToken.Drop);
        reader.Match(SqlToken.Temporary);
        SqlDropObjectType dropType;
        if (reader.Match(SqlToken.Table))
        {
            dropType = SqlDropObjectType.Table;
        }
        else if (reader.Match(SqlToken.View))
        {
            dropType = SqlDropObjectType.View;
        }
        else if (reader.MatchAny(SqlToken.Procedure, SqlToken.Proc))
        {
            dropType = SqlDropObjectType.Procedure;
        }
        else if (reader.Match(SqlToken.Function))
        {
            dropType = SqlDropObjectType.Function;
        }
        else
        {
            var raw = reader.ReadIdentifierOrString();
            dropType = raw?.ToUpperInvariant() switch
            {
                "DATABASE" => SqlDropObjectType.Database,
                "SCHEMA" => SqlDropObjectType.Schema,
                "INDEX" => SqlDropObjectType.Index,
                "VIEW" => SqlDropObjectType.View,
                "PROCEDURE" => SqlDropObjectType.Procedure,
                "FUNCTION" => SqlDropObjectType.Function,
                _ => SqlDropObjectType.Table
            };
        }

        if (reader.Match(SqlToken.If))
        {
            if (!reader.Match(SqlToken.Exists) && !reader.IsAtEnd && reader.Current.ToStringValue().Equals("EXIST", StringComparison.OrdinalIgnoreCase))
            {
                reader.Advance();
            }
        }

        var target = ParseSingleTableReference(reader, [], isWriteTarget: true);
        if (target == null) return null;

        return new SqlDropStatementAst(dropType, target);
    }

    private SqlTruncateStatementAst? ParseTruncate(SqlTokenReader reader)
    {
        reader.Match(SqlToken.Truncate);
        reader.Match(SqlToken.Table);

        var target = ParseSingleTableReference(reader, [], isWriteTarget: true);
        if (target == null) return null;

        return new SqlTruncateStatementAst(target);
    }

    private SqlExecStatementAst? ParseExec(SqlTokenReader reader)
    {
        reader.MatchAny(SqlToken.Exec, SqlToken.Execute, SqlToken.Call);
        var procToken = reader.ReadMultipartIdentifier();
        if (string.IsNullOrEmpty(procToken)) return null;

        var (db, schema, procName) = ParseTableIdentifier(procToken);
        return new SqlExecStatementAst(new SqlProcRefAst(db, schema, procName));
    }

    private List<SqlTableRefAst> ParseTableReferenceList(SqlTokenReader reader, HashSet<string> cteNames, bool isWriteTarget)
    {
        var list = new List<SqlTableRefAst>();
        do
        {
            var table = ParseSingleTableReference(reader, cteNames, isWriteTarget);
            if (table != null)
            {
                list.Add(table);
            }
        }
        while (reader.Match(SqlToken.Comma));

        return list;
    }

    private SqlTableRefAst? ParseSingleTableReference(SqlTokenReader reader, HashSet<string> cteNames, bool isWriteTarget)
    {
        // 1. Skip dialect modifiers like ONLY (Postgres) or FINAL/SAMPLE (ClickHouse)
        if (reader.Match(SqlToken.Only))
        {
            // FROM ONLY table_name
        }

        // 2. Check for Subquery: FROM (SELECT ...) [AS] alias
        if (reader.Match(SqlToken.LParen))
        {
            if (reader.Current.Kind == SqlToken.Select)
            {
                var subquery = ParseSelect(reader, [], cteNames);
                reader.SkipToMatchingRightParen();
                // Consume optional alias
                reader.Match(SqlToken.As);
                reader.ReadIdentifierOrString();
                return null; // Derived table, not a physical table
            }
            reader.SkipToMatchingRightParen();
            return null;
        }

        // 3. Read table identifier (which could be multipart: db.schema.table)
        var rawIdent = reader.ReadMultipartIdentifier();
        if (string.IsNullOrWhiteSpace(rawIdent))
        {
            return null;
        }

        var (db, schema, tableName) = ParseTableIdentifier(rawIdent);
        if (string.IsNullOrWhiteSpace(tableName))
        {
            return null;
        }

        // Check if followed by '(' -> if not write target, check if TVF or hint like (NOLOCK)
        if (!isWriteTarget && reader.Current.Kind == SqlToken.LParen)
        {
            if (IsTableValuedFunction(tableName))
            {
                reader.SkipToMatchingRightParen();
                return null; // Function call, not a table
            }
            reader.SkipToMatchingRightParen();
        }

        // 4. Validate Table Name against TVFs, CTEs, Temporary markers, and Keywords
        if (IsTableValuedFunction(tableName) ||
            IsDialectKeyword(tableName) ||
            IsDialectKeyword(rawIdent) ||
            cteNames.Contains(tableName) ||
            IsTemporaryTable(tableName))
        {
            return null;
        }

        // Consume optional alias: [AS] alias
        string? alias = null;
        if (reader.Match(SqlToken.As))
        {
            alias = reader.ReadIdentifierOrString();
        }
        else if (reader.Current.Kind is SqlToken.Identifier or SqlToken.QuotedIdentifier)
        {
            // In SQL: FROM table alias
            if (!IsClauseStart(reader.Current.Kind))
            {
                alias = reader.ReadIdentifierOrString();
            }
        }

        // Consume optional table hint: WITH (NOLOCK)
        if (reader.Match(SqlToken.With))
        {
            if (reader.Match(SqlToken.LParen))
            {
                reader.SkipToMatchingRightParen();
            }
        }

        // Consume ClickHouse FINAL or SAMPLE if present
        if (reader.Match(SqlToken.Final)) { }
        if (reader.Match(SqlToken.Sample)) { reader.ReadIdentifierOrString(); }

        return new SqlTableRefAst(db, schema, tableName, alias, IsTemporaryTable(tableName), false, false, isWriteTarget);
    }

    private static bool IsClauseStart(SqlToken token) => token is
        SqlToken.Where or SqlToken.Join or SqlToken.Inner or SqlToken.Left or SqlToken.Right
        or SqlToken.Full or SqlToken.Cross or SqlToken.Outer or SqlToken.Natural or SqlToken.On
        or SqlToken.Using or SqlToken.Group or SqlToken.Having or SqlToken.Order or SqlToken.Limit
        or SqlToken.Offset or SqlToken.Union or SqlToken.Intersect or SqlToken.Except or SqlToken.Semicolon;
}
