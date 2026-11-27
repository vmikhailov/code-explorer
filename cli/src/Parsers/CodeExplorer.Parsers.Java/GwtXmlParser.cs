using System.Xml;
using System.Xml.Linq;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Parser.Syntax;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parsers.Java;

/// <summary>
/// Dedicated file parser for Google Web Toolkit (GWT) module descriptors (*.gwt.xml)
/// and UiBinder declarative UI templates (*.ui.xml).
/// </summary>
public class GwtXmlParser : IFileParser
{
    private static class Constants
    {
        public const string LanguageName = "gwt-xml";
        public const string XmlExtension = ".xml";
        public const string GwtModuleSuffix = ".gwt.xml";
        public const string UiBinderSuffix = ".ui.xml";
        public const string UiBinderNamespace = "urn:ui:com.google.gwt.uibinder";
        public const string UiBinderTagToken = ":UiBinder";
        public const string ModuleRoot = "module";
        public const string UiBinderRoot = "UiBinder";
        public const string EntryPointTag = "entry-point";
        public const string ServletTag = "servlet";
        public const string InheritsTag = "inherits";
        public const string WithTag = "with";
        public const string StyleTag = "style";
        public const string ClassAttr = "class";
        public const string PathAttr = "path";
        public const string NameAttr = "name";
        public const string FieldAttr = "field";
        public const string TypeAttr = "type";
        public const string PostMethod = "POST";
        public const string ProtocolGwtRpc = "gwt-rpc";
        public const string StreamingModeUnary = "Unary";
        public const string PomXml = "pom.xml";
        public const string WebXml = "web.xml";
        public const string PagesXml = "pages.xml";
        public const string ComponentsXml = "components.xml";
    }

    public string LanguageName => Constants.LanguageName;
    public bool UsesTreeSitter => false;
    public IReadOnlyList<ISemanticExtension> SemanticExtensions { get; } = [];

    public bool CanParse(string fileExtension) => false;

    public ImportType ResolveImportType(string importPath, string filePath, string? absoluteWorkspacePath) =>
        ImportType.Internal;

    public bool CanParseFile(string filePath)
    {
        if (string.IsNullOrEmpty(filePath)) return false;

        if (filePath.EndsWith(Constants.GwtModuleSuffix, StringComparison.OrdinalIgnoreCase) ||
            filePath.EndsWith(Constants.UiBinderSuffix, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var ext = Path.GetExtension(filePath);
        if (!ext.Equals(Constants.XmlExtension, StringComparison.OrdinalIgnoreCase)) return false;

        var fileName = Path.GetFileName(filePath);
        if (fileName.Equals(Constants.PomXml, StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals(Constants.WebXml, StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals(Constants.PagesXml, StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals(Constants.ComponentsXml, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var text = FileStreamReader.ReadTextOnce(filePath);
        if (string.IsNullOrEmpty(text)) return false;

        return text.Contains(Constants.UiBinderNamespace, StringComparison.OrdinalIgnoreCase) ||
               text.Contains(Constants.UiBinderTagToken, StringComparison.Ordinal);
    }

    public BaseParserVisitor CreateVisitor(
        Node rootNode,
        List<ISemanticExtension> activeExtensions,
        string relativePath,
        string absoluteWorkspacePath,
        IFileParser fileParser,
        SemanticExtensionRegistry extensionRegistry)
    {
        throw new NotSupportedException("GwtXmlParser does not use TreeSitter visitors.");
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
        var rawTypeBindings = new List<RawTypeBinding>();

        var content = preloadedContent ?? await FileStreamReader.ReadTextOnceAsync(filePath);
        var doc = SyntaxDocument.Create(content, LanguageName, filePath, relativePath);
        if (string.IsNullOrWhiteSpace(content))
        {
            return new SyntaxTree(filePath, relativePath, doc, fileNode, this, [], [], rawTypeBindings);
        }

        try
        {
            ParseGwtXml(content, fileName, fileNode, relativePath, workspaceId, rawTypeBindings);
        }
        catch (Exception ex) when (ex is XmlException or InvalidOperationException)
        {
            // Tolerate malformed XML gracefully
        }

        return new SyntaxTree(filePath, relativePath, doc, fileNode, this, [], [], rawTypeBindings);
    }

    private static void ParseGwtXml(
        string content,
        string fileName,
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
        if (rootName.Equals(Constants.ModuleRoot, StringComparison.OrdinalIgnoreCase))
        {
            ParseGwtModule(xDoc.Root, fileNode, relativePath, workspaceId);
        }
        else if (rootName.Equals(Constants.UiBinderRoot, StringComparison.OrdinalIgnoreCase))
        {
            ParseUiBinder(xDoc.Root, fileName, fileNode, rawTypeBindings);
        }
    }

    private static void ParseGwtModule(
        XElement root,
        FileNode fileNode,
        string relativePath,
        string workspaceId)
    {
        foreach (var elem in root.Descendants())
        {
            var lineInfo = (IXmlLineInfo)elem;
            var line = lineInfo.HasLineInfo() ? lineInfo.LineNumber : 1;
            var localName = elem.Name.LocalName;

            if (localName.Equals(Constants.EntryPointTag, StringComparison.OrdinalIgnoreCase))
            {
                var cls = elem.Attribute(Constants.ClassAttr)?.Value?.Trim();
                EmitClassReference(fileNode, fileNode.Id, cls, OntologyConstants.Relationships.UsesType, line);
            }
            else if (localName.Equals(Constants.InheritsTag, StringComparison.OrdinalIgnoreCase))
            {
                var inherited = elem.Attribute(Constants.NameAttr)?.Value?.Trim();
                EmitClassReference(fileNode, fileNode.Id, inherited, OntologyConstants.Relationships.DependsOn, line);
            }
            else if (localName.Equals(Constants.ServletTag, StringComparison.OrdinalIgnoreCase))
            {
                var path = elem.Attribute(Constants.PathAttr)?.Value?.Trim();
                var cls = elem.Attribute(Constants.ClassAttr)?.Value?.Trim();
                var sourceId = fileNode.Id;

                if (!string.IsNullOrWhiteSpace(path))
                {
                    var route = path.StartsWith('/') ? path : "/" + path;
                    var epId = $"{workspaceId}:{OntologyConstants.IdPrefixes.Endpoint}:{Constants.PostMethod}:{route}";
                    var ep = new EndpointNode(
                        epId,
                        $"{Constants.PostMethod}:{route}",
                        relativePath,
                        Constants.PostMethod,
                        route,
                        Constants.ProtocolGwtRpc,
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
                        Operation = Constants.PostMethod,
                        EvidenceKind = OntologyConstants.EvidenceKinds.Endpoint
                    };
                    fileNode.Children.Add(ep);
                    sourceId = ep.Id;
                }

                EmitClassReference(fileNode, sourceId, cls, OntologyConstants.Relationships.UsesType, line);
                if (sourceId != fileNode.Id)
                {
                    EmitClassReference(fileNode, fileNode.Id, cls, OntologyConstants.Relationships.UsesType, line);
                }
            }
        }
    }

    private static void ParseUiBinder(
        XElement root,
        string fileName,
        FileNode fileNode,
        List<RawTypeBinding> rawTypeBindings)
    {
        if (fileName.EndsWith(Constants.UiBinderSuffix, StringComparison.OrdinalIgnoreCase) &&
            fileName.Length > Constants.UiBinderSuffix.Length)
        {
            var companionClass = fileName[..^Constants.UiBinderSuffix.Length];
            fileNode.References.Add(new Reference(fileNode.Id, companionClass, OntologyConstants.Relationships.UsesType, 1, 1));
        }

        foreach (var elem in root.Descendants())
        {
            var lineInfo = (IXmlLineInfo)elem;
            var line = lineInfo.HasLineInfo() ? lineInfo.LineNumber : 1;
            var localName = elem.Name.LocalName;

            if (localName.Equals(Constants.WithTag, StringComparison.OrdinalIgnoreCase) ||
                localName.Equals(Constants.StyleTag, StringComparison.OrdinalIgnoreCase))
            {
                var typeAttr = elem.Attribute(Constants.TypeAttr)?.Value?.Trim();
                if (!string.IsNullOrWhiteSpace(typeAttr))
                {
                    var simpleClass = ExtractSimpleClassName(typeAttr);
                    EmitClassReference(fileNode, fileNode.Id, typeAttr, OntologyConstants.Relationships.UsesType, line);

                    var fieldAttr = elem.Attribute(Constants.FieldAttr)?.Value?.Trim();
                    if (!string.IsNullOrWhiteSpace(fieldAttr))
                    {
                        rawTypeBindings.Add(new RawTypeBinding(fieldAttr, simpleClass, string.Empty, string.Empty));
                    }
                }
            }
        }
    }

    private static void EmitClassReference(
        FileNode fileNode,
        string sourceId,
        string? fqn,
        string relationKind,
        int line)
    {
        if (string.IsNullOrWhiteSpace(fqn)) return;

        var simple = ExtractSimpleClassName(fqn);
        fileNode.References.Add(new Reference(sourceId, simple, relationKind, line, 1));
        if (!string.Equals(simple, fqn, StringComparison.Ordinal))
        {
            fileNode.References.Add(new Reference(sourceId, fqn, relationKind, line, 1));
        }
    }

    private static string ExtractSimpleClassName(string fqn)
    {
        var dotIdx = fqn.LastIndexOf('.');
        return (dotIdx >= 0 && dotIdx < fqn.Length - 1) ? fqn[(dotIdx + 1)..] : fqn;
    }
}
