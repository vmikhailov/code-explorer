using System.Text.RegularExpressions;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Parser.Sql;
using CodeExplorer.Core.Parser.Sql.Ast;
using CodeExplorer.Core.Parser.Sql.Dialects;
using CodeExplorer.Core.Parser.Syntax;

namespace CodeExplorer.Parser.SQL;

public class SqlFileParser : IFileParser
{
    public string LanguageName => "sql";

    public IReadOnlyList<ISemanticExtension> SemanticExtensions => [];

    private const string RelationalEngine = "relational";

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
        var queryCounter = 0;
        var sqlChildren = new List<SyntaxNode>();

        void ProcessCreateTable(SqlCreateTableStatementAst createTable)
        {
            var tableName = createTable.Table.TableName;
            var schema = createTable.Table.Schema ?? "dbo";
            if (string.IsNullOrWhiteSpace(tableName) || IsDisallowedIdentifier(tableName)) return;

            var ds = GetOrCreateDataSet(schema);
            var tableNodeId = $"{ds.Id}:table:{tableName.ToLowerInvariant()}";
            if (!tables.TryGetValue(tableName, out var tableNode))
            {
                tableNode = new TableNode(tableNodeId, tableName, relativePath);
                tables[tableName] = tableNode;
                ds.Children.Add(tableNode);
            }
            tables[createTable.Table.CanonicalName] = tableNode;

            sqlChildren.Add(new SqlStmtSyntax
            {
                Operation = SqlOperationKind.CreateTable,
                OperationVerb = "CREATE TABLE",
                Tables = [new SqlTableRefSyntax(tableName, IsWriteTarget: true)],
                RawSql = tableName
            });
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

                case SqlCreateProcedureStatementAst createProc:
                {
                    var procName = createProc.Procedure.ProcedureName;
                    var schema = createProc.Procedure.Schema ?? "dbo";
                    if (string.IsNullOrWhiteSpace(procName) || IsDisallowedIdentifier(procName)) continue;

                    var ds = GetOrCreateDataSet(schema);
                    var procNodeId = $"{ds.Id}:procedure:{procName.ToLowerInvariant()}";
                    if (!procedures.TryGetValue(procName, out var procNode))
                    {
                        procNode = new ProcedureNode(procNodeId, procName, relativePath);
                        procedures[procName] = procNode;
                        ds.Children.Add(procNode);
                    }

                    sqlChildren.Add(new SqlStmtSyntax
                    {
                        Operation = SqlOperationKind.CreateProcedure,
                        OperationVerb = "CREATE PROCEDURE",
                        Procedures = [procName],
                        RawSql = createProc.RawBody
                    });

                    foreach (var bodyStmt in createProc.BodyStatements)
                    {
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
