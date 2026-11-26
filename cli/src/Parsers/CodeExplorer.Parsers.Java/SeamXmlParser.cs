using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Parser;
using CodeExplorer.Core.Parser.Syntax;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parsers.Java;

/// <summary>
/// Dedicated file parser for JBoss Seam 2 XML descriptors (pages.xml, components.xml) and Tuckey urlrewrite.xml.
/// Extracts page/URL endpoints, navigation redirects, component-to-class wiring, and EL bean action references.
/// </summary>
public class SeamXmlParser : IFileParser
{
    private static class Constants
    {
        public const string LanguageName = "seam-xml";
        public const string XmlExtension = ".xml";
        public const string SeamPagesNs = "seam/pages";
        public const string SeamComponentsNs = "seam/components";
        public const string UrlRewriteTag = "<urlrewrite";
        public const string PagesRoot = "pages";
        public const string ComponentsRoot = "components";
        public const string UrlRewriteRoot = "urlrewrite";
        public const string PageTag = "page";
        public const string RedirectTag = "redirect";
        public const string ComponentTag = "component";
        public const string RuleTag = "rule";
        public const string FromTag = "from";
        public const string ToTag = "to";
        public const string ViewIdAttr = "view-id";
        public const string ClassAttr = "class";
        public const string GetMethod = "GET";
        public const string ProtocolSeam = "jsf-seam";
        public const string ProtocolUrlRewrite = "urlrewrite";
        public const string WildcardView = "*";
        public const string StreamingModeUnary = "Unary";
        public const string PomXml = "pom.xml";
        public const string WebXml = "web.xml";
        public const string PagesXml = "pages.xml";
        public const string ComponentsXml = "components.xml";
        public const string UrlRewriteXml = "urlrewrite.xml";
        public const string PageXmlSuffix = ".page.xml";
    }

    private static readonly Regex ElReferenceRegex = new(
        @"#\{[^}]*?([A-Za-z_][A-Za-z0-9_]*)\.([A-Za-z_][A-Za-z0-9_]*)",
        RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(200));

    private static readonly HashSet<string> ElAttributes = new(StringComparer.OrdinalIgnoreCase)
    {
        "action", "from-action", "evaluate", "if", "value", "execute", "url"
    };

    private static readonly HashSet<string> BuiltInElRoots = new(StringComparer.OrdinalIgnoreCase)
    {
        "msgs", "messages", "param", "facesContext", "request", "session",
        "application", "cc", "component", "flash", "view", "empty", "not", "true", "false"
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

        if (fileName.Equals(Constants.PagesXml, StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals(Constants.ComponentsXml, StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals(Constants.UrlRewriteXml, StringComparison.OrdinalIgnoreCase) ||
            fileName.EndsWith(Constants.PageXmlSuffix, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var text = PhysicalFileStore.ReadAllText(filePath);
        if (string.IsNullOrEmpty(text)) return false;

        return text.Contains(Constants.SeamPagesNs, StringComparison.OrdinalIgnoreCase) ||
               text.Contains(Constants.SeamComponentsNs, StringComparison.OrdinalIgnoreCase) ||
               text.Contains(Constants.UrlRewriteTag, StringComparison.OrdinalIgnoreCase);
    }

    public BaseParserVisitor CreateVisitor(
        Node rootNode,
        List<ISemanticExtension> activeExtensions,
        string relativePath,
        string absoluteWorkspacePath,
        IFileParser fileParser,
        SemanticExtensionRegistry extensionRegistry)
    {
        throw new NotSupportedException("SeamXmlParser does not use TreeSitter visitors.");
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
            ParseSeamXml(content, fileNode, relativePath, workspaceId);
        }
        catch (Exception ex) when (ex is XmlException or InvalidOperationException or RegexMatchTimeoutException)
        {
            // Tolerate malformed XML gracefully
        }

        return new SyntaxTree(filePath, relativePath, doc, fileNode, this, [], [], []);
    }

    private static void ParseSeamXml(
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

        var rootName = xDoc.Root.Name.LocalName;
        if (rootName.Equals(Constants.PagesRoot, StringComparison.OrdinalIgnoreCase))
        {
            ParseSeamPages(xDoc.Root, fileNode, relativePath, workspaceId);
        }
        else if (rootName.Equals(Constants.ComponentsRoot, StringComparison.OrdinalIgnoreCase))
        {
            ParseSeamComponents(xDoc.Root, fileNode);
        }
        else if (rootName.Equals(Constants.UrlRewriteRoot, StringComparison.OrdinalIgnoreCase))
        {
            ParseUrlRewrite(xDoc.Root, fileNode, relativePath, workspaceId);
        }
    }

    private static void ParseSeamPages(
        XElement root,
        FileNode fileNode,
        string relativePath,
        string workspaceId)
    {
        var emittedRoutes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var page in root.Descendants().Where(e => e.Name.LocalName.Equals(Constants.PageTag, StringComparison.OrdinalIgnoreCase)))
        {
            var viewId = page.Attribute(Constants.ViewIdAttr)?.Value?.Trim();
            if (string.IsNullOrWhiteSpace(viewId) || viewId == Constants.WildcardView) continue;

            var lineInfo = (IXmlLineInfo)page;
            var line = lineInfo.HasLineInfo() ? lineInfo.LineNumber : 1;
            var route = viewId.StartsWith('/') ? viewId : "/" + viewId;
            var sourceId = fileNode.Id;

            if (emittedRoutes.Add(route))
            {
                var ep = CreateRouteEndpoint(workspaceId, relativePath, route, Constants.ProtocolSeam, line);
                fileNode.Children.Add(ep);
                sourceId = ep.Id;
            }

            foreach (var elem in page.DescendantsAndSelf())
            {
                var elemLine = ((IXmlLineInfo)elem).HasLineInfo() ? ((IXmlLineInfo)elem).LineNumber : line;

                foreach (var attr in elem.Attributes())
                {
                    if (ElAttributes.Contains(attr.Name.LocalName))
                    {
                        ExtractElReferences(attr.Value, sourceId, fileNode, elemLine);
                    }
                }

                if (elem.Name.LocalName.Equals(Constants.RedirectTag, StringComparison.OrdinalIgnoreCase))
                {
                    var targetView = elem.Attribute(Constants.ViewIdAttr)?.Value?.Trim();
                    if (!string.IsNullOrWhiteSpace(targetView))
                    {
                        fileNode.References.Add(new Reference(sourceId, targetView, OntologyConstants.Relationships.Calls, elemLine, 1));
                    }
                }
            }
        }
    }

    private static void ParseSeamComponents(XElement root, FileNode fileNode)
    {
        foreach (var elem in root.Descendants())
        {
            var lineInfo = (IXmlLineInfo)elem;
            var line = lineInfo.HasLineInfo() ? lineInfo.LineNumber : 1;

            var classAttr = elem.Attribute(Constants.ClassAttr)?.Value?.Trim();
            if (!string.IsNullOrWhiteSpace(classAttr))
            {
                fileNode.References.Add(new Reference(fileNode.Id, classAttr, OntologyConstants.Relationships.References, line, 1));
                var dotIdx = classAttr.LastIndexOf('.');
                if (dotIdx >= 0 && dotIdx < classAttr.Length - 1)
                {
                    fileNode.References.Add(new Reference(fileNode.Id, classAttr[(dotIdx + 1)..], OntologyConstants.Relationships.References, line, 1));
                }
            }

            foreach (var attr in elem.Attributes())
            {
                if (attr.Value.Contains("#{", StringComparison.Ordinal))
                {
                    ExtractElReferences(attr.Value, fileNode.Id, fileNode, line);
                }
            }
        }
    }

    private static void ParseUrlRewrite(
        XElement root,
        FileNode fileNode,
        string relativePath,
        string workspaceId)
    {
        var emittedRoutes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var rule in root.Descendants().Where(e => e.Name.LocalName.Equals(Constants.RuleTag, StringComparison.OrdinalIgnoreCase)))
        {
            var fromElem = rule.Elements().FirstOrDefault(e => e.Name.LocalName.Equals(Constants.FromTag, StringComparison.OrdinalIgnoreCase));
            var toElem = rule.Elements().FirstOrDefault(e => e.Name.LocalName.Equals(Constants.ToTag, StringComparison.OrdinalIgnoreCase));
            var rawFrom = fromElem?.Value?.Trim();
            if (string.IsNullOrWhiteSpace(rawFrom) || rawFrom == ".*" || rawFrom == "^.*$") continue;

            var cleanRoute = NormalizeRewritePattern(rawFrom);
            if (string.IsNullOrWhiteSpace(cleanRoute) || !cleanRoute.StartsWith('/')) continue;

            var lineInfo = (IXmlLineInfo)rule;
            var line = lineInfo.HasLineInfo() ? lineInfo.LineNumber : 1;

            if (emittedRoutes.Add(cleanRoute))
            {
                var ep = CreateRouteEndpoint(workspaceId, relativePath, cleanRoute, Constants.ProtocolUrlRewrite, line);
                fileNode.Children.Add(ep);

                var target = toElem?.Value?.Trim();
                if (!string.IsNullOrWhiteSpace(target))
                {
                    var qIdx = target.IndexOfAny(['?', '\\']);
                    var cleanTarget = qIdx > 0 ? target[..qIdx] : target;
                    fileNode.References.Add(new Reference(ep.Id, cleanTarget, OntologyConstants.Relationships.Calls, line, 1));
                }
            }
        }
    }

    private static readonly Regex RewriteGroupRegex = new(
        @"\([^)]+\)",
        RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(100));

    private static string NormalizeRewritePattern(string pattern)
    {
        var s = pattern.Trim();
        if (s.StartsWith('^')) s = s[1..];
        if (s.EndsWith('$')) s = s[..^1];
        s = RewriteGroupRegex.Replace(s, "{param}");
        return s.Trim();
    }

    private static EndpointNode CreateRouteEndpoint(
        string workspaceId,
        string relativePath,
        string route,
        string protocol,
        int line)
    {
        var name = $"{Constants.GetMethod}:{route}";
        var epId = $"{workspaceId}:{OntologyConstants.IdPrefixes.Endpoint}:{Constants.GetMethod}:{route}";
        return new EndpointNode(
            epId,
            name,
            relativePath,
            Constants.GetMethod,
            route,
            protocol,
            false,
            null,
            null,
            null,
            null,
            Constants.StreamingModeUnary)
        {
            FilePath = relativePath,
            StartLine = line,
            EndLine = line,
            Line = line,
            LineEnd = line,
            Operation = Constants.GetMethod,
            EvidenceKind = OntologyConstants.EvidenceKinds.Endpoint
        };
    }

    private static void ExtractElReferences(string text, string sourceId, FileNode fileNode, int line)
    {
        if (string.IsNullOrEmpty(text) || !text.Contains("#{", StringComparison.Ordinal)) return;

        foreach (Match m in ElReferenceRegex.Matches(text))
        {
            var bean = m.Groups[1].Value;
            var member = m.Groups[2].Value;
            if (BuiltInElRoots.Contains(bean)) continue;

            fileNode.References.Add(new Reference(sourceId, $"{bean}.{member}", OntologyConstants.Relationships.Calls, line, 1));
            fileNode.References.Add(new Reference(sourceId, bean, OntologyConstants.Relationships.References, line, 1));
        }
    }
}
