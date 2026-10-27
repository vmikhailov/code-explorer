using System.Text.RegularExpressions;
using CodeExplorer.Common;
using CodeExplorer.Core.Analysis;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace CodeExplorer.Core.Parser;

public class SqlDependencyVisitor : TSqlFragmentVisitor
{
    public List<(string? Db, string? Schema, string Table)> Tables { get; } = [];
    public List<(string? Db, string? Schema, string Procedure)> Procedures { get; } = [];
    public HashSet<string> CteNames { get; } = new(StringComparer.OrdinalIgnoreCase);

    private void AddSchemaObject(SchemaObjectName? schemaObject)
    {
        if (schemaObject == null) return;
        var db = NestedSqlParser.CleanSqlIdentifier(schemaObject.DatabaseIdentifier?.Value ?? "");
        var schema = NestedSqlParser.CleanSqlIdentifier(schemaObject.SchemaIdentifier?.Value ?? "");
        var table = NestedSqlParser.CleanSqlIdentifier(schemaObject.BaseIdentifier?.Value ?? "");
        if (!string.IsNullOrEmpty(table) && !NestedSqlParser.IsSqlKeyword(table))
        {
            Tables.Add((string.IsNullOrEmpty(db) ? null : db, string.IsNullOrEmpty(schema) ? null : schema, table));
        }
    }

    public override void Visit(CommonTableExpression node)
    {
        var cteName = NestedSqlParser.CleanSqlIdentifier(node.ExpressionName?.Value ?? "");
        if (!string.IsNullOrEmpty(cteName))
        {
            CteNames.Add(cteName);
        }
        base.Visit(node);
    }

    public override void Visit(NamedTableReference node)
    {
        AddSchemaObject(node.SchemaObject);
        base.Visit(node);
    }

    public override void Visit(CreateTableStatement node)
    {
        AddSchemaObject(node.SchemaObjectName);
        base.Visit(node);
    }

    public override void Visit(AlterTableStatement node)
    {
        AddSchemaObject(node.SchemaObjectName);
        base.Visit(node);
    }

    public override void Visit(DropTableStatement node)
    {
        if (node.Objects != null)
        {
            foreach (var obj in node.Objects)
            {
                AddSchemaObject(obj);
            }
        }
        base.Visit(node);
    }

    public override void Visit(TruncateTableStatement node)
    {
        AddSchemaObject(node.TableName);
        base.Visit(node);
    }

    public override void Visit(CreateViewStatement node)
    {
        AddSchemaObject(node.SchemaObjectName);
        base.Visit(node);
    }

    public override void Visit(ForeignKeyConstraintDefinition node)
    {
        AddSchemaObject(node.ReferenceTableName);
        base.Visit(node);
    }

    public override void Visit(ExecutableProcedureReference node)
    {
        if (node.ProcedureReference?.ProcedureReference?.Name != null)
        {
            var db = NestedSqlParser.CleanSqlIdentifier(node.ProcedureReference.ProcedureReference.Name.DatabaseIdentifier?.Value ?? "");
            var schema = NestedSqlParser.CleanSqlIdentifier(node.ProcedureReference.ProcedureReference.Name.SchemaIdentifier?.Value ?? "");
            var proc = NestedSqlParser.CleanSqlIdentifier(node.ProcedureReference.ProcedureReference.Name.BaseIdentifier?.Value ?? "");
            if (!string.IsNullOrEmpty(proc))
            {
                Procedures.Add((string.IsNullOrEmpty(db) ? null : db, string.IsNullOrEmpty(schema) ? null : schema, proc));
            }
        }
        base.Visit(node);
    }
}

public static class NestedSqlParser
{
    private static readonly HashSet<string> SqlKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "SELECT", "INSERT", "UPDATE", "DELETE", "MERGE",
        "CREATE", "ALTER", "DROP", "TRUNCATE", "WITH"
    };

    private static bool FastCheckSqlCandidate(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        int i = 0;
        while (i < text.Length)
        {
            while (i < text.Length && (char.IsWhiteSpace(text[i]) || text[i] is '"' or '\'' or '`' or '('))
            {
                i++;
            }
            if (i + 1 < text.Length && text[i] == '-' && text[i + 1] == '-')
            {
                i += 2;
                while (i < text.Length && text[i] != '\n') i++;
                continue;
            }
            if (i + 1 < text.Length && text[i] == '/' && text[i + 1] == '*')
            {
                i += 2;
                while (i + 1 < text.Length && !(text[i] == '*' && text[i + 1] == '/')) i++;
                i += 2;
                continue;
            }
            break;
        }
        if (i >= text.Length) return false;
        char c = char.ToUpperInvariant(text[i]);
        return c is 'S' or 'I' or 'U' or 'D' or 'M' or 'C' or 'A' or 'T' or 'W';
    }

    public static string StripSqlComments(string sql)
    {
        if (string.IsNullOrEmpty(sql)) return sql;
        return Regex.Replace(sql, @"('(?:''|[^'])*'|""(?:""""|[^""])*"")|--[^\r\n]*|/\*[\s\S]*?\*/", m => m.Groups[1].Success ? m.Groups[1].Value : " ");
    }

    public static bool TryParseSql(string text, out string? firstWord, out string cleanedSql)
    {
        firstWord = null;
        cleanedSql = text;
        if (!FastCheckSqlCandidate(text))
        {
            return false;
        }

        cleanedSql = CleanQueryText(text).Trim();
        if (string.IsNullOrEmpty(cleanedSql)) return false;

        // Reject natural language sentences or UI prompt labels (e.g. "Select person.", "Select:")
        if (cleanedSql.EndsWith('.') || cleanedSql.EndsWith('!') ||
            cleanedSql.Contains("<div", StringComparison.OrdinalIgnoreCase) ||
            cleanedSql.Contains("<span", StringComparison.OrdinalIgnoreCase) ||
            cleanedSql.Contains("</", StringComparison.OrdinalIgnoreCase) ||
            cleanedSql.Contains("/>", StringComparison.OrdinalIgnoreCase) ||
            cleanedSql.Contains("&nbsp;", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var uncommented = StripSqlComments(cleanedSql);
        var match = Regex.Match(uncommented, @"^\s*([a-zA-Z]+)\b");
        if (match.Success)
        {
            var word = match.Groups[1].Value.ToUpperInvariant();
            if (SqlKeywords.Contains(word))
            {
                // Reject colon right after the keyword (e.g. "Select: Person", "Update:")
                if (Regex.IsMatch(uncommented, @"^\s*[a-zA-Z]+:\s*"))
                {
                    return false;
                }

                // Verify structural syntax for SQL statements to avoid false positives (e.g. Swagger / UI strings)
                if (word == "SELECT")
                {
                    var isScalar = Regex.IsMatch(uncommented, @"^\s*SELECT\s+(?:[\d@'""\(]|COUNT\(|NOW\(|CURRENT_|@@|TRUE\b|FALSE\b|NULL\b)", RegexOptions.IgnoreCase);
                    var hasFrom = Regex.Match(uncommented, @"\bFROM\s+([a-zA-Z0-9_\.\[\]""`#@']+)", RegexOptions.IgnoreCase);

                    if (!isScalar && !hasFrom.Success)
                    {
                        return false;
                    }

                    if (hasFrom.Success)
                    {
                        var targetTable = CleanSqlIdentifier(hasFrom.Groups[1].Value);
                        if (IsSqlKeyword(targetTable))
                        {
                            return false;
                        }
                    }
                }
                else if (word == "INSERT")
                {
                    var insertMatch = Regex.Match(uncommented, @"^\s*INSERT\s+(?:OR\s+(?:REPLACE|IGNORE|ABORT|FAIL|ROLLBACK)\s+|IGNORE\s+)?INTO\s+([a-zA-Z0-9_\.\[\]""`#@']+)", RegexOptions.IgnoreCase);
                    if (!insertMatch.Success)
                    {
                        return false;
                    }
                    var targetTable = CleanSqlIdentifier(insertMatch.Groups[1].Value);
                    if (IsSqlKeyword(targetTable))
                    {
                        return false;
                    }
                }
                else if (word == "UPDATE")
                {
                    var updateMatch = Regex.Match(uncommented, @"^\s*UPDATE\s+(?:OR\s+(?:REPLACE|IGNORE|ABORT|FAIL|ROLLBACK)\s+)?([a-zA-Z0-9_\.\[\]""`#@']+)\s+SET\b", RegexOptions.IgnoreCase);
                    if (!updateMatch.Success)
                    {
                        return false;
                    }
                    var targetTable = CleanSqlIdentifier(updateMatch.Groups[1].Value);
                    if (IsSqlKeyword(targetTable))
                    {
                        return false;
                    }
                }
                else if (word == "DELETE")
                {
                    var deleteMatch = Regex.Match(uncommented, @"^\s*DELETE\s+(?:FROM\s+|[a-zA-Z0-9_]+\s+FROM\s+)([a-zA-Z0-9_\.\[\]""`#@']+)", RegexOptions.IgnoreCase);
                    if (!deleteMatch.Success)
                    {
                        return false;
                    }
                    var targetTable = CleanSqlIdentifier(deleteMatch.Groups[1].Value);
                    if (IsSqlKeyword(targetTable))
                    {
                        return false;
                    }
                }
                else if (word == "MERGE")
                {
                    var mergeMatch = Regex.Match(uncommented, @"^\s*MERGE\s+(?:INTO\s+)?([a-zA-Z0-9_\.\[\]""`#@']+)\s+(?:USING\b|AS\b)", RegexOptions.IgnoreCase);
                    if (!mergeMatch.Success)
                    {
                        return false;
                    }
                    var targetTable = CleanSqlIdentifier(mergeMatch.Groups[1].Value);
                    if (IsSqlKeyword(targetTable))
                    {
                        return false;
                    }
                }
                else if (word == "CREATE")
                {
                    var createMatch = Regex.Match(uncommented, @"^\s*CREATE\s+(?:OR\s+REPLACE\s+)?(?:TEMP(?:ORARY)?\s+|EXTERNAL\s+|MATERIALIZED\s+|UNIQUE\s+)*?(?:TABLE|VIEW)\s+(?:IF\s+NOT\s+EXISTS\s+)?([a-zA-Z0-9_\.\[\]""`#@']+)", RegexOptions.IgnoreCase);
                    if (!createMatch.Success)
                    {
                        return false;
                    }
                    var targetTable = CleanSqlIdentifier(createMatch.Groups[1].Value);
                    if (IsSqlKeyword(targetTable))
                    {
                        return false;
                    }
                }
                else if (word is "ALTER" or "DROP")
                {
                    var ddlMatch = Regex.Match(uncommented, @"^\s*(?:ALTER|DROP)\s+(?:TABLE|VIEW)\s+(?:IF\s+EXISTS\s+)?([a-zA-Z0-9_\.\[\]""`#@']+)", RegexOptions.IgnoreCase);
                    if (!ddlMatch.Success)
                    {
                        return false;
                    }
                    var targetTable = CleanSqlIdentifier(ddlMatch.Groups[1].Value);
                    if (IsSqlKeyword(targetTable))
                    {
                        return false;
                    }
                }
                else if (word == "TRUNCATE")
                {
                    var truncMatch = Regex.Match(uncommented, @"^\s*TRUNCATE\s+TABLE\s+([a-zA-Z0-9_\.\[\]""`#@']+)", RegexOptions.IgnoreCase);
                    if (!truncMatch.Success)
                    {
                        return false;
                    }
                    var targetTable = CleanSqlIdentifier(truncMatch.Groups[1].Value);
                    if (IsSqlKeyword(targetTable))
                    {
                        return false;
                    }
                }
                else if (word == "WITH")
                {
                    var cteMatch = Regex.IsMatch(uncommented, @"^\s*WITH\s+(?:RECURSIVE\s+)?[a-zA-Z0-9_\[\]""`]+(?:\s*\([^\)]*\))?\s+AS\s*\(", RegexOptions.IgnoreCase) &&
                                   Regex.IsMatch(uncommented, @"\b(?:SELECT|INSERT|UPDATE|DELETE|MERGE)\b", RegexOptions.IgnoreCase);
                    if (!cteMatch)
                    {
                        return false;
                    }
                }

                firstWord = word;
                return true;
            }
        }

        return false;
    }

    public static string CleanQueryText(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        
        var cleaned = text.Trim();

        // Strip Python/Rust/raw string prefixes (f, r, b, u) before quotes
        var pLen = 0;
        while (pLen < cleaned.Length && pLen < 2 && char.ToLowerInvariant(cleaned[pLen]) is 'f' or 'r' or 'b' or 'u')
        {
            pLen++;
        }
        if (pLen > 0 && pLen < cleaned.Length && cleaned[pLen] is '"' or '\'')
        {
            cleaned = cleaned[pLen..];
        }

        if (cleaned.Length >= 6 &&
            ((cleaned.StartsWith("\"\"\"", StringComparison.Ordinal) && cleaned.EndsWith("\"\"\"", StringComparison.Ordinal)) ||
             (cleaned.StartsWith("'''", StringComparison.Ordinal) && cleaned.EndsWith("'''", StringComparison.Ordinal))))
        {
            cleaned = cleaned[3..^3].Trim();
        }

        while (cleaned.Length >= 2 && 
               ((cleaned.StartsWith('"') && cleaned.EndsWith('"')) || 
                (cleaned.StartsWith('\'') && cleaned.EndsWith('\'')) || 
                (cleaned.StartsWith('`') && cleaned.EndsWith('`'))))
        {
            cleaned = cleaned[1..^1].Trim();
        }

        // Unescape standard escape sequences
        cleaned = cleaned
            .Replace("\\n", "\n")
            .Replace("\\r", "\r")
            .Replace("\\t", "\t")
            .Replace("\\\"", "\"")
            .Replace("\\'", "'")
            .Replace("\\`", "`")
            .Replace("\\$", "$");

        // 1. Resolve known constants in interpolation syntax: ${varName} or {varName}
        cleaned = Regex.Replace(cleaned, @"(?:\$|\#)?\{\s*([a-zA-Z0-9_\.]+)\s*\}", m =>
        {
            var varName = m.Groups[1].Value;
            if (ConstantRegistry.TryResolve(null, varName, out var resolved) && !string.IsNullOrWhiteSpace(resolved))
            {
                return resolved;
            }
            return m.Value;
        });

        // Strip format-placeholder schema prefixes before a dot and table name (e.g. FROM "%s".irises or FROM "{}".irises)
        cleaned = Regex.Replace(cleaned, @"[""'`]?(?:%[sdv]|\{\s*\})[""'`]?\.(?=[a-zA-Z_`""\[])", "");

        // 2. Normalize parameterized table template patterns into wildcard table families (e.g. data_tracker_${tableSuffix} -> data_tracker_*)
        cleaned = Regex.Replace(cleaned, @"(?<=[a-zA-Z0-9_]+[_\-])(?:\$|\#)?\{[^}]+\}", "*");
        cleaned = Regex.Replace(cleaned, @"(?:\$|\#)?\{[^}]+\}(?=[_\-][a-zA-Z0-9_]+)", "*");
        cleaned = Regex.Replace(cleaned, @"(?:\$|\#)?\{\s*(?:[a-zA-Z0-9_\.]*(?:suffix|date|part|shard)[a-zA-Z0-9_\.]*)\s*\}", "*", RegexOptions.IgnoreCase);
        // Collapse consecutive wildcards in table patterns (e.g. table_*_* -> table_*)
        cleaned = Regex.Replace(cleaned, @"(?<=[a-zA-Z0-9_]+)[\*_\-]+(?:\*[\*_\-]*)+", "_*");

        cleaned = Regex.Replace(cleaned, @"\$\{(.*?)\}", "$1");

        // Convert backticks to square brackets for ScriptDom T-SQL parser compatibility.
        // If backticks contain dots (e.g. `dataset.table`), wrap each segment individually.
        cleaned = Regex.Replace(cleaned, @"`([^`]+)`", m =>
        {
            var content = m.Groups[1].Value;
            if (content.Contains('.'))
            {
                var segs = content.Split('.');
                return string.Join(".", segs.Select(s => $"[{s.Trim('[', ']', '`', '\'', '"')}]"));
            }
            return $"[{content.Trim('[', ']', '`', '\'', '"')}]";
        });

        return cleaned;
    }

    public static QueryNode? ParseNestedSql(string rawText, string id, string filePath, ParsingContext? ctx = null, string? customQueryName = null, int startLine = 0, int endLine = 0)
    {
        if (!TryParseSql(rawText, out var firstWord, out var cleanedSql))
        {
            return null;
        }

        var queryName = !string.IsNullOrEmpty(customQueryName) ? customQueryName : $"{firstWord} Query";
        var verb = (firstWord ?? "SELECT").ToUpperInvariant();
        var queryExt = new Dictionary<string, string>
        {
            ["verb"] = verb,
            [OntologyConstants.PropertyNames.Operation] = verb,
            [OntologyConstants.PropertyNames.EvidenceKind] = OntologyConstants.EvidenceKinds.Query,
            [OntologyConstants.PropertyNames.FilePath] = filePath
        };
        if (startLine > 0) queryExt[OntologyConstants.PropertyNames.Line] = startLine.ToString();
        if (endLine > 0) queryExt[OntologyConstants.PropertyNames.LineEnd] = endLine.ToString();

        var queryNode = new QueryNode(
            id,
            queryName,
            cleanedSql,
            filePath,
            startLine,
            endLine,
            queryExt
        );

        BuildSqlHierarchy(cleanedSql, rawText, queryNode, filePath, ctx, verb, startLine, endLine);

        return queryNode;
    }

    private static string ResolveSqlIdentifier(string? part, string rawText)
    {
        if (string.IsNullOrEmpty(part)) return "";
        var clean = part.Trim().Trim('`', '"', '\'', '[', ']');
        if (ConstantRegistry.TryResolve(null, clean, out var resolved) &&
            !string.IsNullOrWhiteSpace(resolved) &&
            PostIndexAnalyzer.IsValidTableIdentifier(resolved))
        {
            return resolved;
        }
        return clean;
    }

    private static bool IsUnresolvedVariable(string? part, string rawText)
    {
        if (string.IsNullOrEmpty(part)) return false;
        var clean = part.Trim().Trim('`', '"', '\'', '[', ']');
        if (ConstantRegistry.TryResolve(null, clean, out _)) return false;
        if (rawText.Contains($"{{{part}}}") || rawText.Contains($"${part}") || part.StartsWith('$') || part.StartsWith('{'))
            return true;
        return false;
    }

    private static string NormalizeDialectForTSqlParser(string sql)
    {
        if (string.IsNullOrEmpty(sql)) return sql;
        var norm = Regex.Replace(sql, @"\bIF\s+NOT\s+EXISTS\b", "", RegexOptions.IgnoreCase);
        norm = Regex.Replace(norm, @"\bAUTOINCREMENT\b", "IDENTITY(1,1)", RegexOptions.IgnoreCase);
        norm = Regex.Replace(norm, @"\bINSERT\s+OR\s+(?:REPLACE|IGNORE|ABORT|FAIL|ROLLBACK)\s+INTO\b", "INSERT INTO", RegexOptions.IgnoreCase);
        // Replace positional '?' placeholders outside single/double quotes with valid T-SQL variable '@p'
        norm = Regex.Replace(norm, @"('[^']*'|""[^""]*"")|\?", m => m.Groups[1].Success ? m.Groups[1].Value : "@p");
        return norm;
    }

    private static void ExtractSqlDependencies(
        string cleanedSql,
        string rawText,
        HashSet<(string? Db, string? Schema, string Table)> tables,
        HashSet<(string? Db, string? Schema, string Procedure)> procedures)
    {
        cleanedSql = StripSqlComments(cleanedSql);

        var cteNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match cteMatch in Regex.Matches(cleanedSql, @"(?:\bWITH\s+(?:RECURSIVE\s+)?|,\s*)([a-zA-Z0-9_\[\]""`]+)(?:\s*\([^\)]*\))?\s+AS\s*\(", RegexOptions.IgnoreCase))
        {
            var cte = CleanSqlIdentifier(cteMatch.Groups[1].Value);
            if (!string.IsNullOrEmpty(cte)) cteNames.Add(cte);
        }

        // 1. Try Grammar Parser (ScriptDom)
        try
        {
            var parser = new TSql160Parser(true);

            using var reader = new StringReader(NormalizeDialectForTSqlParser(cleanedSql));

            var fragment = parser.Parse(reader, out _);

            var visitor = new SqlDependencyVisitor();
            fragment?.Accept(visitor);

            foreach (var c in visitor.CteNames) cteNames.Add(c);

            foreach (var t in visitor.Tables)
            {
                var table = ResolveSqlIdentifier(t.Table, rawText);
                var schema = ResolveSqlIdentifier(t.Schema, rawText);
                var db = ResolveSqlIdentifier(t.Db, rawText);

                if (cteNames.Contains(table)) continue;
                if (IsSqlKeyword(table)) continue;
                if (!PostIndexAnalyzer.IsValidTableIdentifier(table)) continue;
                if (!string.IsNullOrEmpty(schema) && !string.Equals(schema, "default", StringComparison.OrdinalIgnoreCase) && IsSqlKeyword(schema)) continue;

                if (IsUnresolvedVariable(table, rawText) || IsUnresolvedVariable(schema, rawText) ||
                    IsUnresolvedVariable(db, rawText)) continue;

                tables.Add((string.IsNullOrEmpty(db) ? null : db, string.IsNullOrEmpty(schema) ? null : schema,
                    table));
            }

            foreach (var p in visitor.Procedures)
            {
                var proc = ResolveSqlIdentifier(p.Procedure, rawText);
                var schema = ResolveSqlIdentifier(p.Schema, rawText);
                var db = ResolveSqlIdentifier(p.Db, rawText);

                if (IsUnresolvedVariable(proc, rawText) || IsUnresolvedVariable(schema, rawText) ||
                    IsUnresolvedVariable(db, rawText)) continue;

                procedures.Add((string.IsNullOrEmpty(db) ? null : db, string.IsNullOrEmpty(schema) ? null : schema,
                    proc));
            }
        }
        catch
        {
            // Ignore syntax errors, fall back to regex
        }

        // 2. Lexical Fallback: Match identifiers after DML (FROM, JOIN, UPDATE, INTO, MERGE, REFERENCES) and DDL (CREATE/ALTER/DROP/TRUNCATE TABLE/VIEW)
        var tableMatches = Regex.Matches(
            cleanedSql,
            @"\b(?:FROM|JOIN|(?<!FOR\s+)UPDATE(?:\s+OR\s+(?:REPLACE|IGNORE|ABORT|FAIL|ROLLBACK))?|INTO|MERGE|REFERENCES|CREATE\s+(?:OR\s+REPLACE\s+)?(?:EXTERNAL\s+|MATERIALIZED\s+|UNIQUE\s+)*?(?:TABLE|VIEW)\s+(?:IF\s+NOT\s+EXISTS\s+)?|(?:ALTER|DROP)\s+(?:TABLE|VIEW)\s+(?:IF\s+EXISTS\s+)?|TRUNCATE\s+TABLE)\s+(?!(?:EXTERNAL_QUERY|UNNEST|GENERATE_SERIES)\s*\()([a-zA-Z0-9_\.\[\]""#@'`\$\{\}\*]+)",
            RegexOptions.IgnoreCase);
        foreach (Match match in tableMatches)
        {
            // If the matched identifier is immediately followed by '(', it is a function call unless it is a SQL Server table hint like (NOLOCK)
            var afterMatch = cleanedSql.AsSpan(match.Index + match.Length).TrimStart();
            if (afterMatch.StartsWith("(", StringComparison.Ordinal))
            {
                if (!Regex.IsMatch(afterMatch.ToString(), @"^\(\s*(?:NOLOCK|READUNCOMMITTED|UPDLOCK|REPEATABLEREAD|SERIALIZABLE|READCOMMITTED|TABLOCK|TABLOCKX|PAGLOCK|ROWLOCK|NOWAIT|READPAST|XLOCK|SNAPSHOT|INDEX|FORCESEEK)\b", RegexOptions.IgnoreCase))
                {
                    continue;
                }
            }

            var rawTableName = match.Groups[1].Value.Trim();
            var parts = rawTableName.Split('.')
                .Select(CleanSqlIdentifier)
                .Where(p => !string.IsNullOrEmpty(p))
                .ToArray();

            if (parts.Length == 0) continue;

            string? dbName = null;
            string? schemaName = null;
            string tableName;

            if (parts.Length >= 3)
            {
                dbName = parts[0];
                schemaName = parts[1];
                tableName = parts[2];
            }
            else if (parts.Length == 2)
            {
                schemaName = parts[0];
                tableName = parts[1];
            }
            else
            {
                tableName = parts[0];
            }

            tableName = ResolveSqlIdentifier(tableName, rawText);
            schemaName = ResolveSqlIdentifier(schemaName, rawText);
            dbName = ResolveSqlIdentifier(dbName, rawText);

            if (cteNames.Contains(tableName)) continue;
            if (IsSqlKeyword(tableName) || IsSqlKeyword(rawTableName)) continue;
            if (!PostIndexAnalyzer.IsValidTableIdentifier(tableName)) continue;
            if (!string.IsNullOrEmpty(schemaName) && !string.Equals(schemaName, "default", StringComparison.OrdinalIgnoreCase) && IsSqlKeyword(schemaName)) continue;
            if (IsUnresolvedVariable(tableName, rawText) || IsUnresolvedVariable(schemaName, rawText) || IsUnresolvedVariable(dbName, rawText)) continue;
            tables.Add((string.IsNullOrEmpty(dbName) ? null : dbName, string.IsNullOrEmpty(schemaName) ? null : schemaName, tableName));
        }

        // 3. Lexical Fallback: Match procedure calls after EXEC/EXECUTE
        var execMatches = Regex.Matches(cleanedSql, @"\bEXEC(?:UTE)?\s+([a-zA-Z0-9_\.\[\]""#@'`\$\{\}\*]+)", RegexOptions.IgnoreCase);
        foreach (Match match in execMatches)
        {
            var rawProcName = match.Groups[1].Value.Trim();
            var parts = rawProcName.Split('.')
                .Select(CleanSqlIdentifier)
                .Where(p => !string.IsNullOrEmpty(p))
                .ToArray();

            if (parts.Length == 0) continue;

            string? dbName = null;
            string? schemaName = null;
            string procName;

            if (parts.Length >= 3)
            {
                dbName = parts[0];
                schemaName = parts[1];
                procName = parts[2];
            }
            else if (parts.Length == 2)
            {
                schemaName = parts[0];
                procName = parts[1];
            }
            else
            {
                procName = parts[0];
            }

            procName = ResolveSqlIdentifier(procName, rawText);
            schemaName = ResolveSqlIdentifier(schemaName, rawText);
            dbName = ResolveSqlIdentifier(dbName, rawText);

            if (IsSqlKeyword(procName) || IsSqlKeyword(rawProcName)) continue;
            if (!string.IsNullOrEmpty(schemaName) && !string.Equals(schemaName, "default", StringComparison.OrdinalIgnoreCase) && IsSqlKeyword(schemaName)) continue;
            if (IsUnresolvedVariable(procName, rawText) || IsUnresolvedVariable(schemaName, rawText) || IsUnresolvedVariable(dbName, rawText)) continue;
            procedures.Add((string.IsNullOrEmpty(dbName) ? null : dbName, string.IsNullOrEmpty(schemaName) ? null : schemaName, procName));
        }
    }

    private static void BuildSqlHierarchy(
        string cleanedSql,
        string rawText,
        QueryNode queryNode,
        string filePath,
        ParsingContext? ctx = null,
        string verb = "SELECT",
        int startLine = 0,
        int endLine = 0)
    {
        var tables = new HashSet<(string? Db, string? Schema, string Table)>();
        var procedures = new HashSet<(string? Db, string? Schema, string Procedure)>();

        ExtractSqlDependencies(cleanedSql, rawText, tables, procedures);

        var dbNodes = new Dictionary<string, DatabaseNode>(StringComparer.OrdinalIgnoreCase);
        var datasetNodes = new Dictionary<string, DataSetNode>(StringComparer.OrdinalIgnoreCase);

        var colonIdx = queryNode.Id.IndexOf(':');
        var wsPrefix = colonIdx > 0 ? queryNode.Id[..colonIdx] : "";
        var dbPrefix = string.IsNullOrEmpty(wsPrefix) ? "db" : $"{wsPrefix}:db";

        // Resolve engine
        var engine = "default";
        string? concreteDbName = null;

        bool isBigQueryContext = 
            rawText.Contains("_TABLE_SUFFIX", StringComparison.OrdinalIgnoreCase) ||
            rawText.Contains("BigQuery", StringComparison.OrdinalIgnoreCase) ||
            rawText.Contains("bq.driver", StringComparison.OrdinalIgnoreCase) ||
            filePath.Contains("bq-", StringComparison.OrdinalIgnoreCase) ||
            filePath.Contains("bigquery", StringComparison.OrdinalIgnoreCase);

        bool isClickHouseContext =
            rawText.Contains("MergeTree", StringComparison.OrdinalIgnoreCase) ||
            rawText.Contains("clickhouse", StringComparison.OrdinalIgnoreCase) ||
            filePath.Contains("clickhouse", StringComparison.OrdinalIgnoreCase);

        if (isBigQueryContext)
        {
            engine = "BigQuery";
        }
        else if (isClickHouseContext)
        {
            engine = "ClickHouse";
        }
        else
        {
            var canonical = ctx?.ResourceRegistry.ResolveResource(null, expectedDbType: "relational");
            if (canonical != null)
            {
                if (!string.IsNullOrEmpty(canonical.Engine) && !canonical.Engine.Equals("Database", StringComparison.OrdinalIgnoreCase))
                {
                    engine = canonical.Engine;
                }
                else
                {
                    engine = "PostgreSQL";
                }
                if (!string.IsNullOrEmpty(canonical.Name) &&
                    !canonical.Name.Equals("Database", StringComparison.OrdinalIgnoreCase) &&
                    !canonical.Name.Equals(engine, StringComparison.OrdinalIgnoreCase) &&
                    !ResourceReconciliationService.IsGenericConfigKey(canonical.Name))
                {
                    concreteDbName = canonical.Name;
                }
            }
            else if (ctx?.ResourceRegistry.AllResources.FirstOrDefault(r => r.Kind == "Database" && (r.DbType == "relational" || r.DbType == "analytics")) is { } anyDb)
            {
                engine = !string.IsNullOrEmpty(anyDb.Engine) && !anyDb.Engine.Equals("Database", StringComparison.OrdinalIgnoreCase)
                    ? anyDb.Engine
                    : (!string.IsNullOrEmpty(anyDb.Name) && !anyDb.Name.Equals("Database", StringComparison.OrdinalIgnoreCase) ? anyDb.Name : "PostgreSQL");

                if (!string.IsNullOrEmpty(anyDb.Name) &&
                    !anyDb.Name.Equals("Database", StringComparison.OrdinalIgnoreCase) &&
                    !anyDb.Name.Equals(engine, StringComparison.OrdinalIgnoreCase) &&
                    !ResourceReconciliationService.IsGenericConfigKey(anyDb.Name) &&
                    anyDb.DeclaringFilePath != null &&
                    string.Equals(anyDb.DeclaringFilePath.Replace('\\', '/'), filePath.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase))
                {
                    concreteDbName = anyDb.Name;
                }
            }
        }

        // Process Tables
        foreach (var tableRef in tables)
        {
            var targetEngine = engine;
            if (!string.IsNullOrEmpty(tableRef.Db) && !tableRef.Db.Equals("default", StringComparison.OrdinalIgnoreCase))
            {
                var candidate = tableRef.Db;
                if (candidate.Contains('.'))
                {
                    var dotParts = candidate.Split('.', 2);
                    targetEngine = dotParts[0];
                }
                else
                {
                    targetEngine = candidate;
                }
            }

            var targetDbType = targetEngine.Equals("BigQuery", StringComparison.OrdinalIgnoreCase) || targetEngine.Equals("ClickHouse", StringComparison.OrdinalIgnoreCase)
                ? "analytics"
                : "relational";

            var defaultSchema = targetEngine.Equals("SQL Server", StringComparison.OrdinalIgnoreCase) ? "dbo"
                              : targetEngine.Equals("SQLite", StringComparison.OrdinalIgnoreCase) ? "main"
                              : targetEngine.Equals("BigQuery", StringComparison.OrdinalIgnoreCase) ? "default"
                              : targetEngine.Equals("default", StringComparison.OrdinalIgnoreCase) ? "dbo"
                              : "public";

            var isImplicitSchema = string.IsNullOrEmpty(tableRef.Schema) && string.IsNullOrEmpty(concreteDbName);
            var schemaName = !string.IsNullOrEmpty(tableRef.Schema) ? tableRef.Schema : defaultSchema;
            var tableName = tableRef.Table;
            if (!PostIndexAnalyzer.IsValidTableIdentifier(tableName)) continue;

            var fullDbName = !string.IsNullOrEmpty(concreteDbName)
                ? concreteDbName
                : $"{targetEngine}.{schemaName}";
            var dbKey = !string.IsNullOrEmpty(concreteDbName)
                ? concreteDbName.ToLowerInvariant()
                : $"{targetEngine.ToLowerInvariant()}:{schemaName.ToLowerInvariant()}";
            if (!dbNodes.TryGetValue(dbKey, out var dbNode))
            {
                var dbNodeId = $"{dbPrefix}:{dbKey}";
                var extensions = new Dictionary<string, string>
                {
                    ["name"] = fullDbName,
                    ["engine"] = targetEngine,
                    ["schema"] = schemaName,
                    ["db_type"] = targetDbType
                };
                if (isImplicitSchema)
                {
                    extensions["is_implicit_schema"] = "true";
                }
                if (WorkspaceConventions.IsTestFilePath(filePath))
                {
                    extensions["is_test"] = "true";
                    extensions["role"] = "Test";
                }
                dbNode = new DatabaseNode(dbNodeId, fullDbName, filePath, targetDbType, extensions);
                dbNodes[dbKey] = dbNode;
                queryNode.Children.Add(dbNode);
            }
            else if (!isImplicitSchema && dbNode.Extensions != null)
            {
                dbNode.Extensions.Remove("is_implicit_schema");
            }

            var schemaKey = $"{dbKey}:{schemaName.ToLowerInvariant()}";
            if (!datasetNodes.TryGetValue(schemaKey, out var schemaNode))
            {
                var schemaNodeId = $"{dbNode.Id}:{OntologyConstants.IdPrefixes.DataSet}:{schemaName.ToLowerInvariant()}";
                schemaNode = new DataSetNode(schemaNodeId, schemaName, filePath);
                dbNode.Children.Add(schemaNode);
            }

            var tableNodeId = $"{schemaNode.Id}:{OntologyConstants.IdPrefixes.Table}:{tableName.ToLowerInvariant()}";
            var tableNode = new TableNode(tableNodeId, tableName, filePath);
            
            if (!schemaNode.Children.Any(c => c.Id.Equals(tableNodeId, StringComparison.OrdinalIgnoreCase)))
            {
                schemaNode.Children.Add(tableNode);
            }

            if (ctx != null)
            {
                var relKind = verb is "INSERT" or "UPDATE" or "DELETE" or "MERGE" or "CREATE" or "ALTER" or "DROP" or "TRUNCATE"
                    ? OntologyConstants.Relationships.WritesTo
                    : OntologyConstants.Relationships.ReadsFrom;
                var relProps = new Dictionary<string, object>
                {
                    [OntologyConstants.PropertyNames.FilePath] = filePath,
                    [OntologyConstants.PropertyNames.Operation] = verb,
                    [OntologyConstants.PropertyNames.EvidenceKind] = OntologyConstants.EvidenceKinds.Query
                };
                if (startLine > 0) relProps[OntologyConstants.PropertyNames.Line] = startLine;
                if (endLine > 0) relProps[OntologyConstants.PropertyNames.LineEnd] = endLine;
                ctx.AddGlobalProjectDependency(new Database.Relationship(queryNode.Id, tableNodeId, relKind, relProps));
            }
        }

        // Process Procedures
        foreach (var procRef in procedures)
        {
            var targetEngine = engine;
            if (!string.IsNullOrEmpty(procRef.Db) && !procRef.Db.Equals("default", StringComparison.OrdinalIgnoreCase))
            {
                var candidate = procRef.Db;
                if (candidate.Contains('.'))
                {
                    var dotParts = candidate.Split('.', 2);
                    targetEngine = dotParts[0];
                }
                else
                {
                    targetEngine = candidate;
                }
            }

            var defaultSchema = targetEngine.Equals("SQL Server", StringComparison.OrdinalIgnoreCase) ? "dbo"
                              : targetEngine.Equals("SQLite", StringComparison.OrdinalIgnoreCase) ? "main"
                              : targetEngine.Equals("BigQuery", StringComparison.OrdinalIgnoreCase) ? "default"
                              : targetEngine.Equals("default", StringComparison.OrdinalIgnoreCase) ? "dbo"
                              : "public";

            var isImplicitSchema = string.IsNullOrEmpty(procRef.Schema) && string.IsNullOrEmpty(concreteDbName);
            var schemaName = !string.IsNullOrEmpty(procRef.Schema) ? procRef.Schema : defaultSchema;
            var procName = procRef.Procedure;

            var fullDbName = !string.IsNullOrEmpty(concreteDbName)
                ? concreteDbName
                : $"{targetEngine}.{schemaName}";
            var dbKey = !string.IsNullOrEmpty(concreteDbName)
                ? concreteDbName.ToLowerInvariant()
                : $"{targetEngine.ToLowerInvariant()}:{schemaName.ToLowerInvariant()}";
            if (!dbNodes.TryGetValue(dbKey, out var dbNode))
            {
                var dbNodeId = $"{dbPrefix}:{dbKey}";
                var extensions = new Dictionary<string, string>
                {
                    ["name"] = fullDbName,
                    ["engine"] = targetEngine,
                    ["schema"] = schemaName,
                    ["db_type"] = "relational"
                };
                if (isImplicitSchema)
                {
                    extensions["is_implicit_schema"] = "true";
                }
                if (WorkspaceConventions.IsTestFilePath(filePath))
                {
                    extensions["is_test"] = "true";
                    extensions["role"] = "Test";
                }
                dbNode = new DatabaseNode(dbNodeId, fullDbName, filePath, "relational", extensions);
                dbNodes[dbKey] = dbNode;
                queryNode.Children.Add(dbNode);
            }
            else if (!isImplicitSchema && dbNode.Extensions != null)
            {
                dbNode.Extensions.Remove("is_implicit_schema");
            }

            var schemaKey = $"{dbKey}:{schemaName.ToLowerInvariant()}";
            if (!datasetNodes.TryGetValue(schemaKey, out var schemaNode))
            {
                var schemaNodeId = $"{dbNode.Id}:{OntologyConstants.IdPrefixes.DataSet}:{schemaName.ToLowerInvariant()}";
                schemaNode = new DataSetNode(schemaNodeId, schemaName, filePath);
                dbNode.Children.Add(schemaNode);
            }

            var procNodeId = $"{schemaNode.Id}:{OntologyConstants.IdPrefixes.Procedure}:{procName.ToLowerInvariant()}";
            var procNode = new ProcedureNode(procNodeId, procName, filePath);
            
            if (!schemaNode.Children.Any(c => c.Id.Equals(procNodeId, StringComparison.OrdinalIgnoreCase)))
            {
                schemaNode.Children.Add(procNode);
            }

            if (ctx != null)
            {
                var relProps = new Dictionary<string, object>
                {
                    [OntologyConstants.PropertyNames.FilePath] = filePath,
                    [OntologyConstants.PropertyNames.Operation] = "EXEC",
                    [OntologyConstants.PropertyNames.EvidenceKind] = OntologyConstants.EvidenceKinds.Procedure
                };
                if (startLine > 0) relProps[OntologyConstants.PropertyNames.Line] = startLine;
                if (endLine > 0) relProps[OntologyConstants.PropertyNames.LineEnd] = endLine;
                ctx.AddGlobalProjectDependency(new Database.Relationship(queryNode.Id, procNodeId, OntologyConstants.Relationships.Calls, relProps));
            }
        }
    }

    public static void TryDetectSqlDependencies(string rawText, string scopeSymbolId, List<Reference> references)
    {
        if (!TryParseSql(rawText, out _, out var cleanedSql)) return;

        var tables = new HashSet<(string? Db, string? Schema, string Table)>();
        var procedures = new HashSet<(string? Db, string? Schema, string Procedure)>();

        ExtractSqlDependencies(cleanedSql, rawText, tables, procedures);

        var addedProcs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var procRef in procedures)
        {
            if (addedProcs.Add(procRef.Procedure))
            {
                references.Add(new Reference(scopeSymbolId, procRef.Procedure, OntologyConstants.Relationships.Calls));
            }
        }

        var addedTables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var tableRef in tables)
        {
            if (addedTables.Add(tableRef.Table))
            {
                references.Add(new Reference(scopeSymbolId, tableRef.Table, OntologyConstants.Relationships.DependsOn));
            }
            if (!string.IsNullOrEmpty(tableRef.Schema) && 
                !tableRef.Schema.Equals("dbo", StringComparison.OrdinalIgnoreCase) && 
                !tableRef.Schema.Equals("public", StringComparison.OrdinalIgnoreCase) &&
                addedTables.Add(tableRef.Schema))
            {
                references.Add(new Reference(scopeSymbolId, tableRef.Schema, OntologyConstants.Relationships.DependsOn));
            }
        }
    }

    public static string CleanSqlIdentifier(string identifier)
    {
        if (string.IsNullOrEmpty(identifier)) return identifier;

        var current = identifier.Trim();
        string previous;
        do
        {
            previous = current;
            
            // Strip outer quotes, brackets, backticks
            if (current.Length >= 2 &&
                ((current.StartsWith('`') && current.EndsWith('`')) ||
                 (current.StartsWith('\'') && current.EndsWith('\'')) ||
                 (current.StartsWith('"') && current.EndsWith('"')) ||
                 (current.StartsWith('[') && current.EndsWith(']'))))
            {
                current = current[1..^1].Trim();
            }
            
            current = current
                .Replace("\"", "")
                .Replace("`", "")
                .Replace("'", "")
                .Replace("[", "")
                .Replace("]", "")
                .Trim();
            
        } while (current != previous);

        return current;
    }

    private static readonly HashSet<string> ExtendedSqlKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "SELECT", "INSERT", "UPDATE", "DELETE", "MERGE",
        "FROM", "JOIN", "WHERE", "AND", "OR", "IN", "ON", "AS", "INTO", "VALUES", "SET",
        "LATERAL", "INTERVAL", "CROSS", "OUTER", "INNER", "LEFT", "RIGHT", "FULL", "USING",
        "GROUP", "BY", "ORDER", "HAVING", "LIMIT", "OFFSET", "WITH", "RECURSIVE",
        "CASE", "WHEN", "THEN", "ELSE", "END", "DISTINCT", "ALL", "ANY", "EXISTS", "EXIST",
        "PARTITION", "OVER", "WINDOW", "TABLE", "VIEW", "INDEX", "SCHEMA", "DATABASE",
        "PROCEDURE", "FUNCTION", "TRIGGER", "DECLARE", "EXEC", "EXECUTE", "BEGIN", "COMMIT", "ROLLBACK",
        "IF", "NOT", "DROP", "ALTER", "CREATE", "TEMPORARY", "TEMP", "EXTERNAL",
        "UNION", "INTERSECT", "EXCEPT", "QUALIFY",
        "EXTERNAL_QUERY", "UNNEST", "GENERATE_SERIES", "TABLE_FUNCTION",
        "JSONB_ARRAY_ELEMENTS_TEXT", "JSONB_TO_RECORDSET", "JSONB_OBJECT_KEYS", "JSONB_ARRAY_ELEMENTS",
        "JSONB_EACH", "JSONB_EACH_TEXT", "DBLINK", "COALESCE",
        "COUNT", "SUM", "AVG", "MIN", "MAX", "NOW",
        "THIS", "THE", "AN", "A", "NULL", "UNDEFINED", "TRUE", "FALSE",
        "SKIP", "LOCKED", "NOWAIT", "RETURNING", "CASCADE", "RESTRICT",
        "CONFLICT", "DO", "NOTHING", "FETCH", "NEXT", "ROWS", "ONLY", "ROW", "FIRST", "LAST",
        "FILTER", "MATERIALIZED", "COLLATE", "CHECK", "CONSTRAINT", "DEFAULT",
        "HERE", "DROPDOWN", "MODAL", "COMPONENT", "OPTIONS",
        "__TABLES__", "__TABLES_SUMMARY__", "TABLESUFFIX", "TABLE_SUFFIX"
    };

    public static bool IsSqlKeyword(string? word)
    {
        if (string.IsNullOrWhiteSpace(word)) return true;
        var trimmed = CleanSqlIdentifier(word);
        if (trimmed.Length <= 1) return true;
        if (char.IsDigit(trimmed[0])) return true;
        if (trimmed.Contains('(') || trimmed.Contains(')')) return true;
        if (trimmed.StartsWith('{') || trimmed.EndsWith('}') || trimmed.StartsWith('$')) return true;
        if (trimmed.Contains("tablesuffix", StringComparison.OrdinalIgnoreCase)) return true;
        return ExtendedSqlKeywords.Contains(trimmed);
    }
}
