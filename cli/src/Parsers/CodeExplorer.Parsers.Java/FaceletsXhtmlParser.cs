using System.Text.RegularExpressions;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Parser;
using CodeExplorer.Core.Parser.Syntax;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parsers.Java;

/// <summary>
/// Dedicated file parser for JSF / Facelets XHTML view templates (*.xhtml).
/// Extracts view page endpoints, Facelets template/include composition links, navigation outcomes, and EL bean bindings (#{bean.action}).
/// </summary>
public class FaceletsXhtmlParser : IFileParser
{
    private static class Constants
    {
        public const string LanguageName = "facelets-xhtml";
        public const string XhtmlExtension = ".xhtml";
        public const string GetMethod = "GET";
        public const string ProtocolFacelets = "jsf-facelets";
        public const string StreamingModeUnary = "Unary";
        public const string WebappMarker = "/webapp/";
        public const string WebInfMarker = "/WEB-INF/";
    }

    private static readonly Regex TemplateOrIncludeRegex = new(
        @"<(?:ui:composition|ui:decorate|ui:include)\b[^>]*?\b(?:template|src)\s*=\s*[""']([^""']+\.xhtml)[""']",
        RegexOptions.IgnoreCase | RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(250));

    private static readonly Regex NavigationOutcomeRegex = new(
        @"\b(?:outcome|view)\s*=\s*[""'](/[^""']+\.xhtml)[""']",
        RegexOptions.IgnoreCase | RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(250));

    private static readonly Regex ElExpressionRegex = new(
        @"#\{([^}]{1,256})\}",
        RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(250));

    private static readonly Regex ElIdentifierChainRegex = new(
        @"\b([A-Za-z_][A-Za-z0-9_]*)\.([A-Za-z_][A-Za-z0-9_]*)\b",
        RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(200));

    private static readonly HashSet<string> BuiltInElRoots = new(StringComparer.OrdinalIgnoreCase)
    {
        "msgs", "messages", "param", "facesContext", "request", "session",
        "application", "cc", "component", "flash", "view", "header", "cookie",
        "initParam", "empty", "not", "true", "false", "null", "fn"
    };

    public string LanguageName => Constants.LanguageName;
    public bool UsesTreeSitter => false;
    public IReadOnlyList<ISemanticExtension> SemanticExtensions { get; } = [];

    public bool CanParse(string fileExtension) =>
        !string.IsNullOrEmpty(fileExtension) &&
        fileExtension.Equals(Constants.XhtmlExtension, StringComparison.OrdinalIgnoreCase);

    public bool CanParseFile(string filePath) =>
        !string.IsNullOrEmpty(filePath) &&
        Path.GetExtension(filePath).Equals(Constants.XhtmlExtension, StringComparison.OrdinalIgnoreCase);

    public ImportType ResolveImportType(string importPath, string filePath, string? absoluteWorkspacePath) =>
        ImportType.Internal;

    public BaseParserVisitor CreateVisitor(
        Node rootNode,
        List<ISemanticExtension> activeExtensions,
        string relativePath,
        string absoluteWorkspacePath,
        IFileParser fileParser,
        SemanticExtensionRegistry extensionRegistry)
    {
        throw new NotSupportedException("FaceletsXhtmlParser does not use TreeSitter visitors.");
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
            ParseFaceletsPage(content, fileNode, relativePath, workspaceId);
        }
        catch (RegexMatchTimeoutException)
        {
            // Tolerate pathologically large or malformed templates gracefully
        }

        return new SyntaxTree(filePath, relativePath, doc, fileNode, this, [], [], []);
    }

    private static void ParseFaceletsPage(
        string content,
        FileNode fileNode,
        string relativePath,
        string workspaceId)
    {
        var sourceId = fileNode.Id;
        var pageRoute = TryDeriveWebappRoute(relativePath);
        if (pageRoute != null)
        {
            var epId = $"{workspaceId}:{OntologyConstants.IdPrefixes.Endpoint}:{Constants.GetMethod}:{pageRoute}";
            var ep = new EndpointNode(
                epId,
                $"{Constants.GetMethod}:{pageRoute}",
                relativePath,
                Constants.GetMethod,
                pageRoute,
                Constants.ProtocolFacelets,
                false,
                null,
                null,
                null,
                null,
                Constants.StreamingModeUnary)
            {
                FilePath = relativePath,
                StartLine = 1,
                EndLine = 1,
                Line = 1,
                LineEnd = 1,
                Operation = Constants.GetMethod,
                EvidenceKind = OntologyConstants.EvidenceKinds.Endpoint
            };
            fileNode.Children.Add(ep);
            sourceId = ep.Id;
        }

        var seenTemplates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match m in TemplateOrIncludeRegex.Matches(content))
        {
            var target = m.Groups[1].Value.Trim();
            if (seenTemplates.Add(target))
            {
                fileNode.References.Add(new Reference(sourceId, target, OntologyConstants.Relationships.References, 1, 1));
            }
        }

        var seenOutcomes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match m in NavigationOutcomeRegex.Matches(content))
        {
            var outcome = m.Groups[1].Value.Trim();
            if (seenOutcomes.Add(outcome))
            {
                fileNode.References.Add(new Reference(sourceId, outcome, OntologyConstants.Relationships.Calls, 1, 1));
            }
        }

        var seenEl = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match elMatch in ElExpressionRegex.Matches(content))
        {
            var expr = elMatch.Groups[1].Value;
            foreach (Match chain in ElIdentifierChainRegex.Matches(expr))
            {
                var bean = chain.Groups[1].Value;
                var member = chain.Groups[2].Value;
                if (BuiltInElRoots.Contains(bean)) continue;

                var qualified = $"{bean}.{member}";
                if (seenEl.Add(qualified))
                {
                    fileNode.References.Add(new Reference(sourceId, qualified, OntologyConstants.Relationships.Calls, 1, 1));
                }
                if (seenEl.Add(bean))
                {
                    fileNode.References.Add(new Reference(sourceId, bean, OntologyConstants.Relationships.References, 1, 1));
                }
            }
        }
    }

    private static string? TryDeriveWebappRoute(string relativePath)
    {
        var normalized = "/" + relativePath.TrimStart('/');
        var idx = normalized.IndexOf(Constants.WebappMarker, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return null;

        var subPath = normalized[(idx + Constants.WebappMarker.Length - 1)..];
        if (subPath.Contains(Constants.WebInfMarker, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return subPath;
    }
}
