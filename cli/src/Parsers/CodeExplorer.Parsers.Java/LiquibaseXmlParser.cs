using System.Xml;
using System.Xml.Linq;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Parser.Syntax;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parsers.Java;

/// <summary>
/// Dedicated file parser for Liquibase XML database migration changelogs (&lt;databaseChangeLog&gt;).
/// Extracts Table DDL mutations, foreign-key table references, inline &lt;sql&gt; blocks, and &lt;sqlFile&gt; references.
/// </summary>
public class LiquibaseXmlParser : IFileParser
{
    private static class Constants
    {
        public const string LanguageName = "liquibase-xml";
        public const string XmlExtension = ".xml";
        public const string RootElementName = "databaseChangeLog";
        public const string RootOpenTag = "<databaseChangeLog";
        public const string NamespaceMarker = "liquibase.org/xml/ns/dbchangelog";
        public const string ChangeSetTag = "changeSet";
        public const string SqlTag = "sql";
        public const string SqlFileTag = "sqlFile";
        public const string RenameTableTag = "renameTable";
        public const string AddForeignKeyTag = "addForeignKeyConstraint";
        public const string IncludeTag = "include";
        public const string IdAttr = "id";
        public const string TableNameAttr = "tableName";
        public const string OldTableNameAttr = "oldTableName";
        public const string NewTableNameAttr = "newTableName";
        public const string BaseTableNameAttr = "baseTableName";
        public const string ReferencedTableNameAttr = "referencedTableName";
        public const string PathAttr = "path";
        public const string FileAttr = "file";
        public const string VerbDdl = "DDL";
        public const string VerbSql = "SQL";
        public const string DefaultDialect = "sql";
        public const string PomXml = "pom.xml";
        public const string WebXml = "web.xml";
        public const string ChangelogNameMarker = "changelog";
        public const string ChangelogsDirMarker = "/changelogs/";
        public const string LiquibaseDirMarker = "/liquibase/";
    }

    private static readonly HashSet<string> TableMutationTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "createTable", "dropTable", "addColumn", "dropColumn", "modifyDataType",
        "renameColumn", "createIndex", "dropIndex", "addPrimaryKey", "dropPrimaryKey",
        "addUniqueConstraint", "dropUniqueConstraint", "addNotNullConstraint",
        "dropNotNullConstraint", "addDefaultValue", "dropDefaultValue",
        "addAutoIncrement", "insert", "update", "delete", "loadData", "loadUpdateData"
    };

    public string LanguageName => Constants.LanguageName;
    public bool UsesTreeSitter => false;
    public IReadOnlyList<ISemanticExtension> SemanticExtensions { get; } = [];

    public bool CanParse(string fileExtension) => false;

    public ImportType ResolveImportType(string importPath, string filePath, string? absoluteWorkspacePath) =>
        ImportType.Internal;

    public bool CanParseFile(string filePath)
    {
        if (string.IsNullOrEmpty(filePath)) return false;

        var ext = Path.GetExtension(filePath);
        if (!ext.Equals(Constants.XmlExtension, StringComparison.OrdinalIgnoreCase)) return false;

        var fileName = Path.GetFileName(filePath);
        if (fileName.Equals(Constants.PomXml, StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals(Constants.WebXml, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (fileName.Contains(Constants.ChangelogNameMarker, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var normalized = filePath.Replace('\\', '/');
        if (normalized.Contains(Constants.ChangelogsDirMarker, StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains(Constants.LiquibaseDirMarker, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var text = FileStreamReader.ReadTextOnce(filePath);
        if (string.IsNullOrEmpty(text)) return false;

        return text.Contains(Constants.RootOpenTag, StringComparison.OrdinalIgnoreCase) ||
               text.Contains(Constants.NamespaceMarker, StringComparison.OrdinalIgnoreCase);
    }

    public BaseParserVisitor CreateVisitor(
        Node rootNode,
        List<ISemanticExtension> activeExtensions,
        string relativePath,
        string absoluteWorkspacePath,
        IFileParser fileParser,
        SemanticExtensionRegistry extensionRegistry)
    {
        throw new NotSupportedException("LiquibaseXmlParser does not use TreeSitter visitors.");
    }

    public async Task<SyntaxTree> ParseAsync(
        string filePath,
        string parentNodeId,
        string workspaceId,
        string absoluteWorkspacePath,
        string? preloadedContent = null)
    {
        var relativePath = Path.GetRelativePath(absoluteWorkspacePath, filePath).Replace('\\', '/');
        var fileName = Path.GetFileName(filePath);
        var fileId = $"{workspaceId}:{OntologyConstants.IdPrefixes.File}:{relativePath}";
        var fileNode = new FileNode(fileId, fileName, relativePath, filePath);

        var content = preloadedContent ?? await FileStreamReader.ReadTextOnceAsync(filePath);
        var doc = SyntaxDocument.Create(content, LanguageName, filePath, relativePath);
        if (string.IsNullOrWhiteSpace(content))
        {
            return new SyntaxTree(filePath, relativePath, doc, fileNode, this, [], [], []);
        }

        try
        {
            ParseChangelogDocument(content, fileNode, relativePath, workspaceId);
        }
        catch (Exception ex) when (ex is XmlException or InvalidOperationException)
        {
            // Tolerate malformed Liquibase XML gracefully without interrupting indexing
        }

        return new SyntaxTree(filePath, relativePath, doc, fileNode, this, [], [], []);
    }

    private static void ParseChangelogDocument(
        string content,
        FileNode fileNode,
        string relativePath,
        string workspaceId)
    {
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Ignore,
            XmlResolver = null
        };

        using var stringReader = new StringReader(content);
        using var xmlReader = XmlReader.Create(stringReader, settings);
        var xDoc = XDocument.Load(xmlReader, LoadOptions.SetLineInfo);
        if (xDoc.Root == null || !xDoc.Root.Name.LocalName.Equals(Constants.RootElementName, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var emittedTables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queryIndex = 0;

        foreach (var elem in xDoc.Root.Descendants())
        {
            var tagName = elem.Name.LocalName;
            var lineInfo = (IXmlLineInfo)elem;
            var line = lineInfo.HasLineInfo() ? lineInfo.LineNumber : 1;

            if (TableMutationTags.Contains(tagName))
            {
                var tableName = elem.Attribute(Constants.TableNameAttr)?.Value?.Trim();
                if (!string.IsNullOrWhiteSpace(tableName))
                {
                    EmitTableAndMutation(fileNode, workspaceId, relativePath, tableName, tagName, line, emittedTables, ref queryIndex);
                }
            }
            else if (tagName.Equals(Constants.RenameTableTag, StringComparison.OrdinalIgnoreCase))
            {
                var oldName = elem.Attribute(Constants.OldTableNameAttr)?.Value?.Trim();
                var newName = elem.Attribute(Constants.NewTableNameAttr)?.Value?.Trim();
                if (!string.IsNullOrWhiteSpace(oldName))
                {
                    EmitTableAndMutation(fileNode, workspaceId, relativePath, oldName, tagName, line, emittedTables, ref queryIndex);
                }
                if (!string.IsNullOrWhiteSpace(newName))
                {
                    EmitTableAndMutation(fileNode, workspaceId, relativePath, newName, tagName, line, emittedTables, ref queryIndex);
                }
            }
            else if (tagName.Equals(Constants.AddForeignKeyTag, StringComparison.OrdinalIgnoreCase))
            {
                var baseTable = elem.Attribute(Constants.BaseTableNameAttr)?.Value?.Trim();
                var refTable = elem.Attribute(Constants.ReferencedTableNameAttr)?.Value?.Trim();
                if (!string.IsNullOrWhiteSpace(baseTable))
                {
                    EmitTableNodeOnce(fileNode, workspaceId, relativePath, baseTable, line, emittedTables);
                }
                if (!string.IsNullOrWhiteSpace(refTable))
                {
                    EmitTableNodeOnce(fileNode, workspaceId, relativePath, refTable, line, emittedTables);
                }
                if (!string.IsNullOrWhiteSpace(baseTable) && !string.IsNullOrWhiteSpace(refTable))
                {
                    var baseId = $"{workspaceId}:{OntologyConstants.IdPrefixes.Table}:{baseTable}";
                    fileNode.References.Add(new Reference(baseId, refTable, OntologyConstants.Relationships.References, line, 1));
                }
            }
            else if (tagName.Equals(Constants.SqlTag, StringComparison.OrdinalIgnoreCase))
            {
                var rawSql = elem.Value?.Trim();
                if (!string.IsNullOrWhiteSpace(rawSql))
                {
                    queryIndex++;
                    var changeSetId = elem.Parent?.Attribute(Constants.IdAttr)?.Value?.Trim() ?? queryIndex.ToString();
                    var queryId = $"{workspaceId}:{OntologyConstants.IdPrefixes.Query}:{relativePath}:sql:{changeSetId}:{queryIndex}";
                    var cleanSql = NestedSqlParser.CleanQueryText(rawSql);
                    var queryNode = NestedSqlParser.ParseNestedSql(rawSql, queryId, relativePath, null, $"liquibase:{changeSetId}", line, line)
                                    ?? new QueryNode(queryId, $"liquibase:{changeSetId}", cleanSql, relativePath, line, line)
                                    {
                                        Verb = Constants.VerbSql,
                                        Operation = Constants.VerbSql,
                                        EvidenceKind = OntologyConstants.EvidenceKinds.Query,
                                        FilePath = relativePath,
                                        Line = line,
                                        LineEnd = line
                                    };

                    fileNode.Children.Add(queryNode);
                    var sqlTables = new List<string>();
                    CollectTableNames(queryNode, sqlTables);
                    foreach (var tbl in sqlTables)
                    {
                        EmitTableNodeOnce(fileNode, workspaceId, relativePath, tbl, line, emittedTables);
                        fileNode.References.Add(new Reference(queryNode.Id, tbl, OntologyConstants.Relationships.WritesTo, line, 1));
                    }
                }
            }
            else if (tagName.Equals(Constants.SqlFileTag, StringComparison.OrdinalIgnoreCase))
            {
                var sqlPath = elem.Attribute(Constants.PathAttr)?.Value?.Trim();
                if (!string.IsNullOrWhiteSpace(sqlPath))
                {
                    fileNode.References.Add(new Reference(fileNode.Id, sqlPath, OntologyConstants.Relationships.References, line, 1));
                }
            }
            else if (tagName.Equals(Constants.IncludeTag, StringComparison.OrdinalIgnoreCase))
            {
                var incFile = elem.Attribute(Constants.FileAttr)?.Value?.Trim();
                if (!string.IsNullOrWhiteSpace(incFile))
                {
                    fileNode.References.Add(new Reference(fileNode.Id, incFile, OntologyConstants.Relationships.References, line, 1));
                }
            }
        }
    }

    private static void CollectTableNames(IOntologyNode node, List<string> tableNames)
    {
        if (node is TableNode tbl && !string.IsNullOrEmpty(tbl.Name))
        {
            tableNames.Add(tbl.Name);
        }
        foreach (var child in node.Children)
        {
            CollectTableNames(child, tableNames);
        }
    }

    private static void EmitTableAndMutation(
        FileNode fileNode,
        string workspaceId,
        string relativePath,
        string tableName,
        string operation,
        int line,
        HashSet<string> emittedTables,
        ref int queryIndex)
    {
        EmitTableNodeOnce(fileNode, workspaceId, relativePath, tableName, line, emittedTables);
        queryIndex++;
        var queryId = $"{workspaceId}:{OntologyConstants.IdPrefixes.Query}:{relativePath}:{operation}:{tableName}:{queryIndex}";
        var queryNode = new QueryNode(queryId, $"{operation} {tableName}", $"{operation} {tableName}", relativePath, line, line)
        {
            Verb = Constants.VerbDdl,
            Operation = operation,
            EvidenceKind = OntologyConstants.EvidenceKinds.Query,
            FilePath = relativePath,
            Line = line,
            LineEnd = line
        };
        fileNode.Children.Add(queryNode);
        fileNode.References.Add(new Reference(queryNode.Id, tableName, OntologyConstants.Relationships.WritesTo, line, 1));
    }

    private static void EmitTableNodeOnce(
        FileNode fileNode,
        string workspaceId,
        string relativePath,
        string tableName,
        int line,
        HashSet<string> emittedTables)
    {
        if (!emittedTables.Add(tableName)) return;

        var tableId = $"{workspaceId}:{OntologyConstants.IdPrefixes.Table}:{tableName}";
        var tableNode = new TableNode(tableId, tableName, relativePath)
        {
            Engine = Constants.DefaultDialect
        };
        fileNode.Children.Add(tableNode);
        fileNode.References.Add(new Reference(fileNode.Id, tableName, OntologyConstants.Relationships.UsesDb, line, 1));
    }
}
