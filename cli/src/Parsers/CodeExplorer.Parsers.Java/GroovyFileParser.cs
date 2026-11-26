using System.Text.RegularExpressions;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;
using CodeExplorer.Core.Parser;
using CodeExplorer.Core.Parser.Syntax;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parsers.Java;

/// <summary>
/// Lightweight file parser for Groovy source and build/automation scripts (*.groovy).
/// Extracts imports, classes/traits/interfaces/enums, methods, and method/constructor calls.
/// </summary>
public class GroovyFileParser : IFileParser
{
    private static class Constants
    {
        public const string LanguageName = "groovy";
        public const string GroovyExtension = ".groovy";
        public const string ClassKind = "class";
        public const string InterfaceKind = "interface";
        public const string EnumKind = "enum";
        public const string JavaPrefix = "java.";
        public const string JavaxPrefix = "javax.";
        public const string GroovyPrefix = "groovy.";
        public const string OrgCodehausGroovyPrefix = "org.codehaus.groovy.";
        public const string WildcardImportSuffix = ".*";
        public const string TypeSymbolSegment = "Type";
        public const string FunctionSymbolSegment = "Function";
    }

    private static readonly Regex ImportRegex = new(
        @"^\s*import\s+(?:static\s+)?([A-Za-z_][A-Za-z0-9_.*]*)",
        RegexOptions.Multiline | RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(200));

    private static readonly Regex TypeDeclRegex = new(
        @"^\s*(?:(?:public|protected|private|abstract|final|static)\s+)*(class|interface|enum|trait)\s+([A-Za-z_][A-Za-z0-9_]*)(?:\s+extends\s+([A-Za-z_][A-Za-z0-9_.]*))?",
        RegexOptions.Multiline | RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(200));

    private static readonly Regex MethodDeclRegex = new(
        @"^\s*(?:(?:public|protected|private|static|final|synchronized|override)\s+)*(?:def|[A-Za-z_][A-Za-z0-9_<>\[\]]*)\s+([a-z_][A-Za-z0-9_]*)\s*\([^;{]*\)\s*\{",
        RegexOptions.Multiline | RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(250));

    private static readonly Regex CallSiteRegex = new(
        @"\b(?:([A-Za-z_][A-Za-z0-9_]*)\.)?([A-Za-z_][A-Za-z0-9_]*)\s*\(",
        RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(250));

    private static readonly HashSet<string> GroovyKeywords = new(StringComparer.Ordinal)
    {
        "if", "for", "while", "switch", "catch", "return", "throw", "new",
        "def", "class", "interface", "enum", "trait", "import", "package",
        "println", "print", "assert", "synchronized", "super", "this"
    };

    public string LanguageName => Constants.LanguageName;
    public bool UsesTreeSitter => false;
    public IReadOnlyList<ISemanticExtension> SemanticExtensions { get; } = [];

    public bool CanParse(string fileExtension) =>
        !string.IsNullOrEmpty(fileExtension) &&
        fileExtension.Equals(Constants.GroovyExtension, StringComparison.OrdinalIgnoreCase);

    public ImportType ResolveImportType(string importPath, string filePath, string? absoluteWorkspacePath)
    {
        if (string.IsNullOrEmpty(importPath)) return ImportType.External;
        if (importPath.StartsWith(Constants.JavaPrefix, StringComparison.Ordinal) ||
            importPath.StartsWith(Constants.JavaxPrefix, StringComparison.Ordinal) ||
            importPath.StartsWith(Constants.GroovyPrefix, StringComparison.Ordinal) ||
            importPath.StartsWith(Constants.OrgCodehausGroovyPrefix, StringComparison.Ordinal))
        {
            return ImportType.External;
        }
        return ImportType.Internal;
    }

    public BaseParserVisitor CreateVisitor(
        Node rootNode,
        List<ISemanticExtension> activeExtensions,
        string relativePath,
        string absoluteWorkspacePath,
        IFileParser fileParser,
        SemanticExtensionRegistry extensionRegistry)
    {
        throw new NotSupportedException("GroovyFileParser does not use TreeSitter visitors.");
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
        var rawImports = new List<RawImport>();

        var content = await PhysicalFileStore.ConsumeFileTextAsync(filePath);
        var doc = SyntaxDocument.Create(content, LanguageName, filePath, relativePath);
        if (string.IsNullOrWhiteSpace(content))
        {
            return new SyntaxTree(filePath, relativePath, doc, fileNode, this, rawImports, [], []);
        }

        try
        {
            ParseGroovySource(content, fileNode, relativePath, workspaceId, rawImports);
        }
        catch (RegexMatchTimeoutException)
        {
            // Tolerate pathologically large scripts gracefully
        }

        return new SyntaxTree(filePath, relativePath, doc, fileNode, this, rawImports, [], []);
    }

    private void ParseGroovySource(
        string content,
        FileNode fileNode,
        string relativePath,
        string workspaceId,
        List<RawImport> rawImports)
    {
        var lineStarts = FaceletsXhtmlParser.BuildLineStarts(content);

        foreach (Match im in ImportRegex.Matches(content))
        {
            var imported = im.Groups[1].Value.Trim();
            if (string.IsNullOrEmpty(imported)) continue;

            var line = FaceletsXhtmlParser.GetLineNumber(lineStarts, im.Index);
            var importKind = ResolveImportType(imported, relativePath, null);
            rawImports.Add(new RawImport(imported, relativePath, importKind));

            if (!imported.EndsWith(Constants.WildcardImportSuffix, StringComparison.Ordinal))
            {
                var dotIdx = imported.LastIndexOf('.');
                var simple = (dotIdx >= 0 && dotIdx < imported.Length - 1) ? imported[(dotIdx + 1)..] : imported;
                fileNode.References.Add(new Reference(fileNode.Id, simple, OntologyConstants.Relationships.UsesType, line, 1));
            }
        }

        TypeNode? primaryType = null;
        foreach (Match tm in TypeDeclRegex.Matches(content))
        {
            var declKeyword = tm.Groups[1].Value;
            var typeName = tm.Groups[2].Value;
            var extendsName = tm.Groups[3].Success ? tm.Groups[3].Value.Trim() : null;
            var line = FaceletsXhtmlParser.GetLineNumber(lineStarts, tm.Index);

            var kindLabel = declKeyword switch
            {
                Constants.InterfaceKind => Constants.InterfaceKind,
                Constants.EnumKind => Constants.EnumKind,
                _ => Constants.ClassKind
            };

            var typeId = $"{workspaceId}:{OntologyConstants.IdPrefixes.Symbol}:{relativePath}:{Constants.TypeSymbolSegment}:{typeName}";
            var typeNode = new TypeNode(typeId, typeName, typeName, relativePath, relativePath, line, line, 0, 0, kindLabel)
            {
                Extends = extendsName
            };
            fileNode.Children.Add(typeNode);
            primaryType ??= typeNode;

            if (!string.IsNullOrEmpty(extendsName))
            {
                fileNode.References.Add(new Reference(typeId, extendsName, OntologyConstants.Relationships.InheritsFrom, line, 1));
            }
        }

        var seenMethods = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match mm in MethodDeclRegex.Matches(content))
        {
            var methodName = mm.Groups[1].Value;
            if (GroovyKeywords.Contains(methodName) || !seenMethods.Add(methodName)) continue;

            var line = FaceletsXhtmlParser.GetLineNumber(lineStarts, mm.Index);
            var qualified = primaryType != null ? $"{primaryType.Name}.{methodName}" : methodName;
            var fnId = $"{workspaceId}:{OntologyConstants.IdPrefixes.Symbol}:{relativePath}:{Constants.FunctionSymbolSegment}:{qualified}";
            var fnNode = new FunctionNode(fnId, methodName, qualified, relativePath, relativePath, line, line, 0, 0);

            if (primaryType != null)
            {
                primaryType.Children.Add(fnNode);
            }
            else
            {
                fileNode.Children.Add(fnNode);
            }
        }

        var seenCalls = new HashSet<(string Target, int Line)>();
        foreach (Match cm in CallSiteRegex.Matches(content))
        {
            var recv = cm.Groups[1].Success ? cm.Groups[1].Value : null;
            var fn = cm.Groups[2].Value;
            if (GroovyKeywords.Contains(fn)) continue;

            var line = FaceletsXhtmlParser.GetLineNumber(lineStarts, cm.Index);
            var target = !string.IsNullOrEmpty(recv) && !GroovyKeywords.Contains(recv)
                ? $"{recv}.{fn}"
                : fn;

            if (seenCalls.Add((target, line)))
            {
                fileNode.References.Add(new Reference(fileNode.Id, target, OntologyConstants.Relationships.Calls, line, 1));
            }
        }
    }
}
