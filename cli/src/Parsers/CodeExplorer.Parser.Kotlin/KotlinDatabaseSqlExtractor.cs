using System.Text.RegularExpressions;
using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;

namespace CodeExplorer.Parser.Kotlin;

internal static class KotlinDatabaseSqlExtractor
{
    private const string DbTypeRelational = "relational";
    private const string EnginePostgres = "PostgreSQL";
    private const string EngineH2 = "H2";
    private const string EngineMySql = "MySQL";
    private const string EngineSqlite = "SQLite";
    private const string OpSelect = "SELECT";
    private const string OpInsert = "INSERT";
    private const string OpUpdate = "UPDATE";
    private const string OpDelete = "DELETE";
    private const string OpUpsert = "UPSERT";

    private static readonly Regex ExposedOperationRegex = new(
        @"(?<!\bval\b\s*|var\s*)\b(?<tableOrObj>[A-Z][a-zA-Z0-9_]*)\s*\.\s*(?<op>selectAll|select|insert|insertIgnore|insertAndGetId|batchInsert|update|deleteWhere|deleteAll|upsert)\b",
        RegexOptions.Compiled);

    private static readonly Regex ExposedExecRegex = new(
        @"\bexec\s*\(\s*(?<quote>['""])(?<sql>(?:\\.|(?!\k<quote>).)+)\k<quote>",
        RegexOptions.Compiled);

    internal static string MaskCommentsAndStrings(string source)
    {
        var chars = source.ToCharArray();
        var len = chars.Length;
        var i = 0;

        while (i < len)
        {
            if (i + 1 < len && chars[i] == '/' && chars[i + 1] == '/')
            {
                chars[i] = ' ';
                chars[i + 1] = ' ';
                i += 2;
                while (i < len && chars[i] != '\n')
                {
                    chars[i] = ' ';
                    i++;
                }
                continue;
            }

            if (i + 1 < len && chars[i] == '/' && chars[i + 1] == '*')
            {
                var depth = 1;
                chars[i] = ' ';
                chars[i + 1] = ' ';
                i += 2;
                while (i < len && depth > 0)
                {
                    if (i + 1 < len && chars[i] == '/' && chars[i + 1] == '*')
                    {
                        depth++;
                        chars[i] = ' ';
                        chars[i + 1] = ' ';
                        i += 2;
                    }
                    else if (i + 1 < len && chars[i] == '*' && chars[i + 1] == '/')
                    {
                        depth--;
                        chars[i] = ' ';
                        chars[i + 1] = ' ';
                        i += 2;
                    }
                    else
                    {
                        if (chars[i] != '\n') chars[i] = ' ';
                        i++;
                    }
                }
                continue;
            }

            if (i + 2 < len && chars[i] == '"' && chars[i + 1] == '"' && chars[i + 2] == '"')
            {
                chars[i] = ' '; chars[i + 1] = ' '; chars[i + 2] = ' ';
                i += 3;
                while (i < len)
                {
                    if (i + 2 < len && chars[i] == '"' && chars[i + 1] == '"' && chars[i + 2] == '"')
                    {
                        chars[i] = ' '; chars[i + 1] = ' '; chars[i + 2] = ' ';
                        i += 3;
                        break;
                    }
                    if (chars[i] != '\n') chars[i] = ' ';
                    i++;
                }
                continue;
            }

            if (chars[i] == '"')
            {
                chars[i] = ' ';
                i++;
                while (i < len && chars[i] != '"')
                {
                    if (chars[i] == '\\' && i + 1 < len)
                    {
                        chars[i] = ' ';
                        i++;
                    }
                    if (chars[i] != '\n') chars[i] = ' ';
                    i++;
                }
                if (i < len && chars[i] == '"')
                {
                    chars[i] = ' ';
                    i++;
                }
                continue;
            }

            if (chars[i] == '\'')
            {
                chars[i] = ' ';
                i++;
                while (i < len && chars[i] != '\'')
                {
                    if (chars[i] == '\\' && i + 1 < len)
                    {
                        chars[i] = ' ';
                        i++;
                    }
                    if (chars[i] != '\n') chars[i] = ' ';
                    i++;
                }
                if (i < len && chars[i] == '\'')
                {
                    chars[i] = ' ';
                    i++;
                }
                continue;
            }

            i++;
        }

        return new string(chars);
    }

    internal static string MaskComments(string source)
    {
        var chars = source.ToCharArray();
        var len = chars.Length;
        var i = 0;

        while (i < len)
        {
            if (i + 1 < len && chars[i] == '/' && chars[i + 1] == '/')
            {
                chars[i] = ' ';
                chars[i + 1] = ' ';
                i += 2;
                while (i < len && chars[i] != '\n')
                {
                    chars[i] = ' ';
                    i++;
                }
                continue;
            }

            if (i + 1 < len && chars[i] == '/' && chars[i + 1] == '*')
            {
                var depth = 1;
                chars[i] = ' ';
                chars[i + 1] = ' ';
                i += 2;
                while (i < len && depth > 0)
                {
                    if (i + 1 < len && chars[i] == '/' && chars[i + 1] == '*')
                    {
                        depth++;
                        chars[i] = ' ';
                        chars[i + 1] = ' ';
                        i += 2;
                    }
                    else if (i + 1 < len && chars[i] == '*' && chars[i + 1] == '/')
                    {
                        depth--;
                        chars[i] = ' ';
                        chars[i + 1] = ' ';
                        i += 2;
                    }
                    else
                    {
                        if (chars[i] != '\n') chars[i] = ' ';
                        i++;
                    }
                }
                continue;
            }

            if (i + 2 < len && chars[i] == '"' && chars[i + 1] == '"' && chars[i + 2] == '"')
            {
                i += 3;
                while (i < len)
                {
                    if (i + 2 < len && chars[i] == '"' && chars[i + 1] == '"' && chars[i + 2] == '"')
                    {
                        i += 3;
                        break;
                    }
                    i++;
                }
                continue;
            }

            if (chars[i] == '"')
            {
                i++;
                while (i < len && chars[i] != '"')
                {
                    if (chars[i] == '\\' && i + 1 < len)
                    {
                        i += 2;
                        continue;
                    }
                    i++;
                }
                if (i < len && chars[i] == '"') i++;
                continue;
            }

            if (chars[i] == '\'')
            {
                i++;
                while (i < len && chars[i] != '\'')
                {
                    if (chars[i] == '\\' && i + 1 < len)
                    {
                        i += 2;
                        continue;
                    }
                    i++;
                }
                if (i < len && chars[i] == '\'') i++;
                continue;
            }

            i++;
        }

        return new string(chars);
    }

    internal static void DetectDatabaseConfiguration(
        string content,
        FileNode fileNode,
        string workspaceId,
        string relativePath)
    {
        if (content.Contains("org.postgresql.Driver", StringComparison.OrdinalIgnoreCase) ||
            content.Contains("jdbc:postgresql:", StringComparison.OrdinalIgnoreCase))
        {
            var dbId = $"{workspaceId}:{OntologyConstants.IdPrefixes.Database}:relational:postgresql";
            if (!fileNode.Children.Any(c => c.Id == dbId))
            {
                var dbNode = new DatabaseNode(dbId, EnginePostgres, relativePath, DbTypeRelational)
                {
                    Engine = EnginePostgres,
                    Technology = EnginePostgres,
                    Provider = EnginePostgres,
                    IsOrm = true
                };
                fileNode.Children.Add(dbNode);
                fileNode.References.Add(new Reference(fileNode.Id, dbId, OntologyConstants.Relationships.Configures));
                fileNode.References.Add(new Reference(fileNode.Id, dbId, OntologyConstants.Relationships.UsesDb));
            }
        }

        if (content.Contains("org.h2.Driver", StringComparison.OrdinalIgnoreCase) ||
            content.Contains("jdbc:h2:", StringComparison.OrdinalIgnoreCase))
        {
            var dbId = $"{workspaceId}:{OntologyConstants.IdPrefixes.Database}:relational:h2";
            if (!fileNode.Children.Any(c => c.Id == dbId))
            {
                var dbNode = new DatabaseNode(dbId, EngineH2, relativePath, DbTypeRelational)
                {
                    Engine = EngineH2,
                    Technology = EngineH2,
                    Provider = EngineH2,
                    IsOrm = true
                };
                fileNode.Children.Add(dbNode);
                fileNode.References.Add(new Reference(fileNode.Id, dbId, OntologyConstants.Relationships.Configures));
                fileNode.References.Add(new Reference(fileNode.Id, dbId, OntologyConstants.Relationships.UsesDb));
            }
        }

        if (content.Contains("com.mysql.cj.jdbc.Driver", StringComparison.OrdinalIgnoreCase) ||
            content.Contains("com.mysql.jdbc.Driver", StringComparison.OrdinalIgnoreCase) ||
            content.Contains("jdbc:mysql:", StringComparison.OrdinalIgnoreCase))
        {
            var dbId = $"{workspaceId}:{OntologyConstants.IdPrefixes.Database}:relational:mysql";
            if (!fileNode.Children.Any(c => c.Id == dbId))
            {
                var dbNode = new DatabaseNode(dbId, EngineMySql, relativePath, DbTypeRelational)
                {
                    Engine = EngineMySql,
                    Technology = EngineMySql,
                    Provider = EngineMySql,
                    IsOrm = true
                };
                fileNode.Children.Add(dbNode);
                fileNode.References.Add(new Reference(fileNode.Id, dbId, OntologyConstants.Relationships.Configures));
                fileNode.References.Add(new Reference(fileNode.Id, dbId, OntologyConstants.Relationships.UsesDb));
            }
        }

        // Android Room Database detection
        if (content.Contains("RoomDatabase", StringComparison.OrdinalIgnoreCase) ||
            content.Contains("databaseBuilder", StringComparison.OrdinalIgnoreCase) ||
            content.Contains("androidx.room", StringComparison.OrdinalIgnoreCase) ||
            content.Contains("@Database", StringComparison.OrdinalIgnoreCase))
        {
            string? dbName = null;
            var builderMatch = Regex.Match(content, @"databaseBuilder\s*\([^,]+,[^,]+,\s*['""]([^'""]+)['""]");
            if (builderMatch.Success)
            {
                dbName = builderMatch.Groups[1].Value.Trim();
            }

            if (string.IsNullOrWhiteSpace(dbName))
            {
                var dbClassMatch = Regex.Match(content, @"(?<![:.])\babstract\s+class\s+([A-Z][a-zA-Z0-9_]*)\s*:\s*RoomDatabase");
                if (dbClassMatch.Success)
                {
                    var cls = dbClassMatch.Groups[1].Value.Trim();
                    if (cls.EndsWith("Database", StringComparison.OrdinalIgnoreCase) && cls.Length > 8)
                    {
                        dbName = $"{cls[..^8].ToLowerInvariant()}-database";
                    }
                    else
                    {
                        dbName = cls;
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(dbName) && content.Contains("@Database", StringComparison.OrdinalIgnoreCase))
            {
                var classMatch = Regex.Match(content, @"(?<![:.])\b(?:class|interface)\s+([A-Z][a-zA-Z0-9_]*)");
                if (classMatch.Success)
                {
                    var cls = classMatch.Groups[1].Value.Trim();
                    if (cls.EndsWith("Database", StringComparison.OrdinalIgnoreCase) && cls.Length > 8)
                    {
                        dbName = $"{cls[..^8].ToLowerInvariant()}-database";
                    }
                    else
                    {
                        dbName = cls;
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(dbName))
            {
                var canonicalName = $"SQLite.{dbName}";
                var dbId = $"{workspaceId}:{OntologyConstants.IdPrefixes.Database}:relational:sqlite:{dbName.ToLowerInvariant()}";
                if (!fileNode.Children.Any(c => c.Id == dbId))
                {
                    var dbNode = new DatabaseNode(dbId, canonicalName, relativePath, DbTypeRelational)
                    {
                        Engine = EngineSqlite,
                        Technology = "Room",
                        Provider = "Room",
                        Schema = dbName,
                        IsOrm = true
                    };
                    fileNode.Children.Add(dbNode);
                    fileNode.References.Add(new Reference(fileNode.Id, dbId, OntologyConstants.Relationships.Configures));
                    fileNode.References.Add(new Reference(fileNode.Id, dbId, OntologyConstants.Relationships.UsesDb));
                }
            }
        }
    }

    internal static void ParseExposedOperations(
        string commentMasked,
        string masked,
        int[] lineOffsets,
        FileNode fileNode,
        string workspaceId,
        string relativePath,
        List<(int Start, int End, FunctionNode Node)> funSpans)
    {
        _ = masked;
        foreach (Match m in ExposedOperationRegex.Matches(commentMasked))
        {
            var tableOrObj = m.Groups["tableOrObj"].Value.Trim();
            var op = m.Groups["op"].Value.Trim();

            if (tableOrObj is "String" or "Int" or "Boolean" or "List" or "Map" or "Set" or "Array" or "File" or "Path" or "Log" or "Logger")
            {
                continue;
            }

            var operation = op switch
            {
                "selectAll" or "select" => OpSelect,
                "insert" or "insertIgnore" or "insertAndGetId" or "batchInsert" => OpInsert,
                "update" => OpUpdate,
                "deleteWhere" or "deleteAll" => OpDelete,
                "upsert" => OpUpsert,
                _ => OpSelect
            };

            var edgeKind = operation == OpSelect
                ? OntologyConstants.Relationships.ReadsFrom
                : OntologyConstants.Relationships.WritesTo;

            var cleanSql = operation switch
            {
                OpSelect => $"SELECT * FROM {tableOrObj}",
                OpInsert => $"INSERT INTO {tableOrObj}",
                OpUpdate => $"UPDATE {tableOrObj}",
                OpDelete => $"DELETE FROM {tableOrObj}",
                _ => $"{operation} {tableOrObj}"
            };

            var (opLine, opCol) = KotlinFileParser.GetLineAndColumn(lineOffsets, m.Index);
            var qId = $"{workspaceId}:{OntologyConstants.IdPrefixes.Query}:{relativePath}:{opLine}:{opCol}";
            var qName = $"Exposed: {operation} {tableOrObj}";

            var queryNode = new QueryNode(qId, qName, cleanSql, relativePath, opLine, opLine)
            {
                FilePath = relativePath,
                Line = opLine,
                LineEnd = opLine,
                Operation = operation,
                Verb = operation,
                EvidenceKind = OntologyConstants.EvidenceKinds.Query
            };
            queryNode.CustomProperties ??= new();
            queryNode.CustomProperties["file_path"] = relativePath;
            fileNode.Children.Add(queryNode);

            var tableId = $"{workspaceId}:{OntologyConstants.IdPrefixes.Table}:{tableOrObj.ToLowerInvariant()}";
            fileNode.References.Add(new Reference(qId, tableId, edgeKind, opLine, opCol));

            var enclosingFunc = KotlinFileParser.FindEnclosingFunction(m.Index, funSpans);
            if (enclosingFunc != null)
            {
                fileNode.References.Add(new Reference(qId, enclosingFunc.Id, OntologyConstants.Relationships.AccessedBy, opLine, opCol));
            }
        }

        foreach (Match m in ExposedExecRegex.Matches(commentMasked))
        {
            var rawSql = m.Groups["sql"].Value.Trim();
            if (string.IsNullOrWhiteSpace(rawSql)) continue;

            var cleanSql = NestedSqlParser.CleanQueryText(rawSql);
            var operation = OpSelect;
            if (cleanSql.StartsWith("INSERT", StringComparison.OrdinalIgnoreCase)) operation = OpInsert;
            else if (cleanSql.StartsWith("UPDATE", StringComparison.OrdinalIgnoreCase)) operation = OpUpdate;
            else if (cleanSql.StartsWith("DELETE", StringComparison.OrdinalIgnoreCase)) operation = OpDelete;

            var edgeKind = operation == OpSelect
                ? OntologyConstants.Relationships.ReadsFrom
                : OntologyConstants.Relationships.WritesTo;

            var (opLine, opCol) = KotlinFileParser.GetLineAndColumn(lineOffsets, m.Index);
            var qId = $"{workspaceId}:{OntologyConstants.IdPrefixes.Query}:{relativePath}:{opLine}:{opCol}";
            var qName = $"SQL Query: {(cleanSql.Length > 30 ? cleanSql[..30] + "..." : cleanSql)}";

            var queryNode = new QueryNode(qId, qName, cleanSql, relativePath, opLine, opLine)
            {
                FilePath = relativePath,
                Line = opLine,
                LineEnd = opLine,
                Operation = operation,
                Verb = operation,
                EvidenceKind = OntologyConstants.EvidenceKinds.Query
            };
            queryNode.CustomProperties ??= new();
            queryNode.CustomProperties["file_path"] = relativePath;
            fileNode.Children.Add(queryNode);

            var tblMatch = Regex.Match(cleanSql, @"\b(?:FROM|INTO|UPDATE)\s+[""']?([a-zA-Z0-9_]+)[""']?", RegexOptions.IgnoreCase);
            if (tblMatch.Success)
            {
                var tblName = tblMatch.Groups[1].Value.Trim();
                var tableId = $"{workspaceId}:{OntologyConstants.IdPrefixes.Table}:{tblName.ToLowerInvariant()}";
                fileNode.References.Add(new Reference(qId, tableId, edgeKind, opLine, opCol));
            }

            var enclosingFunc = KotlinFileParser.FindEnclosingFunction(m.Index, funSpans);
            if (enclosingFunc != null)
            {
                fileNode.References.Add(new Reference(qId, enclosingFunc.Id, OntologyConstants.Relationships.AccessedBy, opLine, opCol));
            }
        }
    }

    internal static void CollectTables(IOntologyNode node, HashSet<string> tableNames)
    {
        if (node is TableNode tbl && !string.IsNullOrWhiteSpace(tbl.Name))
        {
            tableNames.Add(tbl.Name);
        }
        foreach (var child in node.Children)
        {
            CollectTables(child, tableNames);
        }
    }

    internal static void ExtractTablesFromSql(string sql, HashSet<string> tableNames)
    {
        var matches = Regex.Matches(sql, @"\b(?:FROM|JOIN|INTO|UPDATE|TABLE)\s+[`""']?([a-zA-Z0-9_]+)[`""']?", RegexOptions.IgnoreCase);
        foreach (Match m in matches)
        {
            var tbl = m.Groups[1].Value.Trim();
            if (!NestedSqlParser.IsSqlKeyword(tbl))
            {
                tableNames.Add(tbl);
            }
        }
    }

    internal static void ParseMyBatisProvider(
        KotlinFileParser.AnnotationInfo ann,
        FunctionNode funcNode,
        KotlinFileParser.ClassScope? enclosingClass,
        FileNode fileNode,
        string workspaceId,
        string relativePath,
        int funLine,
        int funCol)
    {
        var (providerClass, providerMethod) = KotlinFileParser.ExtractProviderInfo(ann.Args);
        var op = ann.Name.ToUpperInvariant() switch
        {
            "INSERTPROVIDER" => OpInsert,
            "UPDATEPROVIDER" => OpUpdate,
            "DELETEPROVIDER" => OpDelete,
            _ => OpSelect
        };
        var edgeKind = op == OpSelect
            ? OntologyConstants.Relationships.ReadsFrom
            : OntologyConstants.Relationships.WritesTo;

        var provSummary = !string.IsNullOrEmpty(providerClass)
            ? $"{providerClass}.{providerMethod ?? "query"}"
            : "DynamicSqlProvider";

        var qId = $"{workspaceId}:{OntologyConstants.IdPrefixes.Query}:{relativePath}:{funLine}:{funCol}";
        var qName = $"MyBatis: {op} via {provSummary}";

        var queryNode = new QueryNode(qId, qName, $"-- MyBatis Provider: {provSummary}", relativePath, funLine, funLine)
        {
            FilePath = relativePath,
            Line = funLine,
            LineEnd = funLine,
            Operation = op,
            Verb = op,
            EvidenceKind = OntologyConstants.EvidenceKinds.Query
        };
        queryNode.CustomProperties ??= new();
        queryNode.CustomProperties["file_path"] = relativePath;
        if (!string.IsNullOrEmpty(providerClass))
        {
            queryNode.CustomProperties["provider_class"] = providerClass;
        }
        if (!string.IsNullOrEmpty(providerMethod))
        {
            queryNode.CustomProperties["provider_method"] = providerMethod;
        }
        fileNode.Children.Add(queryNode);

        fileNode.References.Add(new Reference(qId, funcNode.Id, OntologyConstants.Relationships.AccessedBy, funLine, funCol));
        if (enclosingClass != null)
        {
            fileNode.References.Add(new Reference(qId, enclosingClass.Node.Id, OntologyConstants.Relationships.AccessedBy, funLine, funCol));
        }

        if (!string.IsNullOrEmpty(enclosingClass?.RepositoryEntity))
        {
            var tableId = $"{workspaceId}:{OntologyConstants.IdPrefixes.Table}:{enclosingClass.RepositoryEntity.ToLowerInvariant()}";
            fileNode.References.Add(new Reference(qId, tableId, edgeKind, funLine, funCol));
        }
    }

    internal static void ParseMyBatisOrQuerySql(
        KotlinFileParser.AnnotationInfo ann,
        string sqlCandidate,
        FunctionNode funcNode,
        KotlinFileParser.ClassScope? enclosingClass,
        FileNode fileNode,
        string workspaceId,
        string relativePath,
        int funLine,
        int funCol)
    {
        var cleanSql = KotlinFileParser.NormalizeMyBatisParameters(NestedSqlParser.CleanQueryText(sqlCandidate));
        var op = ann.Name.ToUpperInvariant() switch
        {
            "INSERT" => OpInsert,
            "UPDATE" => OpUpdate,
            "DELETE" => OpDelete,
            "UPSERT" => OpInsert,
            "SELECT" => OpSelect,
            _ => cleanSql.StartsWith("INSERT", StringComparison.OrdinalIgnoreCase) ? OpInsert :
                 cleanSql.StartsWith("UPDATE", StringComparison.OrdinalIgnoreCase) ? OpUpdate :
                 cleanSql.StartsWith("DELETE", StringComparison.OrdinalIgnoreCase) ? OpDelete : OpSelect
        };

        var edgeKind = op == OpSelect
            ? OntologyConstants.Relationships.ReadsFrom
            : OntologyConstants.Relationships.WritesTo;

        var qId = $"{workspaceId}:{OntologyConstants.IdPrefixes.Query}:{relativePath}:{funLine}:{funCol}";
        var qName = $"SQL: {(cleanSql.Length > 30 ? cleanSql[..30] + "..." : cleanSql)}";

        var queryNode = new QueryNode(qId, qName, cleanSql, relativePath, funLine, funLine)
        {
            FilePath = relativePath,
            Line = funLine,
            LineEnd = funLine,
            Operation = op,
            Verb = op,
            EvidenceKind = OntologyConstants.EvidenceKinds.Query
        };
        queryNode.CustomProperties ??= new();
        queryNode.CustomProperties["file_path"] = relativePath;
        fileNode.Children.Add(queryNode);

        fileNode.References.Add(new Reference(qId, funcNode.Id, OntologyConstants.Relationships.AccessedBy, funLine, funCol));
        if (enclosingClass != null)
        {
            fileNode.References.Add(new Reference(qId, enclosingClass.Node.Id, OntologyConstants.Relationships.AccessedBy, funLine, funCol));
        }

        var tables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        ExtractTablesFromSql(cleanSql, tables);

        if (tables.Count == 0 && !string.IsNullOrEmpty(enclosingClass?.RepositoryEntity))
        {
            tables.Add(enclosingClass.RepositoryEntity);
        }

        foreach (var tbl in tables)
        {
            var tableId = $"{workspaceId}:{OntologyConstants.IdPrefixes.Table}:{tbl.ToLowerInvariant()}";
            fileNode.References.Add(new Reference(qId, tableId, edgeKind, funLine, funCol));
            fileNode.References.Add(new Reference(funcNode.Id, tableId, edgeKind, funLine, funCol));
            if (enclosingClass != null)
            {
                fileNode.References.Add(new Reference(enclosingClass.Node.Id, tableId, edgeKind, funLine, funCol));
            }
        }
    }

    internal static void ParseRoomDaoCrud(
        KotlinFileParser.AnnotationInfo ann,
        Match fm,
        string content,
        FunctionNode funcNode,
        KotlinFileParser.ClassScope? enclosingClass,
        FileNode fileNode,
        string workspaceId,
        string relativePath,
        int funLine,
        int funCol)
    {
        var op = ann.Name.ToUpperInvariant() switch
        {
            "UPSERT" => OpUpsert,
            "INSERT" => OpInsert,
            "UPDATE" => OpUpdate,
            "DELETE" => OpDelete,
            _ => OpSelect
        };
        var edgeKind = op == OpSelect
            ? OntologyConstants.Relationships.ReadsFrom
            : OntologyConstants.Relationships.WritesTo;

        var sigSnippet = content.Substring(fm.Index, Math.Min(content.Length - fm.Index, fm.Length + 200));
        var paramMatch = Regex.Match(sigSnippet, @"\(\s*(?:vararg\s+)?(?:val|var)?\s*[a-zA-Z0-9_]+\s*:\s*(?:(?:List|Set|Collection|Array|Iterable)<([A-Za-z0-9_]+)>|([A-Za-z0-9_]+))");
        var targetEntity = paramMatch.Success
            ? (!string.IsNullOrEmpty(paramMatch.Groups[1].Value) ? paramMatch.Groups[1].Value : paramMatch.Groups[2].Value)
            : enclosingClass?.RepositoryEntity;

        if (!string.IsNullOrWhiteSpace(targetEntity))
        {
            var qId = $"{workspaceId}:{OntologyConstants.IdPrefixes.Query}:{relativePath}:{funLine}:{funCol}";
            var qName = $"{op} {targetEntity}";
            var synthSql = $"{op} {targetEntity}";

            var queryNode = new QueryNode(qId, qName, synthSql, relativePath, funLine, funLine)
            {
                FilePath = relativePath,
                Line = funLine,
                LineEnd = funLine,
                Operation = op,
                Verb = op,
                EvidenceKind = OntologyConstants.EvidenceKinds.Query
            };
            queryNode.CustomProperties ??= new();
            queryNode.CustomProperties["file_path"] = relativePath;
            fileNode.Children.Add(queryNode);

            fileNode.References.Add(new Reference(qId, funcNode.Id, OntologyConstants.Relationships.AccessedBy, funLine, funCol));
            if (enclosingClass != null)
            {
                fileNode.References.Add(new Reference(qId, enclosingClass.Node.Id, OntologyConstants.Relationships.AccessedBy, funLine, funCol));
            }

            var tableId = $"{workspaceId}:{OntologyConstants.IdPrefixes.Table}:{targetEntity.ToLowerInvariant()}";
            fileNode.References.Add(new Reference(qId, tableId, edgeKind, funLine, funCol));
            fileNode.References.Add(new Reference(funcNode.Id, tableId, edgeKind, funLine, funCol));
            if (enclosingClass != null)
            {
                fileNode.References.Add(new Reference(enclosingClass.Node.Id, tableId, edgeKind, funLine, funCol));
            }
        }
    }
}
