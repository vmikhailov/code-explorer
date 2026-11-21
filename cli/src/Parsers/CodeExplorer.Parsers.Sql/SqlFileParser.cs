using System.Text.RegularExpressions;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Parser.Sql;
using CodeExplorer.Core.Parser.Sql.Ast;
using CodeExplorer.Core.Parser.Sql.Dialects;
using CodeExplorer.Core.Parser.Syntax;

namespace CodeExplorer.Parsers.Sql;

public class SqlFileParser : IFileParser
{
    public string LanguageName => "sql";

    public IReadOnlyList<ISemanticExtension> SemanticExtensions => [];

    private const string RelationalEngine = "relational";
    private static readonly string TableSubKind = "table";

    public bool CanParse(string fileExtension)
    {
        return fileExtension.Equals(".sql", StringComparison.OrdinalIgnoreCase);
    }

    public BaseParserVisitor CreateVisitor(
        SyntaxNode rootNode,
        List<ISemanticExtension> activeExtensions,
        string relativePath,
        string absoluteWorkspacePath,
        IFileParser fileParser,
        SemanticExtensionRegistry extensionRegistry)
    {
        throw new NotSupportedException("SQL Parser does not use TreeSitter visitors.");
    }

    public ImportType ResolveImportType(string importPath, string filePath, string? absoluteWorkspacePath)
    {
        return ImportType.External;
    }

    public bool UsesTreeSitter => false;

    public async Task<SyntaxTree> ParseAsync(string filePath, string parentNodeId, string workspaceId, string absoluteWorkspacePath)
    {
        var relativePath = Path.GetRelativePath(absoluteWorkspacePath, filePath).Replace('\\', '/');
        var fileNodeId = $"{workspaceId}:file:{relativePath}";

        var fileNode = new FileNode(fileNodeId, Path.GetFileName(filePath), relativePath, filePath);
        var sqlText = await PhysicalFileStore.ConsumeFileTextAsync(filePath);

        var dialect = SqlDialectDetector.DetectDialect(sqlText, filePath);
        var dialectParser = SqlDialectParserFactory.GetParser(dialect);
        var script = dialectParser.Parse(sqlText);

        var datasets = new Dictionary<string, DataSetNode>(StringComparer.OrdinalIgnoreCase);
        var tables = new Dictionary<string, TableNode>(StringComparer.OrdinalIgnoreCase);
        var procedures = new Dictionary<string, ProcedureNode>(StringComparer.OrdinalIgnoreCase);

        // 1. Identify Database (DB)
        var dbMatch = Regex.Match(sqlText, @"CREATE\s+DATABASE\s+([a-zA-Z0-9_\[\]""#@`]+)", RegexOptions.IgnoreCase);
        DatabaseNode? dbNode = null;
        string baseParentId = fileNodeId;

        if (dbMatch.Success)
        {
            var dbName = dbMatch.Groups[1].Value.Trim('[', ']', '"', '`');
            var dbNodeId = $"{workspaceId}:db:{dbName.ToLowerInvariant()}";
            dbNode = new DatabaseNode(dbNodeId, dbName, relativePath, RelationalEngine);
            fileNode.Children.Add(dbNode);
            baseParentId = dbNodeId;
        }

        void AddToParent(IOntologyNode child)
        {
            if (dbNode != null)
            {
                dbNode.Children.Add(child);
            }
            else
            {
                fileNode.Children.Add(child);
            }
        }

        DataSetNode GetOrCreateDataSet(string schemaName)
        {
            if (string.IsNullOrWhiteSpace(schemaName) || IsDisallowedIdentifier(schemaName))
            {
                schemaName = "dbo";
            }

            if (!datasets.TryGetValue(schemaName, out var schemaNode))
            {
                var schemaNodeId = $"{baseParentId}:dataset:{schemaName.ToLowerInvariant()}";
                schemaNode = new DataSetNode(schemaNodeId, schemaName, relativePath);
                datasets[schemaName] = schemaNode;
                AddToParent(schemaNode);
            }
            return schemaNode;
        }

        // 2. Identify Schema (DataSet)
        var schemaMatches = Regex.Matches(sqlText,
            @"(?i)\bCREATE\s+SCHEMA\s+(?>(?:IF\s+NOT\s+EXISTS?\s+)?)(?<schemaName>[^\s\(;]+)");
        foreach (Match match in schemaMatches)
        {
            var rawSchemaName = match.Groups["schemaName"].Value;
            var schemaName = CleanIdentifier(rawSchemaName);
            if (!string.IsNullOrWhiteSpace(schemaName) && !IsDisallowedIdentifier(schemaName))
            {
                GetOrCreateDataSet(schemaName);
            }
        }

        // 3. Process statements from dialect AST
        var engineName = dialect.ToString().ToLowerInvariant();
        var queryCounter = 0;
        var sqlChildren = new List<SyntaxNode>();

        TableNode GetOrCreateTableNode(string tableName, string schema, string? subKind = null)
        {
            var resolvedSubKind = string.IsNullOrEmpty(subKind) ? TableSubKind : subKind;
            var ds = GetOrCreateDataSet(schema);
            var tableNodeId = $"{ds.Id}:table:{tableName.ToLowerInvariant()}";
            if (!tables.TryGetValue(tableName, out var tableNode))
            {
                tableNode = new TableNode(tableNodeId, tableName, relativePath)
                {
                    Engine = engineName,
                    Schema = schema,
                    SubKind = resolvedSubKind
                };
                tables[tableName] = tableNode;
                ds.Children.Add(tableNode);
            }
            else if (string.IsNullOrEmpty(tableNode.SubKind))
            {
                tableNode.SubKind = resolvedSubKind;
            }
            return tableNode;
        }

        void AddForeignKeyReferences(TableNode sourceTable, IReadOnlyList<SqlForeignKeyAst>? foreignKeys, IReadOnlyList<SqlTableRefAst>? fallbackRefs)
        {
            var emitted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (foreignKeys is { Count: > 0 })
            {
                foreach (var fk in foreignKeys)
                {
                    var target = fk.TargetTable.TableName;
                    if (string.IsNullOrWhiteSpace(target) || IsDisallowedIdentifier(target)) continue;
                    var ctx = !string.IsNullOrWhiteSpace(fk.FromColumn)
                        ? $"{fk.FromColumn}->{SqlSchemaHelper.FormatFk(fk)}"
                        : SqlSchemaHelper.FormatFk(fk);
                    if (emitted.Add($"{target}:{ctx}"))
                    {
                        sourceTable.References.Add(new Reference(sourceTable.Id, target, OntologyConstants.Relationships.References, argFlow: ctx));
                    }
                }
            }
            else if (fallbackRefs is { Count: > 0 })
            {
                foreach (var refTbl in fallbackRefs)
                {
                    if (string.IsNullOrWhiteSpace(refTbl.TableName) || IsDisallowedIdentifier(refTbl.TableName)) continue;
                    if (emitted.Add(refTbl.TableName))
                    {
                        sourceTable.References.Add(new Reference(sourceTable.Id, refTbl.TableName, OntologyConstants.Relationships.References));
                    }
                }
            }
        }

        void ProcessCreateTable(SqlCreateTableStatementAst createTable, string subKind = "table")
        {
            var tableName = createTable.Table.TableName;
            var schema = createTable.Table.Schema ?? "dbo";
            if (string.IsNullOrWhiteSpace(tableName) || IsDisallowedIdentifier(tableName)) return;

            var tableNode = GetOrCreateTableNode(tableName, schema, subKind);
            tables[createTable.Table.CanonicalName] = tableNode;

            SqlSchemaHelper.MergeColumnsIntoTable(tableNode, createTable.Columns, createTable.ForeignKeys);
            AddForeignKeyReferences(tableNode, createTable.ForeignKeys, createTable.ReferencedTables);

            sqlChildren.Add(new SqlStmtSyntax
            {
                Operation = SqlOperationKind.CreateTable,
                OperationVerb = subKind == "table" ? "CREATE TABLE" : "CREATE VIEW",
                Tables = [new SqlTableRefSyntax(tableName, IsWriteTarget: true)],
                RawSql = tableName
            });

            if (createTable.AsSelectQuery != null)
            {
                var readTables = new List<SqlTableRefAst>();
                var writeTables = new List<SqlTableRefAst>();
                var calledProcs = new List<SqlProcRefAst>();
                SqlSchemaHelper.CollectStatementTableAccess(createTable.AsSelectQuery, readTables, writeTables, calledProcs);
                foreach (var src in readTables.DistinctBy(t => t.TableName, StringComparer.OrdinalIgnoreCase))
                {
                    if (!string.IsNullOrEmpty(src.TableName) && !IsDisallowedIdentifier(src.TableName))
                    {
                        tableNode.References.Add(new Reference(tableNode.Id, src.TableName, OntologyConstants.Relationships.ReadsFrom));
                    }
                }
                AddQueryNode(createTable.AsSelectQuery, fileNode, fileNodeId);
            }
        }

        foreach (var stmt in script.Statements)
        {
            switch (stmt)
            {
                case SqlCreateTableStatementAst createTable:
                {
                    ProcessCreateTable(createTable);
                    break;
                }

                case SqlAlterTableStatementAst alterTable:
                {
                    var tableName = alterTable.TargetTable.TableName;
                    var schema = alterTable.TargetTable.Schema ?? "dbo";
                    if (string.IsNullOrWhiteSpace(tableName) || IsDisallowedIdentifier(tableName)) break;

                    var tableNode = GetOrCreateTableNode(tableName, schema);
                    SqlSchemaHelper.MergeColumnsIntoTable(tableNode, alterTable.AddedColumns, alterTable.ForeignKeys);
                    AddForeignKeyReferences(tableNode, alterTable.ForeignKeys, alterTable.ReferencedTables);
                    break;
                }

                case SqlCreateViewStatementAst createView:
                {
                    var viewKind = createView.IsMaterialized ? "materialized_view" : "view";
                    ProcessCreateTable(new SqlCreateTableStatementAst(createView.View, false, createView.AsSelectQuery), viewKind);
                    break;
                }

                case SqlCreateProcedureStatementAst createProc:
                {
                    var procName = createProc.Procedure.ProcedureName;
                    var schema = createProc.Procedure.Schema ?? "dbo";
                    if (string.IsNullOrWhiteSpace(procName) || IsDisallowedIdentifier(procName)) continue;

                    var routineSubKind = createProc.RoutineKind switch
                    {
                        SqlRoutineKind.Function => "function",
                        SqlRoutineKind.Trigger => "trigger",
                        _ => "procedure"
                    };

                    var ds = GetOrCreateDataSet(schema);
                    var procNodeId = $"{ds.Id}:procedure:{procName.ToLowerInvariant()}";
                    if (!procedures.TryGetValue(procName, out var procNode))
                    {
                        procNode = new ProcedureNode(procNodeId, procName, relativePath)
                        {
                            Engine = engineName,
                            Schema = schema,
                            SubKind = routineSubKind,
                            TriggerTable = createProc.TriggerTable?.TableName,
                            TriggerEvent = createProc.TriggerEvent
                        };
                        procedures[procName] = procNode;
                        ds.Children.Add(procNode);
                    }

                    sqlChildren.Add(new SqlStmtSyntax
                    {
                        Operation = SqlOperationKind.CreateProcedure,
                        OperationVerb = $"CREATE {routineSubKind.ToUpperInvariant()}",
                        Procedures = [procName],
                        RawSql = createProc.RawBody
                    });

                    var procReads = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    var procWrites = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    var procCalls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    foreach (var bodyStmt in createProc.BodyStatements)
                    {
                        var rList = new List<SqlTableRefAst>();
                        var wList = new List<SqlTableRefAst>();
                        var cList = new List<SqlProcRefAst>();
                        SqlSchemaHelper.CollectStatementTableAccess(bodyStmt, rList, wList, cList);

                        foreach (var r in rList)
                        {
                            if (!string.IsNullOrEmpty(r.TableName) && !IsDisallowedIdentifier(r.TableName) && procReads.Add(r.TableName))
                            {
                                procNode.References.Add(new Reference(procNode.Id, r.TableName, OntologyConstants.Relationships.ReadsFrom));
                            }
                        }
                        foreach (var w in wList)
                        {
                            if (!string.IsNullOrEmpty(w.TableName) && !IsDisallowedIdentifier(w.TableName) && procWrites.Add(w.TableName))
                            {
                                procNode.References.Add(new Reference(procNode.Id, w.TableName, OntologyConstants.Relationships.WritesTo));
                            }
                        }
                        foreach (var c in cList)
                        {
                            if (!string.IsNullOrEmpty(c.ProcedureName) && !IsDisallowedIdentifier(c.ProcedureName) && procCalls.Add(c.ProcedureName))
                            {
                                procNode.References.Add(new Reference(procNode.Id, c.ProcedureName, OntologyConstants.Relationships.Calls));
                            }
                        }

                        if (bodyStmt is SqlCreateTableStatementAst innerTable)
                        {
                            ProcessCreateTable(innerTable);
                        }
                        else
                        {
                            AddQueryNode(bodyStmt, procNode, procNodeId);
                        }
                    }
                    break;
                }

                default:
                    AddQueryNode(stmt, fileNode, fileNodeId);
                    break;
            }
        }

        void AddQueryNode(SqlStatementAst queryStmt, IOntologyNode parentNode, string parentId)
        {
            var queryTables = new List<SqlTableRefAst>();
            var queryProcs = new List<SqlProcRefAst>();
            string verb;

            switch (queryStmt)
            {
                case SqlSelectStatementAst sel:
                    verb = "SELECT";
                    queryTables.AddRange(sel.Tables);
                    queryTables.AddRange(sel.Joins);
                    break;
                case SqlInsertStatementAst ins:
                    verb = "INSERT";
                    queryTables.Add(ins.TargetTable);
                    if (ins.SourceQuery is SqlSelectStatementAst selSub)
                    {
                        queryTables.AddRange(selSub.Tables);
                        queryTables.AddRange(selSub.Joins);
                    }
                    break;
                case SqlUpdateStatementAst upd:
                    verb = "UPDATE";
                    queryTables.Add(upd.TargetTable);
                    queryTables.AddRange(upd.FromTables);
                    break;
                case SqlDeleteStatementAst del:
                    verb = "DELETE";
                    queryTables.Add(del.TargetTable);
                    queryTables.AddRange(del.UsingTables);
                    break;
                case SqlMergeStatementAst mrg:
                    verb = "MERGE";
                    queryTables.Add(mrg.TargetTable);
                    if (mrg.UsingTable != null) queryTables.Add(mrg.UsingTable);
                    break;
                case SqlExecStatementAst ex:
                    verb = "EXEC";
                    queryProcs.Add(ex.Procedure);
                    break;
                default:
                    return;
            }

            if (queryStmt.Ctes.Count > 0)
            {
                foreach (var cte in queryStmt.Ctes)
                {
                    if (cte.InnerStatement is SqlSelectStatementAst cteSel)
                    {
                        queryTables.AddRange(cteSel.Tables);
                        queryTables.AddRange(cteSel.Joins);
                    }
                }
            }

            if (queryTables.Count == 0 && queryProcs.Count == 0) return;

            queryCounter++;
            var qId = $"{parentId}:query:{queryCounter}";
            var qName = $"{verb} Query #{queryCounter}";
            var qNode = new QueryNode(qId, qName, $"{verb} Query", relativePath);
            parentNode.Children.Add(qNode);

            foreach (var t in queryTables)
            {
                if (!string.IsNullOrEmpty(t.TableName) && !IsDisallowedIdentifier(t.TableName))
                {
                    qNode.References.Add(new Reference(qId, t.TableName, OntologyConstants.Relationships.DependsOn));
                }
            }
            foreach (var p in queryProcs)
            {
                if (!string.IsNullOrEmpty(p.ProcedureName) && !IsDisallowedIdentifier(p.ProcedureName))
                {
                    qNode.References.Add(new Reference(qId, p.ProcedureName, OntologyConstants.Relationships.Calls));
                }
            }
        }

        var doc = SyntaxDocument.Create(sqlText, LanguageName, filePath, relativePath, sqlChildren);
        return new SyntaxTree(filePath, relativePath, doc, fileNode, this, [], [], []);
    }

    public static string CleanIdentifier(string raw)
    {
        return BaseSqlParser.CleanIdentifier(raw);
    }

    public static bool IsDisallowedIdentifier(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return true;
        var trimmed = CleanIdentifier(name);
        if (trimmed.Length <= 1) return true;
        if (char.IsDigit(trimmed[0])) return true;
        return BaseSqlParser.CleanIdentifier(trimmed).Length <= 1;
    }

    public static (string schemaName, string tableName) ParseTableIdentifier(string rawTableName)
    {
        var (_, schema, table) = BaseSqlParser.ParseTableIdentifier(rawTableName);
        return (schema ?? "dbo", table);
    }

    public void CollectSemanticData(CodeExplorer.Core.Parser.Syntax.SyntaxNode node, string filePath, List<RawImport> rawImports, List<RawVariable> rawVariables, List<RawTypeBinding> rawTypeBindings)
    {
    }
}
