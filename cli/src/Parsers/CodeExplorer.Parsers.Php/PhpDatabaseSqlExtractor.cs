using System.Text.RegularExpressions;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Parser;
using CodeExplorer.Core.Parser.Syntax;

namespace CodeExplorer.Parsers.Php;

internal static class PhpDatabaseSqlExtractor
{
    private const string DbTypeRelational = "relational";
    private const string EngineMySql = "MySQL";
    private const string OpSelect = "SELECT";
    private const string OpInsert = "INSERT";
    private const string OpUpdate = "UPDATE";
    private const string OpDelete = "DELETE";
    private const string FilePathKey = "file_path";
    private const string NameKey = "name";
    private const string GroupByWithoutOrderByKey = "has_group_by_without_order_by";
    private const string TrueString = "true";
    private const string GroupByKeyword = "GROUP BY";
    private const string OrderByKeyword = "ORDER BY";
    private const string InsertPrefix = "INSERT";
    private const string ReplacePrefix = "REPLACE";
    private const string UpdatePrefix = "UPDATE";
    private const string DeletePrefix = "DELETE";
    private const string SelectPrefix = "SELECT";
    private const string ZendDbMarker = "Zend_Db";
    private const string DoctrineMarker = "Doctrine\\";
    private const string NetteDatabaseMarker = "Nette\\Database";
    private const string PdoMarker = "PDO";
    private const string MysqlQueryMarker = "mysql_query";
    private const string MysqliQueryMarker = "mysqli_query";
    private const string ArrowInsert = "->insert";
    private const string ArrowUpdate = "->update";
    private const string ArrowDelete = "->delete";
    private const string SnakeCasePattern = @"([a-z0-9])([A-Z])";
    private const string SnakeCaseReplacement = "$1_$2";
    private const char BacktickChar = '`';
    private const char SingleQuoteChar = '\'';
    private const char DoubleQuoteChar = '"';
    private const char DotChar = '.';

    private static readonly char[] SqlIdentifierTrimChars = [BacktickChar, SingleQuoteChar, DoubleQuoteChar, ' ', '\t', '\r', '\n'];

    private static readonly Regex DoctrineTableRegex = new(
        @"(?:@(?:ORM\\)?Table|#\[\s*(?:ORM\\)?Table)\s*\(\s*(?:(?:name\s*[:=]\s*)?['""](?<table>[a-zA-Z0-9_]+)['""])",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex DoctrineEntityRegex = new(
        @"(?:@(?:ORM\\)?Entity|#\[\s*(?:ORM\\)?Entity)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex ZendDbTablePropRegex = new(
        @"(?:protected|public|private|var)\s+\$_(?:name|tableName)\s*=\s*['""](?<table>[a-zA-Z0-9_]+)['""]\s*;",
        RegexOptions.Compiled);

    private static readonly Regex NetteTableCallRegex = new(
        @"->\s*table\s*\(\s*['""](?<table>[a-zA-Z0-9_]+)['""]\s*\)(?<chain>(?:\s*->\s*[a-zA-Z0-9_]+\s*\([^\)]*\))*)",
        RegexOptions.Compiled);

    private static readonly Regex FluentTableCrudRegex = new(
        @"->\s*(?<op>insert|update|delete|from|join|innerJoin|leftJoin)\s*\(\s*(?:array\s*\(\s*['""][a-zA-Z0-9_]+['""]\s*=>\s*)?['""](?<table>[a-zA-Z0-9_.]+)['""]",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex SqlQueryCallRegex = new(
        @"(?:->\s*(?:query|prepare|fetchAll|fetchRow|fetchOne|fetchCol|fetchPairs|fetchAssoc|fetchAssociative|executeQuery|executeUpdate|executeStatement|createQuery)|(?:\bmysql_query|\bmysqli_query))\s*\(\s*(?:\$[a-zA-Z0-9_]+\s*,\s*)?(?<quote>['""])(?<sql>(?:\\.|(?!\k<quote>)[\s\S])+)\k<quote>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex SqlTableExtractRegex = new(
        @"\b(?:FROM|JOIN|INTO|UPDATE|TABLE)\s+[`""']?(?<tbl>[a-zA-Z_][a-zA-Z0-9_]*)[`""']?",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly HashSet<string> SqlIgnoredWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "select", "where", "set", "values", "and", "or", "null", "not", "in", "as", "on", "dual", "exists", "index", "if"
    };

    internal static string? ExtractClassTableMapping(
        string content,
        string commentMasked,
        int classDeclIndex,
        int openBrace,
        int closeBrace,
        string className,
        string? cleanExtends,
        string? docBlock,
        TypeNode typeNode,
        FileNode fileNode,
        string workspaceId,
        string relativePath,
        int startLine,
        int startCol,
        List<SyntacticSymbol> semanticSymbols)
    {
        _ = commentMasked;
        string? tableName = null;

        if (!string.IsNullOrEmpty(docBlock))
        {
            var dtMatch = DoctrineTableRegex.Match(docBlock);
            if (dtMatch.Success)
            {
                tableName = dtMatch.Groups["table"].Value.Trim();
            }
            else if (DoctrineEntityRegex.IsMatch(docBlock))
            {
                tableName = ToSnakeCase(className);
            }
        }

        if (string.IsNullOrEmpty(tableName) && openBrace >= 0 && closeBrace > openBrace)
        {
            var classBody = content.Substring(openBrace, closeBrace - openBrace + 1);
            var zfMatch = ZendDbTablePropRegex.Match(classBody);
            if (zfMatch.Success)
            {
                tableName = zfMatch.Groups["table"].Value.Trim();
            }
            else if (!string.IsNullOrEmpty(cleanExtends) &&
                     cleanExtends.Contains(PhpFileParser.ZendDbTableBase, StringComparison.OrdinalIgnoreCase))
            {
                tableName = ToSnakeCase(className);
            }
        }

        if (string.IsNullOrWhiteSpace(tableName))
        {
            return null;
        }

        var tableNode = EnsureTableNode(fileNode, workspaceId, relativePath, tableName);
        fileNode.References.Add(new Reference(typeNode.Id, tableName, OntologyConstants.Relationships.PersistedIn, startLine, startCol));
        fileNode.References.Add(new Reference(tableNode.Id, typeNode.Id, OntologyConstants.Relationships.ImplementedBy, startLine, startCol));
        fileNode.References.Add(new Reference(tableNode.Id, typeNode.Id, OntologyConstants.Relationships.AccessedBy, startLine, startCol));

        var typeSyntaxNode = new TypeDeclSyntax
        {
            Name = className,
            Span = new TextSpan(classDeclIndex, classDeclIndex + className.Length, startLine - 1, startCol - 1, startLine - 1, startCol - 1)
        };
        semanticSymbols.Add(new SyntacticSymbol(OntologyConstants.NodeLabels.Table, tableName, typeSyntaxNode)
        {
            ParentScopeId = typeNode.Id
        });

        return tableName;
    }

    internal static void ExtractDatabaseAndQueries(
        string content,
        string commentMasked,
        string masked,
        int[] lineOffsets,
        List<PhpFileParser.PhpClassScope> classScopes,
        List<PhpFileParser.PhpFunctionSpan> funSpans,
        FileNode fileNode,
        string workspaceId,
        string relativePath)
    {
        _ = masked;

        if (content.Contains(ZendDbMarker, StringComparison.OrdinalIgnoreCase) ||
            content.Contains(DoctrineMarker, StringComparison.OrdinalIgnoreCase) ||
            content.Contains(NetteDatabaseMarker, StringComparison.OrdinalIgnoreCase) ||
            content.Contains(PdoMarker, StringComparison.Ordinal) ||
            content.Contains(MysqlQueryMarker, StringComparison.OrdinalIgnoreCase) ||
            content.Contains(MysqliQueryMarker, StringComparison.OrdinalIgnoreCase))
        {
            EnsureMySqlDatabaseNode(fileNode, workspaceId, relativePath);
        }

        foreach (Match nm in NetteTableCallRegex.Matches(commentMasked))
        {
            var tableName = nm.Groups["table"].Value.Trim();
            var chain = nm.Groups["chain"].Value;
            var op = DetermineChainOperation(chain);
            var (line, col) = PhpFileParser.GetLineAndColumn(lineOffsets, nm.Index);
            RecordTableAccess(fileNode, workspaceId, relativePath, tableName, op, $"Nette: {op} {tableName}", line, col, nm.Index, classScopes, funSpans, hasUnorderedGroupBy: false);
        }

        foreach (Match fm in FluentTableCrudRegex.Matches(commentMasked))
        {
            var rawOp = fm.Groups["op"].Value.ToLowerInvariant();
            var rawTable = fm.Groups["table"].Value.Trim();
            if (rawTable.Contains(DotChar))
            {
                rawTable = rawTable.Split(DotChar)[0].Trim();
            }
            if (SqlIgnoredWords.Contains(rawTable) || rawTable.Length < 2) continue;

            var op = rawOp switch
            {
                "insert" => OpInsert,
                "update" => OpUpdate,
                "delete" => OpDelete,
                _ => OpSelect
            };
            var (line, col) = PhpFileParser.GetLineAndColumn(lineOffsets, fm.Index);
            RecordTableAccess(fileNode, workspaceId, relativePath, rawTable, op, $"Fluent: {op} {rawTable}", line, col, fm.Index, classScopes, funSpans, hasUnorderedGroupBy: false);
        }

        foreach (Match sm in SqlQueryCallRegex.Matches(commentMasked))
        {
            var rawSql = sm.Groups["sql"].Value.Trim();
            if (string.IsNullOrWhiteSpace(rawSql)) continue;

            var op = DetermineSqlOperation(rawSql);
            var tables = ExtractTablesFromSqlString(rawSql);
            if (tables.Count == 0 && !LooksLikeSqlStatement(rawSql))
            {
                continue;
            }

            var hasGroupByWithoutOrder = rawSql.Contains(GroupByKeyword, StringComparison.OrdinalIgnoreCase) &&
                                         !rawSql.Contains(OrderByKeyword, StringComparison.OrdinalIgnoreCase);
            var (line, col) = PhpFileParser.GetLineAndColumn(lineOffsets, sm.Index);

            if (tables.Count == 0)
            {
                var cls = PhpFileParser.FindEnclosingClass(sm.Index, classScopes);
                if (!string.IsNullOrEmpty(cls?.BoundTableName))
                {
                    tables.Add(cls.BoundTableName);
                }
            }

            foreach (var tbl in tables)
            {
                RecordTableAccess(fileNode, workspaceId, relativePath, tbl, op, rawSql, line, col, sm.Index, classScopes, funSpans, hasGroupByWithoutOrder);
            }
        }
    }

    private static void RecordTableAccess(
        FileNode fileNode,
        string workspaceId,
        string relativePath,
        string tableName,
        string operation,
        string sqlText,
        int line,
        int col,
        int charIndex,
        List<PhpFileParser.PhpClassScope> classScopes,
        List<PhpFileParser.PhpFunctionSpan> funSpans,
        bool hasUnorderedGroupBy)
    {
        EnsureMySqlDatabaseNode(fileNode, workspaceId, relativePath);
        var tableNode = EnsureTableNode(fileNode, workspaceId, relativePath, tableName);
        var edgeKind = operation == OpSelect
            ? OntologyConstants.Relationships.ReadsFrom
            : OntologyConstants.Relationships.WritesTo;

        var qId = $"{workspaceId}:{OntologyConstants.IdPrefixes.Query}:{relativePath}:{tableName.ToLowerInvariant()}:{line}:{col}";
        if (!fileNode.Children.Any(c => c.Id == qId))
        {
            var qName = $"{operation} {tableName}";
            var queryNode = new QueryNode(qId, qName, sqlText, relativePath, line, line)
            {
                FilePath = relativePath,
                Line = line,
                LineEnd = line,
                Operation = operation,
                Verb = operation,
                EvidenceKind = OntologyConstants.EvidenceKinds.Query
            };
            queryNode.CustomProperties ??= new();
            queryNode.CustomProperties[FilePathKey] = relativePath;
            if (hasUnorderedGroupBy)
            {
                queryNode.CustomProperties[GroupByWithoutOrderByKey] = TrueString;
            }
            fileNode.Children.Add(queryNode);
            fileNode.References.Add(new Reference(qId, tableNode.Id, edgeKind, line, col));
        }

        var enclosingFn = PhpFileParser.FindEnclosingFunction(charIndex, funSpans);
        var enclosingCls = PhpFileParser.FindEnclosingClass(charIndex, classScopes);

        if (enclosingFn != null)
        {
            fileNode.References.Add(new Reference(qId, enclosingFn.Id, OntologyConstants.Relationships.AccessedBy, line, col));
            enclosingFn.References.Add(new Reference(enclosingFn.Id, tableName, edgeKind, line, col));
        }

        if (enclosingCls != null)
        {
            fileNode.References.Add(new Reference(qId, enclosingCls.Node.Id, OntologyConstants.Relationships.AccessedBy, line, col));
            fileNode.References.Add(new Reference(enclosingCls.Node.Id, tableName, edgeKind, line, col));
        }
    }

    internal static TableNode EnsureTableNode(FileNode fileNode, string workspaceId, string relativePath, string tableName)
    {
        var cleanName = tableName.Trim(SqlIdentifierTrimChars);
        var tableId = $"{workspaceId}:{OntologyConstants.IdPrefixes.Table}:{cleanName.ToLowerInvariant()}";
        var existing = fileNode.Children.OfType<TableNode>().FirstOrDefault(t => t.Id == tableId);
        if (existing != null)
        {
            return existing;
        }

        var tableNode = new TableNode(tableId, cleanName, relativePath);
        tableNode.CustomProperties ??= new();
        tableNode.CustomProperties[FilePathKey] = relativePath;
        tableNode.CustomProperties[NameKey] = cleanName;
        fileNode.Children.Add(tableNode);
        return tableNode;
    }

    internal static void EnsureMySqlDatabaseNode(FileNode fileNode, string workspaceId, string relativePath)
    {
        var dbId = $"{workspaceId}:{OntologyConstants.IdPrefixes.Database}:relational:mysql";
        if (fileNode.Children.Any(c => c.Id == dbId))
        {
            return;
        }

        var dbNode = new DatabaseNode(dbId, EngineMySql, relativePath, DbTypeRelational)
        {
            Engine = EngineMySql,
            Technology = EngineMySql,
            Provider = EngineMySql,
            IsOrm = true
        };
        fileNode.Children.Add(dbNode);
        fileNode.References.Add(new Reference(fileNode.Id, dbId, OntologyConstants.Relationships.UsesDb));
    }

    internal static HashSet<string> ExtractTablesFromSqlString(string sql)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match m in SqlTableExtractRegex.Matches(sql))
        {
            var tbl = m.Groups["tbl"].Value.Trim(SqlIdentifierTrimChars);
            if (!string.IsNullOrWhiteSpace(tbl) && !SqlIgnoredWords.Contains(tbl))
            {
                result.Add(tbl);
            }
        }
        return result;
    }

    internal static string DetermineSqlOperation(string sql)
    {
        var trimmed = sql.TrimStart();
        if (trimmed.StartsWith(InsertPrefix, StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith(ReplacePrefix, StringComparison.OrdinalIgnoreCase))
        {
            return OpInsert;
        }
        if (trimmed.StartsWith(UpdatePrefix, StringComparison.OrdinalIgnoreCase))
        {
            return OpUpdate;
        }
        if (trimmed.StartsWith(DeletePrefix, StringComparison.OrdinalIgnoreCase))
        {
            return OpDelete;
        }
        return OpSelect;
    }

    private static bool LooksLikeSqlStatement(string sql)
    {
        var trimmed = sql.TrimStart();
        return trimmed.StartsWith(SelectPrefix, StringComparison.OrdinalIgnoreCase) ||
               trimmed.StartsWith(InsertPrefix, StringComparison.OrdinalIgnoreCase) ||
               trimmed.StartsWith(UpdatePrefix, StringComparison.OrdinalIgnoreCase) ||
               trimmed.StartsWith(DeletePrefix, StringComparison.OrdinalIgnoreCase) ||
               trimmed.StartsWith(ReplacePrefix, StringComparison.OrdinalIgnoreCase);
    }

    private static string DetermineChainOperation(string chain)
    {
        if (string.IsNullOrWhiteSpace(chain)) return OpSelect;
        if (chain.Contains(ArrowInsert, StringComparison.OrdinalIgnoreCase)) return OpInsert;
        if (chain.Contains(ArrowUpdate, StringComparison.OrdinalIgnoreCase)) return OpUpdate;
        if (chain.Contains(ArrowDelete, StringComparison.OrdinalIgnoreCase)) return OpDelete;
        return OpSelect;
    }

    private static string ToSnakeCase(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return input;
        return Regex.Replace(input, SnakeCasePattern, SnakeCaseReplacement).ToLowerInvariant();
    }

    internal static string MaskComments(string source) => MaskPhpSource(source, maskStrings: false);

    internal static string MaskCommentsAndStrings(string source) => MaskPhpSource(source, maskStrings: true);

    private static string MaskPhpSource(string source, bool maskStrings)
    {
        var chars = source.ToCharArray();
        var len = chars.Length;
        var i = 0;

        while (i < len)
        {
            if ((i + 1 < len && chars[i] == '/' && chars[i + 1] == '/') ||
                (chars[i] == '#' && (i + 1 >= len || chars[i + 1] != '[')))
            {
                while (i < len && chars[i] != '\n')
                {
                    chars[i] = ' ';
                    i++;
                }
                continue;
            }

            if (i + 1 < len && chars[i] == '/' && chars[i + 1] == '*')
            {
                chars[i] = ' ';
                chars[i + 1] = ' ';
                i += 2;
                while (i < len)
                {
                    if (i + 1 < len && chars[i] == '*' && chars[i + 1] == '/')
                    {
                        chars[i] = ' ';
                        chars[i + 1] = ' ';
                        i += 2;
                        break;
                    }
                    if (chars[i] != '\n') chars[i] = ' ';
                    i++;
                }
                continue;
            }

            if (chars[i] is '"' or '\'')
            {
                var q = chars[i];
                if (maskStrings) chars[i] = ' ';
                i++;
                while (i < len && chars[i] != q)
                {
                    if (chars[i] == '\\' && i + 1 < len)
                    {
                        if (maskStrings && chars[i] != '\n') chars[i] = ' ';
                        i++;
                    }
                    if (maskStrings && chars[i] != '\n') chars[i] = ' ';
                    i++;
                }
                if (i < len && chars[i] == q)
                {
                    if (maskStrings) chars[i] = ' ';
                    i++;
                }
                continue;
            }

            i++;
        }

        return new string(chars);
    }
}
