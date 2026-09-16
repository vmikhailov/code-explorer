using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.CSharp.Libraries;

public class AspNetCoreLibraryParser : ILibraryParser
{
    public string Name => "ASP.NET Core";
    public string Id => "aspnetcore";
    public string Type => "framework";
    public IReadOnlyList<string> SupportedPatterns => ["Microsoft.AspNetCore", "Microsoft.AspNetCore.Mvc"];
    public bool IsImplemented => true;
    public bool IsBuiltIn => true;

    private static readonly HashSet<string> RouteAttributes =
    [
        "Route", "RoutePrefix", "HttpGet", "HttpPost", "HttpPut", "HttpDelete", "HttpPatch", "HttpHead", "HttpOptions",
        "Get", "Post", "Put", "Delete", "Patch", "Head", "Options"
    ];
    private static readonly HashSet<string> EndpointMethods = ["MapHub", "MapGet", "MapPost", "MapPut", "MapDelete", "MapPatch"];

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (IsRouteAttribute(node))
        {
            if (IsClassLevelAttribute(node))
            {
                return null;
            }
            if (IsInsideInterface(node))
            {
                return OntologyConstants.NodeLabels.ExternalService;
            }
            return OntologyConstants.NodeLabels.EntryPoint;
        }
        if (IsEndpointInvocation(node)) return OntologyConstants.NodeLabels.EntryPoint;
        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx)
    {
        if (IsRouteAttribute(node))
        {
            if (IsClassLevelAttribute(node))
            {
                return null;
            }
            if (IsInsideInterface(node))
            {
                return ExtractDeclarativeClientRoute(node);
            }
            return ExtractRoute(node);
        }
        if (IsEndpointInvocation(node)) return ExtractEndpointIdentifier(node);
        return null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (IsEndpointInvocation(node))
        {
            var argList = node.FindChildOfType(TreeSitterSyntax.Common.ArgumentList);
            if (argList.IsValid())
            {
                var args = argList.FindChildrenOfType(TreeSitterSyntax.CSharp.Argument).ToList();
                Node? handlerArg = args.Count > 1 ? args[1] : (args.Count == 1 ? args[0] : null);
                if (handlerArg != null && handlerArg.IsValid())
                {
                    var expr = handlerArg.GetField(TreeSitterSyntax.Fields.Expression)
                               ?? handlerArg.Children.FirstOrDefault(c => !c.Type.Contains("string"));
                    if (expr.IsValid())
                    {
                        if (expr.Is(TreeSitterSyntax.CSharp.MemberAccessExpression))
                        {
                            var nameChild = expr.GetField(TreeSitterSyntax.Fields.Name);
                            var exprChild = expr.GetField(TreeSitterSyntax.Fields.Expression);
                            var methodName = nameChild.IsValid() ? nameChild.Text : "";
                            var typeName = exprChild.IsValid() ? exprChild.Text : "";
                            if (!string.IsNullOrEmpty(methodName))
                            {
                                references.Add(new Reference(scopeSymbolId, methodName, OntologyConstants.Relationships.Triggers));
                                if (!string.IsNullOrEmpty(typeName))
                                {
                                    references.Add(new Reference(scopeSymbolId, $"{typeName}.{methodName}", OntologyConstants.Relationships.Triggers));
                                }
                            }
                        }
                        else if (expr.Is(TreeSitterSyntax.Common.Identifier))
                        {
                            var handlerName = expr.Text;
                            if (!string.IsNullOrEmpty(handlerName))
                            {
                                references.Add(new Reference(scopeSymbolId, handlerName, OntologyConstants.Relationships.Triggers));
                            }
                        }
                    }
                }
            }
        }
    }

    public void EnrichSymbol(Node node, SyntacticSymbol symbol, ParsingContext ctx)
    {
        if (IsRouteAttribute(node))
        {
            var attrList = node.Parent;
            if (!attrList.IsValid()) return;
            var parentDecl = attrList.Parent;
            if (!parentDecl.IsValid()) return;

            EnrichFromAttributes(parentDecl, symbol);

            if (parentDecl.Is(TreeSitterSyntax.CSharp.MethodDeclaration))
            {
                ExtractPayloadSchemas(parentDecl, symbol);

                var classDecl = parentDecl.Parent;
                while (classDecl.IsValid())
                {
                    if (classDecl.IsAny(TreeSitterSyntax.CSharp.ClassDeclaration, TreeSitterSyntax.CSharp.StructDeclaration, TreeSitterSyntax.CSharp.RecordDeclaration))
                    {
                        EnrichFromAttributes(classDecl, symbol);
                        break;
                    }
                    classDecl = classDecl.Parent;
                }
            }
        }
        else if (IsEndpointInvocation(node))
        {
            EnrichMinimalApiEndpoint(node, symbol);
        }
    }

    private static void EnrichMinimalApiEndpoint(Node invocationNode, SyntacticSymbol symbol)
    {
        var argList = invocationNode.FindChildOfType(TreeSitterSyntax.Common.ArgumentList);
        if (argList.IsValid())
        {
            var args = argList.FindChildrenOfType(TreeSitterSyntax.CSharp.Argument).ToList();
            Node? handlerArg = args.Count > 1 ? args[1] : (args.Count == 1 ? args[0] : null);
            if (handlerArg != null && handlerArg.IsValid())
            {
                var expr = handlerArg.GetField(TreeSitterSyntax.Fields.Expression)
                           ?? handlerArg.Children.FirstOrDefault(c => !c.Type.Contains("string"));
                if (expr.IsValid())
                {
                    if (expr.Is(TreeSitterSyntax.CSharp.MemberAccessExpression))
                    {
                        var nameChild = expr.GetField(TreeSitterSyntax.Fields.Name);
                        var exprChild = expr.GetField(TreeSitterSyntax.Fields.Expression);
                        var methodName = nameChild.IsValid() ? nameChild.Text : "";
                        var typeName = exprChild.IsValid() ? exprChild.Text : "";
                        if (!string.IsNullOrEmpty(methodName))
                        {
                            symbol.References.Add(new Reference(symbol.Name, methodName, OntologyConstants.Relationships.Triggers));
                            if (!string.IsNullOrEmpty(typeName))
                            {
                                symbol.References.Add(new Reference(symbol.Name, $"{typeName}.{methodName}", OntologyConstants.Relationships.Triggers));
                            }
                        }
                    }
                    else if (expr.Is(TreeSitterSyntax.Common.Identifier))
                    {
                        var handlerName = expr.Text;
                        if (!string.IsNullOrEmpty(handlerName))
                        {
                            symbol.References.Add(new Reference(symbol.Name, handlerName, OntologyConstants.Relationships.Triggers));
                        }
                    }
                }
            }
        }

        var current = invocationNode.Parent;
        while (current.IsValid() && current.Is(TreeSitterSyntax.CSharp.InvocationExpression))
        {
            var func = current.GetFunctionNode();
            if (func.IsValid() && func.Is(TreeSitterSyntax.CSharp.MemberAccessExpression))
            {
                var chainedMethod = func.GetField(TreeSitterSyntax.Fields.Name)?.Text;
                if (chainedMethod == "RequireAuthorization")
                {
                    var pArgs = current.FindChildOfType(TreeSitterSyntax.Common.ArgumentList);
                    var firstP = pArgs?.FindChildOfType(TreeSitterSyntax.CSharp.Argument)?.Children.FirstOrDefault(c => c.Type.Contains("string"));
                    if (firstP.IsValid())
                    {
                        var policy = firstP.Text.Trim('"');
                        symbol.Policies = string.IsNullOrEmpty(symbol.Policies) ? policy : $"{symbol.Policies},{policy}";
                    }
                }
                else if (chainedMethod == "AllowAnonymous")
                {
                    symbol.IsAnonymous = true;
                }
                else if (chainedMethod is "Produces" or "ProducesProblem")
                {
                    var typeArgs = current.FindChildOfType(TreeSitterSyntax.CSharp.TypeArgumentList)
                                   ?? func.FindChildOfType(TreeSitterSyntax.CSharp.TypeArgumentList);
                    var tNode = typeArgs?.Children.FirstOrDefault(c => c.IsAny(TreeSitterSyntax.CSharp.TypeIdentifier, TreeSitterSyntax.Common.Identifier));
                    if (tNode.IsValid() && symbol.ResponseType == null && chainedMethod == "Produces")
                    {
                        symbol.ResponseType = tNode.Text;
                    }
                }
            }
            current = current.Parent;
        }
    }

    private static void ExtractPayloadSchemas(Node methodDecl, SyntacticSymbol symbol)
    {
        var typeNode = methodDecl.GetField("returns")
                       ?? methodDecl.GetField(TreeSitterSyntax.Fields.Type)
                       ?? methodDecl.GetField("return_type")
                       ?? methodDecl.Children.FirstOrDefault(c => c.IsAny(TreeSitterSyntax.CSharp.GenericName, TreeSitterSyntax.CSharp.TypeIdentifier, TreeSitterSyntax.CSharp.QualifiedName, "predefined_type", "nullable_type", "generic_name", "type_identifier"));
        if (typeNode.IsValid())
        {
            var returnTypeText = CleanTypeName(typeNode.Text);
            if (!string.IsNullOrEmpty(returnTypeText) && returnTypeText != "void" && returnTypeText != "Task" && returnTypeText != "ValueTask" && returnTypeText != "IActionResult" && returnTypeText != "IResult")
            {
                symbol.ResponseType = returnTypeText;
            }
        }

        var paramList = methodDecl.GetField(TreeSitterSyntax.Fields.Parameters)
                        ?? methodDecl.FindChildOfType(TreeSitterSyntax.CSharp.ParameterList);
        if (paramList.IsValid())
        {
            foreach (var param in paramList.Children)
            {
                if (!param.Is(TreeSitterSyntax.CSharp.Parameter)) continue;

                var isFromBody = false;
                foreach (var child in param.Children)
                {
                    if (child.Is(TreeSitterSyntax.CSharp.AttributeList))
                    {
                        if (child.Text.Contains("FromBody") || child.Text.Contains("FromForm") || child.Text.Contains("FromQuery"))
                        {
                            isFromBody = true;
                            break;
                        }
                    }
                }

                var pType = param.GetField(TreeSitterSyntax.Fields.Type)
                            ?? param.Children.FirstOrDefault(c => c.IsAny(TreeSitterSyntax.CSharp.GenericName, TreeSitterSyntax.CSharp.TypeIdentifier, TreeSitterSyntax.CSharp.QualifiedName, "predefined_type", "nullable_type", "generic_name", "type_identifier"));
                if (pType.IsValid())
                {
                    var pTypeName = CleanTypeName(pType.Text);
                    if (isFromBody || (!IsPrimitiveOrSystemType(pTypeName) && symbol.RequestType == null))
                    {
                        symbol.RequestType = pTypeName;
                        if (isFromBody) break;
                    }
                }
            }
        }
    }

    private static string CleanTypeName(string rawType)
    {
        if (string.IsNullOrWhiteSpace(rawType)) return "";
        var type = rawType.Trim();
        while (true)
        {
            var genericIdx = type.IndexOf('<');
            if (genericIdx > 0 && type.EndsWith('>'))
            {
                var outer = type.Substring(0, genericIdx).Trim();
                if (outer is "Task" or "ValueTask" or "ActionResult" or "Results" or "ResponseEntity" or "Promise" or "Observable" or "CompletableFuture" or "Mono" or "Flux")
                {
                    type = type.Substring(genericIdx + 1, type.Length - genericIdx - 2).Trim();
                    continue;
                }
            }
            break;
        }
        return type;
    }

    private static bool IsPrimitiveOrSystemType(string type)
    {
        return type is "int" or "long" or "string" or "bool" or "double" or "float" or "decimal" or "Guid" or "DateTime" or "DateTimeOffset" or "CancellationToken" or "HttpContext" or "HttpRequest" or "HttpResponse" or "ClaimsPrincipal" or "object";
    }

    private static void EnrichFromAttributes(Node targetDecl, SyntacticSymbol symbol)
    {
        foreach (var child in targetDecl.Children)
        {
            if (child.Is(TreeSitterSyntax.CSharp.AttributeList))
            {
                foreach (var attr in child.Children)
                {
                    if (attr.Is(TreeSitterSyntax.CSharp.Attribute))
                    {
                        var nameNode = attr.GetField(TreeSitterSyntax.Fields.Name)
                                       ?? attr.FindChildOfType(TreeSitterSyntax.Common.Identifier);
                        if (!nameNode.IsValid()) continue;
                        var attrName = nameNode.Text;

                        if (attrName is "AllowAnonymous" or "AllowAnonymousAttribute")
                        {
                            symbol.IsAnonymous = true;
                        }
                        else if (attrName is "Authorize" or "AuthorizeAttribute")
                        {
                            foreach (var attrChild in attr.Children)
                            {
                                if (attrChild.Is(TreeSitterSyntax.CSharp.AttributeArgumentList))
                                {
                                    foreach (var arg in attrChild.Children)
                                    {
                                        if (arg.Is(TreeSitterSyntax.CSharp.AttributeArgument))
                                        {
                                            var argText = arg.Text;
                                            if (argText.StartsWith("Roles", StringComparison.OrdinalIgnoreCase) && argText.Contains('='))
                                            {
                                                var val = ExtractStringFromArg(arg);
                                                if (!string.IsNullOrEmpty(val))
                                                    symbol.RequiredRoles = string.IsNullOrEmpty(symbol.RequiredRoles) ? val : $"{symbol.RequiredRoles},{val}";
                                            }
                                            else if (argText.StartsWith("Policy", StringComparison.OrdinalIgnoreCase) && argText.Contains('='))
                                            {
                                                var val = ExtractStringFromArg(arg);
                                                if (!string.IsNullOrEmpty(val))
                                                    symbol.Policies = string.IsNullOrEmpty(symbol.Policies) ? val : $"{symbol.Policies},{val}";
                                            }
                                            else
                                            {
                                                var val = ExtractStringFromArg(arg);
                                                if (!string.IsNullOrEmpty(val))
                                                    symbol.Policies = string.IsNullOrEmpty(symbol.Policies) ? val : $"{symbol.Policies},{val}";
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        else if (attrName is "Query" or "Mutation" or "Subscription" or "ExtendObjectType")
                        {
                            symbol.Protocol = "GraphQL";
                        }
                        else if (attrName is "GrpcMethod" or "GrpcService")
                        {
                            symbol.Protocol = "gRPC";
                        }
                    }
                }
            }
        }
    }

    private static string? ExtractStringFromArg(Node argNode)
    {
        foreach (var child in argNode.Children)
        {
            if (child.Type.Contains("string"))
            {
                return child.Text.Trim('"');
            }
        }
        var text = argNode.Text;
        if (text.Contains('=')) text = text.Substring(text.IndexOf('=') + 1).Trim();
        return text.Trim('"');
    }

    private static bool IsClassLevelAttribute(Node node)
    {
        var attrList = node.Parent;
        if (!attrList.IsValid() || !attrList.Is(TreeSitterSyntax.CSharp.AttributeList)) return false;
        var parentDecl = attrList.Parent;
        return parentDecl.IsValid() && parentDecl.IsAny(TreeSitterSyntax.CSharp.ClassDeclaration, TreeSitterSyntax.CSharp.StructDeclaration, TreeSitterSyntax.CSharp.RecordDeclaration);
    }

    private static bool IsInsideInterface(Node node)
    {
        var current = node.Parent;
        while (current.IsValid())
        {
            if (current.Is(TreeSitterSyntax.CSharp.InterfaceDeclaration)) return true;
            if (current.IsAny(TreeSitterSyntax.CSharp.ClassDeclaration, TreeSitterSyntax.CSharp.StructDeclaration, TreeSitterSyntax.CSharp.RecordDeclaration)) return false;
            current = current.Parent;
        }
        return false;
    }

    private static bool IsRouteAttribute(Node node)
    {
        if (!node.Is(TreeSitterSyntax.CSharp.Attribute)) return false;
        var nameNode = node.FindChildOfType(TreeSitterSyntax.Common.Identifier);
        return nameNode.IsValid() && (RouteAttributes.Contains(nameNode.Text) || RouteAttributes.Contains(nameNode.Text.Replace("Attribute", "")));
    }

    private static bool IsEndpointInvocation(Node node)
    {
        if (!node.Is(TreeSitterSyntax.CSharp.InvocationExpression)) return false;
        var func = node.GetFunctionNode();
        if (!func.IsValid()) return false;

        if (func.Is(TreeSitterSyntax.CSharp.MemberAccessExpression))
        {
            var nameNode = func.GetField(TreeSitterSyntax.Fields.Name);
            if (nameNode.IsValid())
            {
                var methodName = nameNode.Is(TreeSitterSyntax.CSharp.GenericName)
                    ? nameNode.FindChildOfType(TreeSitterSyntax.Common.Identifier)?.Text
                    : nameNode.Text;
                return methodName != null && EndpointMethods.Contains(methodName);
            }
        }
        return false;
    }

    private static string? ExtractDeclarativeClientRoute(Node node)
    {
        var nameNode = node.FindChildOfType(TreeSitterSyntax.Common.Identifier);
        if (!nameNode.IsValid()) return null;

        var argList = node.FindChildOfType(TreeSitterSyntax.CSharp.AttributeArgumentList);
        var routeVal = "/";
        if (argList.IsValid())
        {
            var arg = argList.FindChildOfType(TreeSitterSyntax.CSharp.AttributeArgument);
            if (arg.IsValid())
            {
                var strNode = arg.Children.FirstOrDefault(c => c.Type.Contains("string"));
                if (strNode.IsValid()) routeVal = strNode.Text.Trim('"');
            }
        }

        var current = node.Parent;
        string interfaceName = "api-client";
        while (current.IsValid())
        {
            if (current.Is(TreeSitterSyntax.CSharp.InterfaceDeclaration))
            {
                var idNode = current.GetField(TreeSitterSyntax.Fields.Name);
                if (idNode.IsValid())
                {
                    interfaceName = idNode.Text;
                    if (interfaceName.StartsWith("I") && interfaceName.Length > 1 && char.IsUpper(interfaceName[1]))
                    {
                        interfaceName = interfaceName[1..];
                    }
                }
                break;
            }
            current = current.Parent;
        }

        routeVal = "/" + routeVal.Trim('/');
        return $"http:{interfaceName}{routeVal}";
    }

    private static string? ExtractEndpointIdentifier(Node node)
    {
        var func = node.GetFunctionNode();
        if (!func.IsValid()) return null;

        var nameNode = func.GetField(TreeSitterSyntax.Fields.Name);
        if (!nameNode.IsValid()) return null;

        string? methodName = null;
        string? typeArg = null;

        if (nameNode.Is(TreeSitterSyntax.CSharp.GenericName))
        {
            methodName = nameNode.FindChildOfType(TreeSitterSyntax.Common.Identifier)?.Text;
            var typeArgs = nameNode.FindChildOfType(TreeSitterSyntax.CSharp.TypeArgumentList);
            if (typeArgs.IsValid())
            {
                var typeId = typeArgs.Children.FirstOrDefault(c => c.IsAny(TreeSitterSyntax.CSharp.TypeIdentifier, TreeSitterSyntax.Common.Identifier));
                typeArg = typeId.IsValid() ? typeId.Text : null;
            }
        }
        else
        {
            methodName = nameNode.Text;
        }

        if (string.IsNullOrEmpty(methodName)) return null;

        var routeVal = "/";
        var argList = node.FindChildOfType(TreeSitterSyntax.Common.ArgumentList);
        if (argList.IsValid())
        {
            foreach (var arg in argList.FindChildrenOfType(TreeSitterSyntax.CSharp.Argument))
            {
                var strNode = arg.Children.FirstOrDefault(c => c.Type.Contains("string"));
                if (strNode.IsValid())
                {
                    routeVal = strNode.Text.Trim('"');
                    break;
                }
            }
        }

        routeVal = "/" + routeVal.Trim('/');

        // Resolve Minimal API route groups: app.MapGroup("/api/v1")
        var groupPrefix = FindGroupPrefix(node);
        if (!string.IsNullOrEmpty(groupPrefix))
        {
            routeVal = CombineRoutes(groupPrefix, routeVal);
        }

        if (methodName == "MapHub")
        {
            return string.IsNullOrEmpty(typeArg) ? $"WS:{routeVal}" : $"WS:{routeVal} ({typeArg})";
        }

        var httpMethod = methodName.Replace("Map", "").ToUpperInvariant();
        return $"{httpMethod}:{routeVal}";
    }

    private static string? FindGroupPrefix(Node invocationNode)
    {
        var func = invocationNode.GetFunctionNode();
        if (!func.IsValid() || !func.Is(TreeSitterSyntax.CSharp.MemberAccessExpression)) return null;

        var expr = func.GetField(TreeSitterSyntax.Fields.Expression);
        if (!expr.IsValid()) return null;

        // 1. Direct chaining: app.MapGroup("/api/v1").WithTags().MapGet(...)
        if (expr.Is(TreeSitterSyntax.CSharp.InvocationExpression))
        {
            return ExtractGroupRouteFromChain(expr);
        }

        // 2. Variable reference: group.MapGet(...)
        if (expr.Is(TreeSitterSyntax.Common.Identifier))
        {
            var varName = expr.Text;
            var groupRoute = TryResolveVariableGroupRoute(invocationNode, varName);
            if (!string.IsNullOrEmpty(groupRoute)) return groupRoute;
        }

        return null;
    }

    private static string? ExtractGroupRouteFromChain(Node invocation)
    {
        var routes = new List<string>();
        var current = invocation;
        while (current.IsValid() && current.Is(TreeSitterSyntax.CSharp.InvocationExpression))
        {
            var func = current.GetFunctionNode();
            if (func.IsValid() && func.Is(TreeSitterSyntax.CSharp.MemberAccessExpression))
            {
                var nameNode = func.GetField(TreeSitterSyntax.Fields.Name);
                if (nameNode.IsValid() && nameNode.Text == "MapGroup")
                {
                    var argList = current.FindChildOfType(TreeSitterSyntax.Common.ArgumentList);
                    var firstArg = argList?.FindChildOfType(TreeSitterSyntax.CSharp.Argument);
                    var strNode = firstArg?.Children.FirstOrDefault(c => c.Type.Contains("string"));
                    if (strNode.IsValid())
                    {
                        var groupRoute = strNode.Text.Trim('"');
                        if (!string.IsNullOrEmpty(groupRoute))
                        {
                            routes.Insert(0, groupRoute);
                        }
                    }
                }
                current = func.GetField(TreeSitterSyntax.Fields.Expression);
                continue;
            }
            break;
        }

        if (current.IsValid() && current.Is(TreeSitterSyntax.Common.Identifier))
        {
            var parentVarRoute = TryResolveVariableGroupRoute(current, current.Text);
            if (!string.IsNullOrEmpty(parentVarRoute))
            {
                routes.Insert(0, parentVarRoute);
            }
        }

        if (routes.Count == 0) return null;
        var combined = routes[0];
        for (var i = 1; i < routes.Count; i++)
        {
            combined = CombineRoutes(combined, routes[i]);
        }
        return combined;
    }

    private static string? TryResolveVariableGroupRoute(Node node, string varName)
    {
        var current = node.Parent;
        while (current.IsValid())
        {
            if (current.IsAny(TreeSitterSyntax.CSharp.Block, TreeSitterSyntax.CSharp.MethodDeclaration, TreeSitterSyntax.CSharp.LocalFunctionStatement, TreeSitterSyntax.CSharp.CompilationUnit))
            {
                var route = FindGroupRouteInScope(current, varName, 0);
                if (!string.IsNullOrEmpty(route)) return route;
            }
            current = current.Parent;
        }
        return null;
    }

    private static string? FindGroupRouteInScope(Node scopeNode, string targetVar, int depth)
    {
        if (depth > 5) return null;

        foreach (var child in scopeNode.Children)
        {
            if (child.IsAny(TreeSitterSyntax.CSharp.LocalDeclarationStatement, TreeSitterSyntax.CSharp.VariableDeclaration, TreeSitterSyntax.CSharp.GlobalStatement))
            {
                var decls = FindNodesOfType(child, TreeSitterSyntax.CSharp.VariableDeclarator);
                foreach (var decl in decls)
                {
                    var nameNode = decl.GetField(TreeSitterSyntax.Fields.Name) ?? decl.FindChildOfType(TreeSitterSyntax.Common.Identifier);
                    if (nameNode.IsValid() && nameNode.Text == targetVar)
                    {
                        var valueNode = decl.GetField(TreeSitterSyntax.Fields.Value);
                        if (!valueNode.IsValid())
                        {
                            var eqClause = decl.FindChildOfType(TreeSitterSyntax.CSharp.EqualsValueClause);
                            if (eqClause.IsValid() && eqClause.Children.Count > 1)
                            {
                                valueNode = eqClause.Children[1];
                            }
                            else if (decl.Children.Count >= 3 && decl.Children[1].Text == "=")
                            {
                                valueNode = decl.Children[2];
                            }
                        }

                        if (valueNode.IsValid() && valueNode.Is(TreeSitterSyntax.CSharp.InvocationExpression))
                        {
                            var directRoute = ExtractGroupRouteFromChain(valueNode);
                            if (!string.IsNullOrEmpty(directRoute))
                            {
                                return directRoute;
                            }
                        }
                    }
                }
            }
        }

        return null;
    }

    private static List<Node> FindNodesOfType(Node root, string targetType)
    {
        var result = new List<Node>();
        void Recurse(Node current)
        {
            if (current.Is(targetType))
            {
                result.Add(current);
                return;
            }
            foreach (var child in current.Children)
            {
                Recurse(child);
            }
        }
        Recurse(root);
        return result;
    }

    public static string? ExtractRoute(Node node)
    {
        var nameNode = node.FindChildOfType(TreeSitterSyntax.Common.Identifier);
        if (!nameNode.IsValid()) return null;
        var name = nameNode.Text;
        if (!RouteAttributes.Contains(name)) return null;

        var argList = node.FindChildOfType(TreeSitterSyntax.CSharp.AttributeArgumentList);
        string? explicitRoute = null;
        if (argList.IsValid())
        {
            var arg = argList.FindChildOfType(TreeSitterSyntax.CSharp.AttributeArgument);
            if (arg.IsValid())
            {
                var strNode = arg.Children.FirstOrDefault(c => c.Type.Contains("string"));
                if (strNode.IsValid()) explicitRoute = strNode.Text.Trim('"');
            }
        }

        var method = name switch
        {
            "Route" => "GET",
            "Get" => "GET",
            "Post" => "POST",
            "Put" => "PUT",
            "Delete" => "DELETE",
            "Patch" => "PATCH",
            "Head" => "HEAD",
            "Options" => "OPTIONS",
            _ => name.Replace("Http", "").ToUpperInvariant()
        };

        var attrList = node.Parent;
        if (!attrList.IsValid() || !attrList.Is(TreeSitterSyntax.CSharp.AttributeList)) return $"{method}:{explicitRoute ?? "/"}";

        var parentDecl = attrList.Parent;
        if (!parentDecl.IsValid()) return $"{method}:{explicitRoute ?? "/"}";

        // Case A: Attribute directly on class/struct/record
        if (parentDecl.IsAny(TreeSitterSyntax.CSharp.ClassDeclaration, TreeSitterSyntax.CSharp.StructDeclaration, TreeSitterSyntax.CSharp.RecordDeclaration))
        {
            var classNameNode = parentDecl.GetField(TreeSitterSyntax.Fields.Name);
            var routeVal = explicitRoute ?? "/";
            if (classNameNode.IsValid())
            {
                var className = classNameNode.Text;
                if (className.EndsWith("Controller", StringComparison.OrdinalIgnoreCase))
                {
                    className = className[..^"Controller".Length];
                }
                routeVal = routeVal.Replace("[controller]", className, StringComparison.OrdinalIgnoreCase);
                if (string.IsNullOrEmpty(routeVal) || routeVal == "/")
                {
                    routeVal = "/" + className;
                }
                else if (!routeVal.StartsWith("/"))
                {
                    routeVal = "/" + routeVal;
                }
            }
            return $"{method}:{routeVal}";
        }

        // Case B: Attribute on method declaration
        if (parentDecl.Is(TreeSitterSyntax.CSharp.MethodDeclaration))
        {
            var classDecl = parentDecl.Parent;
            while (classDecl.IsValid())
            {
                if (classDecl.IsAny(TreeSitterSyntax.CSharp.ClassDeclaration, TreeSitterSyntax.CSharp.StructDeclaration, TreeSitterSyntax.CSharp.RecordDeclaration))
                    break;
                classDecl = classDecl.Parent;
            }

            string className = "";
            if (classDecl.IsValid())
            {
                var classNameNode = classDecl.GetField(TreeSitterSyntax.Fields.Name);
                if (classNameNode.IsValid())
                {
                    className = classNameNode.Text;
                    if (className.EndsWith("Controller", StringComparison.OrdinalIgnoreCase))
                    {
                        className = className[..^"Controller".Length];
                    }
                }
            }

            var methodNameNode = parentDecl.GetField(TreeSitterSyntax.Fields.Name);
            var methodName = methodNameNode.IsValid() ? methodNameNode.Text : "";
            if (methodName.EndsWith("Async", StringComparison.Ordinal) && methodName.Length > 5)
            {
                methodName = methodName[..^"Async".Length];
            }

            var classPrefix = GetControllerRoutePrefix(node);
            string routeVal;

            if (string.IsNullOrEmpty(explicitRoute))
            {
                var baseRoute = !string.IsNullOrEmpty(classPrefix)
                    ? classPrefix
                    : (!string.IsNullOrEmpty(className) ? "/" + className : "/");

                if (!string.IsNullOrEmpty(methodName) &&
                    !methodName.Equals("Get", StringComparison.OrdinalIgnoreCase) &&
                    !methodName.Equals("Post", StringComparison.OrdinalIgnoreCase) &&
                    !methodName.Equals("Put", StringComparison.OrdinalIgnoreCase) &&
                    !methodName.Equals("Delete", StringComparison.OrdinalIgnoreCase) &&
                    !methodName.Equals("Patch", StringComparison.OrdinalIgnoreCase) &&
                    !methodName.Equals("Index", StringComparison.OrdinalIgnoreCase))
                {
                    if (baseRoute.Contains("[action]", StringComparison.OrdinalIgnoreCase))
                    {
                        routeVal = baseRoute;
                    }
                    else
                    {
                        routeVal = CombineRoutes(baseRoute, methodName);
                    }
                }
                else
                {
                    routeVal = baseRoute;
                }
            }
            else if (explicitRoute.StartsWith("/") || explicitRoute.StartsWith("~/"))
            {
                routeVal = "/" + explicitRoute.TrimStart('~', '/');
            }
            else
            {
                routeVal = !string.IsNullOrEmpty(classPrefix)
                    ? CombineRoutes(classPrefix, explicitRoute)
                    : "/" + explicitRoute;
            }

            if (!string.IsNullOrEmpty(className) && routeVal.Contains("[controller]"))
            {
                routeVal = routeVal.Replace("[controller]", className, StringComparison.OrdinalIgnoreCase);
            }
            if (!string.IsNullOrEmpty(methodName) && routeVal.Contains("[action]"))
            {
                routeVal = routeVal.Replace("[action]", methodName, StringComparison.OrdinalIgnoreCase);
            }

            if (string.IsNullOrEmpty(routeVal) || routeVal == "/")
            {
                routeVal = !string.IsNullOrEmpty(className) ? "/" + className : "/";
            }
            else if (!routeVal.StartsWith("/"))
            {
                routeVal = "/" + routeVal;
            }

            return $"{method}:{routeVal}";
        }

        return $"{method}:{explicitRoute ?? "/"}";
    }

    private static string CombineRoutes(string prefix, string route)
    {
        prefix = (prefix ?? "").Trim('/');
        route = (route ?? "").Trim('/');
        if (string.IsNullOrEmpty(prefix)) return "/" + route;
        if (string.IsNullOrEmpty(route)) return "/" + prefix;
        return $"/{prefix}/{route}";
    }

    private static string? GetControllerRoutePrefix(Node attributeNode)
    {
        var attrList = attributeNode.Parent;
        if (!attrList.IsValid() || !attrList.Is(TreeSitterSyntax.CSharp.AttributeList)) return null;

        var current = attrList.Parent;
        Node? classDecl = null;

        if (current.IsValid() && current.IsAny(TreeSitterSyntax.CSharp.ClassDeclaration, TreeSitterSyntax.CSharp.StructDeclaration, TreeSitterSyntax.CSharp.RecordDeclaration))
        {
            classDecl = current;
        }
        else
        {
            while (current.IsValid())
            {
                if (current.IsAny(TreeSitterSyntax.CSharp.ClassDeclaration, TreeSitterSyntax.CSharp.StructDeclaration, TreeSitterSyntax.CSharp.RecordDeclaration))
                {
                    classDecl = current;
                    break;
                }
                current = current.Parent;
            }
        }

        if (!classDecl.IsValid()) return null;

        foreach (var child in classDecl.FindChildrenOfType(TreeSitterSyntax.CSharp.AttributeList))
        {
            foreach (var attr in child.FindChildrenOfType(TreeSitterSyntax.CSharp.Attribute))
            {
                var nameNode = attr.FindChildOfType(TreeSitterSyntax.Common.Identifier);
                if (nameNode.IsValid() && (nameNode.Text is "Route" or "RoutePrefix" || nameNode.Text.StartsWith("Http")))
                {
                    var argList = attr.FindChildOfType(TreeSitterSyntax.CSharp.AttributeArgumentList);
                    if (argList.IsValid())
                    {
                        var arg = argList.FindChildOfType(TreeSitterSyntax.CSharp.AttributeArgument);
                        if (arg.IsValid())
                        {
                            var strNode = arg.Children.FirstOrDefault(c => c.Type.Contains("string"));
                            if (strNode.IsValid())
                            {
                                var prefix = strNode.Text.Trim('"');
                                var classNameNode = classDecl.GetField(TreeSitterSyntax.Fields.Name);
                                if (classNameNode.IsValid() && prefix.Contains("[controller]"))
                                {
                                    var className = classNameNode.Text;
                                    if (className.EndsWith("Controller", StringComparison.OrdinalIgnoreCase))
                                    {
                                        className = className[..^"Controller".Length];
                                    }
                                    prefix = prefix.Replace("[controller]", className, StringComparison.OrdinalIgnoreCase);
                                }
                                return prefix;
                            }
                        }
                    }
                }
            }
        }

        return null;
    }
}