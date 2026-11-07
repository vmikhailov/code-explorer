using System.Text.RegularExpressions;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public class RoutingControllersLibraryParser : DeclarativeSemanticExtension
{
    public override string Name => "RoutingControllers";
    public override string Id => "routing-controllers";
    public override SemanticExtensionType Type => SemanticExtensionType.Framework;
    public override IReadOnlyList<string> SupportedPatterns => ["routing-controllers"];
    public override LibraryRole LibraryRole => LibraryRole.WebService;

    private static readonly HashSet<string> ActionDecorators = new(StringComparer.OrdinalIgnoreCase)
    {
        "Get", "Post", "Put", "Delete", "Patch", "Head", "Options", "All"
    };

    private const string ActionDecoratorPattern = "Get|Post|Put|Delete|Patch|Head|Options|All";

    private static readonly NodeSelector _decoratorEntryPointSelector = NodeSelector.New()
        .HasType(TreeSitterSyntax.TypeScript.Decorator)
        .FirstChild
        .HasType(TreeSitterSyntax.TypeScript.CallExpression)
        .GetChildForField(TreeSitterSyntax.Fields.Function)
        .Text(ActionDecoratorPattern);

    public override IReadOnlyDictionary<string, NodeSelector> Selectors => new Dictionary<string, NodeSelector>
    {
        { OntologyConstants.NodeLabels.EntryPoint, _decoratorEntryPointSelector }
    };

    private static readonly IReadOnlyList<CustomAstRule> Rules =
    [
        new CustomAstRule
        {
            NodeTypes = [TreeSitterSyntax.TypeScript.Decorator, TreeSitterSyntax.TypeScript.MethodDefinition],
            MapNodeType = MapNodeTypeCore,
            ExtractIdentifier = ExtractIdentifierStatic,
            CollectReferences = CollectReferencesCore,
            EnrichSymbol = EnrichSymbolCore
        }
    ];

    public override IReadOnlyList<CustomAstRule> CustomAstRules => Rules;

    private static string? MapNodeTypeCore(Node node, ParsingContext ctx)
    {
        if (TryGetDecoratorNameAndCall(node, out var decName, out _) && ActionDecorators.Contains(decName))
        {
            return OntologyConstants.NodeLabels.EntryPoint;
        }
        return null;
    }

    private static string? ExtractIdentifierStatic(Node node, ParsingContext ctx, string? projectName, string? relativePath)
    {
        if (!TryGetDecoratorNameAndCall(node, out var decName, out var callExpr) || !ActionDecorators.Contains(decName))
        {
            return null;
        }

        var method = decName.ToUpperInvariant();
        var projectContext = projectName ?? ctx?.WorkspaceId;
        var routeVal = callExpr != null && callExpr.IsValid()
            ? AstHelper.ExtractFirstStringArgument(callExpr, projectContext) ?? ""
            : "";

        var classPrefix = GetControllerPrefixForNode(node, projectContext);
        var combinedRoute = CombineRoutes(classPrefix ?? "", routeVal);

        if (RouteDictionaryRegistry.TryGetGlobalPrefix(projectContext, out var globalPrefix) && !string.IsNullOrEmpty(globalPrefix))
        {
            combinedRoute = CombineRoutes(globalPrefix, combinedRoute);
        }

        if (string.IsNullOrEmpty(combinedRoute) || combinedRoute == "/")
        {
            combinedRoute = "/";
        }
        else if (!combinedRoute.StartsWith('/'))
        {
            combinedRoute = "/" + combinedRoute;
        }

        return $"{method}:{combinedRoute}";
    }

    private static void CollectReferencesCore(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (node.Is(TreeSitterSyntax.TypeScript.MethodDefinition))
        {
            var decorators = GetPrecedingDecorators(node);
            foreach (var dec in decorators)
            {
                if (TryGetDecoratorNameAndCall(dec, out var decName, out _) && ActionDecorators.Contains(decName))
                {
                    var route = ExtractIdentifierStatic(dec, ctx, null, null);
                    if (!string.IsNullOrEmpty(route))
                    {
                        var colonIdx = route.IndexOf(':');
                        var routeKey = colonIdx > 0
                            ? string.Concat(route.AsSpan(0, colonIdx), " ", route.AsSpan(colonIdx + 1))
                            : route;
                        references.Add(new Reference(scopeSymbolId, routeKey, OntologyConstants.Relationships.Implements));
                    }
                }
            }
        }
    }

    private static void EnrichSymbolCore(Node node, SyntacticSymbol symbol, ParsingContext ctx)
    {
        var decorators = new List<Node>();
        Node? methodNode = null;

        if (node.Is(TreeSitterSyntax.TypeScript.Decorator))
        {
            decorators.Add(node);
            var parent = node.Parent;
            if (parent.IsValid())
            {
                var children = parent.Children.ToList();
                var idx = children.FindIndex(c => c.Id == node.Id);
                if (idx >= 0)
                {
                    for (int i = 0; i < children.Count; i++)
                    {
                        if (children[i].Is(TreeSitterSyntax.TypeScript.Decorator))
                            decorators.Add(children[i]);
                    }

                    for (int i = idx + 1; i < children.Count; i++)
                    {
                        if (children[i].Is(TreeSitterSyntax.TypeScript.MethodDefinition))
                        {
                            methodNode = children[i];
                            break;
                        }
                        if (!children[i].Is(TreeSitterSyntax.TypeScript.Decorator))
                        {
                            break;
                        }
                    }
                }
            }

            if (!methodNode.IsValid())
            {
                var p = node.Parent;
                while (p.IsValid())
                {
                    if (p.Is(TreeSitterSyntax.TypeScript.MethodDefinition))
                    {
                        methodNode = p;
                        break;
                    }
                    p = p.Parent;
                }
            }
        }
        else if (node.Is(TreeSitterSyntax.TypeScript.MethodDefinition))
        {
            methodNode = node;
            decorators.AddRange(GetPrecedingDecorators(node));
            decorators.AddRange(node.FindChildrenOfType(TreeSitterSyntax.TypeScript.Decorator));
        }

        var classDecl = node;
        while (classDecl.IsValid() && !classDecl.IsAny(TreeSitterSyntax.TypeScript.ClassDeclaration, TreeSitterSyntax.TypeScript.ClassExpression))
        {
            classDecl = classDecl.Parent;
        }
        if (classDecl.IsValid())
        {
            decorators.AddRange(classDecl.FindChildrenOfType(TreeSitterSyntax.TypeScript.Decorator));
            decorators.AddRange(GetPrecedingDecorators(classDecl));
            if (classDecl.Parent.Is(TreeSitterSyntax.TypeScript.ExportStatement))
            {
                decorators.AddRange(classDecl.Parent.FindChildrenOfType(TreeSitterSyntax.TypeScript.Decorator));
                decorators.AddRange(GetPrecedingDecorators(classDecl.Parent));
            }
        }

        symbol.Protocol = "REST";

        foreach (var dec in decorators)
        {
            if (TryGetDecoratorNameAndCall(dec, out var decName, out var callExpr))
            {
                if (decName is "Authorized")
                {
                    symbol.IsAnonymous = false;
                    if (callExpr != null && callExpr.IsValid())
                    {
                        var role = AstHelper.ExtractFirstStringArgument(callExpr);
                        if (!string.IsNullOrEmpty(role))
                        {
                            symbol.RequiredRoles = string.IsNullOrEmpty(symbol.RequiredRoles) ? role : $"{symbol.RequiredRoles},{role}";
                        }
                    }
                }
                else if (decName is "HttpCode")
                {
                    if (callExpr != null && callExpr.IsValid())
                    {
                        var code = AstHelper.ExtractFirstStringArgument(callExpr);
                        if (!string.IsNullOrEmpty(code))
                        {
                            symbol.Properties["StatusCode"] = code;
                        }
                    }
                }
            }
        }

        if (methodNode != null && methodNode.IsValid())
        {
            ExtractTypeScriptPayloadSchemas(methodNode, symbol);
        }
    }

    public override void DiscoverDeclarations(string filePath, string content, string? projectName, ParsingContext? ctx)
    {
        if (string.IsNullOrWhiteSpace(content) || !content.Contains("routePrefix")) return;

        var match = Regex.Match(content, @"routePrefix\s*:\s*['""]([^'""]+)['""]");
        if (match.Success)
        {
            var targetProj = projectName;
            if (string.IsNullOrEmpty(targetProj) && ctx != null && !string.IsNullOrEmpty(ctx.AbsoluteWorkspacePath) && Path.IsPathRooted(filePath))
            {
                var rel = Path.GetRelativePath(ctx.AbsoluteWorkspacePath, filePath).Replace('\\', '/');
                var parts = rel.Split('/');
                if (parts.Length > 0 && !string.IsNullOrEmpty(parts[0]) && parts[0] != "src") targetProj = parts[0];
                else targetProj = Path.GetFileName(ctx.AbsoluteWorkspacePath.TrimEnd('/', '\\'));
            }

            var prefix = match.Groups[1].Value.Trim().Trim('\'', '"', '`');
            if (!string.IsNullOrEmpty(prefix))
            {
                RouteDictionaryRegistry.RegisterGlobalPrefix(targetProj, prefix);
            }
        }
    }

    public override void DiscoverDeclarations(Node rootNode, string relativePath, string? projectName, ParsingContext? ctx)
    {
    }

    private static bool TryGetDecoratorNameAndCall(Node decoratorNode, out string decoratorName, out Node? callExpr)
    {
        decoratorName = string.Empty;
        callExpr = null;

        if (!decoratorNode.Is(TreeSitterSyntax.TypeScript.Decorator)) return false;

        var call = decoratorNode.FindChildOfType(TreeSitterSyntax.TypeScript.CallExpression);
        if (call.IsValid())
        {
            callExpr = call;
            var func = call.GetFunctionNode();
            if (func.IsValid())
            {
                decoratorName = func.Text.Trim();
                return true;
            }
        }

        foreach (var child in decoratorNode.Children)
        {
            if (child.Is(TreeSitterSyntax.TypeScript.Identifier))
            {
                decoratorName = child.Text.Trim();
                return true;
            }
        }

        var text = decoratorNode.Text.TrimStart('@').Trim();
        var parenIdx = text.IndexOf('(');
        if (parenIdx >= 0) text = text[..parenIdx].Trim();
        decoratorName = text;
        return !string.IsNullOrEmpty(decoratorName);
    }

    private static string CombineRoutes(string prefix, string route)
    {
        prefix = (prefix ?? "").Trim().Trim('/');
        route = (route ?? "").Trim().Trim('/');
        if (string.IsNullOrEmpty(prefix)) return "/" + route;
        if (string.IsNullOrEmpty(route)) return "/" + prefix;
        if (route.Equals(prefix, StringComparison.OrdinalIgnoreCase) || route.StartWithIC(prefix + "/"))
            return "/" + route;
        return $"/{prefix}/{route}";
    }

    private static string? GetControllerPrefixForNode(Node node, string? contextOrProject = null)
    {
        var classDecl = node;
        while (classDecl.IsValid() && !classDecl.IsAny(TreeSitterSyntax.TypeScript.ClassDeclaration, TreeSitterSyntax.TypeScript.ClassExpression))
        {
            classDecl = classDecl.Parent;
        }
        if (!classDecl.IsValid()) return null;

        var candidates = new List<Node>();
        candidates.AddRange(classDecl.Children);
        candidates.AddRange(GetPrecedingDecorators(classDecl));

        var parent = classDecl.Parent;
        if (parent.IsValid())
        {
            candidates.AddRange(parent.Children);
            candidates.AddRange(GetPrecedingDecorators(parent));
        }

        foreach (var c in candidates)
        {
            if (c.Is(TreeSitterSyntax.TypeScript.Decorator))
            {
                if (TryGetDecoratorNameAndCall(c, out var decName, out var callExpr))
                {
                    if (decName is "JsonController" or "Controller")
                    {
                        if (callExpr != null && callExpr.IsValid())
                        {
                            var prefix = AstHelper.ExtractFirstStringArgument(callExpr, contextOrProject);
                            return !string.IsNullOrEmpty(prefix) ? prefix : "/";
                        }
                        return "/";
                    }
                }
            }
        }

        return null;
    }

    private static List<Node> GetPrecedingDecorators(Node node)
    {
        var result = new List<Node>();
        var parent = node.Parent;
        if (!parent.IsValid()) return result;
        var children = parent.Children;
        var idx = children.ToList().FindIndex(c => c.Id == node.Id);
        if (idx <= 0) return result;

        for (var i = idx - 1; i >= 0; i--)
        {
            var sibling = children[i];
            if (sibling.Is(TreeSitterSyntax.TypeScript.Decorator))
            {
                result.Add(sibling);
            }
            else
            {
                break;
            }
        }
        return result;
    }

    private static void ExtractTypeScriptPayloadSchemas(Node methodNode, SyntacticSymbol symbol)
    {
        var typeAnnot = methodNode.GetField(TreeSitterSyntax.Fields.Return)
                        ?? methodNode.FindChildOfType(TreeSitterSyntax.TypeScript.TypeAnnotation);
        if (typeAnnot.IsValid())
        {
            var ret = CleanTypeScriptTypeName(typeAnnot.Text.TrimStart(':').Trim());
            if (!string.IsNullOrEmpty(ret) && ret != "void" && ret != "Promise")
            {
                symbol.ResponseType = ret;
            }
        }

        var paramsNode = methodNode.GetField(TreeSitterSyntax.Fields.Parameters);
        if (paramsNode.IsValid())
        {
            foreach (var param in paramsNode.Children)
            {
                if (param.Type.Contains("parameter") || param.Type.Contains("identifier"))
                {
                    var isBody = param.Text.Contains("@Body");
                    var pAnnot = param.FindChildOfType(TreeSitterSyntax.TypeScript.TypeAnnotation);
                    if (pAnnot.IsValid())
                    {
                        var pType = CleanTypeScriptTypeName(pAnnot.Text.TrimStart(':').Trim());
                        if (isBody || (!IsTypeScriptPrimitive(pType) && symbol.RequestType == null))
                        {
                            symbol.RequestType = pType;
                            if (isBody) break;
                        }
                    }
                }
            }
        }
    }

    private static string CleanTypeScriptTypeName(string rawType)
    {
        if (string.IsNullOrWhiteSpace(rawType)) return "";
        var type = rawType.Trim();
        while (true)
        {
            var genericIdx = type.IndexOf('<');
            if (genericIdx > 0 && type.EndsWith('>'))
            {
                var outer = type[..genericIdx].Trim();
                if (outer is "Promise" or "Observable" or "Array" or "Partial" or "Readonly")
                {
                    type = type[(genericIdx + 1)..^1].Trim();
                    continue;
                }
            }
            break;
        }
        if (type.EndsWith("[]"))
        {
            type = type[..^2].Trim();
        }
        return type;
    }

    private static bool IsTypeScriptPrimitive(string type)
    {
        return type is "string" or "number" or "boolean" or "any" or "unknown" or "void" or "null" or "undefined" or "never" or "object" or "Record<string, any>" or "Request" or "Response";
    }
}
