using System.Text.RegularExpressions;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Parser;
using CodeExplorer.Core.Parser.Syntax;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.Kotlin;

public partial class KotlinFileParser : IFileParser
{
    private const string ProtocolRest = "REST";
    private static readonly Regex KafkaSubscribeRegex = new(
        @"subscribe\s*\(\s*(?:listOf\s*\(\s*)?(?:['""]([^'""]+)['""]|([a-zA-Z0-9_]+))",
        RegexOptions.Compiled | RegexOptions.Multiline);

    private static readonly Regex AnnotationRegex = new(
        @"@(?<target>[a-zA-Z0-9_]+:)?(?<name>[a-zA-Z0-9_.]+)(?:\s*\((?<args>[^)]*)\))?",
        RegexOptions.Compiled);

    private static readonly Regex ClassDeclRegex = new(
        @"(?<![:.])\b(?<kind>class|interface|object)\s+(?<name>[a-zA-Z0-9_]+)",
        RegexOptions.Compiled);

    private static readonly Regex FunDeclRegex = new(
        @"\bfun\s+(?:<[^>]+>\s+)?(?:(?<receiver>[a-zA-Z0-9_]+)\.)?(?<name>[a-zA-Z0-9_]+)\s*\(",
        RegexOptions.Compiled);

    public string LanguageName => "kotlin";

    public bool UsesTreeSitter => false;

    public IReadOnlyList<ISemanticExtension> SemanticExtensions { get; } =
    [
        new Libraries.KtorServerLibraryParser(),
        new Libraries.KtorClientLibraryParser(),
        new Libraries.MicronautKotlinLibraryParser(),
        new Libraries.GraphQlKotlinLibraryParser(),
        new Libraries.SpringCloudStreamKotlinLibraryParser(),
        new Libraries.ExposedLibraryParser(),
        new Libraries.KtormLibraryParser(),
        new Libraries.KoinLibraryParser(),
        new Libraries.ArrowKotlinLibraryParser(),
        new Libraries.KotlinxSerializationLibraryParser(),
        new Libraries.CoroutinesFlowChannelLibraryParser(),
        new Libraries.Http4kLibraryParser(),
        new Libraries.GrpcKotlinLibraryParser(),
        new Libraries.ApolloKotlinLibraryParser(),
        new Libraries.SqlDelightLibraryParser(),
        new Libraries.SupabaseFirebaseKotlinLibraryParser(),
        new Libraries.DaggerHiltKodeinLibraryParser(),
        new Libraries.KotestMockkLibraryParser(),
        new Libraries.ComposeNavigationKotlinLibraryParser()
    ];

    public bool CanParse(string fileExtension)
    {
        return fileExtension.Equals(".kt", StringComparison.OrdinalIgnoreCase) ||
               fileExtension.Equals(".kts", StringComparison.OrdinalIgnoreCase);
    }

    public BaseParserVisitor CreateVisitor(
        Node rootNode,
        List<ISemanticExtension> activeExtensions,
        string relativePath,
        string absoluteWorkspacePath,
        IFileParser fileParser,
        SemanticExtensionRegistry extensionRegistry)
    {
        throw new NotSupportedException("Kotlin parser uses high-resilience native C# token analysis.");
    }

    public ImportType ResolveImportType(string importPath, string filePath, string? absoluteWorkspacePath)
    {
        if (string.IsNullOrEmpty(importPath)) return ImportType.External;

        if (importPath.StartWithIC("kotlin.") ||
            importPath.StartWithIC("kotlinx.") ||
            importPath.StartWithIC("java.") ||
            importPath.StartWithIC("javax.") ||
            importPath.StartWithIC("android.") ||
            importPath.StartWithIC("androidx.") ||
            importPath.StartWithIC("io.flutter."))
        {
            return ImportType.External;
        }

        if (!string.IsNullOrEmpty(absoluteWorkspacePath))
        {
            var rel = importPath.Replace('.', '/');
            if (rel.Contains('/'))
            {
                var dirPart = rel[..rel.LastIndexOf('/')];
                if (Directory.Exists(Path.Combine(absoluteWorkspacePath, "src/main/kotlin", dirPart)) ||
                    Directory.Exists(Path.Combine(absoluteWorkspacePath, "src/main/java", dirPart)) ||
                    Directory.Exists(Path.Combine(absoluteWorkspacePath, "src", dirPart)))
                {
                    return ImportType.Internal;
                }
            }
        }

        return ImportType.External;
    }

    public async Task<SyntaxTree> ParseAsync(
        string filePath,
        string parentNodeId,
        string workspaceId,
        string absoluteWorkspacePath)
    {
        var relativePath = Path.GetRelativePath(absoluteWorkspacePath, filePath).Replace('\\', '/');
        var fileName = Path.GetFileName(filePath);
        var fileNodeId = $"{workspaceId}:{OntologyConstants.IdPrefixes.File}:{relativePath}";

        var fileNode = new FileNode(fileNodeId, fileName, relativePath, filePath);
        var content = await PhysicalFileStore.ConsumeFileTextAsync(filePath);

        var rawImports = new List<RawImport>();
        var rawVariables = new List<RawVariable>();
        var rawTypeBindings = new List<RawTypeBinding>();

        // 1. Package declaration
        string? packageName = null;
        var pkgMatch = Regex.Match(content, @"^\s*package\s+([a-zA-Z0-9_.]+)", RegexOptions.Multiline);
        if (pkgMatch.Success)
        {
            packageName = pkgMatch.Groups[1].Value.Trim();
        }

        // 2. Imports
        var importMatches = Regex.Matches(content, @"^\s*import\s+([a-zA-Z0-9_.*]+)", RegexOptions.Multiline);
        foreach (Match m in importMatches)
        {
            var imp = m.Groups[1].Value.Trim();
            rawImports.Add(new RawImport(imp, filePath));
        }

        // 3. String constant channel names: val installChannel = "hearai/apk_install"
        var stringVars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var valMatches = Regex.Matches(content, @"(?:val|var)\s+([a-zA-Z0-9_]+)\s*=\s*['""]([^'""]+)['""]");
        foreach (Match m in valMatches)
        {
            var varName = m.Groups[1].Value.Trim();
            var varVal = m.Groups[2].Value.Trim();
            stringVars[varName] = varVal;
            rawVariables.Add(new RawVariable(varName, varVal, "global", true, filePath, 1, 1, 0, 0));
        }

        // 4. Line offsets and comment/string masking
        var lineOffsets = BuildLineOffsets(content);
        var masked = KotlinDatabaseSqlExtractor.MaskCommentsAndStrings(content);
        var commentMasked = KotlinDatabaseSqlExtractor.MaskComments(content);
        var semanticSymbols = new List<SyntacticSymbol>();

        // 5. Classes / Interfaces / Objects (with JPA @Table / @Entity)
        var classScopes = ParseClassesAndTypes(content, commentMasked, masked, lineOffsets, fileNode, workspaceId, relativePath, semanticSymbols);

        // 6. Functions / Methods (with Spring HTTP mappings and JPA @Query)
        ParseFunctionsAndEndpoints(content, commentMasked, masked, lineOffsets, classScopes, fileNode, workspaceId, relativePath, semanticSymbols);

        // 7. Flutter MethodChannels & Kafka Subscriptions
        ParseChannelsAndMessaging(content, fileNode, stringVars, workspaceId, relativePath, classScopes);

        // 8. Relational database configuration detection (e.g. PostgreSQL driver)
        KotlinDatabaseSqlExtractor.DetectDatabaseConfiguration(content, fileNode, workspaceId, relativePath);

        Libraries.CoroutinesFlowChannelLibraryParser.AnalyzeSource(content, fileNode);
        Libraries.Http4kLibraryParser.AnalyzeSource(content, fileNode);
        Libraries.GrpcKotlinLibraryParser.AnalyzeSource(content, fileNode);
        Libraries.ApolloKotlinLibraryParser.AnalyzeSource(content, fileNode);
        Libraries.SqlDelightLibraryParser.AnalyzeSource(content, fileNode);
        Libraries.SupabaseFirebaseKotlinLibraryParser.AnalyzeSource(content, fileNode);
        Libraries.DaggerHiltKodeinLibraryParser.AnalyzeSource(content, fileNode);
        Libraries.KotestMockkLibraryParser.AnalyzeSource(content, fileNode);
        Libraries.ComposeNavigationKotlinLibraryParser.AnalyzeSource(content, fileNode);

        var doc = SyntaxDocument.Create(content, LanguageName, filePath, relativePath);
        doc.PackageOrNamespace = packageName;

        var syntaxTree = new SyntaxTree(
            filePath,
            relativePath,
            doc,
            fileNode,
            this,
            rawImports,
            rawVariables,
            rawTypeBindings
        );

        if (semanticSymbols.Count > 0)
        {
            syntaxTree.SemanticSymbols.AddRange(semanticSymbols);
        }

        return syntaxTree;
    }

    internal sealed record ClassScope(
        string Name,
        string Kind,
        TypeNode Node,
        int StartIndex,
        int OpenBraceIndex,
        int CloseBraceIndex,
        string? RoutePrefix,
        bool IsGrpcService = false,
        bool IsClient = false,
        bool IsDao = false,
        bool IsRoomDatabase = false,
        bool IsMapper = false,
        string? RepositoryEntity = null
    );

    internal sealed record AnnotationInfo(string Name, string Args);

    private static (int OpenBrace, int CloseBrace) FindClassBodyRange(string masked, int headerStartIndex, int matchLength)
    {
        var searchStart = headerStartIndex + matchLength;
        var openBrace = -1;

        var parenDepth = 0;
        for (var i = searchStart; i < masked.Length; i++)
        {
            var c = masked[i];
            if (c == '(') { parenDepth++; continue; }
            if (c == ')') { if (parenDepth > 0) parenDepth--; continue; }

            if (parenDepth > 0) continue;

            if (char.IsWhiteSpace(c)) continue;

            if (c == '{')
            {
                openBrace = i;
                break;
            }

            if (c == ';' || (i + 5 < masked.Length && (
                masked.AsSpan(i).StartsWith("class ") ||
                masked.AsSpan(i).StartsWith("interface ") ||
                masked.AsSpan(i).StartsWith("object "))))
            {
                return (-1, -1);
            }
        }

        if (openBrace < 0) return (-1, -1);

        var depth = 1;
        for (var i = openBrace + 1; i < masked.Length; i++)
        {
            if (masked[i] == '{') depth++;
            else if (masked[i] == '}')
            {
                depth--;
                if (depth == 0) return (openBrace, i);
            }
        }

        return (openBrace, masked.Length - 1);
    }

    private static string? ExtractSuperPart(string header)
    {
        var parenDepth = 0;
        for (var i = 0; i < header.Length; i++)
        {
            var c = header[i];
            if (c == '(') parenDepth++;
            else if (c == ')') { if (parenDepth > 0) parenDepth--; }
            else if (c == ':' && parenDepth == 0)
            {
                return header[(i + 1)..].Trim();
            }
        }
        return null;
    }

    private static List<AnnotationInfo> ExtractPrecedingAnnotations(string commentMasked, string masked, int declStartIndex)
    {
        var list = new List<AnnotationInfo>();
        if (declStartIndex <= 0) return list;

        var start = Math.Max(0, declStartIndex - 4000);
        var maskedCandidate = masked[start..declStartIndex];

        var stopIdx = maskedCandidate.LastIndexOfAny(['}', '{', ';']);
        var funIdx = maskedCandidate.LastIndexOf("fun ", StringComparison.Ordinal);
        if (funIdx > stopIdx)
        {
            var openParen = maskedCandidate.IndexOf('(', funIdx);
            if (openParen >= 0)
            {
                var closeParen = FindMatchingParen(maskedCandidate, openParen);
                if (closeParen > openParen)
                {
                    var eqIdx = maskedCandidate.IndexOf('=', closeParen);
                    var nextNl = maskedCandidate.IndexOf('\n', closeParen);
                    if (eqIdx >= 0 && (nextNl < 0 || eqIdx < nextNl))
                    {
                        var nlIdx = maskedCandidate.IndexOf('\n', eqIdx);
                        if (nlIdx >= 0 && nlIdx > stopIdx)
                        {
                            stopIdx = nlIdx;
                        }
                    }
                    else if (nextNl >= 0 && nextNl > stopIdx)
                    {
                        stopIdx = nextNl;
                    }
                }
            }
        }

        var valIdx = Math.Max(maskedCandidate.LastIndexOf("val ", StringComparison.Ordinal), maskedCandidate.LastIndexOf("var ", StringComparison.Ordinal));
        if (valIdx > stopIdx)
        {
            var nlIdx = maskedCandidate.IndexOf('\n', valIdx);
            if (nlIdx >= 0 && nlIdx > stopIdx)
            {
                stopIdx = nlIdx;
            }
        }

        var scanStart = stopIdx >= 0 ? start + stopIdx + 1 : start;
        var textToScan = commentMasked[scanStart..declStartIndex];

        return ParseAnnotationsFromText(textToScan);
    }

    private static List<AnnotationInfo> ParseAnnotationsFromText(string textToScan)
    {
        var list = new List<AnnotationInfo>();
        if (string.IsNullOrWhiteSpace(textToScan)) return list;

        var atMatches = Regex.Matches(textToScan, @"@(?<target>[a-zA-Z0-9_]+:)?(?<name>[a-zA-Z0-9_.]+)");
        foreach (Match m in atMatches)
        {
            var rawName = m.Groups["name"].Value.Trim();
            var shortName = rawName.Contains('.') ? rawName[(rawName.LastIndexOf('.') + 1)..] : rawName;
            if (IsIgnoredJavadocTag(shortName)) continue;

            var nextIdx = m.Index + m.Length;
            while (nextIdx < textToScan.Length && char.IsWhiteSpace(textToScan[nextIdx]))
            {
                nextIdx++;
            }

            string args = string.Empty;
            if (nextIdx < textToScan.Length && textToScan[nextIdx] == '(')
            {
                var openParen = nextIdx;
                var closeParen = FindMatchingParen(textToScan, openParen);
                if (closeParen > openParen)
                {
                    args = textToScan.Substring(openParen + 1, closeParen - openParen - 1).Trim();
                }
            }

            list.Add(new AnnotationInfo(shortName, args));
        }

        return list;
    }

    private static int FindMatchingParen(string text, int openParen)
    {
        var depth = 0;
        var inQuotes = false;
        var inTripleQuotes = false;
        var quoteChar = '\0';

        for (var i = openParen; i < text.Length; i++)
        {
            var c = text[i];
            if (!inQuotes && !inTripleQuotes)
            {
                if (i + 2 < text.Length && text[i] == '"' && text[i + 1] == '"' && text[i + 2] == '"')
                {
                    inTripleQuotes = true;
                    i += 2;
                    continue;
                }
                if (c is '"' or '\'')
                {
                    inQuotes = true;
                    quoteChar = c;
                    continue;
                }
                if (c == '(') depth++;
                else if (c == ')')
                {
                    depth--;
                    if (depth == 0) return i;
                }
            }
            else if (inTripleQuotes)
            {
                if (i + 2 < text.Length && text[i] == '"' && text[i + 1] == '"' && text[i + 2] == '"')
                {
                    inTripleQuotes = false;
                    i += 2;
                    continue;
                }
            }
            else
            {
                if (c == '\\') { i++; continue; }
                if (c == quoteChar)
                {
                    inQuotes = false;
                }
            }
        }
        return -1;
    }

    private static bool IsIgnoredJavadocTag(string name) =>
        name is "author" or "param" or "return" or "see" or "since" or "throws" or "version" or "constructor";

    private static List<string> ExtractRoutePathsFromAnnotation(string annotationArgs)
    {
        var paths = new List<string>();
        if (string.IsNullOrWhiteSpace(annotationArgs))
        {
            paths.Add(string.Empty);
            return paths;
        }

        var namedArrayMatch = Regex.Match(annotationArgs, @"\b(?:value|path|uri)\s*=\s*(?:\[|listOf\s*\(|arrayOf\s*\()([^\]\)]*)");
        if (namedArrayMatch.Success && !string.IsNullOrWhiteSpace(namedArrayMatch.Groups[1].Value))
        {
            var strMatches = Regex.Matches(namedArrayMatch.Groups[1].Value, @"['""]([^'""]*)['""]");
            foreach (Match sm in strMatches)
            {
                paths.Add(ConfigurationIndex.ResolvePlaceholders(sm.Groups[1].Value.Trim()));
            }
            if (paths.Count > 0) return paths;
        }

        var namedMatch = Regex.Match(annotationArgs, @"\b(?:value|path|uri)\s*=\s*['""]([^'""]*)['""]");
        if (namedMatch.Success)
        {
            paths.Add(ConfigurationIndex.ResolvePlaceholders(namedMatch.Groups[1].Value.Trim()));
            return paths;
        }

        var firstArg = annotationArgs.Split(',')[0].Trim();
        if (!firstArg.Contains('='))
        {
            var positionalArrayMatch = Regex.Match(firstArg, @"(?:\[|listOf\s*\(|arrayOf\s*\()([^\]\)]*)");
            if (positionalArrayMatch.Success && !string.IsNullOrWhiteSpace(positionalArrayMatch.Groups[1].Value))
            {
                var strMatches = Regex.Matches(positionalArrayMatch.Groups[1].Value, @"['""]([^'""]*)['""]");
                foreach (Match sm in strMatches)
                {
                    paths.Add(ConfigurationIndex.ResolvePlaceholders(sm.Groups[1].Value.Trim()));
                }
                if (paths.Count > 0) return paths;
            }

            var strMatch = Regex.Match(firstArg, @"['""]([^'""]*)['""]");
            if (strMatch.Success)
            {
                paths.Add(ConfigurationIndex.ResolvePlaceholders(strMatch.Groups[1].Value.Trim()));
                return paths;
            }
        }

        paths.Add(string.Empty);
        return paths;
    }

    private static string CombineRoutes(string? prefix, string? route)
    {
        var p = (prefix ?? string.Empty).Trim();
        var r = (route ?? string.Empty).Trim();

        if (p.EndsWith('/')) p = p[..^1];
        if (r.StartsWith('/')) r = r[1..];

        if (!string.IsNullOrEmpty(p) && !p.StartsWith('/'))
        {
            p = "/" + p;
        }

        if (string.IsNullOrEmpty(p) && string.IsNullOrEmpty(r))
        {
            return "/";
        }

        if (string.IsNullOrEmpty(p))
        {
            return r.StartsWith('/') ? r : "/" + r;
        }

        if (string.IsNullOrEmpty(r))
        {
            return p;
        }

        return $"{p}/{r}";
    }

    private static string ExtractTableName(string? tableArgs, string? entityArgs, string fallbackClassName)
    {
        if (!string.IsNullOrWhiteSpace(tableArgs))
        {
            var nameMatch = Regex.Match(tableArgs, @"\b(?:name|value|tableName)\s*=\s*['""]([^'""]+)['""]");
            if (nameMatch.Success) return nameMatch.Groups[1].Value.Trim();

            var firstArg = tableArgs.Split(',')[0].Trim();
            if (!firstArg.Contains('='))
            {
                var strMatch = Regex.Match(firstArg, @"['""]([^'""]+)['""]");
                if (strMatch.Success) return strMatch.Groups[1].Value.Trim();
            }
        }

        if (!string.IsNullOrWhiteSpace(entityArgs))
        {
            var nameMatch = Regex.Match(entityArgs, @"\b(?:name|value|tableName)\s*=\s*['""]([^'""]+)['""]");
            if (nameMatch.Success) return nameMatch.Groups[1].Value.Trim();

            var firstArg = entityArgs.Split(',')[0].Trim();
            if (!firstArg.Contains('='))
            {
                var strMatch = Regex.Match(firstArg, @"['""]([^'""]+)['""]");
                if (strMatch.Success) return strMatch.Groups[1].Value.Trim();
            }
        }

        return fallbackClassName;
    }

    private static string? ExtractTableSchema(string? tableArgs)
    {
        if (string.IsNullOrWhiteSpace(tableArgs)) return null;
        var schemaMatch = Regex.Match(tableArgs, @"\bschema\s*=\s*['""]([^'""]+)['""]");
        return schemaMatch.Success ? schemaMatch.Groups[1].Value.Trim() : null;
    }

    private static bool IsExposedTable(string? cleanBase)
    {
        if (string.IsNullOrEmpty(cleanBase)) return false;
        var b = cleanBase.Trim();
        return b.Equals("Table", StringComparison.Ordinal) ||
               b.Equals("IntIdTable", StringComparison.Ordinal) ||
               b.Equals("LongIdTable", StringComparison.Ordinal) ||
               b.Equals("UUIDTable", StringComparison.Ordinal) ||
               b.Equals("CompositeIdTable", StringComparison.Ordinal) ||
               b.StartsWith("IdTable", StringComparison.Ordinal) ||
               b.EndsWith(".Table", StringComparison.Ordinal) ||
               b.EndsWith(".IntIdTable", StringComparison.Ordinal) ||
               b.EndsWith(".LongIdTable", StringComparison.Ordinal) ||
               b.EndsWith(".UUIDTable", StringComparison.Ordinal);
    }

    private static string ExtractExposedTableName(string? superPart, string fallbackClassName)
    {
        if (!string.IsNullOrWhiteSpace(superPart))
        {
            var match = Regex.Match(superPart, @"(?:Table|IdTable)\s*\(\s*['""]([^'""]+)['""]");
            if (match.Success)
            {
                return match.Groups[1].Value.Trim();
            }
        }
        return fallbackClassName;
    }

    private static string? ExtractMyBatisOrQuerySql(string queryAnnotationArgs)
    {
        if (string.IsNullOrWhiteSpace(queryAnnotationArgs)) return null;

        var tripleMatches = Regex.Matches(queryAnnotationArgs, "\"\"\"([\\s\\S]*?)\"\"\"");
        if (tripleMatches.Count > 0)
        {
            var parts = new List<string>();
            foreach (Match tm in tripleMatches)
            {
                var val = tm.Groups[1].Value.Trim();
                if (!string.IsNullOrEmpty(val)) parts.Add(val);
            }
            if (parts.Count > 0) return string.Join(" ", parts);
        }

        var strMatches = Regex.Matches(queryAnnotationArgs, @"(?<![a-zA-Z0-9_])""((?:\\.|[^""\\])*)""");
        if (strMatches.Count > 0)
        {
            var parts = new List<string>();
            foreach (Match sm in strMatches)
            {
                var strVal = sm.Groups[1].Value.Trim();
                if (!string.IsNullOrEmpty(strVal)) parts.Add(strVal);
            }
            if (parts.Count > 0) return string.Join(" ", parts);
        }

        return null;
    }

    internal static string NormalizeMyBatisParameters(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql)) return sql;
        sql = Regex.Replace(sql, @"#\{[^}]+\}", "@p");
        sql = Regex.Replace(sql, @"\$\{[^}]+\}", "@p");
        return sql;
    }

    internal static (string? ProviderClass, string? ProviderMethod) ExtractProviderInfo(string args)
    {
        if (string.IsNullOrWhiteSpace(args)) return (null, null);

        string? providerClass = null;
        string? providerMethod = null;

        var typeMatch = Regex.Match(args, @"(?:type|value)\s*=\s*([A-Za-z0-9_]+)(?:::class|\.class)?");
        if (typeMatch.Success)
        {
            providerClass = typeMatch.Groups[1].Value;
        }
        else
        {
            var firstClassMatch = Regex.Match(args, @"([A-Za-z0-9_]+)(?:::class|\.class)");
            if (firstClassMatch.Success)
            {
                providerClass = firstClassMatch.Groups[1].Value;
            }
        }

        var methodMatch = Regex.Match(args, @"method\s*=\s*[""']([A-Za-z0-9_]+)[""']");
        if (methodMatch.Success)
        {
            providerMethod = methodMatch.Groups[1].Value;
        }
        else
        {
            var secondStrMatch = Regex.Match(args, @",\s*[""']([A-Za-z0-9_]+)[""']");
            if (secondStrMatch.Success)
            {
                providerMethod = secondStrMatch.Groups[1].Value;
            }
        }

        return (providerClass, providerMethod);
    }

    private static List<ClassScope> ParseClassesAndTypes(
        string content,
        string commentMasked,
        string masked,
        int[] lineOffsets,
        FileNode fileNode,
        string workspaceId,
        string relativePath,
        List<SyntacticSymbol> semanticSymbols)
    {
        var classScopes = new List<ClassScope>();
        var classMatches = ClassDeclRegex.Matches(masked);

        foreach (Match cm in classMatches)
        {
            var kind = cm.Groups["kind"].Value.ToLowerInvariant();
            var className = cm.Groups["name"].Value.Trim();
            var (startLine, startCol) = GetLineAndColumn(lineOffsets, cm.Index);
            var (openBrace, closeBrace) = FindClassBodyRange(masked, cm.Index, cm.Length);

            string? cleanBase = null;
            string? superPart = null;
            if (openBrace >= 0 && openBrace > cm.Index + cm.Length)
            {
                var headerSuffix = commentMasked[(cm.Index + cm.Length)..openBrace];
                superPart = ExtractSuperPart(headerSuffix);
                if (!string.IsNullOrEmpty(superPart))
                {
                    cleanBase = Regex.Replace(superPart, @"\(.*?\)", string.Empty).Trim();
                }
            }

            var typeId = $"{workspaceId}:{OntologyConstants.IdPrefixes.Symbol}:{relativePath}:Type:{className}";
            var typeNode = new TypeNode(
                typeId,
                className,
                className,
                relativePath,
                relativePath,
                startLine,
                startLine,
                startCol,
                startCol,
                kind
            )
            {
                Extends = cleanBase
            };
            fileNode.Children.Add(typeNode);

            string? repoEntity = null;
            if (!string.IsNullOrEmpty(cleanBase) &&
                (cleanBase.Contains("PanacheRepository<", StringComparison.OrdinalIgnoreCase) ||
                 cleanBase.Contains("CrudRepository<", StringComparison.OrdinalIgnoreCase) ||
                 cleanBase.Contains("PageableRepository<", StringComparison.OrdinalIgnoreCase) ||
                 cleanBase.Contains("GenericRepository<", StringComparison.OrdinalIgnoreCase) ||
                 cleanBase.Contains("JpaRepository<", StringComparison.OrdinalIgnoreCase) ||
                 cleanBase.Contains("JdbcRepository<", StringComparison.OrdinalIgnoreCase)))
            {
                var repoKeyword = cleanBase.Contains("PanacheRepository<", StringComparison.OrdinalIgnoreCase)
                    ? "PanacheRepository<"
                    : cleanBase.Contains("CrudRepository<", StringComparison.OrdinalIgnoreCase)
                        ? "CrudRepository<"
                        : cleanBase.Contains("PageableRepository<", StringComparison.OrdinalIgnoreCase)
                            ? "PageableRepository<"
                            : cleanBase.Contains("GenericRepository<", StringComparison.OrdinalIgnoreCase)
                                ? "GenericRepository<"
                                : cleanBase.Contains("JpaRepository<", StringComparison.OrdinalIgnoreCase)
                                    ? "JpaRepository<"
                                    : "JdbcRepository<";
                var idx = cleanBase.IndexOf(repoKeyword, StringComparison.OrdinalIgnoreCase);
                var open = cleanBase.IndexOf('<', idx);
                var close = open > 0 ? cleanBase.IndexOf('>', open) : -1;
                if (open > 0 && close > open)
                {
                    var entityName = cleanBase[(open + 1)..close].Trim();
                    var comma = entityName.IndexOf(',');
                    if (comma > 0) entityName = entityName[..comma].Trim();
                    if (!string.IsNullOrEmpty(entityName))
                    {
                        repoEntity = entityName;
                        fileNode.References.Add(new Reference(typeNode.Id, entityName, OntologyConstants.Relationships.AccessedBy, startLine, startCol));
                    }
                }
            }

            var annotations = ExtractPrecedingAnnotations(commentMasked, masked, cm.Index);

            var isMapper = annotations.Any(a =>
                a.Name.Equals("Mapper", StringComparison.OrdinalIgnoreCase) ||
                a.Name.EndsWith("Mapper", StringComparison.OrdinalIgnoreCase) ||
                (a.Name.Equals("Repository", StringComparison.OrdinalIgnoreCase) && className.EndsWith("Mapper", StringComparison.OrdinalIgnoreCase)));

            if (isMapper)
            {
                typeNode.CustomProperties ??= new();
                typeNode.CustomProperties["is_mapper"] = "true";
                typeNode.CustomProperties["role"] = "DataMapper";
            }

            var isController = annotations.Any(a =>
                a.Name.Equals("RestController", StringComparison.OrdinalIgnoreCase) ||
                a.Name.Equals("Controller", StringComparison.OrdinalIgnoreCase));
            if (isController)
            {
                typeNode.CustomProperties ??= new();
                typeNode.CustomProperties["role"] = "Controller";
            }

            var tableAnn = annotations.FirstOrDefault(a =>
                a.Name.Equals("Table", StringComparison.OrdinalIgnoreCase) ||
                a.Name.Equals("Document", StringComparison.OrdinalIgnoreCase) ||
                a.Name.Equals("MappedEntity", StringComparison.OrdinalIgnoreCase) ||
                a.Name.Equals("NodeEntity", StringComparison.OrdinalIgnoreCase));
            var entityAnn = annotations.FirstOrDefault(a => a.Name.Equals("Entity", StringComparison.OrdinalIgnoreCase));
            var isExposed = IsExposedTable(cleanBase);

            if (tableAnn != null || entityAnn != null || isExposed)
            {
                var tableName = isExposed
                    ? ExtractExposedTableName(superPart, className)
                    : ExtractTableName(tableAnn?.Args, entityAnn?.Args, className);
                var schema = isExposed ? null : ExtractTableSchema(tableAnn?.Args);
                var tableId = $"{workspaceId}:{OntologyConstants.IdPrefixes.Table}:{tableName.ToLowerInvariant()}";

                var tableNode = new TableNode(tableId, tableName, relativePath)
                {
                    Schema = schema
                };
                tableNode.CustomProperties ??= new();
                tableNode.CustomProperties["file_path"] = relativePath;
                tableNode.CustomProperties["name"] = tableName;
                fileNode.Children.Add(tableNode);

                fileNode.References.Add(new Reference(typeNode.Id, tableName, OntologyConstants.Relationships.PersistedIn, startLine, startCol));
                fileNode.References.Add(new Reference(tableId, typeNode.Id, OntologyConstants.Relationships.ImplementedBy, startLine, startCol));
                fileNode.References.Add(new Reference(tableId, typeNode.Id, OntologyConstants.Relationships.AccessedBy, startLine, startCol));

                var typeSyntaxNode = new TypeDeclSyntax
                {
                    Name = className,
                    Span = new TextSpan(cm.Index, cm.Index + cm.Length, startLine - 1, startCol - 1, startLine - 1, startCol - 1)
                };
                var tblSym = new SyntacticSymbol(OntologyConstants.NodeLabels.Table, tableName, typeSyntaxNode)
                {
                    ParentScopeId = typeNode.Id
                };
                if (!string.IsNullOrEmpty(schema))
                {
                    tblSym.Properties["schema_name"] = schema;
                }
                semanticSymbols.Add(tblSym);
            }

            var isRetrofitFile = content.Contains("retrofit", StringComparison.OrdinalIgnoreCase);

            var isDao = annotations.Any(a => a.Name.Equals("Dao", StringComparison.OrdinalIgnoreCase));
            var databaseAnn = annotations.FirstOrDefault(a => a.Name.Equals("Database", StringComparison.OrdinalIgnoreCase));
            var isRoomDb = databaseAnn != null || (!string.IsNullOrEmpty(cleanBase) && cleanBase.Contains("RoomDatabase", StringComparison.OrdinalIgnoreCase));

            if (isRoomDb)
            {
                var entitiesArg = databaseAnn?.Args;
                if (!string.IsNullOrWhiteSpace(entitiesArg))
                {
                    var entitiesMatch = Regex.Match(entitiesArg, @"\bentities\s*=\s*(?:\[|arrayOf\s*\()([^\]\)]*)");
                    var textToSearch = entitiesMatch.Success ? entitiesMatch.Groups[1].Value : entitiesArg;
                    var entMatches = Regex.Matches(textToSearch, @"([A-Z][a-zA-Z0-9_]+)(?:::class|\.class)?");
                    foreach (Match m in entMatches)
                    {
                        var ent = m.Groups[1].Value;
                        if (ent != "AutoMigration" && ent != "Database" && ent != "TypeConverters")
                        {
                            fileNode.References.Add(new Reference(typeNode.Id, ent, OntologyConstants.Relationships.PersistedIn, startLine, startCol));
                        }
                    }
                }
            }

            var clientAnn = annotations.FirstOrDefault(a =>
                a.Name.Equals("Client", StringComparison.OrdinalIgnoreCase) ||
                a.Name.EndsWith("Client", StringComparison.OrdinalIgnoreCase));
            var isClient = (clientAnn != null && kind.Equals("interface", StringComparison.OrdinalIgnoreCase)) ||
                           (kind.Equals("interface", StringComparison.OrdinalIgnoreCase) && isRetrofitFile && !annotations.Any(a => a.Name.Contains("Controller") || a.Name.Contains("Endpoint") || a.Name.Contains("Dao")));
            if (clientAnn != null)
            {
                var clientPaths = ExtractRoutePathsFromAnnotation(clientAnn.Args);
                var clientPath = clientPaths.FirstOrDefault(p => !string.IsNullOrEmpty(p)) ?? "/";
                var cleanClientPath = ConfigurationIndex.ResolvePlaceholders(clientPath);
                var extId = $"{workspaceId}:{OntologyConstants.IdPrefixes.ExternalService}:{relativePath}:{typeNode.Name}:{startLine}";
                var extNode = new ExternalServiceNode(
                    extId,
                    typeNode.Name,
                    ProtocolHttp,
                    typeNode.Name,
                    cleanClientPath)
                {
                    FilePath = relativePath,
                    StartLine = startLine,
                    StartCol = startCol,
                    Url = cleanClientPath
                };
                extNode.CustomProperties ??= new();
                extNode.CustomProperties["file_path"] = relativePath;
                extNode.CustomProperties["target"] = cleanClientPath;
                fileNode.Children.Add(extNode);
                fileNode.References.Add(new Reference(typeNode.Id, extId, OntologyConstants.Relationships.Calls, startLine, startCol));
            }

            string? routePrefix = null;
            var reqMapAnn = annotations.FirstOrDefault(a =>
                (a.Name.Equals("RequestMapping", StringComparison.OrdinalIgnoreCase) ||
                 a.Name.Equals("Path", StringComparison.OrdinalIgnoreCase)) && !string.IsNullOrWhiteSpace(a.Args))
                ?? annotations.FirstOrDefault(a =>
                    a.Name.Equals("RequestMapping", StringComparison.OrdinalIgnoreCase) ||
                    a.Name.Equals("RestController", StringComparison.OrdinalIgnoreCase) ||
                    a.Name.Equals("Path", StringComparison.OrdinalIgnoreCase) ||
                    a.Name.Equals("Controller", StringComparison.OrdinalIgnoreCase));
            if (reqMapAnn != null)
            {
                var prefixes = ExtractRoutePathsFromAnnotation(reqMapAnn.Args);
                routePrefix = prefixes.FirstOrDefault(p => !string.IsNullOrEmpty(p)) ?? string.Empty;
            }

            var isGrpcService = annotations.Any(a => a.Name.Equals("GrpcService", StringComparison.OrdinalIgnoreCase)) ||
                                (!string.IsNullOrEmpty(cleanBase) && cleanBase.Contains("ImplBase", StringComparison.OrdinalIgnoreCase));

            var retrofitCreateMatches = Regex.Matches(content, @"\.create\s*\(\s*([A-Za-z0-9_]+)::class");
            foreach (Match rcm in retrofitCreateMatches)
            {
                var ifaceName = rcm.Groups[1].Value.Trim();
                fileNode.References.Add(new Reference(typeNode.Id, ifaceName, OntologyConstants.Relationships.Calls, startLine, startCol));
            }

            classScopes.Add(new ClassScope(className, kind, typeNode, cm.Index, openBrace, closeBrace, routePrefix, isGrpcService, isClient, isDao, isRoomDb, isMapper, repoEntity));
        }

        return classScopes;
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

    private static int[] BuildLineOffsets(string text)
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
