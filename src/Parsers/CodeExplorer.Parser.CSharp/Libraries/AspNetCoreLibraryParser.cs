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
        "Route", "HttpGet", "HttpPost", "HttpPut", "HttpDelete", "HttpPatch", "HttpHead", "HttpOptions",
        "Get", "Post", "Put", "Delete", "Patch", "Head", "Options"
    ];
    private static readonly HashSet<string> EndpointMethods = ["MapHub", "MapGet", "MapPost", "MapPut", "MapDelete", "MapPatch"];

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (IsRouteAttribute(node))
        {
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
            if (IsInsideInterface(node))
            {
                return ExtractDeclarativeClientRoute(node);
            }
            return ExtractRoute(node);
        }
        if (IsEndpointInvocation(node)) return ExtractEndpointIdentifier(node);
        return null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx) { }

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
        return nameNode.IsValid() && RouteAttributes.Contains(nameNode.Text);
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

        // 1. Direct chaining: app.MapGroup("/api/v1").MapGet(...)
        if (expr.Is(TreeSitterSyntax.CSharp.InvocationExpression))
        {
            var nestedGroup = ExtractGroupRouteFromInvocation(expr);
            if (!string.IsNullOrEmpty(nestedGroup))
            {
                var parentGroup = FindGroupPrefix(expr);
                return !string.IsNullOrEmpty(parentGroup) ? CombineRoutes(parentGroup, nestedGroup) : nestedGroup;
            }
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

    private static string? ExtractGroupRouteFromInvocation(Node invocation)
    {
        var func = invocation.GetFunctionNode();
        if (func.IsValid() && func.Is(TreeSitterSyntax.CSharp.MemberAccessExpression))
        {
            var nameNode = func.GetField(TreeSitterSyntax.Fields.Name);
            if (nameNode.IsValid() && nameNode.Text == "MapGroup")
            {
                var argList = invocation.FindChildOfType(TreeSitterSyntax.Common.ArgumentList);
                var firstArg = argList?.FindChildOfType(TreeSitterSyntax.CSharp.Argument);
                var strNode = firstArg?.Children.FirstOrDefault(c => c.Type.Contains("string"));
                if (strNode.IsValid())
                {
                    return strNode.Text.Trim('"');
                }
            }
        }
        return null;
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
                            var directRoute = ExtractGroupRouteFromInvocation(valueNode);
                            if (!string.IsNullOrEmpty(directRoute))
                            {
                                var parentGroup = FindGroupPrefix(valueNode);
                                return !string.IsNullOrEmpty(parentGroup) ? CombineRoutes(parentGroup, directRoute) : directRoute;
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

            var classPrefix = GetControllerRoutePrefix(node);
            string routeVal;

            if (string.IsNullOrEmpty(explicitRoute))
            {
                routeVal = !string.IsNullOrEmpty(classPrefix)
                    ? classPrefix
                    : (!string.IsNullOrEmpty(className) ? "/" + className : "/");
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
                if (nameNode.IsValid() && (nameNode.Text == "Route" || nameNode.Text.StartsWith("Http")))
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