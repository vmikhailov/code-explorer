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
    private static bool HasEntityOrTableDeclaration(FileNode? fileNode)
    {
        if (fileNode == null) return false;
        if (fileNode.HasEntityOrTable == true) return true;
        if (HasEntityOrTableNode(fileNode)) return true;
        return fileNode.TryGetExtension(Layers.Layer3FileSignalEnricher.ExtHasEntityOrTable, out var val) &&
               val == "true";
    }

    internal static IReadOnlyList<(string ClassName, string ContextStem, string? ConnectionKey)> DetectDeclaredDbContextInfos(
        FileNode? fileNode)
    {
        try
        {
            var list = new List<(string ClassName, string ContextStem, string? ConnectionKey)>();
            if (fileNode == null) return list;

            string? baseConnKey = null;
            var bc = fileNode.DbContextBaseConn;
            if (string.IsNullOrWhiteSpace(bc))
            {
                fileNode.TryGetExtension(Layers.Layer3FileSignalEnricher.ExtDbContextBaseConn, out bc);
            }
            if (!string.IsNullOrWhiteSpace(bc))
            {
                baseConnKey = bc.Trim();
            }

            foreach (var typeNode in fileNode.Children.OfType<CodeExplorer.Core.Common.Nodes.Layer3_Syntactic.TypeNode>())
            {
                var isDbCtx = typeNode.References.Any(r =>
                    r.Kind == "INHERITS_FROM" &&
                    (r.TargetName.Equals("DbContext", StringComparison.OrdinalIgnoreCase) ||
                     r.TargetName.Equals("IdentityDbContext", StringComparison.OrdinalIgnoreCase) ||
                     r.TargetName.EndsWith(".DbContext", StringComparison.OrdinalIgnoreCase) ||
                     r.TargetName.EndsWith(".IdentityDbContext", StringComparison.OrdinalIgnoreCase)));
                if (!isDbCtx) continue;

                AddDbContextClass(list, typeNode.Name, baseConnKey);
            }

            if (list.Count == 0)
            {
                var classesRaw = fileNode.DbContextClasses;
                if (string.IsNullOrWhiteSpace(classesRaw))
                {
                    fileNode.TryGetExtension(Layers.Layer3FileSignalEnricher.ExtDbContextClasses, out classesRaw);
                }
                if (!string.IsNullOrWhiteSpace(classesRaw))
                {
                    foreach (var className in classesRaw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    {
                        AddDbContextClass(list, className, baseConnKey);
                    }
                }
            }

            return list;
        }
        catch
        {
            return [];
        }
    }

    private static void AddDbContextClass(
        List<(string ClassName, string ContextStem, string? ConnectionKey)> list,
        string? className,
        string? baseConnKey)
    {
        if (string.IsNullOrWhiteSpace(className) ||
            className.Equals("DbContext", StringComparison.OrdinalIgnoreCase) ||
            className.Equals("IdentityDbContext", StringComparison.OrdinalIgnoreCase))
        {
            return;
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

        void AddCandidate(string? cand, int line = 0)
        {
            if (string.IsNullOrWhiteSpace(cand)) return;
            var clean = cand.Trim();
            if (IsValidSchemaName(clean))
            {
                if (line > 0 && fileNode != null)
                {
                    fileNode.SetExtension($"schema_line:{clean}", line.ToString());
                }
                if (seen.Add(clean))
                {
                    results.Add(clean);
                }
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
                                if (colonIdx < 0 && node.Children.Count >= 2)
                                {
                                    varName = node.Children[0].Text.Trim();
                                    valNode = node.Children[^1];
                                    if (valNode.Type == "literal_element" && valNode.Children.Count > 0) valNode = valNode.Children[0];
                                }
                                else if (colonIdx > 0 && colonIdx + 1 < node.Children.Count)
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
                                    AddCandidate(s, node.StartPosition.Row + 1);
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
                                            AddCandidate(s, child.StartPosition.Row + 1);
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
                                    AddCandidate(s, node.StartPosition.Row + 1);
                                }
                            }

                            if (funcText.EndsWith("::new", StringComparison.OrdinalIgnoreCase) ||
                                funcText.Contains("Client", StringComparison.OrdinalIgnoreCase))
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
                                                AddCandidate(s, node.StartPosition.Row + 1);
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
            // ignored: syntax tree document may already be disposed during concurrent tear-down
        }

        return results;
    }

    private static string? ResolveDeclaredSchemaFromFileExtensions(FileNode? fileNode, string filePath)
    {
        if (fileNode == null) return null;
        var val = fileNode.DeclaredSchemaRaw;
        if (string.IsNullOrWhiteSpace(val))
        {
            fileNode.TryGetExtension(Layers.Layer3FileSignalEnricher.ExtDeclaredSchemaRaw, out val);
        }
        if (string.IsNullOrWhiteSpace(val))
        {
            return null;
        }

        val = val.Trim().Trim('\'', '"');
        if (ConstantRegistry.TryResolve(filePath, val, out var resolved) && IsValidSchemaName(resolved))
        {
            return resolved;
        }

        if (val.Contains('.'))
        {
            var memberName = val.Split('.').Last();
            if (ConstantRegistry.TryResolve(filePath, memberName, out var resMember) && IsValidSchemaName(resMember))
            {
                return resMember;
            }

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

        return IsValidSchemaName(val) ? val : null;
    }

    private static string? FindConstantInProjectOrDirectory(string filePath, string memberName, string? typeName = null)
    {
        if (!string.IsNullOrWhiteSpace(typeName) &&
            ConstantRegistry.TryResolve(filePath, $"{typeName}.{memberName}", out var qualified) &&
            !string.IsNullOrWhiteSpace(qualified))
        {
            return qualified.Trim().Trim('\'', '"');
        }
        if (ConstantRegistry.TryResolve(filePath, memberName, out var simple) &&
            !string.IsNullOrWhiteSpace(simple))
        {
            return simple.Trim().Trim('\'', '"');
        }
        return null;
    }

    private static string? FindSchemaInNode(IOntologyNode node)
    {
        if (node.Extensions != null && node.Extensions.TryGetValue("schema", out var s) && !string.IsNullOrWhiteSpace(s))
        {
            if (node.TryGetExtension("is_implicit_schema", out var isImp) && isImp == "true")
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
            if (node.TryGetExtension("is_implicit_schema", out var isImp) && isImp == "true")
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
