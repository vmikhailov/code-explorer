using System.Text.RegularExpressions;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Parser.Syntax;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parsers.Java;

/// <summary>
/// Dedicated file parser for JSF / Facelets XHTML view templates (*.xhtml).
/// Extracts view page endpoints, Facelets template/include composition links, navigation outcomes,
/// and EL bean method + JavaBean getter/boolean bindings (#{bean.action()}, #{bean.totalDocuments}).
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
        public const string GetterPrefix = "get";
        public const string BooleanGetterPrefix = "is";
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
        @"#\{([^}]{1,512})\}",
        RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(250));

    private static readonly Regex ElMemberAccessRegex = new(
        @"\b([A-Za-z_][A-Za-z0-9_]*)\.([A-Za-z_][A-Za-z0-9_]*)(\s*\()?",
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
        var lineStarts = BuildLineStarts(content);
        string? endpointId = null;

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
            endpointId = ep.Id;
        }

        var seenTemplates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match m in TemplateOrIncludeRegex.Matches(content))
        {
            var target = m.Groups[1].Value.Trim();
            if (seenTemplates.Add(target))
            {
                var line = GetLineNumber(lineStarts, m.Index);
                fileNode.References.Add(new Reference(fileNode.Id, target, OntologyConstants.Relationships.DependsOn, line, 1));
            }
        }

        var seenOutcomes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match m in NavigationOutcomeRegex.Matches(content))
        {
            var outcome = m.Groups[1].Value.Trim();
            if (seenOutcomes.Add(outcome))
            {
                var line = GetLineNumber(lineStarts, m.Index);
                var src = endpointId ?? fileNode.Id;
                fileNode.References.Add(new Reference(src, outcome, OntologyConstants.Relationships.Calls, line, 1));
            }
        }

        var seenBeanTypes = new HashSet<string>(StringComparer.Ordinal);
        var seenCalls = new HashSet<(string Target, int Line)>();

        foreach (Match elMatch in ElExpressionRegex.Matches(content))
        {
            var expr = elMatch.Groups[1].Value;
            var line = GetLineNumber(lineStarts, elMatch.Index);

            foreach (Match chain in ElMemberAccessRegex.Matches(expr))
            {
                var bean = chain.Groups[1].Value;
                var member = chain.Groups[2].Value;
                var hasParen = chain.Groups[3].Success;
                if (BuiltInElRoots.Contains(bean)) continue;

                if (seenBeanTypes.Add(bean))
                {
                    fileNode.References.Add(new Reference(fileNode.Id, bean, OntologyConstants.Relationships.UsesType, line, 1));
                    var pascalBean = ToPascalIdentifier(bean);
                    if (!string.Equals(pascalBean, bean, StringComparison.Ordinal))
                    {
                        fileNode.References.Add(new Reference(fileNode.Id, pascalBean, OntologyConstants.Relationships.UsesType, line, 1));
                    }
                    if (endpointId != null)
                    {
                        fileNode.References.Add(new Reference(endpointId, bean, OntologyConstants.Relationships.UsesType, line, 1));
                    }
                }

                EmitBeanCall(fileNode, fileNode.Id, endpointId, $"{bean}.{member}", line, seenCalls);

                if (!hasParen &&
                    !member.StartsWith(Constants.GetterPrefix, StringComparison.Ordinal) &&
                    !member.StartsWith(Constants.BooleanGetterPrefix, StringComparison.Ordinal))
                {
                    var pascalProp = ToPascalIdentifier(member);
                    EmitBeanCall(fileNode, fileNode.Id, endpointId, $"{bean}.{Constants.GetterPrefix}{pascalProp}", line, seenCalls);
                    EmitBeanCall(fileNode, fileNode.Id, endpointId, $"{bean}.{Constants.BooleanGetterPrefix}{pascalProp}", line, seenCalls);
                }
            }
        }
    }

    private static void EmitBeanCall(
        FileNode fileNode,
        string fileId,
        string? endpointId,
        string qualifiedTarget,
        int line,
        HashSet<(string Target, int Line)> seenCalls)
    {
        if (!seenCalls.Add((qualifiedTarget, line))) return;

        fileNode.References.Add(new Reference(fileId, qualifiedTarget, OntologyConstants.Relationships.Calls, line, 1));
        if (endpointId != null)
        {
            fileNode.References.Add(new Reference(endpointId, qualifiedTarget, OntologyConstants.Relationships.Calls, line, 1));
        }
    }

    internal static string ToPascalIdentifier(string identifier) =>
        string.IsNullOrEmpty(identifier) || char.IsUpper(identifier[0])
            ? identifier
            : $"{char.ToUpperInvariant(identifier[0])}{identifier[1..]}";

    internal static List<int> BuildLineStarts(string text)
    {
        var starts = new List<int>(128) { 0 };
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                starts.Add(i + 1);
            }
        }
        return starts;
    }

    internal static int GetLineNumber(List<int> lineStarts, int charIndex)
    {
        var idx = lineStarts.BinarySearch(charIndex);
        return idx >= 0 ? idx + 1 : ~idx;
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
