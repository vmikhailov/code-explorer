using System.Xml;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Parser;
using CodeExplorer.Core.Parser.Syntax;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.Java;

/// <summary>
/// Dedicated file parser for Apache Dubbo Spring XML configuration files (*.xml with dubbo:service or dubbo:reference).
/// Extracts RPC service provider endpoints and consumer ExternalService client dependencies.
/// </summary>
public class DubboXmlParser : IFileParser
{
    private static class Constants
    {
        public const string LanguageName = "dubbo-xml";
        public const string XmlExtension = ".xml";
        public const string DubboServiceTag = "service";
        public const string DubboReferenceTag = "reference";
        public const string DubboMethodTag = "method";
        public const string InterfaceAttr = "interface";
        public const string ProtocolAttr = "protocol";
        public const string NameAttr = "name";
        public const string RefAttr = "ref";
        public const string DefaultProtocol = "dubbo";
        public const string RpcMethod = "RPC";
        public const string RpcPrefix = "RPC:/";
        public const string PomXml = "pom.xml";
        public const string AndroidManifestXml = "AndroidManifest.xml";
        public const string WebXml = "web.xml";
        public const string BuildXml = "build.xml";
        public const string SettingsXml = "settings.xml";
        public const string DubboNamespaceMarker = "http://dubbo.apache.org/schema/dubbo";
        public const string DubboSchemaMarker = "dubbo.xsd";
        public const string DubboPrefixTag = "<dubbo:";
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

        var ext = Path.GetExtension(filePath);
        if (!ext.Equals(Constants.XmlExtension, StringComparison.OrdinalIgnoreCase)) return false;

        var fileName = Path.GetFileName(filePath);
        if (fileName.Equals(Constants.PomXml, StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals(Constants.AndroidManifestXml, StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals(Constants.WebXml, StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals(Constants.BuildXml, StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals(Constants.SettingsXml, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string? text = null;
        if (!PhysicalFileStore.TryGetFileText(filePath, out text) && File.Exists(filePath))
        {
            try
            {
                using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var sr = new StreamReader(fs);
                text = sr.ReadToEnd();
            }
            catch
            {
                // Ignore file access errors
            }
        }

        if (!string.IsNullOrEmpty(text))
        {
            return text.Contains(Constants.DubboPrefixTag, StringComparison.OrdinalIgnoreCase) ||
                   text.Contains(Constants.DubboNamespaceMarker, StringComparison.OrdinalIgnoreCase) ||
                   text.Contains(Constants.DubboSchemaMarker, StringComparison.OrdinalIgnoreCase);
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
        throw new NotSupportedException("DubboXmlParser does not use TreeSitter visitors.");
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
            ParseDubboXmlDocument(content, fileNode, relativePath, workspaceId);
        }
        catch (Exception)
        {
            // Tolerate malformed XML gracefully without interrupting pipeline
        }

        return new SyntaxTree(filePath, relativePath, doc, fileNode, this, [], [], []);
    }

    private static void ParseDubboXmlDocument(
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
        using var reader = XmlReader.Create(stringReader, settings);
        var lineInfo = reader as IXmlLineInfo;

        while (reader.Read())
        {
            if (reader.NodeType != XmlNodeType.Element) continue;

            var localName = reader.LocalName;
            var isDubbo = reader.Prefix.Equals("dubbo", StringComparison.OrdinalIgnoreCase) ||
                          reader.NamespaceURI.Contains("dubbo", StringComparison.OrdinalIgnoreCase);

            if (!isDubbo) continue;

            if (localName.Equals(Constants.DubboServiceTag, StringComparison.OrdinalIgnoreCase))
            {
                var iface = reader.GetAttribute(Constants.InterfaceAttr);
                var proto = reader.GetAttribute(Constants.ProtocolAttr) ?? Constants.DefaultProtocol;
                var line = lineInfo?.LineNumber ?? 1;

                if (!string.IsNullOrWhiteSpace(iface))
                {
                    var cleanIface = CleanInterfaceName(iface);
                    var route = $"/{cleanIface}";
                    var name = $"{Constants.RpcPrefix}{cleanIface}";
                    var epId = $"{workspaceId}:{OntologyConstants.IdPrefixes.Endpoint}:{Constants.RpcMethod}:{route}";

                    var ep = new EndpointNode(
                        epId,
                        name,
                        relativePath,
                        Constants.RpcMethod,
                        route,
                        proto,
                        false,
                        null,
                        null,
                        null,
                        null,
                        "Unary")
                    {
                        FilePath = relativePath,
                        StartLine = line,
                        EndLine = line,
                        Line = line,
                        LineEnd = line,
                        Operation = Constants.RpcMethod,
                        EvidenceKind = OntologyConstants.EvidenceKinds.Endpoint
                    };

                    fileNode.Children.Add(ep);
                }
            }
            else if (localName.Equals(Constants.DubboReferenceTag, StringComparison.OrdinalIgnoreCase))
            {
                var iface = reader.GetAttribute(Constants.InterfaceAttr);
                var proto = reader.GetAttribute(Constants.ProtocolAttr) ?? Constants.DefaultProtocol;
                var line = lineInfo?.LineNumber ?? 1;

                if (!string.IsNullOrWhiteSpace(iface))
                {
                    var cleanIface = CleanInterfaceName(iface);
                    var scheme = proto.Equals("tri", StringComparison.OrdinalIgnoreCase) ? "tri://" : "dubbo://";
                    var extId = $"{workspaceId}:{OntologyConstants.IdPrefixes.ExternalService}:{proto}:{cleanIface}";

                    var ext = new ExternalServiceNode(
                        extId,
                        $"{scheme}{cleanIface}",
                        proto,
                        cleanIface,
                        $"/{cleanIface}")
                    {
                        FilePath = relativePath,
                        StartLine = line,
                        StartCol = lineInfo?.LinePosition ?? 1,
                        IsInternal = true,
                        Url = $"/{cleanIface}"
                    };

                    fileNode.Children.Add(ext);
                    fileNode.References.Add(new Reference(fileNode.Id, $"{scheme}{cleanIface}", OntologyConstants.Relationships.Calls));
                }
            }
        }
    }

    private static string CleanInterfaceName(string rawInterface)
    {
        var dotIdx = rawInterface.LastIndexOf('.');
        return (dotIdx >= 0 && dotIdx < rawInterface.Length - 1)
            ? rawInterface[(dotIdx + 1)..].Trim()
            : rawInterface.Trim();
    }
}
