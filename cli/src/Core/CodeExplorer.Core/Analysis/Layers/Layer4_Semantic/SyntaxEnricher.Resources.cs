using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using CodeExplorer.Core.Analysis;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using TsNode = TreeSitter.Node;

namespace CodeExplorer.Core.Parser;

public partial class SyntaxEnricher
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

        if (syntaxTree != null && !string.IsNullOrEmpty(syntaxTree.FilePath) && File.Exists(syntaxTree.FilePath))
        {
            try
            {
                var text = File.ReadAllText(syntaxTree.FilePath);
                if (Regex.IsMatch(text, @"(?::\s*(?:[A-Za-z0-9_.]+\.)?(?:Identity)?DbContext\b|\bI?DbSet\s*<|\b__tablename__\b|@(?:Entity|Table|ViewEntity)\b|\[\s*Table\s*\(|\bdeclarative_base\b|\bmodelBuilder\.Entity\b)", RegexOptions.IgnoreCase))
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
        if (string.IsNullOrEmpty(filePath) || !filePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) || !File.Exists(filePath))
        {
            return [];
        }

        try
        {
            var text = File.ReadAllText(filePath);
            if (!text.Contains("DbContext", StringComparison.Ordinal))
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

        // 2. Walk AST if available
        try
        {
            if (syntaxTree?.Tree != null)
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
                        var type = node.Type;

                        if (isNonTestFile && type == TreeSitterSyntax.Rust.ModItem)
                        {
                            var modName = node.GetField(TreeSitterSyntax.Fields.Name) ??
                                          node.FindChildOfType(TreeSitterSyntax.Rust.Identifier);
                            if (modName.IsValid() &&
                                (modName.Text.Equals("tests", StringComparison.OrdinalIgnoreCase) ||
                                 modName.Text.Equals("test_utils", StringComparison.OrdinalIgnoreCase)))
                            {
                                return;
                            }
                        }

                        // Variable declaration or assignment: let_declaration, const_item, static_item, variable_declarator, short_var_declaration, assignment_statement, keyed_element
                        if (type is TreeSitterSyntax.Rust.LetDeclaration or
                                    TreeSitterSyntax.Rust.ConstItem or
                                    TreeSitterSyntax.Rust.StaticItem or
                                    TreeSitterSyntax.CSharp.VariableDeclarator or
                                    TreeSitterSyntax.TypeScript.VariableDeclarator or
                                    TreeSitterSyntax.Go.ShortVarDeclaration or
                                    "assignment_statement" or
                                    TreeSitterSyntax.Common.AssignmentExpression or
                                    "keyed_element")
                        {
                            string? varName = null;
                            TsNode? valNode = null;

                            if (type == TreeSitterSyntax.Rust.LetDeclaration)
                            {
                                var pat = node.GetField("pattern") ?? node.FindChildOfType(TreeSitterSyntax.Rust.Identifier);
                                if (pat.IsValid()) varName = pat.Text;
                                valNode = node.GetField(TreeSitterSyntax.Fields.Value);
                            }
                            else if (type is TreeSitterSyntax.Rust.ConstItem or TreeSitterSyntax.Rust.StaticItem)
                            {
                                var nameNode = node.GetField(TreeSitterSyntax.Fields.Name) ??
                                               node.Children.FirstOrDefault(c => c.Type == TreeSitterSyntax.Rust.Identifier);
                                if (nameNode.IsValid()) varName = nameNode.Text;
                                valNode = node.GetField(TreeSitterSyntax.Fields.Value);
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
                            else if (type is TreeSitterSyntax.CSharp.VariableDeclarator or TreeSitterSyntax.TypeScript.VariableDeclarator)
                            {
                                var nameNode = node.GetField(TreeSitterSyntax.Fields.Name) ?? node.FindChildOfType(TreeSitterSyntax.Common.Identifier);
                                if (nameNode.IsValid()) varName = nameNode.Text;
                                valNode = node.GetField(TreeSitterSyntax.Fields.Value);
                            }
                            else if (type is TreeSitterSyntax.Go.ShortVarDeclaration or "assignment_statement" or TreeSitterSyntax.Common.AssignmentExpression)
                            {
                                var left = node.GetField(TreeSitterSyntax.Fields.Left);
                                if (left.IsValid()) varName = left.Text;
                                valNode = node.GetField(TreeSitterSyntax.Fields.Right);
                                if (valNode.IsValid() && valNode.Type == TreeSitterSyntax.Go.ExpressionList && valNode.Children.Count > 0)
                                {
                                    valNode = valNode.Children[0];
                                }
                            }
                            else if (type == "keyed_element")
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

                        // Struct field declaration list with #[serde(default = "...")] on a schema field (in service/worker/binary files)
                        if (type == TreeSitterSyntax.Rust.FieldDeclarationList && (projectNode == null || !projectNode.IsLibrary))
                        {
                            TsNode? lastAttr = null;
                            foreach (var child in node.Children)
                            {
                                if (child.Type == TreeSitterSyntax.Rust.AttributeItem)
                                {
                                    lastAttr = child;
                                }
                                else if (child.Type == TreeSitterSyntax.Rust.FieldDeclaration)
                                {
                                    var attr = child.FindChildOfType(TreeSitterSyntax.Rust.AttributeItem);
                                    var effectiveAttr = attr.IsValid() ? attr : lastAttr;
                                    var fNameNode = child.GetField(TreeSitterSyntax.Fields.Name) ?? child.FindChildOfType(TreeSitterSyntax.Rust.FieldIdentifier);
                                    if (effectiveAttr.IsValid() && fNameNode.IsValid() && IsSchemaVarName(fNameNode.Text))
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

                        // Call expression / Invocation expression
                        if (type is TreeSitterSyntax.Common.CallExpression or TreeSitterSyntax.CSharp.InvocationExpression)
                        {
                            var funcNode = node.GetFunctionNode();
                            var funcText = funcNode.IsValid() ? funcNode.Text : "";

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

                            // Check DB client constructor calls: PostgresClient::new, AnonStatsPgClient::new, PgClient::new
                            if (clientConstructors.Any(c => funcText.EndsWith(c, StringComparison.OrdinalIgnoreCase)))
                            {
                                var argsList = node.FindChildOfType(TreeSitterSyntax.Common.ArgumentList) ??
                                               node.FindChildOfType(TreeSitterSyntax.Common.Arguments);
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
                            if (isNonTestFile && child.Type == TreeSitterSyntax.Rust.AttributeItem)
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

    private static string? ScanFileTextForSchema(string filePath)
    {
        try
        {
            var text = File.ReadAllText(filePath);
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

            // 1. Check current directory files
            foreach (var f in Directory.EnumerateFiles(dir, "*.cs"))
            {
                var match = rx.Match(File.ReadAllText(f));
                if (match.Success) return match.Groups[1].Value.Trim().Trim('\'', '"');
            }

            // 2. Find project root directory and search files (e.g. typeName.cs or all .cs files)
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
                // Skip implicit default schema
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

    private static readonly ConcurrentDictionary<string, (string? Engine, string? DbType)> _projectDriverCache = new(StringComparer.OrdinalIgnoreCase);

    private static (string? Engine, string? DbType) DetectProjectDatabaseDriver(ProjectNode projectNode, ParsingContext ctx)
    {
        if (projectNode == null) return (null, null);
        if (_projectDriverCache.TryGetValue(projectNode.Id, out var cached)) return cached;

        try
        {
            var projectAbsDir = Path.IsPathRooted(projectNode.Path)
                ? projectNode.Path
                : Path.GetFullPath(Path.Combine(ctx.AbsoluteWorkspacePath, projectNode.Path)).Replace('\\', '/');

            if (!Directory.Exists(projectAbsDir))
            {
                _projectDriverCache[projectNode.Id] = (null, null);
                return (null, null);
            }

            // 1. TypeScript / JavaScript: package.json
            var pkgJsonPath = Path.Combine(projectAbsDir, "package.json");
            if (File.Exists(pkgJsonPath))
            {
                var content = File.ReadAllText(pkgJsonPath);
                var lower = content.ToLowerInvariant();
                if (lower.Contains("\"pg\"") || lower.Contains("\"pg-promise\"") || lower.Contains("\"@types/pg\"") || lower.Contains("\"postgres\""))
                {
                    var res = ("PostgreSQL", "relational");
                    _projectDriverCache[projectNode.Id] = res;
                    return res;
                }
                if (lower.Contains("\"mysql\"") || lower.Contains("\"mysql2\""))
                {
                    var res = ("MySQL", "relational");
                    _projectDriverCache[projectNode.Id] = res;
                    return res;
                }
                if (lower.Contains("\"sqlite3\"") || lower.Contains("\"better-sqlite3\""))
                {
                    var res = ("SQLite", "relational");
                    _projectDriverCache[projectNode.Id] = res;
                    return res;
                }
                if (lower.Contains("\"mssql\"") || lower.Contains("\"tedious\""))
                {
                    var res = ("SQL Server", "relational");
                    _projectDriverCache[projectNode.Id] = res;
                    return res;
                }
                if (lower.Contains("\"oracledb\""))
                {
                    var res = ("Oracle", "relational");
                    _projectDriverCache[projectNode.Id] = res;
                    return res;
                }
                if (lower.Contains("\"mongodb\""))
                {
                    var res = ("MongoDB", "document");
                    _projectDriverCache[projectNode.Id] = res;
                    return res;
                }
                if (lower.Contains("\"redis\"") || lower.Contains("\"ioredis\""))
                {
                    var res = ("Redis", "cache");
                    _projectDriverCache[projectNode.Id] = res;
                    return res;
                }
            }

            // Fallback for monorepos: check workspace root package.json
            var rootPkgJson = Path.Combine(ctx.AbsoluteWorkspacePath, "package.json");
            if (File.Exists(rootPkgJson))
            {
                var rootContent = File.ReadAllText(rootPkgJson).ToLowerInvariant();
                if (rootContent.Contains("\"pg\"") || rootContent.Contains("\"pg-promise\"") || rootContent.Contains("\"postgres\""))
                {
                    var res = ("PostgreSQL", "relational");
                    _projectDriverCache[projectNode.Id] = res;
                    return res;
                }
            }

            // 2. C# (.csproj files, transitive ProjectReferences, Directory.Build.props)
            var csprojFiles = Directory.GetFiles(projectAbsDir, "*.csproj", SearchOption.TopDirectoryOnly);
            if (csprojFiles.Length > 0)
            {
                var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var csproj in csprojFiles)
                {
                    var res = DetectDriverFromCsproj(csproj, ctx.AbsoluteWorkspacePath, visited);
                    if (res.Engine != null)
                    {
                        _projectDriverCache[projectNode.Id] = res;
                        return res;
                    }
                }

                var propsRes = DetectDriverFromDirectoryBuildProps(projectAbsDir, ctx.AbsoluteWorkspacePath);
                if (propsRes.Engine != null)
                {
                    _projectDriverCache[projectNode.Id] = propsRes;
                    return propsRes;
                }
            }

            // Also check appsettings*.json in project directory
            var appsettingsFiles = Directory.GetFiles(projectAbsDir, "appsettings*.json", SearchOption.TopDirectoryOnly);
            foreach (var appsetting in appsettingsFiles)
            {
                try
                {
                    var content = File.ReadAllText(appsetting).ToLowerInvariant();
                    if (content.Contains("port=5432") || content.Contains("username=postgres") || content.Contains("npgsql") || content.Contains("database=postgres"))
                    {
                        var res = ("PostgreSQL", "relational");
                        _projectDriverCache[projectNode.Id] = res;
                        return res;
                    }
                    if (content.Contains("initial catalog=") || content.Contains("trusted_connection=") || content.Contains("server=localhost;database="))
                    {
                        var res = ("SQL Server", "relational");
                        _projectDriverCache[projectNode.Id] = res;
                        return res;
                    }
                    if (content.Contains("port=3306") || content.Contains("uid=root") || content.Contains("user id=root"))
                    {
                        var res = ("MySQL", "relational");
                        _projectDriverCache[projectNode.Id] = res;
                        return res;
                    }
                    if (content.Contains("data source=") && (content.Contains(".db") || content.Contains(".sqlite")))
                    {
                        var res = ("SQLite", "relational");
                        _projectDriverCache[projectNode.Id] = res;
                        return res;
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
                {
                }
            }

            // 3. Python (requirements.txt, pyproject.toml)
            var reqTxt = Path.Combine(projectAbsDir, "requirements.txt");
            if (File.Exists(reqTxt))
            {
                var lower = File.ReadAllText(reqTxt).ToLowerInvariant();
                if (lower.Contains("psycopg") || lower.Contains("asyncpg"))
                {
                    var res = ("PostgreSQL", "relational");
                    _projectDriverCache[projectNode.Id] = res;
                    return res;
                }
                if (lower.Contains("pymysql") || lower.Contains("mysql"))
                {
                    var res = ("MySQL", "relational");
                    _projectDriverCache[projectNode.Id] = res;
                    return res;
                }
                if (lower.Contains("sqlite"))
                {
                    var res = ("SQLite", "relational");
                    _projectDriverCache[projectNode.Id] = res;
                    return res;
                }
            }

            // 4. Also scan files in config folder for db.config.ts / data-source.ts / etc.
            var configFiles = WorkspaceFileFilter.EnumerateFiles(projectAbsDir, "*config*.ts")
                .Concat(WorkspaceFileFilter.EnumerateFiles(projectAbsDir, "*datasource*.ts"))
                .Take(10);
            foreach (var cfg in configFiles)
            {
                var text = File.ReadAllText(cfg).ToLowerInvariant();
                if (text.Contains("type: 'postgres'") || text.Contains("type: \"postgres\"") ||
                    text.Contains("dialect: 'postgres'") || text.Contains("dialect: \"postgres\""))
                {
                    var res = ("PostgreSQL", "relational");
                    _projectDriverCache[projectNode.Id] = res;
                    return res;
                }
                if (text.Contains("type: 'mysql'") || text.Contains("type: \"mysql\"") ||
                    text.Contains("dialect: 'mysql'") || text.Contains("dialect: \"mysql\""))
                {
                    var res = ("MySQL", "relational");
                    _projectDriverCache[projectNode.Id] = res;
                    return res;
                }
                if (text.Contains("type: 'sqlite'") || text.Contains("type: \"sqlite\"") ||
                    text.Contains("dialect: 'sqlite'") || text.Contains("dialect: \"sqlite\""))
                {
                    var res = ("SQLite", "relational");
                    _projectDriverCache[projectNode.Id] = res;
                    return res;
                }
            }

            // 5. Rust (Cargo.toml)
            var cargoTomlPath = Path.Combine(projectAbsDir, "Cargo.toml");
            if (File.Exists(cargoTomlPath))
            {
                var lower = File.ReadAllText(cargoTomlPath).ToLowerInvariant();
                if (lower.Contains("sqlx") || lower.Contains("sea-orm") || lower.Contains("diesel") || lower.Contains("tokio-postgres"))
                {
                    if (lower.Contains("postgres") || lower.Contains("pgpool") || lower.Contains("pq"))
                    {
                        var res = ("PostgreSQL", "relational");
                        _projectDriverCache[projectNode.Id] = res;
                        return res;
                    }
                    if (lower.Contains("mysql"))
                    {
                        var res = ("MySQL", "relational");
                        _projectDriverCache[projectNode.Id] = res;
                        return res;
                    }
                    if (lower.Contains("sqlite"))
                    {
                        var res = ("SQLite", "relational");
                        _projectDriverCache[projectNode.Id] = res;
                        return res;
                    }
                    var defRes = ("PostgreSQL", "relational");
                    _projectDriverCache[projectNode.Id] = defRes;
                    return defRes;
                }
            }

            // Fallback for Rust monorepos: check workspace root Cargo.toml
            var rootCargoToml = Path.Combine(ctx.AbsoluteWorkspacePath, "Cargo.toml");
            if (File.Exists(rootCargoToml))
            {
                var rootLower = File.ReadAllText(rootCargoToml).ToLowerInvariant();
                if (rootLower.Contains("sqlx") || rootLower.Contains("sea-orm") || rootLower.Contains("diesel") || rootLower.Contains("tokio-postgres"))
                {
                    if (rootLower.Contains("postgres") || rootLower.Contains("pgpool") || rootLower.Contains("pq"))
                    {
                        var res = ("PostgreSQL", "relational");
                        _projectDriverCache[projectNode.Id] = res;
                        return res;
                    }
                    if (rootLower.Contains("mysql"))
                    {
                        var res = ("MySQL", "relational");
                        _projectDriverCache[projectNode.Id] = res;
                        return res;
                    }
                    if (rootLower.Contains("sqlite"))
                    {
                        var res = ("SQLite", "relational");
                        _projectDriverCache[projectNode.Id] = res;
                        return res;
                    }
                }
            }

            // 6. Check if THIS project has a concrete relational database configured in ResourceRegistry
            if (ctx.ResourceRegistry != null)
            {
                var thisProjRelational = ctx.ResourceRegistry.AllResources
                    .FirstOrDefault(r => string.Equals(r.ProjectId, projectNode.Id, StringComparison.OrdinalIgnoreCase) &&
                                         string.Equals(r.DbType, "relational", StringComparison.OrdinalIgnoreCase) &&
                                         !ResourceReconciliationService.IsGenericConfigKey(r.Engine) &&
                                         !string.Equals(r.Name, "Database", StringComparison.OrdinalIgnoreCase));
                if (thisProjRelational != null && !string.IsNullOrEmpty(thisProjRelational.Engine))
                {
                    var res = (thisProjRelational.Engine, "relational");
                    _projectDriverCache[projectNode.Id] = res;
                    return res;
                }
            }
        }
        catch (Exception ex)
        {
            ctx.LogWarning($"[SyntaxEnricher] Driver detection failed for {projectNode.Id}: {ex.Message}");
        }

        _projectDriverCache[projectNode.Id] = (null, null);
        return (null, null);
    }

    private static (string? Engine, string? DbType) DetectDriverFromCsproj(
        string csprojPath,
        string workspaceRoot,
        HashSet<string> visited,
        int depth = 0)
    {
        if (depth > 5 || string.IsNullOrEmpty(csprojPath)) return (null, null);

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(csprojPath).Replace('\\', '/');
        }
        catch
        {
            return (null, null);
        }

        if (!visited.Add(fullPath) || !File.Exists(fullPath)) return (null, null);

        try
        {
            var content = File.ReadAllText(fullPath);
            var lower = content.ToLowerInvariant();

            if (lower.Contains("npgsql"))
                return ("PostgreSQL", "relational");
            if (lower.Contains("sqlclient") || lower.Contains("entityframeworkcore.sqlserver"))
                return ("SQL Server", "relational");
            if (lower.Contains("mysql") || lower.Contains("pomelo"))
                return ("MySQL", "relational");
            if (lower.Contains("sqlite"))
                return ("SQLite", "relational");
            if (lower.Contains("oracle"))
                return ("Oracle", "relational");
            if (lower.Contains("include=\"entityframework\"") || lower.Contains("include='entityframework'") ||
                lower.Contains("include=\"entityframework,") || lower.Contains("include=\"system.data.entity\""))
                return ("SQL Server", "relational");

            // Follow ProjectReference
            var dir = Path.GetDirectoryName(fullPath) ?? "";
            var matches = Regex.Matches(content, @"<ProjectReference\s+Include=""([^""]+)""", RegexOptions.IgnoreCase);
            foreach (Match m in matches)
            {
                var relRef = m.Groups[1].Value.Trim().Replace('\\', '/');
                relRef = Regex.Replace(relRef, @"\$\([A-Za-z0-9_]+\)[\\/]*", "");
                var candidate = Path.Combine(dir, relRef);
                var found = DetectDriverFromCsproj(candidate, workspaceRoot, visited, depth + 1);
                if (found.Engine != null) return found;

                if (!File.Exists(candidate) && !string.IsNullOrEmpty(workspaceRoot))
                {
                    var wsCandidate = Path.Combine(workspaceRoot, relRef);
                    found = DetectDriverFromCsproj(wsCandidate, workspaceRoot, visited, depth + 1);
                    if (found.Engine != null) return found;

                    // Fallback: look up by filename in workspace
                    var csprojMap = GetWorkspaceCsprojMap(workspaceRoot);
                    var projFileName = Path.GetFileName(relRef);
                    if (csprojMap.TryGetValue(projFileName, out var mappedPath))
                    {
                        found = DetectDriverFromCsproj(mappedPath, workspaceRoot, visited, depth + 1);
                        if (found.Engine != null) return found;
                    }
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Xml.XmlException)
        {
        }

        return (null, null);
    }

    private static (string? Engine, string? DbType) DetectDriverFromDirectoryBuildProps(string projectAbsDir, string workspaceRoot)
    {
        try
        {
            var curr = new DirectoryInfo(projectAbsDir);
            var root = new DirectoryInfo(workspaceRoot);
            while (curr != null)
            {
                var p1 = Path.Combine(curr.FullName, "Directory.Packages.props");
                var p2 = Path.Combine(curr.FullName, "Directory.Build.props");
                foreach (var p in new[] { p1, p2 })
                {
                    if (File.Exists(p))
                    {
                        var content = File.ReadAllText(p).ToLowerInvariant();
                        if (content.Contains("npgsql")) return ("PostgreSQL", "relational");
                        if (content.Contains("sqlclient") || content.Contains("entityframeworkcore.sqlserver")) return ("SQL Server", "relational");
                        if (content.Contains("mysql") || content.Contains("pomelo")) return ("MySQL", "relational");
                        if (content.Contains("sqlite")) return ("SQLite", "relational");
                        if (content.Contains("oracle")) return ("Oracle", "relational");
                    }
                }
                if (string.Equals(curr.FullName, root.FullName, StringComparison.OrdinalIgnoreCase))
                    break;
                curr = curr.Parent;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }

        return (null, null);
    }

    private static readonly ConcurrentDictionary<string, Dictionary<string, string>> _workspaceCsprojCache = new(StringComparer.OrdinalIgnoreCase);

    private static Dictionary<string, string> GetWorkspaceCsprojMap(string workspaceRoot)
    {
        if (string.IsNullOrEmpty(workspaceRoot) || !Directory.Exists(workspaceRoot))
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        return _workspaceCsprojCache.GetOrAdd(workspaceRoot, root =>
        {
            try
            {
                var files = WorkspaceFileFilter.EnumerateFiles(root, "*.csproj");
                var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var f in files)
                {
                    var name = Path.GetFileName(f);
                    map.TryAdd(name, f.Replace('\\', '/'));
                }
                return map;
            }
            catch
            {
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
        });
    }
}
