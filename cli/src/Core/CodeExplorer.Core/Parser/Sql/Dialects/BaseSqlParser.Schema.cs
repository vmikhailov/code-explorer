using System.Text;
using CodeExplorer.Core.Parser.Sql.Ast;
using CodeExplorer.Core.Parser.Sql.Tokens;

namespace CodeExplorer.Core.Parser.Sql.Dialects;

public abstract partial class BaseSqlParser
{
    private const string AddKeyword = "ADD";
    private const string ColumnKeyword = "COLUMN";
    private const string BeforeKeyword = "BEFORE";
    private const string AfterKeyword = "AFTER";
    private const string InsteadKeyword = "INSTEAD";
    private const string OfKeyword = "OF";
    private const string ForKeyword = "FOR";
    private const string EachKeyword = "EACH";
    private const string RowKeyword = "ROW";
    private const string StatementKeyword = "STATEMENT";
    private const string WhenKeyword = "WHEN";
    private const string LanguageKeyword = "LANGUAGE";
    private const string ReturnsKeyword = "RETURNS";
    private const string BeginKeyword = "BEGIN";

    protected static bool IsBeginToken(SqlTokenReader reader) =>
        !reader.IsAtEnd && reader.Current.ToStringValue().Equals(BeginKeyword, StringComparison.OrdinalIgnoreCase);

    protected sealed record CreateTableBodyParseResult(
        List<SqlTableRefAst> ReferencedTables,
        List<SqlColumnDefAst> Columns,
        List<SqlForeignKeyAst> ForeignKeys);

    protected CreateTableBodyParseResult ParseCreateTableBody(SqlTokenReader reader, HashSet<string> cteNames)
    {
        var refTables = new List<SqlTableRefAst>();
        var columns = new List<SqlColumnDefAst>();
        var foreignKeys = new List<SqlForeignKeyAst>();
        var tableLevelPkCols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        while (!reader.IsAtEnd)
        {
            if (reader.Match(SqlToken.RParen))
            {
                break;
            }

            if (reader.Match(SqlToken.Comma))
            {
                continue;
            }

            if (IsTableConstraintStart(reader))
            {
                ParseTableConstraint(reader, cteNames, refTables, foreignKeys, tableLevelPkCols);
            }
            else
            {
                var col = ParseColumnDefinition(reader, cteNames, refTables, foreignKeys);
                if (col != null)
                {
                    columns.Add(col);
                }
            }
        }

        if (tableLevelPkCols.Count > 0)
        {
            for (var i = 0; i < columns.Count; i++)
            {
                if (tableLevelPkCols.Contains(columns[i].Name) && !columns[i].IsPrimaryKey)
                {
                    columns[i] = columns[i] with { IsPrimaryKey = true, IsNullable = false };
                }
            }
        }

        return new CreateTableBodyParseResult(refTables, columns, foreignKeys);
    }

    private static bool IsTableConstraintStart(SqlTokenReader reader)
    {
        return reader.Current.Kind is SqlToken.Constraint or SqlToken.Primary or SqlToken.Foreign
            or SqlToken.Unique or SqlToken.Check or SqlToken.Index or SqlToken.Key;
    }

    private void ParseTableConstraint(
        SqlTokenReader reader,
        HashSet<string> cteNames,
        List<SqlTableRefAst> refTables,
        List<SqlForeignKeyAst> foreignKeys,
        HashSet<string> tableLevelPkCols)
    {
        if (reader.Match(SqlToken.Constraint))
        {
            reader.ReadIdentifierOrString();
        }

        if (reader.Match(SqlToken.Primary))
        {
            reader.Match(SqlToken.Key);
            if (reader.Match(SqlToken.LParen))
            {
                foreach (var colName in ReadIdentifierListUntilRightParen(reader))
                {
                    tableLevelPkCols.Add(colName);
                }
            }
            SkipToNextTableBodyItem(reader);
            return;
        }

        if (reader.Match(SqlToken.Foreign))
        {
            reader.Match(SqlToken.Key);
            string? fromCol = null;
            if (reader.Match(SqlToken.LParen))
            {
                var fromCols = ReadIdentifierListUntilRightParen(reader);
                fromCol = fromCols.Count > 0 ? string.Join(",", fromCols) : null;
            }

            if (reader.Match(SqlToken.References))
            {
                var fk = ParseReferencesTarget(reader, cteNames, fromCol);
                if (fk != null)
                {
                    refTables.Add(fk.TargetTable);
                    foreignKeys.Add(fk);
                }
            }
            SkipToNextTableBodyItem(reader);
            return;
        }

        // UNIQUE / CHECK / INDEX / KEY
        SkipToNextTableBodyItem(reader);
    }

    private SqlColumnDefAst? ParseColumnDefinition(
        SqlTokenReader reader,
        HashSet<string> cteNames,
        List<SqlTableRefAst> refTables,
        List<SqlForeignKeyAst> foreignKeys)
    {
        var rawColName = reader.ReadIdentifierOrString();
        if (string.IsNullOrWhiteSpace(rawColName))
        {
            SkipToNextTableBodyItem(reader);
            return null;
        }

        var colName = CleanIdentifier(rawColName);
        if (string.IsNullOrWhiteSpace(colName))
        {
            SkipToNextTableBodyItem(reader);
            return null;
        }

        var dataType = ReadColumnDataType(reader);
        var isPk = false;
        var isNullable = true;
        var isUnique = false;
        SqlForeignKeyAst? inlineFk = null;

        var depth = 0;
        while (!reader.IsAtEnd)
        {
            if (depth == 0 && reader.Current.Kind is SqlToken.Comma or SqlToken.RParen or SqlToken.Semicolon)
            {
                break;
            }

            if (reader.Match(SqlToken.LParen))
            {
                depth++;
                continue;
            }
            if (reader.Match(SqlToken.RParen))
            {
                if (depth > 0) depth--;
                continue;
            }

            if (depth == 0)
            {
                if (reader.Match(SqlToken.Primary))
                {
                    reader.Match(SqlToken.Key);
                    isPk = true;
                    isNullable = false;
                    continue;
                }
                if (reader.Match(SqlToken.Not))
                {
                    if (reader.Match(SqlToken.Null))
                    {
                        isNullable = false;
                    }
                    continue;
                }
                if (reader.Match(SqlToken.Unique))
                {
                    isUnique = true;
                    continue;
                }
                if (reader.Match(SqlToken.References))
                {
                    var fk = ParseReferencesTarget(reader, cteNames, colName);
                    if (fk != null)
                    {
                        inlineFk = fk;
                        refTables.Add(fk.TargetTable);
                        foreignKeys.Add(fk);
                    }
                    continue;
                }
            }

            reader.Advance();
        }

        return new SqlColumnDefAst(colName, dataType, isPk, isNullable, isUnique, inlineFk);
    }

    private static string ReadColumnDataType(SqlTokenReader reader)
    {
        if (reader.IsAtEnd || reader.Current.Kind is SqlToken.Comma or SqlToken.RParen)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        var baseType = reader.ReadMultipartIdentifier();
        if (string.IsNullOrWhiteSpace(baseType))
        {
            return string.Empty;
        }
        sb.Append(baseType);

        // Handle multi-word SQL types like DOUBLE PRECISION, CHARACTER VARYING, TIMESTAMP WITH TIME ZONE
        while (!reader.IsAtEnd && reader.Current.Kind == SqlToken.Identifier)
        {
            var next = reader.Current.ToStringValue();
            if (next.Equals("PRECISION", StringComparison.OrdinalIgnoreCase) ||
                next.Equals("VARYING", StringComparison.OrdinalIgnoreCase) ||
                next.Equals("UNSIGNED", StringComparison.OrdinalIgnoreCase) ||
                next.Equals("TIME", StringComparison.OrdinalIgnoreCase) ||
                next.Equals("ZONE", StringComparison.OrdinalIgnoreCase) ||
                next.Equals("WITHOUT", StringComparison.OrdinalIgnoreCase))
            {
                sb.Append(' ').Append(next);
                reader.Advance();
                continue;
            }
            break;
        }

        if (reader.Match(SqlToken.LParen))
        {
            sb.Append('(');
            var first = true;
            var pDepth = 1;
            while (!reader.IsAtEnd && pDepth > 0)
            {
                if (reader.Match(SqlToken.LParen)) { pDepth++; sb.Append('('); continue; }
                if (reader.Match(SqlToken.RParen))
                {
                    pDepth--;
                    if (pDepth == 0) { sb.Append(')'); break; }
                    sb.Append(')');
                    continue;
                }
                if (reader.Match(SqlToken.Comma)) { sb.Append(','); first = true; continue; }
                if (!first) sb.Append(' ');
                sb.Append(reader.Current.ToStringValue());
                first = false;
                reader.Advance();
            }
        }

        // Array suffix [] (PostgreSQL)
        if (reader.Match(SqlToken.LBracket) && reader.Match(SqlToken.RBracket))
        {
            sb.Append("[]");
        }

        return sb.ToString();
    }

    private SqlForeignKeyAst? ParseReferencesTarget(SqlTokenReader reader, HashSet<string> cteNames, string? fromCol)
    {
        var rawTarget = reader.ReadMultipartIdentifier();
        if (string.IsNullOrWhiteSpace(rawTarget)) return null;

        var (db, schema, tableName) = ParseTableIdentifier(rawTarget);
        if (string.IsNullOrWhiteSpace(tableName)) return null;

        string? toCol = null;
        if (reader.Match(SqlToken.LParen))
        {
            var toCols = ReadIdentifierListUntilRightParen(reader);
            toCol = toCols.Count > 0 ? string.Join(",", toCols) : null;
        }

        var targetRef = new SqlTableRefAst(db, schema, tableName, null, IsTemporaryTable(tableName), false, false, false);
        return new SqlForeignKeyAst(fromCol, targetRef, toCol);
    }

    private static List<string> ReadIdentifierListUntilRightParen(SqlTokenReader reader)
    {
        var items = new List<string>();
        var depth = 1;
        while (!reader.IsAtEnd && depth > 0)
        {
            if (reader.Match(SqlToken.LParen)) { depth++; continue; }
            if (reader.Match(SqlToken.RParen)) { depth--; continue; }
            if (reader.Match(SqlToken.Comma)) continue;

            if (depth == 1)
            {
                var raw = reader.ReadIdentifierOrString();
                if (!string.IsNullOrWhiteSpace(raw))
                {
                    var cleaned = CleanIdentifier(raw);
                    if (!string.IsNullOrWhiteSpace(cleaned))
                    {
                        items.Add(cleaned);
                    }
                    continue;
                }
            }
            reader.Advance();
        }
        return items;
    }

    private static void SkipToNextTableBodyItem(SqlTokenReader reader)
    {
        var depth = 0;
        while (!reader.IsAtEnd)
        {
            if (depth == 0 && reader.Current.Kind is SqlToken.Comma or SqlToken.RParen or SqlToken.Semicolon)
            {
                return;
            }
            if (reader.Match(SqlToken.LParen)) { depth++; continue; }
            if (reader.Match(SqlToken.RParen)) { if (depth > 0) depth--; continue; }
            reader.Advance();
        }
    }

    protected SqlAlterTableStatementAst? ParseAlterTableExtended(SqlTokenReader reader)
    {
        reader.Match(SqlToken.Alter);
        if (!reader.MatchAny(SqlToken.Table, SqlToken.View)) return null;
        if (reader.Match(SqlToken.If))
        {
            reader.Match(SqlToken.Exists);
        }

        var emptyCtes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var rawTarget = reader.ReadMultipartIdentifier();
        if (string.IsNullOrWhiteSpace(rawTarget)) return null;
        var (tDb, tSchema, tName) = ParseTableIdentifier(rawTarget);
        if (string.IsNullOrWhiteSpace(tName)) return null;
        var target = new SqlTableRefAst(tDb, tSchema, tName, null, IsTemporaryTable(tName), false, false, true);

        var refs = new List<SqlTableRefAst>();
        var addedCols = new List<SqlColumnDefAst>();
        var foreignKeys = new List<SqlForeignKeyAst>();

        while (!reader.IsAtEnd && reader.Current.Kind != SqlToken.Semicolon)
        {
            if (reader.CurrentEqualsIgnoreCase(AddKeyword))
            {
                reader.Advance();
                if (reader.Match(SqlToken.Constraint))
                {
                    reader.ReadIdentifierOrString();
                }

                if (reader.Match(SqlToken.Foreign))
                {
                    reader.Match(SqlToken.Key);
                    string? fromCol = null;
                    if (reader.Match(SqlToken.LParen))
                    {
                        var cols = ReadIdentifierListUntilRightParen(reader);
                        fromCol = cols.Count > 0 ? string.Join(",", cols) : null;
                    }
                    if (reader.Match(SqlToken.References))
                    {
                        var fk = ParseReferencesTarget(reader, emptyCtes, fromCol);
                        if (fk != null)
                        {
                            refs.Add(fk.TargetTable);
                            foreignKeys.Add(fk);
                        }
                    }
                    continue;
                }

                var explicitColumn = reader.CurrentEqualsIgnoreCase(ColumnKeyword);
                if (explicitColumn) reader.Advance();
                if (reader.Match(SqlToken.If))
                {
                    reader.Match(SqlToken.Not);
                    reader.Match(SqlToken.Exists);
                }

                if (!IsTableConstraintStart(reader) &&
                    reader.Current.Kind is SqlToken.Identifier or SqlToken.QuotedIdentifier)
                {
                    var col = ParseColumnDefinition(reader, emptyCtes, refs, foreignKeys);
                    if (col != null)
                    {
                        addedCols.Add(col);
                    }
                    continue;
                }
            }

            if (reader.Match(SqlToken.References))
            {
                var fk = ParseReferencesTarget(reader, emptyCtes, null);
                if (fk != null)
                {
                    refs.Add(fk.TargetTable);
                    foreignKeys.Add(fk);
                }
                continue;
            }

            reader.Advance();
        }

        return new SqlAlterTableStatementAst(target, refs, addedCols, foreignKeys);
    }

    protected SqlCreateProcedureStatementAst? ParseCreateTrigger(SqlTokenReader reader, List<SqlCteAst> ctes)
    {
        if (reader.Match(SqlToken.If))
        {
            reader.Match(SqlToken.Not);
            reader.Match(SqlToken.Exists);
        }

        var trigToken = reader.ReadMultipartIdentifier();
        if (string.IsNullOrEmpty(trigToken)) return null;
        var (db, schema, trigName) = ParseTableIdentifier(trigToken);
        var procRef = new SqlProcRefAst(db, schema, trigName);

        SqlTableRefAst? triggerTable = null;
        var eventParts = new List<string>();
        var emptyCtes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Parse timing/events and ON <table>
        // T-SQL: CREATE TRIGGER trg ON dbo.Orders AFTER INSERT, UPDATE AS BEGIN ... END
        // Postgres/MySQL/SQLite: CREATE TRIGGER trg AFTER INSERT ON orders FOR EACH ROW EXECUTE FUNCTION fn() / BEGIN ... END
        while (!reader.IsAtEnd)
        {
            if (reader.Match(SqlToken.On))
            {
                var rawTbl = reader.ReadMultipartIdentifier();
                if (!string.IsNullOrWhiteSpace(rawTbl))
                {
                    var (tDb, tSchema, tName) = ParseTableIdentifier(rawTbl);
                    if (!string.IsNullOrWhiteSpace(tName))
                    {
                        triggerTable = new SqlTableRefAst(tDb, tSchema, tName, null, IsTemporaryTable(tName), false, false, false);
                    }
                }
                continue;
            }

            if (reader.CurrentEqualsIgnoreCase(BeforeKeyword) ||
                reader.CurrentEqualsIgnoreCase(AfterKeyword) ||
                reader.CurrentEqualsIgnoreCase(InsteadKeyword) ||
                reader.CurrentEqualsIgnoreCase(OfKeyword) ||
                reader.Current.Kind is SqlToken.Insert or SqlToken.Update or SqlToken.Delete)
            {
                eventParts.Add(reader.Current.ToStringValue().ToUpperInvariant());
                reader.Advance();
                continue;
            }

            if (reader.Match(SqlToken.Or) || reader.Match(SqlToken.Comma))
            {
                continue;
            }

            if (reader.CurrentEqualsIgnoreCase(ForKeyword) ||
                reader.CurrentEqualsIgnoreCase(EachKeyword) ||
                reader.CurrentEqualsIgnoreCase(RowKeyword) ||
                reader.CurrentEqualsIgnoreCase(StatementKeyword))
            {
                reader.Advance();
                continue;
            }

            if (reader.CurrentEqualsIgnoreCase(WhenKeyword))
            {
                reader.Advance();
                if (reader.Match(SqlToken.LParen))
                {
                    reader.SkipToMatchingRightParen();
                }
                continue;
            }

            // PostgreSQL: EXECUTE FUNCTION fn_name() / EXECUTE PROCEDURE proc_name()
            if (reader.MatchAny(SqlToken.Execute, SqlToken.Exec))
            {
                reader.MatchAny(SqlToken.Function, SqlToken.Procedure, SqlToken.Proc);
                var fnToken = reader.ReadMultipartIdentifier();
                if (reader.Match(SqlToken.LParen))
                {
                    reader.SkipToMatchingRightParen();
                }
                var body = new List<SqlStatementAst>();
                if (!string.IsNullOrWhiteSpace(fnToken))
                {
                    var (fDb, fSchema, fName) = ParseTableIdentifier(fnToken);
                    if (!string.IsNullOrWhiteSpace(fName))
                    {
                        body.Add(new SqlExecStatementAst(new SqlProcRefAst(fDb, fSchema, fName)));
                    }
                }
                var trigEvent = eventParts.Count > 0 ? string.Join(" ", eventParts) : null;
                return new SqlCreateProcedureStatementAst(procRef, string.Empty, body, SqlRoutineKind.Trigger, triggerTable, trigEvent) { Ctes = ctes };
            }

            if (reader.Match(SqlToken.As) || IsBeginToken(reader))
            {
                break;
            }

            if (reader.Current.Kind == SqlToken.Semicolon)
            {
                break;
            }

            reader.Advance();
        }

        var triggerBody = ParseRoutineBodyStatements(reader);
        var eventSummary = eventParts.Count > 0 ? string.Join(" ", eventParts) : null;
        return new SqlCreateProcedureStatementAst(procRef, string.Empty, triggerBody, SqlRoutineKind.Trigger, triggerTable, eventSummary) { Ctes = ctes };
    }

    protected List<SqlStatementAst> ParseRoutineBodyStatements(SqlTokenReader reader)
    {
        var bodyStatements = new List<SqlStatementAst>();

        // PostgreSQL dollar-quoted body: AS $$ BEGIN ... END; $$ LANGUAGE plpgsql;
        if (!reader.IsAtEnd && reader.Current.Kind == SqlToken.StringLiteral)
        {
            var rawLit = reader.Current.ToStringValue();
            reader.Advance();
            var innerSql = UnwrapDollarOrQuotedBody(rawLit);
            if (!string.IsNullOrWhiteSpace(innerSql))
            {
                var nestedScript = Parse(innerSql);
                bodyStatements.AddRange(nestedScript.Statements);
            }
            // Skip trailing LANGUAGE plpgsql;
            while (!reader.IsAtEnd && reader.Current.Kind != SqlToken.Semicolon)
            {
                if (reader.CurrentEqualsIgnoreCase(LanguageKeyword))
                {
                    reader.Advance();
                    reader.ReadIdentifierOrString();
                    continue;
                }
                reader.Advance();
            }
            return bodyStatements;
        }

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

                reader.Advance();
                break;
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

        return bodyStatements;
    }

    private static string UnwrapDollarOrQuotedBody(string rawLiteral)
    {
        if (string.IsNullOrWhiteSpace(rawLiteral) || rawLiteral.Length < 2)
        {
            return string.Empty;
        }

        if (rawLiteral.StartsWith("$$", StringComparison.Ordinal) && rawLiteral.EndsWith("$$", StringComparison.Ordinal) && rawLiteral.Length >= 4)
        {
            return rawLiteral[2..^2];
        }

        if (rawLiteral[0] == '$')
        {
            var secondDollar = rawLiteral.IndexOf('$', 1);
            if (secondDollar > 0)
            {
                var tag = rawLiteral[..(secondDollar + 1)];
                if (rawLiteral.EndsWith(tag, StringComparison.Ordinal) && rawLiteral.Length >= tag.Length * 2)
                {
                    return rawLiteral[tag.Length..^tag.Length];
                }
            }
        }

        if (rawLiteral[0] == '\'' && rawLiteral[^1] == '\'')
        {
            return rawLiteral[1..^1].Replace("''", "'", StringComparison.Ordinal);
        }

        return rawLiteral;
    }
}
