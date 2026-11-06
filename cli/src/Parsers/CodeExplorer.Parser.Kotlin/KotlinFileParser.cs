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

public class KotlinFileParser : IFileParser
{
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

    public IReadOnlyList<ISemanticExtension> SemanticExtensions => [];

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
        var masked = MaskCommentsAndStrings(content);
        var commentMasked = MaskComments(content);
        var semanticSymbols = new List<SyntacticSymbol>();

        // 5. Classes / Interfaces / Objects (with JPA @Table / @Entity)
        var classScopes = ParseClassesAndTypes(content, commentMasked, masked, lineOffsets, fileNode, workspaceId, relativePath, semanticSymbols);

        // 6. Functions / Methods (with Spring HTTP mappings and JPA @Query)
        ParseFunctionsAndEndpoints(content, commentMasked, masked, lineOffsets, classScopes, fileNode, workspaceId, relativePath, semanticSymbols);

        // 7. Flutter MethodChannels & Kafka Subscriptions
        ParseChannelsAndMessaging(content, fileNode, stringVars, workspaceId, relativePath, classScopes);

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

    private sealed record ClassScope(
        string Name,
        string Kind,
        TypeNode Node,
        int StartIndex,
        int OpenBraceIndex,
        int CloseBraceIndex,
        string? RoutePrefix
    );

    private sealed record AnnotationInfo(string Name, string Args);

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

        var start = Math.Max(0, declStartIndex - 1000);
        var maskedCandidate = masked[start..declStartIndex];

        var stopIdx = maskedCandidate.LastIndexOfAny(['}', '{', ';']);
        var funIdx = maskedCandidate.LastIndexOf("fun ", StringComparison.Ordinal);
        if (funIdx > stopIdx)
        {
            var eqIdx = maskedCandidate.IndexOf('=', funIdx);
            if (eqIdx >= 0)
            {
                var nlIdx = maskedCandidate.IndexOf('\n', eqIdx);
                if (nlIdx >= 0 && nlIdx > stopIdx)
                {
                    stopIdx = nlIdx;
                }
            }
        }

        var scanStart = stopIdx >= 0 ? start + stopIdx + 1 : start;
        var textToScan = commentMasked[scanStart..declStartIndex];

        var matches = AnnotationRegex.Matches(textToScan);
        foreach (Match m in matches)
        {
            var name = m.Groups["name"].Value.Trim();
            var args = m.Groups["args"].Success ? m.Groups["args"].Value.Trim() : string.Empty;
            if (name.Contains('.'))
            {
                name = name[(name.LastIndexOf('.') + 1)..];
            }
            if (!IsIgnoredJavadocTag(name))
            {
                list.Add(new AnnotationInfo(name, args));
            }
        }

        return list;
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

        var namedArrayMatch = Regex.Match(annotationArgs, @"\b(?:value|path)\s*=\s*(?:\[|listOf\s*\(|arrayOf\s*\()([^\]\)]*)");
        if (namedArrayMatch.Success && !string.IsNullOrWhiteSpace(namedArrayMatch.Groups[1].Value))
        {
            var strMatches = Regex.Matches(namedArrayMatch.Groups[1].Value, @"['""]([^'""]*)['""]");
            foreach (Match sm in strMatches)
            {
                paths.Add(sm.Groups[1].Value.Trim());
            }
            if (paths.Count > 0) return paths;
        }

        var namedMatch = Regex.Match(annotationArgs, @"\b(?:value|path)\s*=\s*['""]([^'""]*)['""]");
        if (namedMatch.Success)
        {
            paths.Add(namedMatch.Groups[1].Value.Trim());
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
                    paths.Add(sm.Groups[1].Value.Trim());
                }
                if (paths.Count > 0) return paths;
            }

            var strMatch = Regex.Match(firstArg, @"['""]([^'""]*)['""]");
            if (strMatch.Success)
            {
                paths.Add(strMatch.Groups[1].Value.Trim());
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
            var nameMatch = Regex.Match(tableArgs, @"\bname\s*=\s*['""]([^'""]+)['""]");
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
            var nameMatch = Regex.Match(entityArgs, @"\bname\s*=\s*['""]([^'""]+)['""]");
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

    private static string? ExtractQuerySql(string queryAnnotationArgs)
    {
        if (string.IsNullOrWhiteSpace(queryAnnotationArgs)) return null;

        var valueMatch = Regex.Match(queryAnnotationArgs, @"\bvalue\s*=\s*['""]([^'""]+)['""]");
        if (valueMatch.Success) return valueMatch.Groups[1].Value.Trim();

        var firstArg = queryAnnotationArgs.Split(',')[0].Trim();
        if (!firstArg.Contains('='))
        {
            var strMatch = Regex.Match(firstArg, @"['""]([^'""]+)['""]");
            if (strMatch.Success) return strMatch.Groups[1].Value.Trim();
        }

        var anyMatch = Regex.Match(queryAnnotationArgs, @"['""]([^'""]+)['""]");
        return anyMatch.Success ? anyMatch.Groups[1].Value.Trim() : null;
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
            if (openBrace >= 0 && openBrace > cm.Index + cm.Length)
            {
                var headerSuffix = masked[(cm.Index + cm.Length)..openBrace];
                var superPart = ExtractSuperPart(headerSuffix);
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

            var annotations = ExtractPrecedingAnnotations(commentMasked, masked, cm.Index);

            var tableAnn = annotations.FirstOrDefault(a => a.Name.Equals("Table", StringComparison.OrdinalIgnoreCase) || a.Name.Equals("Document", StringComparison.OrdinalIgnoreCase));
            var entityAnn = annotations.FirstOrDefault(a => a.Name.Equals("Entity", StringComparison.OrdinalIgnoreCase));

            if (tableAnn != null || entityAnn != null)
            {
                var tableName = ExtractTableName(tableAnn?.Args, entityAnn?.Args, className);
                var schema = ExtractTableSchema(tableAnn?.Args);
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

            string? routePrefix = null;
            var reqMapAnn = annotations.FirstOrDefault(a => a.Name.Equals("RequestMapping", StringComparison.OrdinalIgnoreCase));
            if (reqMapAnn != null)
            {
                var prefixes = ExtractRoutePathsFromAnnotation(reqMapAnn.Args);
                routePrefix = prefixes.FirstOrDefault(p => !string.IsNullOrEmpty(p)) ?? string.Empty;
            }

            classScopes.Add(new ClassScope(className, kind, typeNode, cm.Index, openBrace, closeBrace, routePrefix));
        }

        return classScopes;
    }

    private static void ParseFunctionsAndEndpoints(
        string content,
        string commentMasked,
        string masked,
        int[] lineOffsets,
        List<ClassScope> classScopes,
        FileNode fileNode,
        string workspaceId,
        string relativePath,
        List<SyntacticSymbol> semanticSymbols)
    {
        var funMatches = FunDeclRegex.Matches(masked);

        foreach (Match fm in funMatches)
        {
            var funName = fm.Groups["name"].Value.Trim();
            var (funLine, funCol) = GetLineAndColumn(lineOffsets, fm.Index);

            ClassScope? enclosingClass = null;
            var bestSpan = int.MaxValue;
            foreach (var cs in classScopes)
            {
                if (cs.OpenBraceIndex >= 0 && cs.CloseBraceIndex >= 0 &&
                    fm.Index >= cs.OpenBraceIndex && fm.Index <= cs.CloseBraceIndex)
                {
                    var span = cs.CloseBraceIndex - cs.OpenBraceIndex;
                    if (span < bestSpan)
                    {
                        bestSpan = span;
                        enclosingClass = cs;
                    }
                }
            }

            var funId = $"{workspaceId}:{OntologyConstants.IdPrefixes.Symbol}:{relativePath}:Function:{funName}";
            var funcNode = new FunctionNode(
                funId,
                funName,
                funName,
                relativePath,
                relativePath,
                funLine,
                funLine,
                funCol,
                funCol
            );

            if (enclosingClass != null)
            {
                enclosingClass.Node.Children.Add(funcNode);
            }
            else
            {
                fileNode.Children.Add(funcNode);
            }

            var annotations = ExtractPrecedingAnnotations(commentMasked, masked, fm.Index);

            var fnSyntaxNode = new FunctionDeclSyntax
            {
                Name = funName,
                Span = new TextSpan(fm.Index, fm.Index + fm.Length, funLine - 1, funCol - 1, funLine - 1, funCol - 1)
            };

            foreach (var ann in annotations)
            {
                var httpMethod = ann.Name switch
                {
                    "GetMapping" => "GET",
                    "PostMapping" => "POST",
                    "PutMapping" => "PUT",
                    "DeleteMapping" => "DELETE",
                    "PatchMapping" => "PATCH",
                    "RequestMapping" => DetermineHttpMethodFromRequestMapping(ann.Args),
                    _ => null
                };

                if (httpMethod != null)
                {
                    var paths = ExtractRoutePathsFromAnnotation(ann.Args);
                    foreach (var rawPath in paths)
                    {
                        var fullRoute = CombineRoutes(enclosingClass?.RoutePrefix, rawPath);
                        var epId = $"{workspaceId}:{OntologyConstants.IdPrefixes.Endpoint}:{httpMethod}:{fullRoute}";
                        var epName = $"{httpMethod} {fullRoute}";

                        var epNode = new EndpointNode(
                            epId,
                            epName,
                            relativePath,
                            httpMethod,
                            fullRoute,
                            "REST"
                        )
                        {
                            FilePath = relativePath,
                            StartLine = funLine,
                            EndLine = funLine,
                            Line = funLine,
                            LineEnd = funLine,
                            Operation = httpMethod,
                            EvidenceKind = OntologyConstants.EvidenceKinds.Endpoint
                        };
                        epNode.CustomProperties ??= new();
                        epNode.CustomProperties["path"] = fullRoute;
                        epNode.CustomProperties["route"] = fullRoute;
                        epNode.CustomProperties["route_template"] = fullRoute;
                        epNode.CustomProperties["method"] = httpMethod;
                        epNode.CustomProperties["http_method"] = httpMethod;
                        epNode.CustomProperties["file_path"] = relativePath;
                        fileNode.Children.Add(epNode);

                        fileNode.References.Add(new Reference(epId, funcNode.Id, OntologyConstants.Relationships.ExposedBy, funLine, funCol));
                    }
                }

                if (ann.Name.Equals("Query", StringComparison.OrdinalIgnoreCase))
                {
                    var sqlCandidate = ExtractQuerySql(ann.Args);
                    if (!string.IsNullOrWhiteSpace(sqlCandidate))
                    {
                        var cleanSql = NestedSqlParser.CleanQueryText(sqlCandidate);
                        var qId = $"{workspaceId}:{OntologyConstants.IdPrefixes.Query}:{relativePath}:{funLine}:{funCol}";
                        var qName = $"JPA Query: {cleanSql}";

                        var queryNode = new QueryNode(qId, qName, cleanSql, relativePath, funLine, funLine)
                        {
                            FilePath = relativePath,
                            Line = funLine,
                            LineEnd = funLine,
                            Operation = "SELECT",
                            EvidenceKind = OntologyConstants.EvidenceKinds.Query
                        };
                        queryNode.CustomProperties ??= new();
                        queryNode.CustomProperties["file_path"] = relativePath;
                        fileNode.Children.Add(queryNode);

                        fileNode.References.Add(new Reference(qId, funcNode.Id, OntologyConstants.Relationships.AccessedBy, funLine, funCol));
                    }
                }
            }
        }
    }

    private static string DetermineHttpMethodFromRequestMapping(string args)
    {
        if (string.IsNullOrWhiteSpace(args)) return "ALL";
        if (args.Contains("RequestMethod.POST", StringComparison.OrdinalIgnoreCase)) return "POST";
        if (args.Contains("RequestMethod.PUT", StringComparison.OrdinalIgnoreCase)) return "PUT";
        if (args.Contains("RequestMethod.DELETE", StringComparison.OrdinalIgnoreCase)) return "DELETE";
        if (args.Contains("RequestMethod.PATCH", StringComparison.OrdinalIgnoreCase)) return "PATCH";
        if (args.Contains("RequestMethod.HEAD", StringComparison.OrdinalIgnoreCase)) return "HEAD";
        if (args.Contains("RequestMethod.OPTIONS", StringComparison.OrdinalIgnoreCase)) return "OPTIONS";
        if (args.Contains("RequestMethod.GET", StringComparison.OrdinalIgnoreCase)) return "GET";
        return "ALL";
    }

    private static void ParseChannelsAndMessaging(
        string content,
        FileNode fileNode,
        Dictionary<string, string> stringVars,
        string workspaceId,
        string relativePath,
        List<ClassScope> classScopes)
    {
        var primaryClassNode = classScopes.Count > 0 ? classScopes[0].Node : null;

        var channelRegex = new Regex(@"MethodChannel\s*\(\s*[^,]+,\s*(?:['""]([^'""]+)['""]|([a-zA-Z0-9_]+))\s*\)");
        var channelMatches = channelRegex.Matches(content);
        var registeredChannels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match chm in channelMatches)
        {
            var literal = chm.Groups[1].Success ? chm.Groups[1].Value.Trim() : null;
            var idVar = chm.Groups[2].Success ? chm.Groups[2].Value.Trim() : null;

            var channelName = literal;
            if (string.IsNullOrEmpty(channelName) && !string.IsNullOrEmpty(idVar) && stringVars.TryGetValue(idVar, out var resolved))
            {
                channelName = resolved;
            }

            if (!string.IsNullOrEmpty(channelName) && registeredChannels.Add(channelName))
            {
                var channelId = $"{workspaceId}:{OntologyConstants.IdPrefixes.EntryPoint}:channel:{channelName.ToLowerInvariant()}";
                var channelEp = new EntryPointNode(
                    channelId,
                    $"MethodChannel {channelName}",
                    relativePath,
                    "channel"
                );
                fileNode.Children.Add(channelEp);
            }
        }

        foreach (var (varName, varVal) in stringVars)
        {
            if (varName.EndsWith("Channel", StringComparison.OrdinalIgnoreCase) && varVal.Contains('/') && registeredChannels.Add(varVal))
            {
                var channelId = $"{workspaceId}:{OntologyConstants.IdPrefixes.EntryPoint}:channel:{varVal.ToLowerInvariant()}";
                var channelEp = new EntryPointNode(
                    channelId,
                    $"MethodChannel {varVal}",
                    relativePath,
                    "channel"
                );
                fileNode.Children.Add(channelEp);
            }
        }

        var kafkaMatches = KafkaSubscribeRegex.Matches(content);
        foreach (Match km in kafkaMatches)
        {
            var literal = km.Groups[1].Success ? km.Groups[1].Value.Trim() : null;
            var idVar = km.Groups[2].Success ? km.Groups[2].Value.Trim() : null;
            var tName = literal;
            if (string.IsNullOrEmpty(tName) && !string.IsNullOrEmpty(idVar))
            {
                if (stringVars.TryGetValue(idVar, out var res)) tName = res;
                else
                {
                    var fallbackMatch = Regex.Match(content, $@"{idVar}\s*:[^=]+=\s*.*?\?:\s*['""]([^'""]+)['""]");
                    if (fallbackMatch.Success) tName = fallbackMatch.Groups[1].Value;
                    else tName = idVar;
                }
            }

            if (!string.IsNullOrEmpty(tName))
            {
                var target = $"kafka:{tName}";
                var scopeId = primaryClassNode?.Id ?? fileNode.Id;
                fileNode.References.Add(new Reference(scopeId, target, OntologyConstants.Relationships.SubscribesTo, 1, 1));
            }
        }
    }

    private static (int Line, int Column) GetLineAndColumn(int[] lineOffsets, int charIndex)
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

    private static string MaskCommentsAndStrings(string source)
    {
        var chars = source.ToCharArray();
        var len = chars.Length;
        var i = 0;

        while (i < len)
        {
            if (i + 1 < len && chars[i] == '/' && chars[i + 1] == '/')
            {
                chars[i] = ' ';
                chars[i + 1] = ' ';
                i += 2;
                while (i < len && chars[i] != '\n')
                {
                    chars[i] = ' ';
                    i++;
                }
                continue;
            }

            if (i + 1 < len && chars[i] == '/' && chars[i + 1] == '*')
            {
                var depth = 1;
                chars[i] = ' ';
                chars[i + 1] = ' ';
                i += 2;
                while (i < len && depth > 0)
                {
                    if (i + 1 < len && chars[i] == '/' && chars[i + 1] == '*')
                    {
                        depth++;
                        chars[i] = ' ';
                        chars[i + 1] = ' ';
                        i += 2;
                    }
                    else if (i + 1 < len && chars[i] == '*' && chars[i + 1] == '/')
                    {
                        depth--;
                        chars[i] = ' ';
                        chars[i + 1] = ' ';
                        i += 2;
                    }
                    else
                    {
                        if (chars[i] != '\n') chars[i] = ' ';
                        i++;
                    }
                }
                continue;
            }

            if (i + 2 < len && chars[i] == '"' && chars[i + 1] == '"' && chars[i + 2] == '"')
            {
                chars[i] = ' '; chars[i + 1] = ' '; chars[i + 2] = ' ';
                i += 3;
                while (i < len)
                {
                    if (i + 2 < len && chars[i] == '"' && chars[i + 1] == '"' && chars[i + 2] == '"')
                    {
                        chars[i] = ' '; chars[i + 1] = ' '; chars[i + 2] = ' ';
                        i += 3;
                        break;
                    }
                    if (chars[i] != '\n') chars[i] = ' ';
                    i++;
                }
                continue;
            }

            if (chars[i] == '"')
            {
                chars[i] = ' ';
                i++;
                while (i < len && chars[i] != '"')
                {
                    if (chars[i] == '\\' && i + 1 < len)
                    {
                        chars[i] = ' ';
                        i++;
                    }
                    if (chars[i] != '\n') chars[i] = ' ';
                    i++;
                }
                if (i < len && chars[i] == '"')
                {
                    chars[i] = ' ';
                    i++;
                }
                continue;
            }

            if (chars[i] == '\'')
            {
                chars[i] = ' ';
                i++;
                while (i < len && chars[i] != '\'')
                {
                    if (chars[i] == '\\' && i + 1 < len)
                    {
                        chars[i] = ' ';
                        i++;
                    }
                    if (chars[i] != '\n') chars[i] = ' ';
                    i++;
                }
                if (i < len && chars[i] == '\'')
                {
                    chars[i] = ' ';
                    i++;
                }
                continue;
            }

            i++;
        }

        return new string(chars);
    }

    private static string MaskComments(string source)
    {
        var chars = source.ToCharArray();
        var len = chars.Length;
        var i = 0;

        while (i < len)
        {
            if (i + 1 < len && chars[i] == '/' && chars[i + 1] == '/')
            {
                chars[i] = ' ';
                chars[i + 1] = ' ';
                i += 2;
                while (i < len && chars[i] != '\n')
                {
                    chars[i] = ' ';
                    i++;
                }
                continue;
            }

            if (i + 1 < len && chars[i] == '/' && chars[i + 1] == '*')
            {
                var depth = 1;
                chars[i] = ' ';
                chars[i + 1] = ' ';
                i += 2;
                while (i < len && depth > 0)
                {
                    if (i + 1 < len && chars[i] == '/' && chars[i + 1] == '*')
                    {
                        depth++;
                        chars[i] = ' ';
                        chars[i + 1] = ' ';
                        i += 2;
                    }
                    else if (i + 1 < len && chars[i] == '*' && chars[i + 1] == '/')
                    {
                        depth--;
                        chars[i] = ' ';
                        chars[i + 1] = ' ';
                        i += 2;
                    }
                    else
                    {
                        if (chars[i] != '\n') chars[i] = ' ';
                        i++;
                    }
                }
                continue;
            }

            if (i + 2 < len && chars[i] == '"' && chars[i + 1] == '"' && chars[i + 2] == '"')
            {
                i += 3;
                while (i < len)
                {
                    if (i + 2 < len && chars[i] == '"' && chars[i + 1] == '"' && chars[i + 2] == '"')
                    {
                        i += 3;
                        break;
                    }
                    i++;
                }
                continue;
            }

            if (chars[i] == '"')
            {
                i++;
                while (i < len && chars[i] != '"')
                {
                    if (chars[i] == '\\' && i + 1 < len)
                    {
                        i += 2;
                        continue;
                    }
                    i++;
                }
                if (i < len && chars[i] == '"') i++;
                continue;
            }

            if (chars[i] == '\'')
            {
                i++;
                while (i < len && chars[i] != '\'')
                {
                    if (chars[i] == '\\' && i + 1 < len)
                    {
                        i += 2;
                        continue;
                    }
                    i++;
                }
                if (i < len && chars[i] == '\'') i++;
                continue;
            }

            i++;
        }

        return new string(chars);
    }
}
