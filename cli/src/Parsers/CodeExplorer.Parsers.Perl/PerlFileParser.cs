using System.Text.RegularExpressions;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Parser;
using CodeExplorer.Core.Parser.Syntax;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parsers.Perl;

public partial class PerlFileParser : IFileParser
{
    internal const string LangPerl = "perl";
    internal const string PlExt = ".pl";
    internal const string PmExt = ".pm";
    internal const string TExt = ".t";
    internal const string KindModule = "module";
    internal const string VisibilityPublic = "public";
    internal const string ComplexityKey = "complexity";
    internal const string CyclomaticComplexityKey = "cyclomatic_complexity";
    internal const string UpgradeRisksKey = "upgrade_risks";
    internal const string SwitchModuleRisk = "switch_module";
    internal const string ModuleInitName = "<module_init>";
    internal const string SelfReceiver = "self";
    internal const string ThisReceiver = "this";
    internal const string ClassReceiver = "class";
    internal const string NewMethodName = "new";
    internal const string SwitchModuleName = "Switch";
    internal const string DoubleColon = "::";
    internal const string DotString = ".";
    internal const string ProtocolCron = "CRON";
    internal const string ProtocolCli = "CLI";
    internal const string MethodExec = "EXEC";
    internal const string PathKey = "path";
    internal const string RouteKey = "route";
    internal const string RouteTemplateKey = "route_template";
    internal const string MethodKey = "method";
    internal const string FilePathKey = "file_path";
    internal const string CronPathKeyword = "cron";
    internal const string NotSupportedVisitorMessage = "Perl parser uses high-resilience native C# token analysis.";
    internal const char SlashChar = '/';
    internal const char BackslashChar = '\\';
    internal const char DotChar = '.';
    internal const char OpenBraceChar = '{';
    internal const char CloseBraceChar = '}';
    internal const char SemicolonChar = ';';
    internal const char SingleQuoteChar = '\'';
    internal const char DoubleQuoteChar = '"';

    private static readonly char[] QuoteTrimChars = [SingleQuoteChar, DoubleQuoteChar, ' ', '\t', '\r', '\n'];
    private static readonly char[] WhitespaceSplitChars = [' ', '\t', '\r', '\n', ',', '(', ')', '\'', '"'];

    private static readonly Regex PackageDeclRegex = new(
        @"^\s*package\s+(?<pkg>[a-zA-Z_][a-zA-Z0-9_:]*)\s*;",
        RegexOptions.Compiled | RegexOptions.Multiline);

    private static readonly Regex UseRequireRegex = new(
        @"^\s*(?:use|require)\s+(?<mod>[a-zA-Z0-9_:/.'""]+)(?:\s+qw\s*[\(\[\{/<](?<imports>[^\)\]\}/>]*)[\)\]\}/>])?\s*;",
        RegexOptions.Compiled | RegexOptions.Multiline);

    private static readonly Regex UseBaseParentRegex = new(
        @"^\s*use\s+(?:base|parent)\s+(?:qw\s*[\(\[\{/<]|['""])(?<bases>[^\)\]\}/>;'""]+)",
        RegexOptions.Compiled | RegexOptions.Multiline);

    private static readonly Regex SubDeclRegex = new(
        @"(?<![a-zA-Z0-9_$])\bsub\s+(?<name>[a-zA-Z_][a-zA-Z0-9_]*)\b",
        RegexOptions.Compiled);

    private static readonly Regex ObjectNewBindingRegex = new(
        @"(?:my|our)?\s*\$(?<var>[a-zA-Z_][a-zA-Z0-9_]*)\s*=\s*(?<pkg>[a-zA-Z_][a-zA-Z0-9_:]*)\s*->\s*(?:new|connect)\b",
        RegexOptions.Compiled);

    private static readonly Regex ArrowCallRegex = new(
        @"(?:\$(?<var>[a-zA-Z_][a-zA-Z0-9_]*)|(?<pkg>[a-zA-Z_][a-zA-Z0-9_:]*))\s*->\s*(?<method>[a-zA-Z_][a-zA-Z0-9_]*)\s*\(?",
        RegexOptions.Compiled);

    private static readonly Regex QualifiedSubCallRegex = new(
        @"(?<![\$@%&a-zA-Z0-9_:])(?<pkg>[a-zA-Z_][a-zA-Z0-9_]*(?:::[a-zA-Z_][a-zA-Z0-9_]*)+)\s*\(",
        RegexOptions.Compiled);

    private static readonly Regex LocalSubCallRegex = new(
        @"(?<![\$@%&>a-zA-Z0-9_:])&?(?<fn>[a-zA-Z_][a-zA-Z0-9_]*)\s*\(",
        RegexOptions.Compiled);

    private static readonly Regex PerlComplexityBranchRegex = new(
        @"\b(?:if|elsif|unless|while|until|for|foreach|case|catch)\b|&&|\|\||\band\b|\bor\b",
        RegexOptions.Compiled);

    private static readonly HashSet<string> PerlExternalStandardModules = new(StringComparer.OrdinalIgnoreCase)
    {
        "strict", "warnings", "utf8", "vars", "constant", "Carp", "POSIX", "Data::Dumper",
        "DBI", "DBD::mysql", "DBD::Pg", "Switch", "JSON", "URI::Escape", "Mail::Sendmail",
        "MIME::Lite", "LWP::UserAgent", "WWW::Mechanize", "IP::Country::Fast", "FindBin",
        "File::Basename", "File::Spec", "File::Path", "Getopt::Long", "Time::Local", "Time::HiRes",
        "Scalar::Util", "List::Util", "Exporter", "base", "parent", "lib"
    };

    private static readonly HashSet<string> IgnoredPerlKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "if", "elsif", "else", "unless", "while", "until", "for", "foreach", "switch", "case",
        "sub", "my", "our", "local", "return", "die", "warn", "print", "printf", "sprintf",
        "open", "close", "read", "write", "chomp", "chop", "split", "join", "push", "pop",
        "shift", "unshift", "keys", "values", "each", "defined", "undef", "exists", "delete",
        "scalar", "ref", "bless", "eval", "use", "require", "package", "qw", "qq", "qr"
    };

    public string LanguageName => LangPerl;

    public bool UsesTreeSitter => false;

    public IReadOnlyList<ISemanticExtension> SemanticExtensions => [];

    public bool CanParse(string fileExtension)
    {
        return fileExtension.Equals(PlExt, StringComparison.OrdinalIgnoreCase) ||
               fileExtension.Equals(PmExt, StringComparison.OrdinalIgnoreCase) ||
               fileExtension.Equals(TExt, StringComparison.OrdinalIgnoreCase);
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
        _ = filePath;
        _ = absoluteWorkspacePath;
        if (string.IsNullOrWhiteSpace(importPath))
        {
            return ImportType.External;
        }

        var clean = importPath.Trim(QuoteTrimChars);
        if (PerlExternalStandardModules.Contains(clean))
        {
            return ImportType.External;
        }

        return ImportType.Internal;
    }

    public async Task<SyntaxTree> ParseAsync(
        string filePath,
        string parentNodeId,
        string workspaceId,
        string absoluteWorkspacePath)
    {
        _ = parentNodeId;
        var relativePath = Path.GetRelativePath(absoluteWorkspacePath, filePath).Replace(BackslashChar, SlashChar);
        var fileName = Path.GetFileName(filePath);
        var isTestFile = filePath.EndsWith(TExt, StringComparison.OrdinalIgnoreCase);
        var isScriptFile = filePath.EndsWith(PlExt, StringComparison.OrdinalIgnoreCase);

        var fileNodeId = $"{workspaceId}:{OntologyConstants.IdPrefixes.File}:{relativePath}";
        var fileNode = new FileNode(fileNodeId, fileName, relativePath, filePath)
        {
            IsTest = isTestFile
        };

        var content = await PhysicalFileStore.ConsumeFileTextAsync(filePath);
        var rawImports = new List<RawImport>();
        var rawVariables = new List<RawVariable>();
        var rawTypeBindings = new List<RawTypeBinding>();

        var lineOffsets = BuildLineOffsets(content);
        var commentMasked = PerlDatabaseAndExternalExtractor.MaskComments(content);
        var masked = PerlDatabaseAndExternalExtractor.MaskCommentsAndStrings(content);

        var (packageName, moduleTypeNode, usesSwitchModule) = ParsePackageAndImports(
            content,
            commentMasked,
            lineOffsets,
            fileNode,
            workspaceId,
            relativePath,
            absoluteWorkspacePath,
            rawImports);

        var subSpans = ParseSubroutinesAndCalls(
            content,
            masked,
            lineOffsets,
            moduleTypeNode,
            fileNode,
            workspaceId,
            relativePath,
            isScriptFile,
            usesSwitchModule,
            rawTypeBindings);

        if (isScriptFile)
        {
            AddScriptEndpoint(fileNode, workspaceId, relativePath, subSpans);
        }

        PerlDatabaseAndExternalExtractor.ExtractDatabaseAndExternalCalls(
            content,
            commentMasked,
            lineOffsets,
            moduleTypeNode,
            subSpans,
            fileNode,
            workspaceId,
            relativePath);

        var doc = SyntaxDocument.Create(content, LanguageName, filePath, relativePath);
        doc.PackageOrNamespace = packageName;

        return new SyntaxTree(
            filePath,
            relativePath,
            doc,
            fileNode,
            this,
            rawImports,
            rawVariables,
            rawTypeBindings);
    }

    internal sealed record PerlSubSpan(
        int Start,
        int End,
        FunctionNode Node);

    private (string PackageName, TypeNode ModuleNode, bool UsesSwitch) ParsePackageAndImports(
        string content,
        string commentMasked,
        int[] lineOffsets,
        FileNode fileNode,
        string workspaceId,
        string relativePath,
        string absoluteWorkspacePath,
        List<RawImport> rawImports)
    {
        _ = content;
        var pm = PackageDeclRegex.Match(commentMasked);
        var rawPkgName = pm.Success
            ? pm.Groups["pkg"].Value.Trim()
            : Path.GetFileNameWithoutExtension(relativePath);
        var shortPkgName = rawPkgName.Contains(DoubleColon, StringComparison.Ordinal)
            ? rawPkgName[(rawPkgName.LastIndexOf(DoubleColon, StringComparison.Ordinal) + 2)..]
            : rawPkgName;
        var dotPkgName = rawPkgName.Replace(DoubleColon, DotString);
        var (pkgLine, pkgCol) = pm.Success ? GetLineAndColumn(lineOffsets, pm.Index) : (1, 1);

        var typeId = $"{workspaceId}:{OntologyConstants.IdPrefixes.Symbol}:{relativePath}:Type:{shortPkgName}";
        var moduleNode = new TypeNode(
            typeId,
            shortPkgName,
            dotPkgName,
            relativePath,
            relativePath,
            pkgLine,
            Math.Max(1, lineOffsets.Length),
            pkgCol,
            1,
            KindModule)
        {
            IsExported = true
        };
        fileNode.Children.Add(moduleNode);

        var usesSwitch = false;
        foreach (Match um in UseRequireRegex.Matches(commentMasked))
        {
            var mod = um.Groups["mod"].Value.Trim(QuoteTrimChars);
            if (string.IsNullOrWhiteSpace(mod) || char.IsDigit(mod[0])) continue;

            if (mod.Equals(SwitchModuleName, StringComparison.Ordinal))
            {
                usesSwitch = true;
            }

            var impType = ResolveImportType(mod, relativePath, absoluteWorkspacePath);
            var normalizedMod = mod.Replace(DoubleColon, DotString);
            rawImports.Add(new RawImport(normalizedMod, relativePath, impType));

            if (impType == ImportType.Internal)
            {
                var shortMod = mod.Contains(DoubleColon, StringComparison.Ordinal)
                    ? mod[(mod.LastIndexOf(DoubleColon, StringComparison.Ordinal) + 2)..]
                    : Path.GetFileNameWithoutExtension(mod);
                var (uLine, uCol) = GetLineAndColumn(lineOffsets, um.Index);
                fileNode.References.Add(new Reference(typeId, shortMod, OntologyConstants.Relationships.DependsOn, uLine, uCol));
            }
        }

        foreach (Match bm in UseBaseParentRegex.Matches(commentMasked))
        {
            var bases = bm.Groups["bases"].Value.Split(WhitespaceSplitChars, StringSplitOptions.RemoveEmptyEntries);
            var (bLine, bCol) = GetLineAndColumn(lineOffsets, bm.Index);
            foreach (var b in bases)
            {
                var shortBase = b.Contains(DoubleColon, StringComparison.Ordinal)
                    ? b[(b.LastIndexOf(DoubleColon, StringComparison.Ordinal) + 2)..]
                    : b;
                moduleNode.Extends ??= shortBase;
                fileNode.References.Add(new Reference(typeId, shortBase, OntologyConstants.Relationships.InheritsFrom, bLine, bCol));
            }
        }

        if (usesSwitch)
        {
            moduleNode.CustomProperties ??= new();
            moduleNode.CustomProperties[UpgradeRisksKey] = SwitchModuleRisk;
        }

        return (dotPkgName, moduleNode, usesSwitch);
    }

    private static List<PerlSubSpan> ParseSubroutinesAndCalls(
        string content,
        string masked,
        int[] lineOffsets,
        TypeNode moduleNode,
        FileNode fileNode,
        string workspaceId,
        string relativePath,
        bool isScriptFile,
        bool usesSwitchModule,
        List<RawTypeBinding> rawTypeBindings)
    {
        var spans = new List<PerlSubSpan>();

        foreach (Match sm in SubDeclRegex.Matches(masked))
        {
            var subName = sm.Groups["name"].Value.Trim();
            var (startLine, startCol) = GetLineAndColumn(lineOffsets, sm.Index);
            var (openBrace, closeBrace) = FindBodyBraces(masked, sm.Index + sm.Length);
            var endIdx = closeBrace > openBrace ? closeBrace : sm.Index + sm.Length;
            var (endLine, endCol) = GetLineAndColumn(lineOffsets, endIdx);

            var symbolName = $"{moduleNode.Name}.{subName}";
            var fnId = $"{workspaceId}:{OntologyConstants.IdPrefixes.Symbol}:{relativePath}:Function:{symbolName}";
            var loc = Math.Max(1, endLine - startLine + 1);

            var fnNode = new FunctionNode(
                fnId,
                subName,
                symbolName,
                relativePath,
                relativePath,
                startLine,
                endLine,
                startCol,
                endCol)
            {
                Loc = loc,
                Visibility = VisibilityPublic,
                IsExported = true
            };
            fnNode.Signals().EnclosingTypeName = moduleNode.Name;

            var maskedBody = openBrace >= 0 && closeBrace > openBrace
                ? masked.Substring(openBrace, closeBrace - openBrace + 1)
                : string.Empty;
            var rawBody = openBrace >= 0 && closeBrace > openBrace
                ? content.Substring(openBrace, closeBrace - openBrace + 1)
                : string.Empty;

            var complexity = 1 + PerlComplexityBranchRegex.Matches(maskedBody).Count;
            fnNode.CustomProperties ??= new();
            fnNode.CustomProperties[ComplexityKey] = complexity.ToString();
            fnNode.CustomProperties[CyclomaticComplexityKey] = complexity.ToString();
            if (usesSwitchModule)
            {
                fnNode.CustomProperties[UpgradeRisksKey] = SwitchModuleRisk;
            }

            moduleNode.Children.Add(fnNode);

            if (!string.IsNullOrEmpty(rawBody))
            {
                ExtractPerlCallsAndBindings(rawBody, maskedBody, openBrace, lineOffsets, fnNode, moduleNode, relativePath, rawTypeBindings);
            }

            spans.Add(new PerlSubSpan(sm.Index, endIdx, fnNode));
        }

        if (isScriptFile && spans.Count == 0)
        {
            var fnId = $"{workspaceId}:{OntologyConstants.IdPrefixes.Symbol}:{relativePath}:Function:{ModuleInitName}";
            var totalLines = Math.Max(1, lineOffsets.Length);
            var initNode = new FunctionNode(
                fnId,
                ModuleInitName,
                ModuleInitName,
                relativePath,
                relativePath,
                1,
                totalLines,
                1,
                1)
            {
                Loc = totalLines,
                Visibility = VisibilityPublic,
                IsModuleInit = true,
                IsExported = true
            };
            var complexity = 1 + PerlComplexityBranchRegex.Matches(masked).Count;
            initNode.CustomProperties ??= new();
            initNode.CustomProperties[ComplexityKey] = complexity.ToString();
            initNode.CustomProperties[CyclomaticComplexityKey] = complexity.ToString();
            if (usesSwitchModule)
            {
                initNode.CustomProperties[UpgradeRisksKey] = SwitchModuleRisk;
            }

            moduleNode.Children.Add(initNode);
            fileNode.Children.Add(initNode);
            ExtractPerlCallsAndBindings(content, masked, 0, lineOffsets, initNode, moduleNode, relativePath, rawTypeBindings);
            spans.Add(new PerlSubSpan(0, content.Length - 1, initNode));
        }

        return spans;
    }

    private static void ExtractPerlCallsAndBindings(
        string rawBody,
        string maskedBody,
        int bodyOffset,
        int[] lineOffsets,
        FunctionNode fnNode,
        TypeNode moduleNode,
        string relativePath,
        List<RawTypeBinding> rawTypeBindings)
    {
        foreach (Match bm in ObjectNewBindingRegex.Matches(rawBody))
        {
            var varName = bm.Groups["var"].Value.Trim();
            var rawPkg = bm.Groups["pkg"].Value.Trim();
            var shortPkg = ShortenPerlPackage(rawPkg);
            if (!string.IsNullOrEmpty(varName) && !string.IsNullOrEmpty(shortPkg))
            {
                rawTypeBindings.Add(new RawTypeBinding(varName, shortPkg, fnNode.Id, relativePath));
                rawTypeBindings.Add(new RawTypeBinding(varName, shortPkg, moduleNode.Id, relativePath));
            }
        }

        foreach (Match am in ArrowCallRegex.Matches(maskedBody))
        {
            var method = am.Groups["method"].Value.Trim();
            var (line, col) = GetLineAndColumn(lineOffsets, bodyOffset + am.Index);

            if (am.Groups["pkg"].Success)
            {
                var shortPkg = ShortenPerlPackage(am.Groups["pkg"].Value.Trim());
                if (method.Equals(NewMethodName, StringComparison.OrdinalIgnoreCase))
                {
                    fnNode.References.Add(new Reference(fnNode.Id, shortPkg, OntologyConstants.Relationships.Instantiates, line, col));
                    fnNode.References.Add(new Reference(fnNode.Id, shortPkg, OntologyConstants.Relationships.UsesType, line, col));
                }
                else
                {
                    fnNode.References.Add(new Reference(fnNode.Id, $"{shortPkg}.{method}", OntologyConstants.Relationships.Calls, line, col));
                }
            }
            else if (am.Groups["var"].Success)
            {
                var v = am.Groups["var"].Value.Trim();
                var receiver = v.Equals(SelfReceiver, StringComparison.OrdinalIgnoreCase) ||
                               v.Equals(ThisReceiver, StringComparison.OrdinalIgnoreCase) ||
                               v.Equals(ClassReceiver, StringComparison.OrdinalIgnoreCase)
                    ? moduleNode.Name
                    : v;
                fnNode.References.Add(new Reference(fnNode.Id, $"{receiver}.{method}", OntologyConstants.Relationships.Calls, line, col));
            }
        }

        foreach (Match qm in QualifiedSubCallRegex.Matches(maskedBody))
        {
            var rawQualified = qm.Groups["pkg"].Value.Trim();
            var lastSep = rawQualified.LastIndexOf(DoubleColon, StringComparison.Ordinal);
            if (lastSep > 0)
            {
                var pkgPart = ShortenPerlPackage(rawQualified[..lastSep]);
                var subPart = rawQualified[(lastSep + 2)..];
                var (line, col) = GetLineAndColumn(lineOffsets, bodyOffset + qm.Index);
                fnNode.References.Add(new Reference(fnNode.Id, $"{pkgPart}.{subPart}", OntologyConstants.Relationships.Calls, line, col));
            }
        }

        foreach (Match lm in LocalSubCallRegex.Matches(maskedBody))
        {
            var fnCall = lm.Groups["fn"].Value.Trim();
            if (IgnoredPerlKeywords.Contains(fnCall)) continue;
            var (line, col) = GetLineAndColumn(lineOffsets, bodyOffset + lm.Index);
            fnNode.References.Add(new Reference(fnNode.Id, fnCall, OntologyConstants.Relationships.Calls, line, col));
        }
    }

    private static void AddScriptEndpoint(
        FileNode fileNode,
        string workspaceId,
        string relativePath,
        List<PerlSubSpan> subSpans)
    {
        var protocol = relativePath.Contains(CronPathKeyword, StringComparison.OrdinalIgnoreCase)
            ? ProtocolCron
            : ProtocolCli;
        var route = $"/{relativePath}";
        var epId = $"{workspaceId}:{OntologyConstants.IdPrefixes.Endpoint}:{MethodExec}:{route}";
        var epNode = new EndpointNode(
            epId,
            $"{protocol} {route}",
            relativePath,
            MethodExec,
            route,
            protocol)
        {
            FilePath = relativePath,
            StartLine = 1,
            EndLine = 1,
            Line = 1,
            LineEnd = 1,
            Operation = MethodExec,
            EvidenceKind = OntologyConstants.EvidenceKinds.Endpoint
        };
        epNode.CustomProperties ??= new();
        epNode.CustomProperties[PathKey] = route;
        epNode.CustomProperties[RouteKey] = route;
        epNode.CustomProperties[RouteTemplateKey] = route;
        epNode.CustomProperties[MethodKey] = MethodExec;
        epNode.CustomProperties[FilePathKey] = relativePath;

        var entryFn = subSpans.FirstOrDefault()?.Node;
        if (entryFn != null)
        {
            epNode.Children.Add(entryFn);
            fileNode.References.Add(new Reference(epId, entryFn.Id, OntologyConstants.Relationships.ExposedBy, 1, 1));
        }

        fileNode.Children.Add(epNode);
    }

    internal static string ShortenPerlPackage(string rawPkg)
    {
        return rawPkg.Contains(DoubleColon, StringComparison.Ordinal)
            ? rawPkg[(rawPkg.LastIndexOf(DoubleColon, StringComparison.Ordinal) + 2)..]
            : rawPkg;
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

        if (openBrace < 0) return (-1, -1);

        var depth = 1;
        for (var i = openBrace + 1; i < masked.Length; i++)
        {
            if (masked[i] == OpenBraceChar) depth++;
            else if (masked[i] == CloseBraceChar)
            {
                depth--;
                if (depth == 0) return (openBrace, i);
            }
        }

        return (openBrace, masked.Length - 1);
    }

    internal static (int Line, int Column) GetLineAndColumn(int[] lineOffsets, int charIndex)
    {
        var line = Array.BinarySearch(lineOffsets, charIndex);
        if (line < 0) line = ~line - 1;
        var col = charIndex - lineOffsets[line] + 1;
        return (line + 1, col);
    }

    internal static int[] BuildLineOffsets(string text)
    {
        var offsets = new List<int> { 0 };
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n') offsets.Add(i + 1);
        }
        return [.. offsets];
    }
}
