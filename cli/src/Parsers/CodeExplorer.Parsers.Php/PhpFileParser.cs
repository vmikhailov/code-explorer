using System.Text.RegularExpressions;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;
using CodeExplorer.Core.Parser.Syntax;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parsers.Php;

public partial class PhpFileParser : IFileParser
{
    internal const string LangPhp = "php";
    internal const string PhpExt = ".php";
    internal const string PhtmlExt = ".phtml";
    internal const string IncExt = ".inc";
    internal const string VisibilityPublic = "public";
    internal const string VisibilityProtected = "protected";
    internal const string VisibilityPrivate = "private";
    internal const string ComplexityKey = "complexity";
    internal const string CyclomaticComplexityKey = "cyclomatic_complexity";
    internal const string ParameterCountKey = "parameter_count";
    internal const string UpgradeRisksKey = "upgrade_risks";
    internal const string RoleKey = "role";
    internal const string ControllerRole = "Controller";
    internal const string EntityRole = "Entity";
    internal const string RepositoryRole = "Repository";
    internal const string GlobalScope = "global";
    internal const string SelfReceiver = "self";
    internal const string StaticReceiver = "static";
    internal const string ParentReceiver = "parent";
    internal const string ThisReceiver = "this";
    internal const string ConstructMethodName = "__construct";
    internal const char SlashChar = '/';
    internal const char BackslashChar = '\\';
    internal const char UnderscoreChar = '_';
    internal const char CommaChar = ',';
    internal const char OpenBraceChar = '{';
    internal const char CloseBraceChar = '}';
    internal const char OpenParenChar = '(';
    internal const char CloseParenChar = ')';
    internal const char SemicolonChar = ';';
    internal const char DollarChar = '$';
    internal const char QuestionMarkChar = '?';
    internal const char DotChar = '.';
    internal const char SingleQuoteChar = '\'';
    internal const char DoubleQuoteChar = '"';
    internal const string DotString = ".";
    internal const string DoubleColon = "::";
    internal const string ArrowOp = "->";
    internal const string ControllerSuffix = "Controller";
    internal const string ActionSuffix = "Action";
    internal const string ZendControllerBase = "Zend_Controller_Action";
    internal const string ZendDbTableBase = "Zend_Db_Table";
    internal const string SymfonyPrefix = "Symfony\\";
    internal const string DoctrinePrefix = "Doctrine\\";
    internal const string ZendPrefix = "Zend_";
    internal const string NettePrefix = "Nette\\";
    internal const string IlluminatePrefix = "Illuminate\\";
    internal const string PsrPrefix = "Psr\\";
    internal const string NotSupportedVisitorMessage = "PHP parser uses high-resilience native C# token analysis.";

    private static readonly char[] QuoteTrimChars = [SingleQuoteChar, DoubleQuoteChar, ' ', '\t', '\r', '\n'];
    private static readonly char[] NamespaceTrimChars = [BackslashChar, SlashChar, ' '];

    private static readonly Regex NamespaceRegex = new(
        @"^\s*namespace\s+(?<ns>[a-zA-Z0-9_\\]+)\s*[;{]",
        RegexOptions.Compiled | RegexOptions.Multiline);

    private static readonly Regex UseRegex = new(
        @"^\s*use\s+(?:function\s+|const\s+)?(?<path>[a-zA-Z0-9_\\]+)(?:\s+as\s+(?<alias>[a-zA-Z0-9_]+))?\s*;",
        RegexOptions.Compiled | RegexOptions.Multiline);

    private static readonly Regex IncludeRequireRegex = new(
        @"\b(?:require_once|require|include_once|include)\s*\(?\s*(?:__DIR__\s*\.\s*|dirname\s*\(\s*__FILE__\s*\)\s*\.\s*)?['""](?<path>[^'""]+)['""]\s*\)?",
        RegexOptions.Compiled);

    private static readonly Regex ClassDeclRegex = new(
        @"(?<![a-zA-Z0-9_$])\b(?:(?:abstract|final|readonly)\s+)*(?<kind>class|interface|trait)\s+(?<name>[a-zA-Z0-9_]+)(?:\s+extends\s+(?<extends>[a-zA-Z0-9_\\]+))?(?:\s+implements\s+(?<implements>[a-zA-Z0-9_\\,\s]+))?",
        RegexOptions.Compiled);

    private static readonly Regex FunctionDeclRegex = new(
        @"(?<![a-zA-Z0-9_$])\b(?:(?<vis>public|protected|private)\s+|(?<mod>static|abstract|final)\s+)*function\s+&?\s*(?<name>[a-zA-Z0-9_]+)\s*\((?<params>[^)]*)\)",
        RegexOptions.Compiled);

    private static readonly Regex PropertyTypedBindingRegex = new(
        @"(?:@var\s+(?<docType>[a-zA-Z0-9_\\]+)\s+\$(?<docProp>[a-zA-Z0-9_]+))|(?:(?:\bpublic|\bprotected|\bprivate)\s+(?:readonly\s+|static\s+)?\??(?<propType>[A-Z][a-zA-Z0-9_\\]*)\s+\$(?<propName>[a-zA-Z0-9_]+))",
        RegexOptions.Compiled);

    private static readonly Regex AssignmentNewRegex = new(
        @"\$(?:this->)?(?<var>[a-zA-Z0-9_]+)\s*=\s*new\s+(?<type>[a-zA-Z0-9_\\]+)\s*\(",
        RegexOptions.Compiled);

    private static readonly Regex ComplexityBranchRegex = new(
        @"\b(?:if|elseif|for|foreach|while|case|catch)\b|&&|\|\||\?(?!:)|\?\?",
        RegexOptions.Compiled);

    public string LanguageName => LangPhp;

    public bool UsesTreeSitter => false;

    public IReadOnlyList<ISemanticExtension> SemanticExtensions => [];

    public bool CanParse(string fileExtension)
    {
        return fileExtension.Equals(PhpExt, StringComparison.OrdinalIgnoreCase) ||
               fileExtension.Equals(PhtmlExt, StringComparison.OrdinalIgnoreCase) ||
               fileExtension.Equals(IncExt, StringComparison.OrdinalIgnoreCase);
    }

    public BaseParserVisitor CreateVisitor(
        Node rootNode,
        List<ISemanticExtension> activeExtensions,
        string relativePath,
        string absoluteWorkspacePath,
        IFileParser fileParser,
        SemanticExtensionRegistry extensionRegistry)
    {
        throw new NotSupportedException(NotSupportedVisitorMessage);
    }

    public ImportType ResolveImportType(string importPath, string filePath, string? absoluteWorkspacePath)
    {
        if (string.IsNullOrWhiteSpace(importPath))
        {
            return ImportType.External;
        }

        var trimmed = importPath.Trim(NamespaceTrimChars);
        if (trimmed.StartsWith(SymfonyPrefix, StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith(DoctrinePrefix, StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith(ZendPrefix, StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith(NettePrefix, StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith(IlluminatePrefix, StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith(PsrPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return ImportType.External;
        }

        if (trimmed.EndsWith(PhpExt, StringComparison.OrdinalIgnoreCase) ||
            trimmed.EndsWith(PhtmlExt, StringComparison.OrdinalIgnoreCase) ||
            trimmed.EndsWith(IncExt, StringComparison.OrdinalIgnoreCase))
        {
            return ImportType.Internal;
        }

        return ImportType.Internal;
    }

    public async Task<SyntaxTree> ParseAsync(
        string filePath,
        string parentNodeId,
        string workspaceId,
        string absoluteWorkspacePath,
        string? preloadedContent = null)
    {
        var relativePath = Path.GetRelativePath(absoluteWorkspacePath, filePath).Replace(BackslashChar, SlashChar);
        var fileName = Path.GetFileName(filePath);
        var fileNodeId = $"{workspaceId}:{OntologyConstants.IdPrefixes.File}:{relativePath}";
        var fileNode = new FileNode(fileNodeId, fileName, relativePath, filePath);
        var content = preloadedContent ?? await FileStreamReader.ReadTextOnceAsync(filePath);

        var rawImports = new List<RawImport>();
        var rawVariables = new List<RawVariable>();
        var rawTypeBindings = new List<RawTypeBinding>();
        var semanticSymbols = new List<SyntacticSymbol>();
        var useAliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var lineOffsets = BuildLineOffsets(content);
        var commentMasked = PhpDatabaseSqlExtractor.MaskComments(content);
        var masked = PhpDatabaseSqlExtractor.MaskCommentsAndStrings(content);

        var namespaceName = ExtractNamespaceAndImports(content, commentMasked, relativePath, absoluteWorkspacePath, rawImports, useAliases);
        var classScopes = ParseClassesAndTypes(content, commentMasked, masked, lineOffsets, fileNode, workspaceId, relativePath, namespaceName, useAliases, rawTypeBindings, semanticSymbols);
        var funSpans = ParseFunctionsAndMethods(content, commentMasked, masked, lineOffsets, classScopes, fileNode, workspaceId, relativePath, useAliases, rawTypeBindings);

        ParseEndpointsAndExternalCalls(content, commentMasked, masked, lineOffsets, classScopes, funSpans, fileNode, workspaceId, relativePath, rawImports);
        PhpDatabaseSqlExtractor.ExtractDatabaseAndQueries(content, commentMasked, masked, lineOffsets, classScopes, funSpans, fileNode, workspaceId, relativePath);

        var doc = SyntaxDocument.Create(content, LanguageName, filePath, relativePath);
        doc.PackageOrNamespace = namespaceName;

        var syntaxTree = new SyntaxTree(
            filePath,
            relativePath,
            doc,
            fileNode,
            this,
            rawImports,
            rawVariables,
            rawTypeBindings);

        if (semanticSymbols.Count > 0)
        {
            syntaxTree.SemanticSymbols.AddRange(semanticSymbols);
        }

        return syntaxTree;
    }

    internal sealed record PhpClassScope(
        string Name,
        string FullName,
        string Kind,
        TypeNode Node,
        int StartIndex,
        int OpenBraceIndex,
        int CloseBraceIndex,
        string? ExtendsName,
        string? RoutePrefix,
        string? BoundTableName,
        bool IsController);

    internal sealed record PhpFunctionSpan(
        int Start,
        int End,
        FunctionNode Node,
        PhpClassScope? EnclosingClass);

    private string? ExtractNamespaceAndImports(
        string content,
        string commentMasked,
        string relativePath,
        string absoluteWorkspacePath,
        List<RawImport> rawImports,
        Dictionary<string, string> useAliases)
    {
        string? namespaceName = null;
        var nsMatch = NamespaceRegex.Match(commentMasked);
        if (nsMatch.Success)
        {
            namespaceName = nsMatch.Groups["ns"].Value.Trim().Replace(BackslashChar, DotChar);
        }

        foreach (Match um in UseRegex.Matches(commentMasked))
        {
            var rawPath = um.Groups["path"].Value.Trim();
            var dotPath = rawPath.Replace(BackslashChar, DotChar);
            var shortName = rawPath.Contains(BackslashChar)
                ? rawPath[(rawPath.LastIndexOf(BackslashChar) + 1)..]
                : rawPath;
            var alias = um.Groups["alias"].Success ? um.Groups["alias"].Value.Trim() : shortName;
            useAliases[alias] = shortName;
            rawImports.Add(new RawImport(dotPath, relativePath, ResolveImportType(rawPath, relativePath, absoluteWorkspacePath)));
        }

        foreach (Match im in IncludeRequireRegex.Matches(content))
        {
            var incPath = im.Groups["path"].Value.Trim(QuoteTrimChars).Replace(BackslashChar, SlashChar);
            if (!string.IsNullOrWhiteSpace(incPath))
            {
                rawImports.Add(new RawImport(incPath, relativePath, ImportType.Internal));
            }
        }

        return namespaceName;
    }

    private static List<PhpClassScope> ParseClassesAndTypes(
        string content,
        string commentMasked,
        string masked,
        int[] lineOffsets,
        FileNode fileNode,
        string workspaceId,
        string relativePath,
        string? namespaceName,
        Dictionary<string, string> useAliases,
        List<RawTypeBinding> rawTypeBindings,
        List<SyntacticSymbol> semanticSymbols)
    {
        var scopes = new List<PhpClassScope>();
        foreach (Match cm in ClassDeclRegex.Matches(masked))
        {
            var kind = cm.Groups["kind"].Value.ToLowerInvariant();
            var className = cm.Groups["name"].Value.Trim();
            var (startLine, startCol) = GetLineAndColumn(lineOffsets, cm.Index);
            var (openBrace, closeBrace) = FindBodyBraces(masked, cm.Index + cm.Length);
            var (endLine, endCol) = closeBrace > openBrace ? GetLineAndColumn(lineOffsets, closeBrace) : (startLine, startCol);

            var rawExtends = cm.Groups["extends"].Success ? cm.Groups["extends"].Value.Trim() : null;
            var cleanExtends = NormalizePhpTypeName(rawExtends, useAliases);
            var fullName = string.IsNullOrEmpty(namespaceName) ? className : $"{namespaceName}.{className}";

            var typeId = $"{workspaceId}:{OntologyConstants.IdPrefixes.Symbol}:{relativePath}:Type:{className}";
            var typeNode = new TypeNode(
                typeId,
                className,
                fullName,
                relativePath,
                relativePath,
                startLine,
                endLine,
                startCol,
                endCol,
                kind)
            {
                Extends = cleanExtends,
                IsExported = true
            };
            fileNode.Children.Add(typeNode);

            if (!string.IsNullOrEmpty(cleanExtends))
            {
                fileNode.References.Add(new Reference(typeId, cleanExtends, OntologyConstants.Relationships.InheritsFrom, startLine, startCol));
            }

            if (cm.Groups["implements"].Success)
            {
                var implParts = cm.Groups["implements"].Value.Split(CommaChar, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                foreach (var rawImpl in implParts)
                {
                    var cleanImpl = NormalizePhpTypeName(rawImpl, useAliases);
                    if (!string.IsNullOrEmpty(cleanImpl))
                    {
                        fileNode.References.Add(new Reference(typeId, cleanImpl, OntologyConstants.Relationships.Implements, startLine, startCol));
                    }
                }
            }

            var docBlock = ExtractPrecedingDocBlock(content, cm.Index);
            var routePrefix = ExtractRouteFromDocBlockOrAttributes(docBlock, out _);
            var boundTable = PhpDatabaseSqlExtractor.ExtractClassTableMapping(content, commentMasked, cm.Index, openBrace, closeBrace, className, cleanExtends, docBlock, typeNode, fileNode, workspaceId, relativePath, startLine, startCol, semanticSymbols);
            var isController = className.EndsWith(ControllerSuffix, StringComparison.OrdinalIgnoreCase) ||
                               (!string.IsNullOrEmpty(cleanExtends) && cleanExtends.Contains(ControllerSuffix, StringComparison.OrdinalIgnoreCase));

            if (isController)
            {
                typeNode.CustomProperties ??= new();
                typeNode.CustomProperties[RoleKey] = ControllerRole;
            }
            else if (!string.IsNullOrEmpty(boundTable))
            {
                typeNode.CustomProperties ??= new();
                typeNode.CustomProperties[RoleKey] = EntityRole;
            }

            if (openBrace >= 0 && closeBrace > openBrace)
            {
                var classBody = content.Substring(openBrace, closeBrace - openBrace + 1);
                ExtractClassPropertyBindings(classBody, typeId, relativePath, useAliases, rawTypeBindings);
            }

            scopes.Add(new PhpClassScope(className, fullName, kind, typeNode, cm.Index, openBrace, closeBrace, cleanExtends, routePrefix, boundTable, isController));
        }

        return scopes;
    }

    private static void ExtractClassPropertyBindings(
        string classBody,
        string typeId,
        string relativePath,
        Dictionary<string, string> useAliases,
        List<RawTypeBinding> rawTypeBindings)
    {
        foreach (Match pm in PropertyTypedBindingRegex.Matches(classBody))
        {
            var rawType = pm.Groups["docType"].Success ? pm.Groups["docType"].Value : pm.Groups["propType"].Value;
            var propName = pm.Groups["docProp"].Success ? pm.Groups["docProp"].Value : pm.Groups["propName"].Value;
            var cleanType = NormalizePhpTypeName(rawType, useAliases);
            if (!string.IsNullOrEmpty(cleanType) && !string.IsNullOrEmpty(propName))
            {
                rawTypeBindings.Add(new RawTypeBinding(propName, cleanType, typeId, relativePath));
            }
        }
    }

    internal static string? NormalizePhpTypeName(string? rawType, Dictionary<string, string>? useAliases = null)
    {
        if (string.IsNullOrWhiteSpace(rawType))
        {
            return null;
        }

        var trimmed = rawType.Trim().TrimStart(BackslashChar, QuestionMarkChar);
        if (useAliases != null && useAliases.TryGetValue(trimmed, out var resolved))
        {
            return resolved;
        }

        if (trimmed.Contains(BackslashChar))
        {
            return trimmed[(trimmed.LastIndexOf(BackslashChar) + 1)..];
        }

        return trimmed;
    }

    internal static (int OpenBrace, int CloseBrace) FindBodyBraces(string masked, int searchStart)
    {
        var openBrace = -1;
        for (var i = searchStart; i < masked.Length; i++)
        {
            var c = masked[i];
            if (c == OpenBraceChar)
            {
                openBrace = i;
                break;
            }
            if (c == SemicolonChar)
            {
                return (-1, -1);
            }
        }

        if (openBrace < 0)
        {
            return (-1, -1);
        }

        var depth = 1;
        for (var i = openBrace + 1; i < masked.Length; i++)
        {
            if (masked[i] == OpenBraceChar) depth++;
            else if (masked[i] == CloseBraceChar)
            {
                depth--;
                if (depth == 0)
                {
                    return (openBrace, i);
                }
            }
        }

        return (openBrace, masked.Length - 1);
    }

    internal static (int Line, int Column) GetLineAndColumn(int[] lineOffsets, int charIndex)
    {
        var line = Array.BinarySearch(lineOffsets, charIndex);
        if (line < 0)
        {
            line = ~line - 1;
        }
        var col = charIndex - lineOffsets[line] + 1;
        return (line + 1, col);
    }

    internal static int[] BuildLineOffsets(string text)
    {
        var offsets = new List<int> { 0 };
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                offsets.Add(i + 1);
            }
        }
        return [.. offsets];
    }
}
