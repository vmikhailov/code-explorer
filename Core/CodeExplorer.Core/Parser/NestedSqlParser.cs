using System.Text.RegularExpressions;
using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Common.Nodes.Layer3_Semantic;
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
            var db = node.SchemaObject.DatabaseIdentifier?.Value;
            var schema = node.SchemaObject.SchemaIdentifier?.Value;
            var table = node.SchemaObject.BaseIdentifier?.Value;
            if (!string.IsNullOrEmpty(table))
            {
                Tables.Add((db, schema, table));
            }
        }
        base.Visit(node);
    }

    public override void Visit(ExecutableProcedureReference node)
    {
        if (node.ProcedureReference?.ProcedureReference?.Name != null)
        {
            var db = node.ProcedureReference.ProcedureReference.Name.DatabaseIdentifier?.Value;
            var schema = node.ProcedureReference.ProcedureReference.Name.SchemaIdentifier?.Value;
            var proc = node.ProcedureReference.ProcedureReference.Name.BaseIdentifier?.Value;
            if (!string.IsNullOrEmpty(proc))
            {
                Procedures.Add((db, schema, proc));
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

    public static bool TryParseSql(string text, out string? firstWord, out string cleanedSql)
    {
        firstWord = null;
        cleanedSql = CleanQueryText(text).Trim();

        // Console.WriteLine($"Attempting to parse SQL from text: {cleanedSql}");
        
        if (string.IsNullOrEmpty(cleanedSql)) return false;

        var match = Regex.Match(cleanedSql, @"^\s*([a-zA-Z]+)\b");
        if (match.Success)
        {
            var word = match.Groups[1].Value.ToUpperInvariant();
            if (SqlKeywords.Contains(word))
            {
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
            cleaned = cleaned.Substring(1, cleaned.Length - 2).Trim();
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

        // Convert backticks to square brackets for ScriptDom T-SQL parser compatibility
        cleaned = Regex.Replace(cleaned, @"`([^`]+)`", "[$1]");

        return cleaned;
    }

    public static QueryNode? ParseNestedSql(string rawText, string id, string filePath)
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

        BuildSqlHierarchy(cleanedSql, rawText, queryNode, filePath);

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
        var tableMatches = Regex.Matches(cleanedSql, @"\b(?:FROM|JOIN|UPDATE|INTO|MERGE)\s+([a-zA-Z0-9_\.\[\]""#@'`\$\{\}]+)", RegexOptions.IgnoreCase);
        foreach (Match match in tableMatches)
        {
            var rawTableName = match.Groups[1].Value.Trim();
            var cleanedTablePath = CleanSqlIdentifier(rawTableName);
            if (string.IsNullOrEmpty(cleanedTablePath)) continue;

            var parts = cleanedTablePath.Split('.');
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
        var execMatches = Regex.Matches(cleanedSql, @"\bEXEC(?:UTE)?\s+([a-zA-Z0-9_\.\[\]""#@'`\$\{\}]+)", RegexOptions.IgnoreCase);
        foreach (Match match in execMatches)
        {
            var rawProcName = match.Groups[1].Value.Trim();
            var cleanedProcPath = CleanSqlIdentifier(rawProcName);
            if (string.IsNullOrEmpty(cleanedProcPath)) continue;

            var parts = cleanedProcPath.Split('.');
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

    private static void BuildSqlHierarchy(string cleanedSql, string rawText, QueryNode queryNode, string filePath)
    {
        var tables = new HashSet<(string? Db, string? Schema, string Table)>();
        var procedures = new HashSet<(string? Db, string? Schema, string Procedure)>();

        ExtractSqlDependencies(cleanedSql, rawText, tables, procedures);

        var dbNodes = new Dictionary<string, DatabaseNode>(StringComparer.OrdinalIgnoreCase);
        var datasetNodes = new Dictionary<string, DataSetNode>(StringComparer.OrdinalIgnoreCase);

        // Process Tables
        foreach (var tableRef in tables)
        {
            var dbName = tableRef.Db ?? "default";
            var schemaName = tableRef.Schema ?? "dbo";
            var tableName = tableRef.Table;

            var dbKey = dbName.ToLowerInvariant();
            if (!dbNodes.TryGetValue(dbKey, out var dbNode))
            {
                var dbNodeId = $"db:{dbKey}";
                dbNode = new DatabaseNode(dbNodeId, dbName, filePath, "relational");
                dbNodes[dbKey] = dbNode;
                queryNode.Children.Add(dbNode);
            }

            var schemaKey = $"{dbKey}:{schemaName.ToLowerInvariant()}";
            if (!datasetNodes.TryGetValue(schemaKey, out var schemaNode))
            {
                var schemaNodeId = $"{dbNode.Id}:dataset:{schemaName.ToLowerInvariant()}";
                schemaNode = new DataSetNode(schemaNodeId, schemaName, filePath);
                dbNode.Children.Add(schemaNode);
            }

            var tableNodeId = $"{schemaNode.Id}:table:{tableName.ToLowerInvariant()}";
            var tableNode = new TableNode(tableNodeId, tableName, filePath);
            
            if (!schemaNode.Children.Any(c => c.Id.Equals(tableNodeId, StringComparison.OrdinalIgnoreCase)))
            {
                schemaNode.Children.Add(tableNode);
            }
        }

        // Process Procedures
        foreach (var procRef in procedures)
        {
            var dbName = procRef.Db ?? "default";
            var schemaName = procRef.Schema ?? "dbo";
            var procName = procRef.Procedure;

            var dbKey = dbName.ToLowerInvariant();
            if (!dbNodes.TryGetValue(dbKey, out var dbNode))
            {
                var dbNodeId = $"db:{dbKey}";
                dbNode = new DatabaseNode(dbNodeId, dbName, filePath, "relational");
                dbNodes[dbKey] = dbNode;
                queryNode.Children.Add(dbNode);
            }

            var schemaKey = $"{dbKey}:{schemaName.ToLowerInvariant()}";
            if (!datasetNodes.TryGetValue(schemaKey, out var schemaNode))
            {
                var schemaNodeId = $"{dbNode.Id}:dataset:{schemaName.ToLowerInvariant()}";
                schemaNode = new DataSetNode(schemaNodeId, schemaName, filePath);
                dbNode.Children.Add(schemaNode);
            }

            var procNodeId = $"{schemaNode.Id}:procedure:{procName.ToLowerInvariant()}";
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
                addedTables.Add(tableRef.Schema))
            {
                references.Add(new Reference(scopeSymbolId, tableRef.Schema, OntologyConstants.Relationships.DependsOn));
            }
            if (!string.IsNullOrEmpty(tableRef.Db) && 
                !tableRef.Db.Equals("default", StringComparison.OrdinalIgnoreCase) && 
                addedTables.Add(tableRef.Db))
            {
                references.Add(new Reference(scopeSymbolId, tableRef.Db, OntologyConstants.Relationships.DependsOn));
            }
        }
    }

    private static string CleanSqlIdentifier(string identifier)
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
                current = current.Substring(1, current.Length - 2);
            }
            
            current = current.Replace("\"", "").Replace("`", "").Replace("'", "").Trim();
            
        } while (current != previous);

        return current;
    }

    private static bool IsSqlKeyword(string word)
    {
        return SqlKeywords.Contains(word) || 
               word.Equals("FROM", StringComparison.OrdinalIgnoreCase) ||
               word.Equals("JOIN", StringComparison.OrdinalIgnoreCase) ||
               word.Equals("WHERE", StringComparison.OrdinalIgnoreCase) ||
               word.Equals("AND", StringComparison.OrdinalIgnoreCase) ||
               word.Equals("OR", StringComparison.OrdinalIgnoreCase) ||
               word.Equals("IN", StringComparison.OrdinalIgnoreCase) ||
               word.Equals("ON", StringComparison.OrdinalIgnoreCase) ||
               word.Equals("AS", StringComparison.OrdinalIgnoreCase) ||
               word.Equals("INTO", StringComparison.OrdinalIgnoreCase) ||
               word.Equals("VALUES", StringComparison.OrdinalIgnoreCase) ||
               word.Equals("SET", StringComparison.OrdinalIgnoreCase);
    }
}
