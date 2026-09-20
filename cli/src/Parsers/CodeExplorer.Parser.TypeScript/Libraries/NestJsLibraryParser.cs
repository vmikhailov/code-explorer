using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public class NestJsLibraryParser : ILibraryParser
{
    public string Name => "NestJS";
    public string Id => "nestjs";
    public string Type => "framework";
    public IReadOnlyList<string> SupportedPatterns => ["@nestjs/common", "@nestjs/core", "@nestjs/microservices", "@nestjs/websockets", "@nestjs/graphql"];
    public bool IsImplemented => true;

    private static readonly NodeSelector _decoratorEntryPointSelector = NodeSelector.New()
        .HasType("decorator")
        .FirstChild
        .HasType("call_expression")
        .GetChildForField("function")
        .Text("Controller|Get|Post|Put|Delete|Patch|SubscribeMessage|Query|Mutation|Subscription|GrpcMethod|GrpcStreamMethod");

    private static readonly NodeSelector _decoratorCallFunctionSelector = NodeSelector.New()
        .FirstChild
        .FunctionNode;

    public IReadOnlyDictionary<string, NodeSelector> Selectors => new Dictionary<string, NodeSelector>
    {
        { OntologyConstants.NodeLabels.EntryPoint, _decoratorEntryPointSelector }
    };

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (_decoratorEntryPointSelector.Matches(node)) return OntologyConstants.NodeLabels.EntryPoint;
        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx)
    {
        if (_decoratorEntryPointSelector.Matches(node))
        {
            var func = _decoratorCallFunctionSelector.Select(node);
            if (!func.IsValid()) return null;

            var name = func.Text;
            var callExpr = node.FindChildOfType(TreeSitterSyntax.TypeScript.CallExpression);
            var routeVal = AstHelper.ExtractFirstStringArgument(callExpr) ?? "";

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

            if (name != "Controller")
            {
                var classPrefix = GetControllerPrefixForNode(node);
                if (!string.IsNullOrEmpty(classPrefix))
                {
                    routeVal = CombineRoutes(classPrefix, routeVal);
                }
            }

            if (string.IsNullOrEmpty(routeVal)) routeVal = "/";
            if (!routeVal.StartsWith("/"))
            {
                routeVal = "/" + routeVal;
            }

            return $"{(name == "Controller" ? "GET" : name.ToUpperInvariant())}:{routeVal}";
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

    private static string? GetControllerPrefixForNode(Node node)
    {
        var classBody = node.Parent;
        if (!classBody.Is(TreeSitterSyntax.TypeScript.ClassBody)) return null;

        var classDecl = classBody.Parent;
        if (!classDecl.IsAny(TreeSitterSyntax.TypeScript.ClassDeclaration, TreeSitterSyntax.TypeScript.ClassExpression)) return null;

        var candidates = new List<Node>();
        candidates.AddRange(classDecl.Children);

        var parent = classDecl.Parent;
        if (parent.Is(TreeSitterSyntax.TypeScript.ExportStatement))
        {
            candidates.AddRange(parent.Children);
        }

        foreach (var c in candidates)
        {
            if (c.Is(TreeSitterSyntax.TypeScript.Decorator))
            {
                var func = _decoratorCallFunctionSelector.Select(c);
                if (func.IsValid() && func.Text == "Controller")
                {
                    var callExpr = c.FindChildOfType(TreeSitterSyntax.TypeScript.CallExpression);
                    var prefix = AstHelper.ExtractFirstStringArgument(callExpr);
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
            if (classDecl.Parent.Is(TreeSitterSyntax.TypeScript.ExportStatement))
            {
                decorators.AddRange(classDecl.Parent.FindChildrenOfType(TreeSitterSyntax.TypeScript.Decorator));
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
                symbol.Protocol = null;
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
}
