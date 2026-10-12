using System.Text.RegularExpressions;
using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public class ExpressLibraryParser : ISemanticExtension
{
    public string Name => "Express";
    public string Id => "express";
    public string Type => "framework";
    public IReadOnlyList<string> SupportedPatterns => ["express", "@types/express"];
    public bool IsImplemented => true;
    public LibraryRole LibraryRole => LibraryRole.WebService;

    private static readonly NodeSelector _expressRouteSelector = NodeSelector.New()
        .HasType(TreeSitterSyntax.TypeScript.CallExpression)
        .FunctionNode
        .HasType(TreeSitterSyntax.TypeScript.MemberExpression)
        .HasChild(TreeSitterSyntax.Fields.Object, NodeSelector.New().TextContains("app|router|route|express"))
        .HasChild(TreeSitterSyntax.Fields.Property, NodeSelector.New().Text("get|post|put|delete|patch|options|head|all"));

    private static readonly NodeSelector _expressRouteMethodSelector = NodeSelector.New()
        .FunctionNode
        .GetChildForField(TreeSitterSyntax.Fields.Property);

    public IReadOnlyDictionary<string, NodeSelector> Selectors => new Dictionary<string, NodeSelector>
    {
        { OntologyConstants.NodeLabels.EntryPoint, _expressRouteSelector }
    };

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (_expressRouteSelector.Matches(node)) return OntologyConstants.NodeLabels.EntryPoint;
        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx) => ExtractIdentifier(node, ctx, null, null);

    public string? ExtractIdentifier(Node node, ParsingContext ctx, string? projectName) => ExtractIdentifier(node, ctx, projectName, null);

    public string? ExtractIdentifier(Node node, ParsingContext ctx, string? projectName, string? relativePath)
    {
        if (_expressRouteSelector.Matches(node))
        {
            var prop = _expressRouteMethodSelector.Select(node);
            if (!prop.IsValid()) return null;

            var method = prop.Text.ToUpperInvariant();
            var routeVal = AstHelper.ExtractFirstStringArgument(node) ?? "/";

            if (!string.IsNullOrEmpty(relativePath) && RouteDictionaryRegistry.TryGetMountPrefix(relativePath, out var mountPrefix))
            {
                routeVal = CombineRoutes(mountPrefix, routeVal);
            }

            if (string.IsNullOrEmpty(routeVal)) routeVal = "/";
            if (!routeVal.StartsWith('/'))
            {
                routeVal = "/" + routeVal;
            }

            return $"{method}:{routeVal}";
        }
        return null;
    }

    private static string CombineRoutes(string prefix, string route)
    {
        prefix = (prefix ?? "").Trim('/');
        route = (route ?? "").Trim('/');
        if (string.IsNullOrEmpty(prefix)) return "/" + route;
        if (string.IsNullOrEmpty(route)) return "/" + prefix;
        return $"/{prefix}/{route}";
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx) { }

    public void DiscoverDeclarations(string filePath, string content, string? projectName, ParsingContext? ctx)
    {
        if (string.IsNullOrWhiteSpace(content) || !content.Contains(".use(")) return;

        // 1. Collect imports and requires: varName -> importPath
        var imports = new Dictionary<string, string>(StringComparer.Ordinal);

        // ES import: import foo from './bar' or import { foo } from './bar'
        var esMatches = Regex.Matches(content, @"import\s+(?:([A-Za-z0-9_]+)|\{\s*([A-Za-z0-9_]+)\s*\})\s+from\s+['""]([^'""]+)['""]");
        foreach (Match m in esMatches)
        {
            var varName = !string.IsNullOrEmpty(m.Groups[1].Value) ? m.Groups[1].Value : m.Groups[2].Value;
            var importPath = m.Groups[3].Value;
            if (!string.IsNullOrEmpty(varName) && !string.IsNullOrEmpty(importPath))
            {
                imports[varName] = importPath;
            }
        }

        // CommonJS require: const foo = require('./bar') or const { foo } = require('./bar')
        var cjsMatches = Regex.Matches(content, @"(?:const|let|var)\s+(?:([A-Za-z0-9_]+)|\{\s*([A-Za-z0-9_]+)\s*\})\s*=\s*require\s*\(\s*['""]([^'""]+)['""]\s*\)");
        foreach (Match m in cjsMatches)
        {
            var varName = !string.IsNullOrEmpty(m.Groups[1].Value) ? m.Groups[1].Value : m.Groups[2].Value;
            var importPath = m.Groups[3].Value;
            if (!string.IsNullOrEmpty(varName) && !string.IsNullOrEmpty(importPath))
            {
                imports[varName] = importPath;
            }
        }

        // 2. Discover single-argument mounts: app.use(routerVar) -> default root mount prefix '/'
        var singleUseMatches = Regex.Matches(content, @"\b(?:app|router|server|[A-Za-z0-9_]*(?:app|router|routes?))\s*\.\s*use\s*\(\s*([A-Za-z0-9_]+)\s*\)", RegexOptions.IgnoreCase);
        foreach (Match m in singleUseMatches)
        {
            var routerVar = m.Groups[1].Value.Trim();
            if (imports.TryGetValue(routerVar, out var importPath))
            {
                RegisterRouterMount(filePath, importPath, "/", ctx);
            }
        }

        // 3. Discover app.use('/mountPrefix', routerVar) and router.use('/mountPrefix', routerVar)
        var useMatches = Regex.Matches(content, @"\b(?:app|router|server|[A-Za-z0-9_]*(?:app|router|routes?))\s*\.\s*use\s*\(\s*(['""`][^'""`]+['""`]|[A-Za-z0-9_]+)\s*,\s*([A-Za-z0-9_]+)", RegexOptions.IgnoreCase);
        foreach (Match m in useMatches)
        {
            var rawPrefix = m.Groups[1].Value.Trim();
            var routerVar = m.Groups[2].Value.Trim();

            var mountPrefix = ResolveMountPrefix(rawPrefix, projectName);
            if (string.IsNullOrEmpty(mountPrefix)) continue;

            if (imports.TryGetValue(routerVar, out var importPath))
            {
                RegisterRouterMount(filePath, importPath, mountPrefix, ctx);
            }
        }

        // 4. Inline require: app.use('/mountPrefix', require('./bar'))
        var inlineMatches = Regex.Matches(content, @"\b(?:app|router|server|[A-Za-z0-9_]*(?:app|router|routes?))\s*\.\s*use\s*\(\s*(['""`][^'""`]+['""`]|[A-Za-z0-9_]+)\s*,\s*require\s*\(\s*['""]([^'""]+)['""]\s*\)", RegexOptions.IgnoreCase);
        foreach (Match m in inlineMatches)
        {
            var rawPrefix = m.Groups[1].Value.Trim();
            var importPath = m.Groups[2].Value.Trim();

            var mountPrefix = ResolveMountPrefix(rawPrefix, projectName);
            if (string.IsNullOrEmpty(mountPrefix) || string.IsNullOrEmpty(importPath)) continue;

            RegisterRouterMount(filePath, importPath, mountPrefix, ctx);
        }
    }

    public void DiscoverDeclarations(Node rootNode, string relativePath, string? projectName, ParsingContext? ctx)
    {
        if (!rootNode.IsValid()) return;
        var imports = new Dictionary<string, string>(StringComparer.Ordinal);
        CollectAstImports(rootNode, imports);
        TraverseForExpressMounts(rootNode, relativePath, projectName, ctx, imports);
    }

    private static void CollectAstImports(Node node, Dictionary<string, string> imports)
    {
        if (node.Is(TreeSitterSyntax.TypeScript.ImportStatement))
        {
            var sourceNode = node.GetField(TreeSitterSyntax.Fields.Source)
                ?? node.Children.LastOrDefault(c => c.IsValid() && (c.Type.Contains("string") || c.Text.StartsWith('\'') || c.Text.StartsWith('"')));
            if (sourceNode.IsValid())
            {
                var src = sourceNode.Text.Trim('\'', '"', '`');
                foreach (var child in node.Children)
                {
                    if (child.Id == sourceNode.Id) continue;
                    FindImportIdentifiers(child, src, imports);
                }
            }
        }
        else if (node.Is(TreeSitterSyntax.TypeScript.VariableDeclarator))
        {
            var nameNode = node.GetField(TreeSitterSyntax.Fields.Name);
            var valueNode = node.GetField(TreeSitterSyntax.Fields.Value);
            if (nameNode.IsValid() && valueNode.IsValid() && valueNode.Is(TreeSitterSyntax.TypeScript.CallExpression))
            {
                var func = valueNode.GetFunctionNode();
                if (func.IsValid() && func.Text == "require")
                {
                    var arg = AstHelper.ExtractFirstStringArgument(valueNode);
                    if (!string.IsNullOrEmpty(arg))
                    {
                        if (nameNode.Is(TreeSitterSyntax.TypeScript.Identifier))
                        {
                            imports[nameNode.Text] = arg;
                        }
                    }
                }
            }
        }

        foreach (var child in node.Children)
        {
            CollectAstImports(child, imports);
        }
    }

    private static void FindImportIdentifiers(Node node, string sourcePath, Dictionary<string, string> imports)
    {
        if (node.Is(TreeSitterSyntax.TypeScript.Identifier))
        {
            var text = node.Text;
            if (text != "import" && text != "from" && text != "as" && text != "type" && text != "default")
            {
                imports[text] = sourcePath;
            }
        }
        foreach (var child in node.Children)
        {
            FindImportIdentifiers(child, sourcePath, imports);
        }
    }

    private static void TraverseForExpressMounts(Node node, string relativePath, string? projectName, ParsingContext? ctx, Dictionary<string, string> imports)
    {
        if (node.Is(TreeSitterSyntax.TypeScript.CallExpression))
        {
            var func = node.GetFunctionNode();
            if (func.IsValid() && func.Is(TreeSitterSyntax.TypeScript.MemberExpression))
            {
                var prop = func.GetChildForField(TreeSitterSyntax.Fields.Property);
                var obj = func.GetChildForField(TreeSitterSyntax.Fields.Object);
                if (prop.IsValid() && prop.Text == "use" && obj.IsValid() && (obj.Text.Contains("app", StringComparison.OrdinalIgnoreCase) || obj.Text.Contains("router", StringComparison.OrdinalIgnoreCase) || obj.Text.Contains("route", StringComparison.OrdinalIgnoreCase)))
                {
                    var args = AstHelper.GetCallArguments(node);
                    if (args.Count >= 2)
                    {
                        var rawPrefix = AstHelper.ResolveStringOrTemplate(args[0], projectName) ?? args[0].Text;
                        var mountPrefix = ResolveMountPrefix(rawPrefix, projectName);
                        if (!string.IsNullOrEmpty(mountPrefix))
                        {
                            var arg1 = args[1];
                            string? importPath = null;
                            if (arg1.Is(TreeSitterSyntax.TypeScript.Identifier) && imports.TryGetValue(arg1.Text, out var ip))
                            {
                                importPath = ip;
                            }
                            else if (arg1.Is(TreeSitterSyntax.TypeScript.CallExpression))
                            {
                                var subFunc = arg1.GetFunctionNode();
                                if (subFunc.IsValid() && subFunc.Text == "require")
                                {
                                    importPath = AstHelper.ExtractFirstStringArgument(arg1);
                                }
                            }

                            if (!string.IsNullOrEmpty(importPath))
                            {
                                RegisterRouterMount(relativePath, importPath, mountPrefix, ctx);
                            }
                        }
                    }
                    else if (args.Count == 1)
                    {
                        var arg0 = args[0];
                        string? importPath = null;
                        if (arg0.Is(TreeSitterSyntax.TypeScript.Identifier) && imports.TryGetValue(arg0.Text, out var ip))
                        {
                            importPath = ip;
                        }
                        else if (arg0.Is(TreeSitterSyntax.TypeScript.CallExpression))
                        {
                            var subFunc = arg0.GetFunctionNode();
                            if (subFunc.IsValid() && subFunc.Text == "require")
                            {
                                importPath = AstHelper.ExtractFirstStringArgument(arg0);
                            }
                        }

                        if (!string.IsNullOrEmpty(importPath))
                        {
                            RegisterRouterMount(relativePath, importPath, "/", ctx);
                        }
                    }
                }
            }
        }

        foreach (var child in node.Children)
        {
            TraverseForExpressMounts(child, relativePath, projectName, ctx, imports);
        }
    }

    private static void RegisterRouterMount(string currentFilePath, string importPath, string mountPrefix, ParsingContext? ctx)
    {
        if (string.IsNullOrWhiteSpace(importPath) || string.IsNullOrWhiteSpace(mountPrefix)) return;

        if (RouteDictionaryRegistry.TryGetMountPrefix(currentFilePath, out var existingPrefix))
        {
            mountPrefix = CombineRoutes(existingPrefix, mountPrefix);
        }

        var dir = Path.GetDirectoryName(currentFilePath) ?? "";
        var combined = NormalizePath(Path.Combine(dir, importPath));
        var cleanImport = NormalizePath(importPath.TrimStart('.', '/'));

        RouteDictionaryRegistry.RegisterMountPrefix(combined, mountPrefix);
        RouteDictionaryRegistry.RegisterMountPrefix(combined + ".ts", mountPrefix);
        RouteDictionaryRegistry.RegisterMountPrefix(combined + ".js", mountPrefix);
        RouteDictionaryRegistry.RegisterMountPrefix(cleanImport, mountPrefix);
        RouteDictionaryRegistry.RegisterMountPrefix(cleanImport + ".ts", mountPrefix);
        RouteDictionaryRegistry.RegisterMountPrefix(cleanImport + ".js", mountPrefix);

        var fileName = Path.GetFileName(importPath);
        if (!string.IsNullOrEmpty(fileName))
        {
            RouteDictionaryRegistry.RegisterMountPrefix(fileName, mountPrefix);
            RouteDictionaryRegistry.RegisterMountPrefix(fileName + ".ts", mountPrefix);
            RouteDictionaryRegistry.RegisterMountPrefix(fileName + ".js", mountPrefix);
        }

        if (ctx != null && !string.IsNullOrEmpty(ctx.AbsoluteWorkspacePath) && Path.IsPathRooted(combined))
        {
            try
            {
                var relToWs = NormalizePath(Path.GetRelativePath(ctx.AbsoluteWorkspacePath, combined));
                RouteDictionaryRegistry.RegisterMountPrefix(relToWs, mountPrefix);
                RouteDictionaryRegistry.RegisterMountPrefix(relToWs + ".ts", mountPrefix);
                RouteDictionaryRegistry.RegisterMountPrefix(relToWs + ".js", mountPrefix);
            }
            catch { }
        }
    }

    private static string? ResolveMountPrefix(string rawPrefix, string? projectName)
    {
        if (string.IsNullOrWhiteSpace(rawPrefix)) return null;
        var trimmed = rawPrefix.Trim();
        if ((trimmed.StartsWith('\'') && trimmed.EndsWith('\'')) ||
            (trimmed.StartsWith('"') && trimmed.EndsWith('"')) ||
            (trimmed.StartsWith('`') && trimmed.EndsWith('`')))
        {
            return trimmed.Trim('\'', '"', '`').Trim('/');
        }
        if (!string.IsNullOrEmpty(projectName) && ConstantRegistry.TryResolve(projectName, trimmed, out var resolvedConst))
        {
            return resolvedConst.Trim('\'', '"', '`').Trim('/');
        }
        if (ConstantRegistry.TryResolve(null, trimmed, out var globalConst))
        {
            return globalConst.Trim('\'', '"', '`').Trim('/');
        }
        return trimmed.Trim('\'', '"', '`').Trim('/');
    }

    private static string NormalizePath(string path)
    {
        var clean = path.Replace('\\', '/');
        var parts = clean.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var stack = new List<string>();
        foreach (var p in parts)
        {
            if (p == ".") continue;
            if (p == "..")
            {
                if (stack.Count > 0 && stack[^1] != "..") stack.RemoveAt(stack.Count - 1);
                else stack.Add(p);
            }
            else
            {
                stack.Add(p);
            }
        }
        var prefix = clean.StartsWith('/') ? "/" : "";
        return prefix + string.Join('/', stack);
    }
}
