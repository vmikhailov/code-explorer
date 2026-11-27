using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Parser.Syntax;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parsers.Java;

/// <summary>
/// Dedicated file parser for JBoss Seam 2 XML descriptors (pages.xml, components.xml) and Tuckey urlrewrite.xml.
/// Extracts page/URL endpoints, navigation redirects, component-to-class wiring, JMS queues, and EL bean action references.
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
        public const string RuleTag = "rule";
        public const string FromTag = "from";
        public const string ToTag = "to";
        public const string ViewIdAttr = "view-id";
        public const string ClassAttr = "class";
        public const string NameAttr = "name";
        public const string TypeAttr = "type";
        public const string QueueJndiAttr = "queue-jndi-name";
        public const string TopicJndiAttr = "topic-jndi-name";
        public const string ResourcePathPrefixAttr = "resource-path-prefix";
        public const string GetMethod = "GET";
        public const string AnyMethod = "ANY";
        public const string ProtocolSeam = "jsf-seam";
        public const string ProtocolRestEasy = "jaxrs-resteasy";
        public const string ProtocolUrlRewrite = "urlrewrite";
        public const string JmsPrefix = "jms:";
        public const string GetterPrefix = "get";
        public const string BooleanGetterPrefix = "is";
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
        @"#\{[^}]*?([A-Za-z_][A-Za-z0-9_]*)\.([A-Za-z_][A-Za-z0-9_]*)(\s*\()?",
        RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(200));

    private static readonly Regex RewriteGroupRegex = new(
        @"\([^)]+\)",
        RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(100));

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

        var text = FileStreamReader.ReadTextOnce(filePath);
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
        string absoluteWorkspacePath,
        string? preloadedContent = null)
    {
        var relativePath = Path.GetRelativePath(absoluteWorkspacePath, filePath).Replace('\\', '/');
        var fileName = Path.GetFileName(filePath);
        var fileId = $"{workspaceId}:{OntologyConstants.IdPrefixes.File}:{relativePath}";
        var fileNode = new FileNode(fileId, fileName, relativePath, filePath);

        var content = preloadedContent ?? await FileStreamReader.ReadTextOnceAsync(filePath);
        var doc = SyntaxDocument.Create(content, LanguageName, filePath, relativePath);
        var rawTypeBindings = new List<RawTypeBinding>();
        if (string.IsNullOrWhiteSpace(content))
        {
            return new SyntaxTree(filePath, relativePath, doc, fileNode, this, [], [], rawTypeBindings);
        }

        try
        {
            ParseSeamXml(content, fileNode, relativePath, workspaceId, rawTypeBindings);
        }
        catch (Exception ex) when (ex is XmlException or InvalidOperationException or RegexMatchTimeoutException)
        {
            // Tolerate malformed XML gracefully
        }

        return new SyntaxTree(filePath, relativePath, doc, fileNode, this, [], [], rawTypeBindings);
    }

    private static void ParseSeamXml(
        string content,
        FileNode fileNode,
        string relativePath,
        string workspaceId,
        List<RawTypeBinding> rawTypeBindings)
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
            ParseSeamComponents(xDoc.Root, fileNode, relativePath, workspaceId, rawTypeBindings);
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
            if (string.IsNullOrWhiteSpace(viewId)) continue;

            var lineInfo = (IXmlLineInfo)page;
            var line = lineInfo.HasLineInfo() ? lineInfo.LineNumber : 1;
            var sourceId = fileNode.Id;

            if (viewId != Constants.WildcardView)
            {
                var route = viewId.StartsWith('/') ? viewId : "/" + viewId;
                if (emittedRoutes.Add(route))
                {
                    var ep = CreateRouteEndpoint(workspaceId, relativePath, route, Constants.ProtocolSeam, line);
                    fileNode.Children.Add(ep);
                    sourceId = ep.Id;
                }
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

    private static void ParseSeamComponents(
        XElement root,
        FileNode fileNode,
        string relativePath,
        string workspaceId,
        List<RawTypeBinding> rawTypeBindings)
    {
        foreach (var elem in root.Descendants())
        {
            var lineInfo = (IXmlLineInfo)elem;
            var line = lineInfo.HasLineInfo() ? lineInfo.LineNumber : 1;

            var classAttr = elem.Attribute(Constants.ClassAttr)?.Value?.Trim()
                            ?? elem.Attribute(Constants.TypeAttr)?.Value?.Trim();
            if (!string.IsNullOrWhiteSpace(classAttr))
            {
                var simpleClass = ExtractSimpleClassName(classAttr);
                fileNode.References.Add(new Reference(fileNode.Id, simpleClass, OntologyConstants.Relationships.UsesType, line, 1));
                if (!string.Equals(simpleClass, classAttr, StringComparison.Ordinal))
                {
                    fileNode.References.Add(new Reference(fileNode.Id, classAttr, OntologyConstants.Relationships.UsesType, line, 1));
                }

                var compName = elem.Attribute(Constants.NameAttr)?.Value?.Trim();
                if (!string.IsNullOrWhiteSpace(compName))
                {
                    rawTypeBindings.Add(new RawTypeBinding(compName, simpleClass, string.Empty, string.Empty));
                }
            }

            var restPrefix = elem.Attribute(Constants.ResourcePathPrefixAttr)?.Value?.Trim();
            if (!string.IsNullOrWhiteSpace(restPrefix))
            {
                var route = restPrefix.StartsWith('/') ? restPrefix : "/" + restPrefix;
                var epId = $"{workspaceId}:{OntologyConstants.IdPrefixes.Endpoint}:{Constants.AnyMethod}:{route}";
                var ep = new EndpointNode(
                    epId,
                    $"{Constants.AnyMethod}:{route}",
                    relativePath,
                    Constants.AnyMethod,
                    route,
                    Constants.ProtocolRestEasy,
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
                    Operation = Constants.AnyMethod,
                    EvidenceKind = OntologyConstants.EvidenceKinds.Endpoint
                };
                fileNode.Children.Add(ep);
            }

            var jndiQueue = elem.Attribute(Constants.QueueJndiAttr)?.Value?.Trim()
                            ?? elem.Attribute(Constants.TopicJndiAttr)?.Value?.Trim();
            if (!string.IsNullOrWhiteSpace(jndiQueue))
            {
                fileNode.References.Add(new Reference(fileNode.Id, Constants.JmsPrefix + jndiQueue, OntologyConstants.Relationships.PublishesTo, line, 1));
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

    private static string ExtractSimpleClassName(string fqn)
    {
        var dotIdx = fqn.LastIndexOf('.');
        return (dotIdx >= 0 && dotIdx < fqn.Length - 1) ? fqn[(dotIdx + 1)..] : fqn;
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
            var hasParen = m.Groups[3].Success;
            if (BuiltInElRoots.Contains(bean)) continue;

            fileNode.References.Add(new Reference(sourceId, bean, OntologyConstants.Relationships.UsesType, line, 1));
            fileNode.References.Add(new Reference(sourceId, $"{bean}.{member}", OntologyConstants.Relationships.Calls, line, 1));

            if (!hasParen &&
                !member.StartsWith(Constants.GetterPrefix, StringComparison.Ordinal) &&
                !member.StartsWith(Constants.BooleanGetterPrefix, StringComparison.Ordinal))
            {
                var pascalProp = FaceletsXhtmlParser.ToPascalIdentifier(member);
                fileNode.References.Add(new Reference(sourceId, $"{bean}.{Constants.GetterPrefix}{pascalProp}", OntologyConstants.Relationships.Calls, line, 1));
                fileNode.References.Add(new Reference(sourceId, $"{bean}.{Constants.BooleanGetterPrefix}{pascalProp}", OntologyConstants.Relationships.Calls, line, 1));
            }
        }
    }
}
