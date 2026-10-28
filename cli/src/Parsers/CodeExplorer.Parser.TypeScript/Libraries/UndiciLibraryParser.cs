using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public class UndiciLibraryParser : ISemanticExtension
{
    public string Type => "api";
    public string Name => "undici";
    public string Id => "undici";
    public IReadOnlyList<string> SupportedPatterns => ["undici"];
    public bool IsImplemented => true;
    public LibraryRole LibraryRole => LibraryRole.General;

    private static readonly HashSet<string> UndiciMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "request", "stream", "pipeline", "connect", "fetch"
    };

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (IsUndiciClientCreation(node) || IsUndiciHttpCall(node))
        {
            return OntologyConstants.NodeLabels.ExternalService;
        }
        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx) => ExtractIdentifier(node, ctx, null);

    public string? ExtractIdentifier(Node node, ParsingContext ctx, string? projectName)
    {
        var projectContext = projectName ?? ctx?.WorkspaceId;

        if (IsUndiciClientCreation(node))
        {
            var args = AstHelper.GetCallArguments(node);
            if (args.Count > 0 && args[0].IsValid())
            {
                var resolved = AstHelper.ResolveStringOrTemplate(args[0], projectContext);
                if (!string.IsNullOrEmpty(resolved)) return resolved;
            }
            return "undici-client";
        }

        if (IsUndiciHttpCall(node))
        {
            var url = ExtractUrl(node, projectContext);
            if (!string.IsNullOrEmpty(url))
            {
                return url;
            }
            return null;
        }

        return null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (IsUndiciClientCreation(node) || IsUndiciHttpCall(node))
        {
            var url = IsUndiciClientCreation(node)
                ? AstHelper.ExtractFirstStringArgument(node)
                : ExtractUrl(node, ctx?.WorkspaceId);

            if (!string.IsNullOrEmpty(url))
            {
                var target = Uri.TryCreate(url, UriKind.Absolute, out var uri) && !string.IsNullOrEmpty(uri.Host)
                    ? uri.Host
                    : url.Split('/')[0];
                references.Add(new Reference(scopeSymbolId, target, OntologyConstants.Relationships.Calls));
            }
        }
    }

    private static bool IsUndiciClientCreation(Node node)
    {
        if (!node.Is(TreeSitterSyntax.TypeScript.NewExpression)) return false;

        var constructorNode = node.GetField(TreeSitterSyntax.Fields.Constructor) ??
                              node.Children.FirstOrDefault(c => c.IsValid() && c.Type != "new");
        if (!constructorNode.IsValid()) return false;

        var cName = constructorNode.Text;
        if (cName is "Client" or "Pool" or "Agent" or "undici.Client" or "undici.Pool")
        {
            var args = AstHelper.GetCallArguments(node);
            return args.Count > 0 && args[0].IsValid();
        }

        return false;
    }

    private static bool IsUndiciHttpCall(Node node)
    {
        if (!node.Is(TreeSitterSyntax.TypeScript.CallExpression)) return false;

        var func = node.GetFunctionNode();
        if (!func.IsValid()) return false;

        // 1. Direct identifier call: undici(...)
        if (func.Is(TreeSitterSyntax.TypeScript.Identifier))
        {
            var name = func.Text;
            if (name == "undici") return true;
        }

        // 2. Member expression: undici.request(...), undici.stream(...), client.request(...), pool.request(...)
        if (func.Is(TreeSitterSyntax.TypeScript.MemberExpression))
        {
            var objNode = func.GetField(TreeSitterSyntax.Fields.Object);
            var propNode = func.GetField(TreeSitterSyntax.Fields.Property);

            if (objNode.IsValid() && propNode.IsValid())
            {
                var objText = objNode.Text;
                var propText = propNode.Text;

                if (objText.Equals("undici", StringComparison.OrdinalIgnoreCase) && UndiciMethods.Contains(propText))
                {
                    return true;
                }

                if ((objText.Contains("client", StringComparison.OrdinalIgnoreCase) ||
                     objText.Contains("pool", StringComparison.OrdinalIgnoreCase)) &&
                    UndiciMethods.Contains(propText))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static string? ExtractUrl(Node callNode, string? projectContext)
    {
        var args = AstHelper.GetCallArguments(callNode);
        if (args.Count == 0) return null;

        var firstArg = args[0];
        if (!firstArg.IsValid()) return null;

        if (firstArg.Is(TreeSitterSyntax.TypeScript.Object))
        {
            if (AstHelper.TryGetObjectProperty(firstArg, "origin", out var originNode) ||
                AstHelper.TryGetObjectProperty(firstArg, "url", out originNode))
            {
                var resolvedOrigin = AstHelper.ResolveStringOrTemplate(originNode, projectContext);
                if (AstHelper.TryGetObjectProperty(firstArg, "path", out var pathNode))
                {
                    var resolvedPath = AstHelper.ResolveStringOrTemplate(pathNode, projectContext);
                    return CombineUrlAndPath(resolvedOrigin, resolvedPath);
                }
                return resolvedOrigin;
            }

            if (AstHelper.TryGetObjectProperty(firstArg, "path", out var pNode))
            {
                var resolvedPath = AstHelper.ResolveStringOrTemplate(pNode, projectContext);
                var baseUrl = FindBaseUrlFromReceiver(callNode, projectContext);
                if (!string.IsNullOrEmpty(baseUrl))
                {
                    return CombineUrlAndPath(baseUrl, resolvedPath);
                }
                return resolvedPath;
            }
        }

        return AstHelper.ResolveStringOrTemplate(firstArg, projectContext);
    }

    private static string? FindBaseUrlFromReceiver(Node callNode, string? projectContext)
    {
        var func = callNode.GetFunctionNode();
        if (!func.IsValid() || !func.Is(TreeSitterSyntax.TypeScript.MemberExpression)) return null;

        var objNode = func.GetField(TreeSitterSyntax.Fields.Object);
        if (!objNode.IsValid()) return null;

        var varName = objNode.Text.Trim();
        if (varName.StartWithIC("this."))
        {
            varName = varName[5..].Trim();
        }

        // 1. Direct lexical search in parent statement blocks / functions
        var curr = callNode.Parent;
        for (var i = 0; curr.IsValid() && i < 15; i++, curr = curr.Parent)
        {
            if (curr.IsAny(TreeSitterSyntax.TypeScript.StatementBlock, TreeSitterSyntax.TypeScript.Program, TreeSitterSyntax.TypeScript.ClassBody))
            {
                foreach (var child in curr.Children)
                {
                    var text = child.Text;
                    if (text.Contains(varName) && (text.Contains("Client(") || text.Contains("Pool(")))
                    {
                        var newExprs = child.FindDescendantsOfType(TreeSitterSyntax.TypeScript.NewExpression);
                        foreach (var ne in newExprs)
                        {
                            var newArgs = AstHelper.GetCallArguments(ne);
                            if (newArgs.Count > 0)
                            {
                                var res = AstHelper.ResolveStringOrTemplate(newArgs[0], projectContext);
                                if (!string.IsNullOrEmpty(res)) return res;
                            }
                        }
                    }
                }
            }
        }

        // 2. AstValueResolver fallback
        var declNode = AstValueResolver.FindVariableDeclarationInScope(callNode, varName) ??
                       AstValueResolver.FindClassFieldInitializer(callNode, varName);

        if (declNode.IsValid())
        {
            if (declNode.Is(TreeSitterSyntax.TypeScript.NewExpression) || declNode.Is(TreeSitterSyntax.TypeScript.CallExpression))
            {
                var newArgs = AstHelper.GetCallArguments(declNode);
                if (newArgs.Count > 0)
                {
                    return AstHelper.ResolveStringOrTemplate(newArgs[0], projectContext);
                }
            }
        }

        var varText = AstHelper.FindVariableInitializerInAst(callNode, varName, projectContext);
        if (!string.IsNullOrEmpty(varText))
        {
            return varText;
        }

        return null;
    }

    private static string CombineUrlAndPath(string? baseUrl, string? path)
    {
        if (string.IsNullOrEmpty(baseUrl)) return path ?? "";
        if (string.IsNullOrEmpty(path)) return baseUrl;
        return $"{baseUrl.TrimEnd('/')}/{path.TrimStart('/')}";
    }
}
