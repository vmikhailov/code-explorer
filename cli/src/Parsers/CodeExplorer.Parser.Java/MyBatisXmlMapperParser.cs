using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Parser;
using CodeExplorer.Core.Parser.Syntax;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.Java;

/// <summary>
/// Dedicated file parser for MyBatis / iBatis XML SQL mappers (*Mapper.xml, *Dao.xml, resources/**/mapper/*.xml).
/// Extracts SQL queries from statement tags (select, insert, update, delete), extracts target database tables,
/// constructs Query nodes, and connects queries to Java mapper interfaces and methods via ACCESSED_BY and IMPLEMENTED_BY.
/// </summary>
public class MyBatisXmlMapperParser : IFileParser
{
    private static class Constants
    {
        public const string LanguageName = "mybatis-xml";
        public const string XmlExtension = ".xml";
        public const string MapperTag = "mapper";
        public const string NamespaceAttr = "namespace";
        public const string SqlTag = "sql";
        public const string IdAttr = "id";
        public const string IncludeTag = "include";
        public const string RefIdAttr = "refid";
        public const string SelectTag = "select";
        public const string InsertTag = "insert";
        public const string UpdateTag = "update";
        public const string DeleteTag = "delete";
        public const string SelectVerb = "SELECT";
        public const string InsertVerb = "INSERT";
        public const string UpdateVerb = "UPDATE";
        public const string DeleteVerb = "DELETE";
        public const string ParamPlaceholder = "@p";

        public const string PomXml = "pom.xml";
        public const string AndroidManifestXml = "AndroidManifest.xml";
        public const string WebXml = "web.xml";
        public const string BuildXml = "build.xml";
        public const string GeneratorConfigXml = "generatorConfig.xml";
        public const string LogbackPrefix = "logback";
        public const string Log4jPrefix = "log4j";
        public const string ApplicationPrefix = "application";
        public const string SettingsXml = "settings.xml";

        public const string MapperSuffix = "mapper.xml";
        public const string DaoSuffix = "dao.xml";
        public const string MapSuffix = "map.xml";
        public const string SqlMapSuffix = "sqlmap.xml";
        public const string RepositorySuffix = "repository.xml";

        public const string MapperDir = "/mapper/";
        public const string MappersDir = "/mappers/";
        public const string DaoDir = "/dao/";
        public const string DaosDir = "/daos/";
        public const string MybatisDir = "/mybatis/";
        public const string SqlMapDir = "/sqlmap/";

        public const string MapperOpenTag = "<mapper";
        public const string MybatisDtdMarker = "mybatis.org";
        public const string IbatisDtdMarker = "ibatis.apache.org";

        public const string WhereTag = "where";
        public const string SetTag = "set";
        public const string SelectKeyTag = "selectKey";
        public const string Space = " ";
        public const string WhereClause = " WHERE ";
        public const string SetClause = " SET ";
        public const string Asterisk = " * ";
        public const string EmptyString = "";
    }

    private static readonly HashSet<string> StatementTags = new(StringComparer.OrdinalIgnoreCase)
    {
        Constants.SelectTag,
        Constants.InsertTag,
        Constants.UpdateTag,
        Constants.DeleteTag
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
            fileName.Equals(Constants.AndroidManifestXml, StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals(Constants.WebXml, StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals(Constants.BuildXml, StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals(Constants.GeneratorConfigXml, StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals(Constants.SettingsXml, StringComparison.OrdinalIgnoreCase) ||
            fileName.StartsWith(Constants.LogbackPrefix, StringComparison.OrdinalIgnoreCase) ||
            fileName.StartsWith(Constants.Log4jPrefix, StringComparison.OrdinalIgnoreCase) ||
            fileName.StartsWith(Constants.ApplicationPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var lowerName = fileName.ToLowerInvariant();
        if (lowerName.EndsWith(Constants.MapperSuffix, StringComparison.Ordinal) ||
            lowerName.EndsWith(Constants.DaoSuffix, StringComparison.Ordinal) ||
            lowerName.EndsWith(Constants.MapSuffix, StringComparison.Ordinal) ||
            lowerName.EndsWith(Constants.SqlMapSuffix, StringComparison.Ordinal) ||
            lowerName.EndsWith(Constants.RepositorySuffix, StringComparison.Ordinal))
        {
            return true;
        }

        var normalized = filePath.Replace('\\', '/').ToLowerInvariant();
        if (normalized.Contains(Constants.MapperDir, StringComparison.Ordinal) ||
            normalized.Contains(Constants.MappersDir, StringComparison.Ordinal) ||
            normalized.Contains(Constants.DaoDir, StringComparison.Ordinal) ||
            normalized.Contains(Constants.DaosDir, StringComparison.Ordinal) ||
            normalized.Contains(Constants.MybatisDir, StringComparison.Ordinal) ||
            normalized.Contains(Constants.SqlMapDir, StringComparison.Ordinal))
        {
            return true;
        }

        if (PhysicalFileStore.TryGetFileText(filePath, out var text) && !string.IsNullOrEmpty(text))
        {
            return text.Contains(Constants.MapperOpenTag, StringComparison.OrdinalIgnoreCase) ||
                   text.Contains(Constants.MybatisDtdMarker, StringComparison.OrdinalIgnoreCase) ||
                   text.Contains(Constants.IbatisDtdMarker, StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    public BaseParserVisitor CreateVisitor(
        Node rootNode,
        List<ISemanticExtension> activeExtensions,
        string relativePath,
        string absoluteWorkspacePath,
        IFileParser fileParser,
        SemanticExtensionRegistry extensionRegistry)
    {
        throw new NotSupportedException("MyBatisXmlMapperParser does not use TreeSitter visitors.");
    }

    public async Task<SyntaxTree> ParseAsync(
        string filePath,
        string parentNodeId,
        string workspaceId,
        string absoluteWorkspacePath)
    {
        var relativePath = Path.GetRelativePath(absoluteWorkspacePath, filePath).Replace('\\', '/');
        var fileName = Path.GetFileName(filePath);
        var fileId = $"{workspaceId}:{OntologyConstants.IdPrefixes.File}:{relativePath}";
        var fileNode = new FileNode(fileId, fileName, relativePath, filePath);

        var content = await PhysicalFileStore.ConsumeFileTextAsync(filePath);
        var doc = SyntaxDocument.Create(content, LanguageName, filePath, relativePath);
        if (string.IsNullOrWhiteSpace(content))
        {
            return new SyntaxTree(filePath, relativePath, doc, fileNode, this, [], [], []);
        }

        try
        {
            ParseXmlMapperDocument(content, fileNode, relativePath, workspaceId);
        }
        catch (Exception)
        {
            // Tolerate malformed XML gracefully without interrupting pipeline
        }

        return new SyntaxTree(filePath, relativePath, doc, fileNode, this, [], [], []);
    }

    private static void ParseXmlMapperDocument(
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
        if (xDoc.Root == null) return;

        var ns = xDoc.Root.Attribute(Constants.NamespaceAttr)?.Value?.Trim();

        var sqlSnippets = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var sqlElem in xDoc.Root.Elements(Constants.SqlTag))
        {
            var snippetId = sqlElem.Attribute(Constants.IdAttr)?.Value?.Trim();
            if (!string.IsNullOrEmpty(snippetId))
            {
                sqlSnippets[snippetId] = ExtractSqlText(sqlElem, sqlSnippets);
            }
        }

        foreach (var elem in xDoc.Root.Elements())
        {
            var tagName = elem.Name.LocalName;
            if (!StatementTags.Contains(tagName)) continue;

            var stmtId = elem.Attribute(Constants.IdAttr)?.Value?.Trim();
            if (string.IsNullOrEmpty(stmtId)) continue;

            var lineInfo = (IXmlLineInfo)elem;
            var startLine = lineInfo.HasLineInfo() ? lineInfo.LineNumber : 1;
            var endLine = startLine;

            var rawSql = ExtractSqlText(elem, sqlSnippets);
            if (string.IsNullOrWhiteSpace(rawSql)) continue;

            var normalizedSql = NormalizeMyBatisParameters(rawSql);
            var cleanSql = NestedSqlParser.CleanQueryText(normalizedSql);

            var verb = tagName.ToUpperInvariant();
            var queryId = $"{workspaceId}:{OntologyConstants.IdPrefixes.Query}:{relativePath}:{stmtId}";
            var queryName = !string.IsNullOrEmpty(ns) ? $"{ns}.{stmtId}" : $"{verb} {stmtId}";

            var queryNode = NestedSqlParser.ParseNestedSql(normalizedSql, queryId, relativePath, null, queryName, startLine, endLine)
                            ?? new QueryNode(queryId, queryName, cleanSql, relativePath, startLine, endLine)
                            {
                                Verb = verb,
                                Operation = verb,
                                EvidenceKind = OntologyConstants.EvidenceKinds.Query,
                                FilePath = relativePath,
                                Line = startLine,
                                LineEnd = endLine
                            };

            fileNode.Children.Add(queryNode);

            RegisterQueryTableReferences(queryNode, fileNode, verb, startLine);
            RegisterQueryMapperReferences(queryNode, fileNode, ns, stmtId, startLine);
        }
    }

    private static void RegisterQueryTableReferences(
        QueryNode queryNode,
        FileNode fileNode,
        string verb,
        int startLine)
    {
        var relKind = verb is Constants.InsertVerb or Constants.UpdateVerb or Constants.DeleteVerb
            ? OntologyConstants.Relationships.WritesTo
            : OntologyConstants.Relationships.ReadsFrom;

        var tableNames = new List<string>();
        CollectTableNames(queryNode, tableNames);
        foreach (var tblName in tableNames)
        {
            fileNode.References.Add(new Reference(queryNode.Id, tblName, relKind, startLine, 1));
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

    private static void RegisterQueryMapperReferences(
        QueryNode queryNode,
        FileNode fileNode,
        string? ns,
        string stmtId,
        int startLine)
    {
        var targetMethod = !string.IsNullOrEmpty(ns) ? $"{ns}.{stmtId}" : stmtId;
        fileNode.References.Add(new Reference(queryNode.Id, targetMethod, OntologyConstants.Relationships.AccessedBy, startLine, 1));
        fileNode.References.Add(new Reference(queryNode.Id, targetMethod, OntologyConstants.Relationships.ImplementedBy, startLine, 1));

        if (!string.IsNullOrEmpty(ns))
        {
            fileNode.References.Add(new Reference(queryNode.Id, ns, OntologyConstants.Relationships.AccessedBy, startLine, 1));
            fileNode.References.Add(new Reference(queryNode.Id, ns, OntologyConstants.Relationships.ImplementedBy, startLine, 1));
        }
    }

    private static string ExtractSqlText(XElement element, IReadOnlyDictionary<string, string> sqlSnippets)
    {
        var textBuilder = new System.Text.StringBuilder();

        foreach (var node in element.Nodes())
        {
            if (node is XText text)
            {
                textBuilder.Append(text.Value);
            }
            else if (node is XCData cdata)
            {
                textBuilder.Append(cdata.Value);
            }
            else if (node is XElement child)
            {
                var local = child.Name.LocalName;
                if (local.Equals(Constants.IncludeTag, StringComparison.OrdinalIgnoreCase))
                {
                    var refid = child.Attribute(Constants.RefIdAttr)?.Value?.Trim();
                    if (!string.IsNullOrEmpty(refid) && sqlSnippets.TryGetValue(refid, out var snippet))
                    {
                        textBuilder.Append(Constants.Space).Append(snippet).Append(Constants.Space);
                    }
                    else
                    {
                        textBuilder.Append(Constants.Asterisk);
                    }
                }
                else if (local.Equals(Constants.SelectKeyTag, StringComparison.OrdinalIgnoreCase))
                {
                    // Ignore secondary selectKey statements inside insert
                }
                else if (local.Equals(Constants.WhereTag, StringComparison.OrdinalIgnoreCase))
                {
                    textBuilder.Append(Constants.WhereClause);
                    textBuilder.Append(ExtractSqlText(child, sqlSnippets));
                }
                else if (local.Equals(Constants.SetTag, StringComparison.OrdinalIgnoreCase))
                {
                    textBuilder.Append(Constants.SetClause);
                    textBuilder.Append(ExtractSqlText(child, sqlSnippets));
                }
                else
                {
                    textBuilder.Append(Constants.Space);
                    textBuilder.Append(ExtractSqlText(child, sqlSnippets));
                    textBuilder.Append(Constants.Space);
                }
            }
        }

        return textBuilder.ToString();
    }

    private static string NormalizeMyBatisParameters(string rawSql)
    {
        if (string.IsNullOrEmpty(rawSql)) return rawSql;
        var result = Regex.Replace(rawSql, @"\#\{[^}]+\}", Constants.ParamPlaceholder);
        result = Regex.Replace(result, @"\$\{[^}]+\}", Constants.ParamPlaceholder);
        return result;
    }
}
