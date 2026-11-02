using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Parser.Syntax;
using TsNode = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Core.Parser;

public partial class ResourceSemanticAnalyzer
{
    private static readonly Regex SchemaCallRegex = new(@"HasDefaultSchema\s*\(\s*[""']?([^""'\)]+)[""']?\s*\)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex SchemaConstRegex = new(@"(?:Schema_?Name|Default_?Schema_?Name|Default_?Schema)\s*(?::\s*[^=]+)?=\s*[""']?([^""';]+)[""']?", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex TypeOrmSchemaRegex = new(@"@(?:Entity|ViewEntity|Table)\s*\(\s*\{[^}]*schema\s*:\s*[""']?([A-Za-z0-9_.]+)[""']?", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex ToTableSchemaRegex = new(@"ToTable\s*\(\s*[^,]+\s*,\s*(?:schema:\s*)?[""']?([A-Za-z0-9_.]+)[""']?\s*\)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex DataSourceSchemaRegex = new(@"\b(?:schema|defaultSchema)\s*:\s*[""']([A-Za-z0-9_-]+)[""']", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static bool HasEntityOrTableDeclaration(FileNode? fileNode, SyntaxTree? syntaxTree)
    {
        if (fileNode != null && HasEntityOrTableNode(fileNode))
        {
            return true;
        }

        if (syntaxTree != null)
        {
            try
            {
                var text = syntaxTree.Document?.SourceText
                           ?? (!string.IsNullOrEmpty(syntaxTree.FilePath) && File.Exists(syntaxTree.FilePath)
                               ? File.ReadAllText(syntaxTree.FilePath)
                               : null);
                if (!string.IsNullOrEmpty(text) &&
                    Regex.IsMatch(text, @"(?::\s*(?:[A-Za-z0-9_.]+\.)?(?:Identity)?DbContext\b|\bI?DbSet\s*<|\b__tablename__\b|@(?:Entity|Table|ViewEntity)\b|\[\s*Table\s*\(|\bdeclarative_base\b|\bmodelBuilder\.Entity\b)", RegexOptions.IgnoreCase))
                {
                    return true;
                }
            }
            catch { }
        }

        return false;
    }

    private static readonly Regex CSharpDbContextClassRegex = new(
        @"class\s+([A-Za-z0-9_]+)\s*:\s*(?:[A-Za-z0-9_.]+\.)?(?:Identity)?DbContext\b",
        RegexOptions.Compiled);

    private static readonly Regex CSharpBaseConnectionNameRegex = new(
        @":\s*base\s*\(\s*@?[""'](?:name\s*=\s*)?([A-Za-z0-9_.\-:]+)[""']\s*\)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    internal static IReadOnlyList<(string ClassName, string ContextStem, string? ConnectionKey)> DetectDeclaredDbContextInfos(
        FileNode? fileNode,
        SyntaxTree? syntaxTree)
    {
        var filePath = syntaxTree?.FilePath ?? fileNode?.FullPath;
        if (string.IsNullOrEmpty(filePath) || !filePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
        {
            return [];
        }

        try
        {
            var text = syntaxTree?.Document?.SourceText
                       ?? (File.Exists(filePath) ? File.ReadAllText(filePath) : null);
            if (string.IsNullOrEmpty(text) || !text.Contains("DbContext", StringComparison.Ordinal))
            {
                return [];
            }

            var matches = CSharpDbContextClassRegex.Matches(text);
            if (matches.Count == 0)
            {
                return [];
            }

            string? baseConnKey = null;
            var baseMatch = CSharpBaseConnectionNameRegex.Match(text);
            if (baseMatch.Success)
            {
                baseConnKey = baseMatch.Groups[1].Value.Trim();
            }

            var list = new List<(string ClassName, string ContextStem, string? ConnectionKey)>();
            foreach (Match m in matches)
            {
                var className = m.Groups[1].Value.Trim();
                if (string.IsNullOrEmpty(className) ||
                    className.Equals("DbContext", StringComparison.OrdinalIgnoreCase) ||
                    className.Equals("IdentityDbContext", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var stem = className;
                if (stem.EndsWith("DbContext", StringComparison.OrdinalIgnoreCase) && stem.Length > "DbContext".Length)
                {
                    stem = stem[..^"DbContext".Length];
                }
                else if (stem.EndsWith("Context", StringComparison.OrdinalIgnoreCase) && stem.Length > "Context".Length)
                {
                    stem = stem[..^"Context".Length];
                }

                list.Add((className, stem, baseConnKey));
            }

            return list;
        }
        catch
        {
            return [];
        }
    }

    internal static bool IsGenericDbContextStem(string? stem)
    {
        if (string.IsNullOrWhiteSpace(stem)) return true;
        return stem.Equals("App", StringComparison.OrdinalIgnoreCase) ||
               stem.Equals("Application", StringComparison.OrdinalIgnoreCase) ||
               stem.Equals("Default", StringComparison.OrdinalIgnoreCase) ||
               stem.Equals("Base", StringComparison.OrdinalIgnoreCase) ||
               stem.Equals("Database", StringComparison.OrdinalIgnoreCase) ||
               stem.Equals("Data", StringComparison.OrdinalIgnoreCase) ||
               stem.Equals("Db", StringComparison.OrdinalIgnoreCase) ||
               stem.Equals("Ef", StringComparison.OrdinalIgnoreCase) ||
               stem.Equals("Test", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasEntityOrTableNode(IOntologyNode node)
    {
        if (node is DatabaseNode dbNode && dbNode.Extensions != null &&
            dbNode.Extensions.TryGetValue("is_implicit_schema", out var imp) && imp == "true")
        {
            return false;
        }
        if (node is TableNode) return true;
        if (node.Extensions != null && (node.Extensions.ContainsKey("table") || node.Extensions.ContainsKey("entity")))
        {
            return true;
        }
        foreach (var child in node.Children)
        {
            if (HasEntityOrTableNode(child)) return true;
        }
        return false;
    }

    public static IReadOnlyList<string> DetectDeclaredSchemas(ProjectNode? projectNode, FileNode? fileNode, SyntaxTree? syntaxTree, string? dbType = null)
    {
        var results = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddCandidate(string? cand)
        {
            if (string.IsNullOrWhiteSpace(cand)) return;
            var clean = cand.Trim();
            if (IsValidSchemaName(clean) && seen.Add(clean))
            {
                results.Add(clean);
            }
        }

        // 1. Collect from DetectDeclaredSchema
        var directSchema = DetectDeclaredSchema(projectNode, fileNode, syntaxTree, dbType);
        AddCandidate(directSchema);

        // 2. Walk AST if available (only if source text actually mentions schema or client constructors)
        try
        {
            var docText = syntaxTree?.Document?.SourceText;
            var shouldWalkAst = string.IsNullOrEmpty(docText) ||
                                docText.Contains("schema", StringComparison.OrdinalIgnoreCase) ||
                                docText.Contains("PgClient", StringComparison.OrdinalIgnoreCase) ||
                                docText.Contains("PostgresClient", StringComparison.OrdinalIgnoreCase);
            if (shouldWalkAst && syntaxTree?.Tree != null)
            {
                var root = syntaxTree.Tree.RootNode;
                if (root.IsValid())
                {
                    static bool IsSchemaVarName(string? name)
                    {
                        if (string.IsNullOrWhiteSpace(name)) return false;
                        var lower = name.ToLowerInvariant();
                        return lower == "schema" || lower.EndsWith("_schema") ||
                               lower == "schemaname" || lower.EndsWith("_schemaname") ||
                               lower == "schema_name" || lower.EndsWith("_schema_name");
                    }

                    var clientConstructors = new[]
                    {
                        "PostgresClient::new", "AnonStatsPgClient::new", "PgClient::new"
                    };

                    var isNonTestFile = (fileNode == null || !fileNode.IsTest) &&
                                        !WorkspaceConventions.IsTestFilePath(fileNode?.Path ?? syntaxTree?.FilePath);

                    void WalkAst(TsNode node)
                    {
                        if (isNonTestFile && node is NamespaceDeclSyntax nsDecl)
                        {
                            var modNameText = !string.IsNullOrEmpty(nsDecl.Name)
                                ? nsDecl.Name
                                : (node.GetField(SyntaxFields.Name) ?? node.Children.FirstOrDefault(c => c is IdentifierExprSyntax))?.Text;
                            if (!string.IsNullOrEmpty(modNameText) &&
                                (modNameText.Equals("tests", StringComparison.OrdinalIgnoreCase) ||
                                 modNameText.Equals("test_utils", StringComparison.OrdinalIgnoreCase)))
                            {
                                return;
                            }
                        }

                        if (node is VariableDeclSyntax or MemberDeclSyntax or AssignmentExprSyntax || node.Type == "keyed_element")
                        {
                            string? varName = null;
                            TsNode? valNode = null;

                            if (node is VariableDeclSyntax varDecl)
                            {
                                varName = !string.IsNullOrEmpty(varDecl.Name)
                                    ? varDecl.Name
                                    : (node.GetField(SyntaxFields.Pattern) ?? node.GetField(SyntaxFields.Name) ?? node.GetField(SyntaxFields.Left) ?? node.Children.FirstOrDefault(c => c is IdentifierExprSyntax))?.Text;
                                valNode = varDecl.Initializer ?? node.GetField(SyntaxFields.Value) ?? node.GetField(SyntaxFields.Right);
                                if (valNode.IsValid() && valNode.Children.Count > 0 && valNode is not ExprSyntax)
                                {
                                    valNode = valNode.Children[^1];
                                }
                            }
                            else if (node is MemberDeclSyntax memDecl)
                            {
                                varName = !string.IsNullOrEmpty(memDecl.Name)
                                    ? memDecl.Name
                                    : (node.GetField(SyntaxFields.Name) ?? node.Children.FirstOrDefault(c => c is IdentifierExprSyntax))?.Text;
                                valNode = memDecl.Initializer ?? node.GetField(SyntaxFields.Value);
                                if (!valNode.IsValid())
                                {
                                    var equalsIdx = -1;
                                    for (var i = 0; i < node.Children.Count; i++)
                                    {
                                        if (node.Children[i].Text == "=")
                                        {
                                            equalsIdx = i;
                                            break;
                                        }
                                    }
                                    if (equalsIdx >= 0 && equalsIdx + 1 < node.Children.Count)
                                    {
                                        valNode = node.Children[equalsIdx + 1];
                                    }
                                }
                            }
                            else if (node is AssignmentExprSyntax assignExpr)
                            {
                                var left = assignExpr.Left ?? node.GetField(SyntaxFields.Left);
                                if (left.IsValid()) varName = left.Text;
                                valNode = assignExpr.Right ?? node.GetField(SyntaxFields.Right);
                                if (valNode.IsValid() && valNode is not ExprSyntax && valNode.Children.Count > 0)
                                {
                                    valNode = valNode.Children[0];
                                }
                            }
                            else if (node.Type == "keyed_element")
                            {
                                var colonIdx = -1;
                                for (var i = 0; i < node.Children.Count; i++)
                                {
                                    if (node.Children[i].Text == ":")
                                    {
                                        colonIdx = i;
                                        break;
                                    }
                                }
                                if (colonIdx > 0 && colonIdx + 1 < node.Children.Count)
                                {
                                    varName = node.Children[0].Text.Trim();
                                    valNode = node.Children[colonIdx + 1];
                                    if (valNode.Type == "literal_element" && valNode.Children.Count > 0)
                                    {
                                        valNode = valNode.Children[0];
                                    }
                                }
                            }

                            if (!string.IsNullOrEmpty(varName) && IsSchemaVarName(varName) && valNode.IsValid())
                            {
                                if (AstValueResolver.TryResolveString(valNode, projectNode?.Name, out var s))
                                {
                                    AddCandidate(s);
                                }
                            }
                        }

                        if ((projectNode == null || !projectNode.IsLibrary) && node.Children.Any(c => c is MemberDeclSyntax))
                        {
                            TsNode? lastAttr = null;
                            foreach (var child in node.Children)
                            {
                                if (child is AnnotationSyntax)
                                {
                                    lastAttr = child;
                                }
                                else if (child is MemberDeclSyntax memChild)
                                {
                                    var attr = child.Children.FirstOrDefault(c => c is AnnotationSyntax);
                                    var effectiveAttr = attr.IsValid() ? attr : lastAttr;
                                    var fNameText = !string.IsNullOrEmpty(memChild.Name)
                                        ? memChild.Name
                                        : (child.GetField(SyntaxFields.Name) ?? child.Children.FirstOrDefault(c => c is IdentifierExprSyntax))?.Text;
                                    if (effectiveAttr.IsValid() && !string.IsNullOrEmpty(fNameText) && IsSchemaVarName(fNameText))
                                    {
                                        if (AstValueResolver.TryResolveString(effectiveAttr, projectNode?.Name ?? fileNode?.Path ?? syntaxTree?.FilePath, out var s))
                                        {
                                            AddCandidate(s);
                                        }
                                    }
                                    lastAttr = null;
                                }
                            }
                        }

                        if (node is CallExprSyntax callExpr)
                        {
                            var funcNode = node.GetFunctionNode();
                            var funcText = funcNode.IsValid() ? funcNode.Text : callExpr.MethodName;

                            if (funcText.EndsWith("_schema", StringComparison.OrdinalIgnoreCase) ||
                                funcText.EndsWith("_schema_name", StringComparison.OrdinalIgnoreCase) ||
                                funcText.EndsWith("Schema", StringComparison.OrdinalIgnoreCase) ||
                                funcText.EndsWith("SchemaName", StringComparison.OrdinalIgnoreCase))
                            {
                                if (AstValueResolver.TryResolveString(node, projectNode?.Name, out var s))
                                {
                                    AddCandidate(s);
                                }
                            }

                            if (clientConstructors.Any(c => funcText.EndsWith(c, StringComparison.OrdinalIgnoreCase)))
                            {
                                var argsList = node.GetField(SyntaxFields.Arguments) ??
                                               node.FindChildOfType("argument_list") ??
                                               node.FindChildOfType("arguments");
                                if (argsList.IsValid())
                                {
                                    foreach (var arg in argsList.Children)
                                    {
                                        if (!arg.IsValid() || arg.Type is "(" or ")" or ",") continue;
                                        if (arg.Text.Contains("schema", StringComparison.OrdinalIgnoreCase))
                                        {
                                            if (AstValueResolver.TryResolveString(arg, projectNode?.Name ?? fileNode?.Path ?? syntaxTree?.FilePath, out var s))
                                            {
                                                AddCandidate(s);
                                            }
                                        }
                                    }
                                }
                            }
                        }

                        var skipNextForTestAttr = false;
                        foreach (var child in node.Children)
                        {
                            if (!child.IsValid()) continue;
                            if (isNonTestFile && child is AnnotationSyntax)
                            {
                                if (child.Text.Contains("test", StringComparison.OrdinalIgnoreCase))
                                {
                                    skipNextForTestAttr = true;
                                }
                                continue;
                            }
                            if (skipNextForTestAttr)
                            {
                                skipNextForTestAttr = false;
                                continue;
                            }
                            WalkAst(child);
                        }
                    }

                    WalkAst(root);
                }
            }
        }
        catch (ObjectDisposedException)
        {
        }

        return results;
    }

    private static string? ScanFileTextForSchema(string filePath, string? sourceText = null)
    {
        try
        {
            var text = sourceText ?? (File.Exists(filePath) ? File.ReadAllText(filePath) : null);
            if (string.IsNullOrEmpty(text)) return null;
            if (!text.Contains("schema", StringComparison.OrdinalIgnoreCase) &&
                !text.Contains("ToTable", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }
            var m1 = SchemaCallRegex.Match(text);
            if (m1.Success)
            {
                var val = m1.Groups[1].Value.Trim().Trim('\'', '"');
                if (ConstantRegistry.TryResolve(filePath, val, out var resolved) && IsValidSchemaName(resolved)) return resolved;
                if (IsValidSchemaName(val)) return val;
            }

            var m2 = SchemaConstRegex.Match(text);
            if (m2.Success)
            {
                var val = m2.Groups[1].Value.Trim().Trim('\'', '"');
                if (ConstantRegistry.TryResolve(filePath, val, out var resolved) && IsValidSchemaName(resolved)) return resolved;
                if (IsValidSchemaName(val)) return val;
            }

            var m3 = TypeOrmSchemaRegex.Match(text);
            if (m3.Success)
            {
                var val = m3.Groups[1].Value.Trim().Trim('\'', '"');
                if (ConstantRegistry.TryResolve(filePath, val, out var resolved) && IsValidSchemaName(resolved)) return resolved;
                if (IsValidSchemaName(val)) return val;
            }

            var m4 = ToTableSchemaRegex.Match(text);
            if (m4.Success)
            {
                var val = m4.Groups[1].Value.Trim().Trim('\'', '"');
                if (ConstantRegistry.TryResolve(filePath, val, out var resolved) && IsValidSchemaName(resolved)) return resolved;
                if (val.Contains('.'))
                {
                    var memberName = val.Split('.').Last();
                    if (ConstantRegistry.TryResolve(filePath, memberName, out var resMember) && IsValidSchemaName(resMember)) return resMember;

                    if (memberName.Equals("DefaultSchema", StringComparison.OrdinalIgnoreCase) ||
                        memberName.Equals("SchemaName", StringComparison.OrdinalIgnoreCase) ||
                        memberName.Equals("DefaultSchemaName", StringComparison.OrdinalIgnoreCase))
                    {
                        var typeName = val.Split('.')[0];
                        var resolvedFromFile = FindConstantInProjectOrDirectory(filePath, memberName, typeName);
                        if (!string.IsNullOrEmpty(resolvedFromFile) && IsValidSchemaName(resolvedFromFile))
                        {
                            ConstantRegistry.Register(filePath, val, resolvedFromFile);
                            ConstantRegistry.Register(filePath, memberName, resolvedFromFile);
                            return resolvedFromFile;
                        }
                    }
                }
                if (IsValidSchemaName(val)) return val;
            }

            var m5 = DataSourceSchemaRegex.Match(text);
            if (m5.Success)
            {
                var val = m5.Groups[1].Value.Trim().Trim('\'', '"');
                if (!val.Equals("true", StringComparison.OrdinalIgnoreCase) && !val.Equals("false", StringComparison.OrdinalIgnoreCase))
                {
                    if (ConstantRegistry.TryResolve(filePath, val, out var resolved) && IsValidSchemaName(resolved)) return resolved;
                    if (IsValidSchemaName(val)) return val;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
        return null;
    }

    private static string? FindConstantInProjectOrDirectory(string filePath, string memberName, string? typeName = null)
    {
        try
        {
            var rx = new Regex(@"(?:const\s+string\s+|string\s+)?" + Regex.Escape(memberName) + @"\s*=\s*[""']([^""']+)[""']", RegexOptions.IgnoreCase);
            var dir = Path.GetDirectoryName(filePath);
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return null;

            foreach (var f in Directory.EnumerateFiles(dir, "*.cs"))
            {
                var match = rx.Match(File.ReadAllText(f));
                if (match.Success) return match.Groups[1].Value.Trim().Trim('\'', '"');
            }

            var projDir = dir;
            while (projDir != null && !Directory.EnumerateFiles(projDir, "*.csproj").Any())
            {
                var parent = Directory.GetParent(projDir);
                if (parent == null || parent.FullName == projDir) break;
                projDir = parent.FullName;
            }

            if (projDir != null && Directory.Exists(projDir))
            {
                if (!string.IsNullOrWhiteSpace(typeName))
                {
                    var candidateFile = Directory.EnumerateFiles(projDir, $"{typeName}.cs", SearchOption.AllDirectories).FirstOrDefault();
                    if (candidateFile != null && File.Exists(candidateFile))
                    {
                        var match = rx.Match(File.ReadAllText(candidateFile));
                        if (match.Success) return match.Groups[1].Value.Trim().Trim('\'', '"');
                    }
                }

                foreach (var f in Directory.EnumerateFiles(projDir, "*.cs", SearchOption.AllDirectories))
                {
                    var match = rx.Match(File.ReadAllText(f));
                    if (match.Success) return match.Groups[1].Value.Trim().Trim('\'', '"');
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
        return null;
    }

    private static string? FindSchemaInNode(IOntologyNode node)
    {
        if (node.Extensions != null && node.Extensions.TryGetValue("schema", out var s) && !string.IsNullOrWhiteSpace(s))
        {
            if (node.Extensions.TryGetValue("is_implicit_schema", out var isImp) && isImp == "true")
            {
            }
            else
            {
                return s;
            }
        }
        foreach (var child in node.Children)
        {
            var found = FindSchemaInNode(child);
            if (!string.IsNullOrEmpty(found)) return found;
        }
        return null;
    }

    private static string? FindImplicitSchemaInNode(IOntologyNode node)
    {
        if (node.Extensions != null && node.Extensions.TryGetValue("schema", out var s) && !string.IsNullOrWhiteSpace(s))
        {
            if (node.Extensions.TryGetValue("is_implicit_schema", out var isImp) && isImp == "true")
            {
                return s;
            }
        }
        foreach (var child in node.Children)
        {
            var found = FindImplicitSchemaInNode(child);
            if (!string.IsNullOrEmpty(found)) return found;
        }
        return null;
    }

    public static string GetDefaultSchemaForEngine(string engine, string dbType)
    {
        var lower = (engine ?? "").ToLowerInvariant();
        if (lower.Contains("postgres")) return "public";
        if (lower.Contains("sqlserver") || lower.Contains("sql server") || lower.Contains("mssql")) return "dbo";
        if (lower.Contains("sqlite")) return "main";
        if (lower.Contains("redis")) return "cache";
        if (lower.Contains("mongo")) return "default";
        if (lower.Contains("bigquery")) return "defaults";
        if (dbType.Equals("cache", StringComparison.OrdinalIgnoreCase)) return "cache";
        return "public";
    }
}
