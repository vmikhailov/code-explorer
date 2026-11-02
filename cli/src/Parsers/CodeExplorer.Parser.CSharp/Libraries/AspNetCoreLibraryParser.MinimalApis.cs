using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.CSharp.Libraries;

public partial class AspNetCoreLibraryParser
{
    private static readonly HashSet<string> EndpointMethods = ["MapHub", "MapGet", "MapPost", "MapPut", "MapDelete", "MapPatch"];

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

    private static void CollectEndpointReferences(Node node, string scopeSymbolId, List<Reference> references)
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

    private static void ExtractPayloadSchemas(Node methodDecl, SyntacticSymbol symbol)
    {
        var typeNode = methodDecl.GetField(TreeSitterSyntax.Fields.Returns)
                       ?? methodDecl.GetField(TreeSitterSyntax.Fields.Type)
                       ?? methodDecl.GetField(TreeSitterSyntax.Fields.ReturnType)
                       ?? methodDecl.Children.FirstOrDefault(c => c.IsAny(TreeSitterSyntax.CSharp.GenericName, TreeSitterSyntax.CSharp.TypeIdentifier, TreeSitterSyntax.CSharp.QualifiedName, TreeSitterSyntax.CSharp.PredefinedType, TreeSitterSyntax.CSharp.NullableType));
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
                            ?? param.Children.FirstOrDefault(c => c.IsAny(TreeSitterSyntax.CSharp.GenericName, TreeSitterSyntax.CSharp.TypeIdentifier, TreeSitterSyntax.CSharp.QualifiedName, TreeSitterSyntax.CSharp.PredefinedType, TreeSitterSyntax.CSharp.NullableType));
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
                var outer = type[..genericIdx].Trim();
                if (outer is "Task" or "ValueTask" or "ActionResult" or "Results" or "ResponseEntity" or "Promise" or "Observable" or "CompletableFuture" or "Mono" or "Flux")
                {
                    type = type[(genericIdx + 1)..^1].Trim();
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
                    if (interfaceName.EndsWith("Api", StringComparison.OrdinalIgnoreCase))
                    {
                        interfaceName = interfaceName[..^"Api".Length];
                    }
                    else if (interfaceName.EndsWith("Client", StringComparison.OrdinalIgnoreCase))
                    {
                        interfaceName = interfaceName[..^"Client".Length];
                    }
                    else if (interfaceName.EndsWith("Service", StringComparison.OrdinalIgnoreCase))
                    {
                        interfaceName = interfaceName[..^"Service".Length];
                    }
                }
                break;
            }
            current = current.Parent;
        }

        routeVal = "/" + routeVal.Trim('/');
        var cleanService = interfaceName.ToLowerInvariant();
        return $"http:{cleanService}{routeVal}";
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

    private static string? ExtractGroupRouteFromChain(Node invocation, int depth = 0, HashSet<string>? visited = null)
    {
        if (depth > 8) return null;
        var routes = new List<string>();
        var current = invocation;
        var steps = 0;
        while (current.IsValid() && current.Is(TreeSitterSyntax.CSharp.InvocationExpression) && ++steps <= 32)
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
            var parentVarRoute = TryResolveVariableGroupRoute(current, current.Text, depth + 1, visited);
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

    private static string? TryResolveVariableGroupRoute(Node node, string varName, int depth = 0, HashSet<string>? visited = null)
    {
        if (depth > 8 || string.IsNullOrWhiteSpace(varName)) return null;
        visited ??= new HashSet<string>(StringComparer.Ordinal);
        if (!visited.Add(varName)) return null;

        var current = node.Parent;
        while (current.IsValid())
        {
            if (current.IsAny(TreeSitterSyntax.CSharp.Block, TreeSitterSyntax.CSharp.MethodDeclaration, TreeSitterSyntax.CSharp.LocalFunctionStatement, TreeSitterSyntax.CSharp.CompilationUnit))
            {
                var route = FindGroupRouteInScope(current, varName, depth + 1, visited);
                if (!string.IsNullOrEmpty(route)) return route;
            }
            current = current.Parent;
        }
        return null;
    }

    private static string? FindGroupRouteInScope(Node scopeNode, string targetVar, int depth, HashSet<string>? visited = null)
    {
        if (depth > 8) return null;

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
                            var directRoute = ExtractGroupRouteFromChain(valueNode, depth + 1, visited);
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
        void Recurse(Node current, int depth)
        {
            if (depth > 64) return;
            if (current.Is(targetType))
            {
                result.Add(current);
                return;
            }
            foreach (var child in current.Children)
            {
                Recurse(child, depth + 1);
            }
        }
        Recurse(root, 0);
        return result;
    }
}
