using System.Text.RegularExpressions;
using CodeExplorer.Common;
using CodeExplorer.Core.Analysis;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace CodeExplorer.Core.Parser;

public class SqlDependencyVisitor : TSqlFragmentVisitor
{
    public List<(string? Db, string? Schema, string Table)> Tables { get; } = [];
    public List<(string? Db, string? Schema, string Procedure)> Procedures { get; } = [];

    public override void Visit(NamedTableReference node)
    {
        if (node.SchemaObject != null)
        {
            var db = NestedSqlParser.CleanSqlIdentifier(node.SchemaObject.DatabaseIdentifier?.Value ?? "");
            var schema = NestedSqlParser.CleanSqlIdentifier(node.SchemaObject.SchemaIdentifier?.Value ?? "");
            var table = NestedSqlParser.CleanSqlIdentifier(node.SchemaObject.BaseIdentifier?.Value ?? "");
            if (!string.IsNullOrEmpty(table))
            {
                Tables.Add((string.IsNullOrEmpty(db) ? null : db, string.IsNullOrEmpty(schema) ? null : schema, table));
            }
        }
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
        "SELECT", "INSERT", "UPDATE", "DELETE", "MERGE"
    };

    private static bool FastCheckSqlCandidate(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        int i = 0;
        while (i < text.Length && (char.IsWhiteSpace(text[i]) || text[i] is '"' or '\'' or '`' or '('))
        {
            i++;
        }
        if (i >= text.Length) return false;
        char c = char.ToUpperInvariant(text[i]);
        return c is 'S' or 'I' or 'U' or 'D' or 'M';
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

        var match = Regex.Match(cleanedSql, @"^\s*([a-zA-Z]+)\b");
        if (match.Success)
        {
            var word = match.Groups[1].Value.ToUpperInvariant();
            if (SqlKeywords.Contains(word))
            {
                // Verify structural syntax for SQL statements to avoid false positives (e.g. Swagger / UI strings)
                if (word == "SELECT")
                {
                    if (!Regex.IsMatch(cleanedSql, @"\b(?:FROM|WHERE)\b", RegexOptions.IgnoreCase) &&
                        !Regex.IsMatch(cleanedSql, @"^\s*SELECT\s+[\d@'""\(]", RegexOptions.IgnoreCase))
                    {
                        return false;
                    }
                }
                else if (word == "INSERT")
                {
                    if (!Regex.IsMatch(cleanedSql, @"\bINTO\b", RegexOptions.IgnoreCase))
                    {
                        return false;
                    }
                }
                else if (word == "UPDATE")
                {
                    if (!Regex.IsMatch(cleanedSql, @"\bSET\b", RegexOptions.IgnoreCase))
                    {
                        return false;
                    }
                }
                else if (word == "DELETE")
                {
                    if (!Regex.IsMatch(cleanedSql, @"\bFROM\b", RegexOptions.IgnoreCase))
                    {
                        return false;
                    }
                }
                else if (word == "MERGE")
                {
                    if (!Regex.IsMatch(cleanedSql, @"\b(?:INTO|USING)\b", RegexOptions.IgnoreCase))
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

        // Remove Javascript/TypeScript interpolation syntax: ${varName} -> varName
        cleaned = Regex.Replace(cleaned, @"\$\{\s*([a-zA-Z0-9_\.]+)\s*\}", "$1");
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

    public static QueryNode? ParseNestedSql(string rawText, string id, string filePath, ParsingContext? ctx = null)
    {
        if (!TryParseSql(rawText, out var firstWord, out var cleanedSql))
        {
            return null;
        }

        var queryName = $"{firstWord} Query";
        var queryNode = new QueryNode(
            id,
            queryName,
            cleanedSql,
            filePath
        );

        BuildSqlHierarchy(cleanedSql, rawText, queryNode, filePath, ctx);

        return queryNode;
    }

    private static bool IsVariable(string? part, string rawText)
    {
        if (string.IsNullOrEmpty(part)) return false;
        
        // Check if it is wrapped in `{}` (like `{tableName}` or `${tableName}`)
        if (rawText.Contains($"{{{part}}}")) return true;
        
        // Check if it is preceded by `$` (like `$tableName`)
        if (rawText.Contains($"${part}")) return true;
        
        return false;
    }

    private static void ExtractSqlDependencies(
        string cleanedSql,
        string rawText,
        HashSet<(string? Db, string? Schema, string Table)> tables,
        HashSet<(string? Db, string? Schema, string Procedure)> procedures)
    {
        // 1. Try Grammar Parser (ScriptDom)
        try
        {
            var parser = new TSql160Parser(true);
            using var reader = new StringReader(cleanedSql);
            var fragment = parser.Parse(reader, out _);

            var visitor = new SqlDependencyVisitor();
            fragment?.Accept(visitor);

            foreach (var t in visitor.Tables)
            {
                if (IsVariable(t.Table, rawText) || IsVariable(t.Schema, rawText) || IsVariable(t.Db, rawText)) continue;
                tables.Add(t);
            }
            foreach (var p in visitor.Procedures)
            {
                if (IsVariable(p.Procedure, rawText) || IsVariable(p.Schema, rawText) || IsVariable(p.Db, rawText)) continue;
                procedures.Add(p);
            }
        }
        catch
        {
            // Ignore syntax errors, fall back to regex
        }

        // 2. Lexical Fallback: Match identifiers after FROM, JOIN, UPDATE, INTO, MERGE
        var tableMatches = Regex.Matches(cleanedSql, @"\b(?:FROM|JOIN|UPDATE|INTO|MERGE)\s+([a-zA-Z0-9_\.\[\]""#@'`\$\{\}\*]+)", RegexOptions.IgnoreCase);
        foreach (Match match in tableMatches)
        {
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

            if (IsSqlKeyword(tableName)) continue;
            if (IsVariable(tableName, rawText) || IsVariable(schemaName, rawText) || IsVariable(dbName, rawText)) continue;
            tables.Add((dbName, schemaName, tableName));
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

            if (IsVariable(procName, rawText) || IsVariable(schemaName, rawText) || IsVariable(dbName, rawText)) continue;
            procedures.Add((dbName, schemaName, procName));
        }
    }

    private static void BuildSqlHierarchy(string cleanedSql, string rawText, QueryNode queryNode, string filePath, ParsingContext? ctx = null)
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
                    !ResourceReconciliationService.IsGenericConfigKey(anyDb.Name))
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

            var schemaName = !string.IsNullOrEmpty(tableRef.Schema) ? tableRef.Schema : defaultSchema;
            var tableName = tableRef.Table;

            var fullDbName = !string.IsNullOrEmpty(concreteDbName)
                ? concreteDbName
                : $"{targetEngine}.{schemaName}";
            var dbKey = !string.IsNullOrEmpty(concreteDbName)
                ? concreteDbName.ToLowerInvariant()
                : $"{targetEngine.ToLowerInvariant()}:{schemaName.ToLowerInvariant()}";
            if (!dbNodes.TryGetValue(dbKey, out var dbNode))
            {
                var dbNodeId = $"{dbPrefix}:{dbKey}";
                dbNode = new DatabaseNode(dbNodeId, fullDbName, filePath, targetDbType, new Dictionary<string, string>
                {
                    ["name"] = fullDbName,
                    ["engine"] = targetEngine,
                    ["schema"] = schemaName,
                    ["db_type"] = targetDbType
                });
                dbNodes[dbKey] = dbNode;
                queryNode.Children.Add(dbNode);
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
                              : targetEngine.Equals("default", StringComparison.OrdinalIgnoreCase) ? "dbo"
                              : "public";

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
                dbNode = new DatabaseNode(dbNodeId, fullDbName, filePath, "relational", new Dictionary<string, string>
                {
                    ["name"] = fullDbName,
                    ["engine"] = targetEngine,
                    ["schema"] = schemaName,
                    ["db_type"] = "relational"
                });
                dbNodes[dbKey] = dbNode;
                queryNode.Children.Add(dbNode);
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
        "CASE", "WHEN", "THEN", "ELSE", "END", "DISTINCT", "ALL", "ANY", "EXISTS",
        "PARTITION", "OVER", "WINDOW", "TABLE", "VIEW", "INDEX", "SCHEMA", "DATABASE",
        "PROCEDURE", "FUNCTION", "TRIGGER", "DECLARE", "EXEC", "EXECUTE", "BEGIN", "COMMIT", "ROLLBACK",
        "JSONB_ARRAY_ELEMENTS_TEXT", "JSONB_TO_RECORDSET", "JSONB_OBJECT_KEYS", "JSONB_ARRAY_ELEMENTS",
        "JSONB_EACH", "JSONB_EACH_TEXT", "DBLINK", "UNNEST", "GENERATE_SERIES", "COALESCE",
        "COUNT", "SUM", "AVG", "MIN", "MAX", "NOW",
        "THIS", "THE", "AN", "A", "NULL", "UNDEFINED", "TRUE", "FALSE"
    };

    public static bool IsSqlKeyword(string? word)
    {
        if (string.IsNullOrWhiteSpace(word)) return true;
        var trimmed = word.Trim();
        if (trimmed.Length <= 1) return true;
        if (char.IsDigit(trimmed[0])) return true;
        if (trimmed.Contains('(') || trimmed.Contains(')')) return true;
        return ExtendedSqlKeywords.Contains(trimmed);
    }
}
