using System.Text.RegularExpressions;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public class NestJsLibraryParser : ISemanticExtension
{
    public string Name => "NestJS";
    public string Id => "nestjs";
    public string Type => "framework";
    public IReadOnlyList<string> SupportedPatterns => ["@nestjs/common", "@nestjs/core", "@nestjs/microservices", "@nestjs/websockets", "@nestjs/graphql"];
    public bool IsImplemented => true;
    public LibraryRole LibraryRole => LibraryRole.WebService;

    private static readonly NodeSelector _decoratorEntryPointSelector = NodeSelector.New()
        .HasType(TreeSitterSyntax.TypeScript.Decorator)
        .FirstChild
        .HasType(TreeSitterSyntax.TypeScript.CallExpression)
        .GetChildForField(TreeSitterSyntax.Fields.Function)
        .Text("Get|Post|Put|Delete|Patch|SubscribeMessage|Query|Mutation|Subscription|GrpcMethod|GrpcStreamMethod");

    private static readonly NodeSelector _decoratorCallFunctionSelector = NodeSelector.New()
        .FirstChild
        .FunctionNode;

    public IReadOnlyDictionary<string, NodeSelector> Selectors => new Dictionary<string, NodeSelector>
    {
        { OntologyConstants.NodeLabels.EntryPoint, _decoratorEntryPointSelector }
    };

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (_decoratorEntryPointSelector.Matches(node) || IsNestFactoryCreate(node)) return OntologyConstants.NodeLabels.EntryPoint;
        return null;
    }

    private static bool IsNestFactoryCreate(Node node)
    {
        if (!node.Is(TreeSitterSyntax.TypeScript.CallExpression)) return false;
        var func = node.GetFunctionNode();
        return func.IsValid() && func.Text.StartsWith("NestFactory.create", StringComparison.Ordinal);
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx) => ExtractIdentifier(node, ctx, null);

    public string? ExtractIdentifier(Node node, ParsingContext ctx, string? projectName)
    {
        if (IsNestFactoryCreate(node))
        {
            return "Nest:Bootstrap";
        }

        if (_decoratorEntryPointSelector.Matches(node))
        {
            var func = _decoratorCallFunctionSelector.Select(node);
            if (!func.IsValid()) return null;

            var projectContext = projectName ?? ctx?.WorkspaceId;
            var name = func.Text;
            var callExpr = node.FindChildOfType(TreeSitterSyntax.TypeScript.CallExpression);
            var routeVal = AstHelper.ExtractFirstStringArgument(callExpr, projectContext) ?? "";

            if (name == "SubscribeMessage") return $"ws:{routeVal.TrimStart('/')}";
            if (name is "Query" or "Mutation" or "Subscription")
            {
                var opType = name.ToUpperInvariant();
                var opName = string.IsNullOrEmpty(routeVal) ? GetMethodNameForNode(node) : routeVal;
                return $"{opType}:{opName}";
            }
            if (name is "GrpcMethod" or "GrpcStreamMethod")
            {
                var args = new List<string>();
                if (callExpr.IsValid())
                {
                    var argsNode = callExpr.FindChildOfType(TreeSitterSyntax.TypeScript.Arguments);
                    if (argsNode.IsValid())
                    {
                        foreach (var arg in argsNode.Children)
                        {
                            if (arg.Type.Contains("string"))
                            {
                                var text = arg.Text.Trim('\'', '"', '`');
                                if (!string.IsNullOrEmpty(text)) args.Add(text);
                            }
                        }
                    }
                }

                string rpcName;
                if (args.Count >= 2)
                {
                    rpcName = $"/{args[0]}/{args[1]}";
                }
                else if (args.Count == 1)
                {
                    rpcName = args[0];
                }
                else
                {
                    rpcName = GetMethodNameForNode(node);
                }
                return $"RPC:{rpcName}";
            }

            var classPrefix = GetControllerPrefixForNode(node, projectContext);
            if (!string.IsNullOrEmpty(classPrefix))
            {
                routeVal = CombineRoutes(classPrefix, routeVal);
            }

            var version = GetVersionForNode(node);
            if (!string.IsNullOrEmpty(version))
            {
                routeVal = CombineRoutes(version, routeVal);
            }

            if (string.IsNullOrEmpty(routeVal)) routeVal = "/";
            if (!routeVal.StartsWith("/"))
            {
                routeVal = "/" + routeVal;
            }

            return $"{name.ToUpperInvariant()}:{routeVal}";
        }
        return null;
    }

    private static string? GetVersionForNode(Node node)
    {
        // 1. Look for @Version on method decorators
        var decorators = new List<Node>();
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
                    {
                        decorators.Add(children[i]);
                    }
                }
            }
        }

        var p = node.Parent;
        while (p.IsValid())
        {
            if (p.Is(TreeSitterSyntax.TypeScript.MethodDefinition))
            {
                decorators.AddRange(GetPrecedingDecorators(p));
                decorators.AddRange(p.FindChildrenOfType(TreeSitterSyntax.TypeScript.Decorator));
                break;
            }
            p = p.Parent;
        }

        // 2. Also look for @Version on class level if not found on method
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

        foreach (var dec in decorators)
        {
            var callFunc = _decoratorCallFunctionSelector.Select(dec);
            var decName = callFunc.IsValid() ? callFunc.Text : dec.Text.TrimStart('@');
            if (decName.Contains('(')) decName = decName[..decName.IndexOf('(')];

            if (decName == "Version")
            {
                var callExpr = dec.FindChildOfType(TreeSitterSyntax.TypeScript.CallExpression);
                if (callExpr.IsValid())
                {
                    var ver = AstHelper.ExtractFirstStringArgument(callExpr);
                    if (string.IsNullOrEmpty(ver))
                    {
                        // Check if argument is an array of versions e.g. ['1', '2']
                        var args = callExpr.FindChildOfType(TreeSitterSyntax.TypeScript.Arguments);
                        var arr = args.FindChildOfType(TreeSitterSyntax.TypeScript.Array);
                        if (arr.IsValid())
                        {
                            foreach (var c in arr.Children)
                            {
                                if (c.Type.Contains("string"))
                                {
                                    ver = c.Text.Trim('\'', '"', '`');
                                    if (!string.IsNullOrEmpty(ver)) break;
                                }
                            }
                        }
                    }

                    if (!string.IsNullOrEmpty(ver))
                    {
                        ver = ver.Trim().Trim('\'', '"', '`');
                        if (!ver.StartWithIC("v"))
                        {
                            ver = "v" + ver;
                        }
                        return ver;
                    }
                }
            }
        }

        return null;
    }

    private static string GetMethodNameForNode(Node node)
    {
        var parent = node.Parent;
        if (parent.IsValid())
        {
            var children = parent.Children.ToList();
            var idx = children.FindIndex(c => c.Id == node.Id);
            if (idx >= 0)
            {
                for (int i = idx + 1; i < children.Count; i++)
                {
                    if (children[i].Is(TreeSitterSyntax.TypeScript.MethodDefinition))
                    {
                        var nameNode = children[i].GetField(TreeSitterSyntax.Fields.Name)
                                       ?? children[i].FindChildOfType(TreeSitterSyntax.TypeScript.PropertyIdentifier);
                        if (nameNode.IsValid()) return nameNode.Text;
                    }
                    if (!children[i].Is(TreeSitterSyntax.TypeScript.Decorator)) break;
                }
            }
        }

        var p = node.Parent;
        while (p.IsValid())
        {
            if (p.Is(TreeSitterSyntax.TypeScript.MethodDefinition))
            {
                var nameNode = p.GetField(TreeSitterSyntax.Fields.Name)
                               ?? p.FindChildOfType(TreeSitterSyntax.TypeScript.PropertyIdentifier);
                if (nameNode.IsValid()) return nameNode.Text;
            }
            p = p.Parent;
        }
        return "anonymous";
    }

    private static string CombineRoutes(string prefix, string route)
    {
        prefix = (prefix ?? "").Trim('/');
        route = (route ?? "").Trim('/');
        if (string.IsNullOrEmpty(prefix)) return "/" + route;
        if (string.IsNullOrEmpty(route)) return "/" + prefix;
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
                var func = _decoratorCallFunctionSelector.Select(c);
                if (func.IsValid() && func.Text == "Controller")
                {
                    var callExpr = c.FindChildOfType(TreeSitterSyntax.TypeScript.CallExpression);
                    var prefix = AstHelper.ExtractFirstStringArgument(callExpr, contextOrProject);
                    return !string.IsNullOrEmpty(prefix) ? prefix : "/";
                }
            }
        }

        return null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (node.Is(TreeSitterSyntax.TypeScript.MethodDefinition))
        {
            var decorators = GetPrecedingDecorators(node);
            foreach (var dec in decorators)
            {
                if (_decoratorEntryPointSelector.Matches(dec))
                {
                    var route = ExtractIdentifier(dec, ctx);
                    if (!string.IsNullOrEmpty(route))
                    {
                        references.Add(new Reference(scopeSymbolId, route.Replace(":", " "), OntologyConstants.Relationships.Implements));
                    }
                }
            }
        }
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

    public void EnrichSymbol(Node node, SyntacticSymbol symbol, ParsingContext ctx)
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

            if (methodNode == null)
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

        foreach (var dec in decorators)
        {
            var callFunc = _decoratorCallFunctionSelector.Select(dec);
            var decName = callFunc.IsValid() ? callFunc.Text : dec.Text.TrimStart('@');
            if (decName.Contains('(')) decName = decName[..decName.IndexOf('(')];

            if (decName is "Public" or "AllowAnonymous")
            {
                symbol.IsAnonymous = true;
            }
            else if (decName is "Roles")
            {
                var callExpr = dec.FindChildOfType(TreeSitterSyntax.TypeScript.CallExpression);
                if (callExpr.IsValid())
                {
                    var args = callExpr.FindChildOfType(TreeSitterSyntax.TypeScript.Arguments);
                    if (args.IsValid())
                    {
                        foreach (var arg in args.Children)
                        {
                            if (arg.Type.Contains("string"))
                            {
                                var r = arg.Text.Trim('\'', '"', '`');
                                symbol.RequiredRoles = string.IsNullOrEmpty(symbol.RequiredRoles) ? r : $"{symbol.RequiredRoles},{r}";
                            }
                        }
                    }
                }
            }
            else if (decName is "Resolver" or "Query" or "Mutation" or "Subscription")
            {
                symbol.Protocol = "GraphQL";
                symbol.OperationType = decName switch
                {
                    "Mutation" => "Mutation",
                    "Subscription" => "Subscription",
                    _ => "Query"
                };
            }
            else if (decName is "GrpcMethod" or "GrpcStreamMethod")
            {
                symbol.Protocol = "gRPC";
                symbol.OperationType = decName == "GrpcStreamMethod" ? "ServerStreaming" : "Unary";
            }
            else if (decName is "SubscribeMessage")
            {
                symbol.Protocol = "WS";
            }
            else if (decName is "Get" or "Post" or "Put" or "Delete" or "Patch")
            {
                symbol.Protocol = "REST";
            }
            else if (decName is "Controller" && !symbol.Name.StartsWith("ws:"))
            {
                symbol.Protocol = "REST";
            }
        }

        if (methodNode != null && methodNode.IsValid())
        {
            ExtractTypeScriptPayloadSchemas(methodNode, symbol);
        }
    }

    private static void ExtractTypeScriptPayloadSchemas(Node methodNode, SyntacticSymbol symbol)
    {
        // Response Type: find return type annotation
        var typeAnnot = methodNode.GetField(TreeSitterSyntax.Fields.Return)
                        ?? methodNode.FindChildOfType(TreeSitterSyntax.TypeScript.TypeAnnotation);
        if (typeAnnot.IsValid())
        {
            var ret = CleanTypeScriptTypeName(typeAnnot.Text.TrimStart(':').Trim());
            if (!string.IsNullOrEmpty(ret) && ret != "void" && ret != "Promise" && ret != "Observable")
            {
                symbol.ResponseType = ret;
            }
        }

        // Request Type: find @Body() or parameters
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
        return type;
    }

    private static bool IsTypeScriptPrimitive(string type)
    {
        return type is "string" or "number" or "boolean" or "any" or "unknown" or "void" or "null" or "undefined" or "never" or "object" or "Record<string, any>" or "Request" or "Response";
    }

    public void DiscoverDeclarations(string filePath, string content, string? projectName, ParsingContext? ctx)
    {
        if (string.IsNullOrWhiteSpace(content) || !content.Contains("setGlobalPrefix")) return;

        var prefixMatch = Regex.Match(content, @"setGlobalPrefix\s*\(\s*([^,\)]+)");
        if (prefixMatch.Success)
        {
            var targetProj = projectName;
            if (string.IsNullOrEmpty(targetProj) && ctx != null && !string.IsNullOrEmpty(ctx.AbsoluteWorkspacePath) && Path.IsPathRooted(filePath))
            {
                var rel = Path.GetRelativePath(ctx.AbsoluteWorkspacePath, filePath).Replace('\\', '/');
                var parts = rel.Split('/');
                if (parts.Length > 0 && !string.IsNullOrEmpty(parts[0]) && parts[0] != "src") targetProj = parts[0];
                else targetProj = Path.GetFileName(ctx.AbsoluteWorkspacePath.TrimEnd('/', '\\'));
            }

            var prefix = ResolveGlobalPrefix(prefixMatch.Groups[1].Value, content, targetProj);
            if (!string.IsNullOrEmpty(prefix))
            {
                RouteDictionaryRegistry.RegisterGlobalPrefix(targetProj, prefix);
            }
        }
    }

    public void DiscoverDeclarations(Node rootNode, string relativePath, string? projectName, ParsingContext? ctx)
    {
        if (!rootNode.IsValid()) return;
        TraverseForGlobalPrefix(rootNode, projectName);
    }

    private static void TraverseForGlobalPrefix(Node node, string? projectName)
    {
        if (node.Is(TreeSitterSyntax.TypeScript.CallExpression))
        {
            var func = node.GetFunctionNode();
            if (func.IsValid() && func.Text.EndsWith("setGlobalPrefix", StringComparison.OrdinalIgnoreCase))
            {
                var args = AstHelper.GetCallArguments(node);
                var rawArg = args.Count > 0 ? args[0].Text : null;
                var resolved = !string.IsNullOrEmpty(rawArg)
                    ? ResolveGlobalPrefix(rawArg, node.Tree?.RootNode?.Text ?? "", projectName)
                    : AstHelper.ExtractFirstStringArgument(node, projectName);

                if (!string.IsNullOrEmpty(resolved) && !resolved.Contains("${"))
                {
                    RouteDictionaryRegistry.RegisterGlobalPrefix(projectName, resolved);
                    return;
                }
            }
        }

        foreach (var child in node.Children)
        {
            TraverseForGlobalPrefix(child, projectName);
        }
    }

    public static string? ResolveGlobalPrefix(string rawExpr, string fileText, string? projectName)
    {
        if (string.IsNullOrWhiteSpace(rawExpr)) return null;

        var cleaned = rawExpr.Trim().Trim('(', ')', ';').Trim();

        // 1. If wrapped in env var fallback: process.env.API_PREFIX || 'api/v1' or configService.get(...) || 'api/v1'
        if (cleaned.Contains("||") || cleaned.Contains("??"))
        {
            var fallbackParts = cleaned.Split(["||", "??"], StringSplitOptions.TrimEntries);
            for (int i = fallbackParts.Length - 1; i >= 0; i--)
            {
                var resolvedPart = ResolveGlobalPrefix(fallbackParts[i], fileText, projectName);
                if (!string.IsNullOrEmpty(resolvedPart) && !resolvedPart.Contains("process.env") && !resolvedPart.Contains("get("))
                {
                    return resolvedPart;
                }
            }
        }

        // 2. If simple string literal: 'api/v1', "api/v1", `api/v1` (without template interpolation)
        if (!cleaned.Contains("${") && (
            (cleaned.StartsWith('\'') && cleaned.EndsWith('\'')) ||
            (cleaned.StartsWith('"') && cleaned.EndsWith('"')) ||
            (cleaned.StartsWith('`') && cleaned.EndsWith('`'))))
        {
            return cleaned.Trim('\'', '"', '`').Trim('/');
        }

        // 3. Scan local variable declarations in fileText (e.g. const apiVersion = 'v1' or process.env.API_VERSION || 'v1')
        var localVars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Pattern A: const|let|var name [: type] = ... 'val'
        var varMatches = Regex.Matches(fileText, @"(?:const|let|var)\s+([A-Za-z0-9_]+)\s*(?::\s*[^=]+)?\s*=\s*(?:.*?(?:\|\||\?\?)\s*)?['""`]([^'""`\r\n]+)['""`]");
        foreach (Match m in varMatches)
        {
            var name = m.Groups[1].Value.Trim();
            var val = m.Groups[2].Value.Trim();
            localVars[name] = val;
            if (!string.IsNullOrEmpty(projectName))
            {
                ConstantRegistry.Register(projectName, name, val);
            }
        }

        // Pattern B: { apiVersion = 'v1' } destructuring defaults
        var destructureMatches = Regex.Matches(fileText, @"(?:const|let|var)\s*\{[^}]*?\b([A-Za-z0-9_]+)\s*=\s*['""`]([^'""`\r\n]+)['""`]");
        foreach (Match m in destructureMatches)
        {
            var name = m.Groups[1].Value.Trim();
            var val = m.Groups[2].Value.Trim();
            localVars.TryAdd(name, val);
        }

        // Pattern C: general assignments: apiVersion = 'v1'
        var assignMatches = Regex.Matches(fileText, @"\b([A-Za-z0-9_]+)\s*=\s*(?:.*?(?:\|\||\?\?)\s*)?['""`]([^'""`\r\n]+)['""`]");
        foreach (Match m in assignMatches)
        {
            var name = m.Groups[1].Value.Trim();
            var val = m.Groups[2].Value.Trim();
            localVars.TryAdd(name, val);
        }

        string ResolveVar(string varName)
        {
            if (localVars.TryGetValue(varName, out var lv)) return lv;
            if (!string.IsNullOrEmpty(projectName) && ConstantRegistry.TryResolve(projectName, varName, out var cv)) return cv;
            if (ConstantRegistry.TryResolve(null, varName, out var gv)) return gv;
            if (ConfigurationIndex.TryGetConfig(projectName, varName, out var cfgVal)) return cfgVal;
            return varName;
        }

        // 4. Template string with interpolation: `api/${apiVersion}` or `api/${apiVersion}/campaigns`
        if (cleaned.StartsWith('`') && cleaned.EndsWith('`'))
        {
            var templateContent = cleaned[1..^1];
            var resolved = Regex.Replace(templateContent, @"\$\{([A-Za-z0-9_\.]+)\}", m =>
            {
                var expr = m.Groups[1].Value.Trim();
                if (expr.Contains("||") || expr.Contains("??"))
                {
                    var sub = ResolveGlobalPrefix(expr, fileText, projectName);
                    if (!string.IsNullOrEmpty(sub)) return sub;
                }
                var varName = expr.Contains('.') ? expr.Split('.')[^1] : expr;
                var val = ResolveVar(varName);
                if (val != varName) return val;

                if (varName.Equals("apiVersion", StringComparison.OrdinalIgnoreCase) ||
                    varName.Equals("version", StringComparison.OrdinalIgnoreCase) ||
                    varName.Equals("ver", StringComparison.OrdinalIgnoreCase) ||
                    varName.Equals("v", StringComparison.OrdinalIgnoreCase))
                {
                    var verMatch = Regex.Match(fileText, @"['""`](v\d+(?:\.\d+)?|\d+)['""`]");
                    if (verMatch.Success)
                    {
                        var foundVer = verMatch.Groups[1].Value;
                        if (!foundVer.StartsWith('v') && int.TryParse(foundVer, out _)) foundVer = "v" + foundVer;
                        return foundVer;
                    }
                    return "v1";
                }
                return m.Value;
            });

            if (!resolved.Contains("${"))
            {
                return resolved.Trim('/');
            }
        }

        // 5. String concatenation with +: 'api/' + apiVersion
        if (cleaned.Contains('+'))
        {
            var parts = cleaned.Split('+');
            var sb = new System.Text.StringBuilder();
            var allOk = true;
            foreach (var rawPart in parts)
            {
                var p = rawPart.Trim();
                if ((p.StartsWith('\'') && p.EndsWith('\'')) ||
                    (p.StartsWith('"') && p.EndsWith('"')) ||
                    (p.StartsWith('`') && p.EndsWith('`')))
                {
                    sb.Append(p.Trim('\'', '"', '`'));
                }
                else
                {
                    var varName = p.Contains('.') ? p.Split('.')[^1] : p;
                    var val = ResolveVar(varName);
                    if (val != varName)
                    {
                        sb.Append(val);
                    }
                    else if (varName.Equals("apiVersion", StringComparison.OrdinalIgnoreCase) ||
                             varName.Equals("version", StringComparison.OrdinalIgnoreCase) ||
                             varName.Equals("ver", StringComparison.OrdinalIgnoreCase) ||
                             varName.Equals("v", StringComparison.OrdinalIgnoreCase))
                    {
                        sb.Append("v1");
                    }
                    else
                    {
                        allOk = false;
                        break;
                    }
                }
            }
            if (allOk && sb.Length > 0)
            {
                return sb.ToString().Trim('/');
            }
        }

        // 6. Direct identifier: apiVersion or API_PREFIX
        var directId = cleaned.Contains('.') ? cleaned.Split('.')[^1] : cleaned;
        if (Regex.IsMatch(directId, @"^[A-Za-z0-9_]+$"))
        {
            var val = ResolveVar(directId);
            if (val != directId)
            {
                return val.Trim('/');
            }
            if (directId.Equals("apiVersion", StringComparison.OrdinalIgnoreCase) ||
                directId.Equals("version", StringComparison.OrdinalIgnoreCase) ||
                directId.Equals("ver", StringComparison.OrdinalIgnoreCase) ||
                directId.Equals("v", StringComparison.OrdinalIgnoreCase))
            {
                return "v1";
            }
        }

        // 7. General template substitution fallback if quotes were stripped or non-standard delimiters were used
        if (cleaned.Contains("${"))
        {
            var generalResolved = Regex.Replace(cleaned.Trim('\'', '"', '`', '\\', '/'), @"\$\{([A-Za-z0-9_\.]+)\}", m =>
            {
                var varName = m.Groups[1].Value.Trim();
                if (varName.Contains('.')) varName = varName.Split('.')[^1];
                var val = ResolveVar(varName);
                if (val != varName) return val;
                if (varName.Equals("apiVersion", StringComparison.OrdinalIgnoreCase) ||
                    varName.Equals("version", StringComparison.OrdinalIgnoreCase) ||
                    varName.Equals("ver", StringComparison.OrdinalIgnoreCase) ||
                    varName.Equals("v", StringComparison.OrdinalIgnoreCase))
                {
                    return "v1";
                }
                return m.Value;
            });
            if (!generalResolved.Contains("${"))
            {
                return generalResolved.Trim('/');
            }
        }

        return null;
    }
}

