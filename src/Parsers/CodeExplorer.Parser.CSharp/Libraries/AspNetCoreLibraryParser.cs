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
        while (current != null && current.Id != IntPtr.Zero)
        {
            if (current.Type == "interface_declaration") return true;
            if (current.Type is "class_declaration" or "struct_declaration" or "record_declaration") return false;
            current = current.Parent;
        }
        return false;
    }

    private static bool IsRouteAttribute(Node node)
    {
        if (node.Type != "attribute") return false;
        var nameNode = node.Children.FirstOrDefault(c => c.Type == "identifier");
        return nameNode != null && RouteAttributes.Contains(nameNode.Text);
    }

    private static bool IsEndpointInvocation(Node node)
    {
        if (node.Type != "invocation_expression") return false;
        var func = node.GetChildForField("function") ?? (node.Children.Count > 0 ? node.Children[0] : null);
        if (func == null || func.Id == IntPtr.Zero) return false;

        if (func.Type == "member_access_expression")
        {
            var nameNode = func.GetChildForField("name");
            if (nameNode != null && nameNode.Id != IntPtr.Zero)
            {
                var methodName = nameNode.Type == "generic_name"
                    ? nameNode.Children.FirstOrDefault(c => c.Type == "identifier")?.Text
                    : nameNode.Text;
                return methodName != null && EndpointMethods.Contains(methodName);
            }
        }
        return false;
    }

    private static string? ExtractDeclarativeClientRoute(Node node)
    {
        var nameNode = node.Children.FirstOrDefault(c => c.Type == "identifier");
        if (nameNode == null) return null;

        var argList = node.Children.FirstOrDefault(c => c.Type == "attribute_argument_list");
        var routeVal = "/";
        if (argList != null)
        {
            var arg = argList.Children.FirstOrDefault(c => c.Type == "attribute_argument");
            if (arg != null)
            {
                var strNode = arg.Children.FirstOrDefault(c => c.Type.Contains("string"));
                if (strNode != null) routeVal = strNode.Text.Trim('"');
            }
        }

        var current = node.Parent;
        string interfaceName = "api-client";
        while (current != null && current.Id != IntPtr.Zero)
        {
            if (current.Type == "interface_declaration")
            {
                var idNode = current.GetChildForField("name");
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
        var func = node.GetChildForField("function") ?? (node.Children.Count > 0 ? node.Children[0] : null);
        if (func == null || func.Id == IntPtr.Zero) return null;

        var nameNode = func.GetChildForField("name");
        if (nameNode == null || nameNode.Id == IntPtr.Zero) return null;

        string? methodName = null;
        string? typeArg = null;

        if (nameNode.Type == "generic_name")
        {
            methodName = nameNode.Children.FirstOrDefault(c => c.Type == "identifier")?.Text;
            var typeArgs = nameNode.Children.FirstOrDefault(c => c.Type == "type_argument_list");
            if (typeArgs != null)
            {
                var typeId = typeArgs.Children.FirstOrDefault(c => c.Type is "type_identifier" or "identifier");
                typeArg = typeId?.Text;
            }
        }
        else
        {
            methodName = nameNode.Text;
        }

        if (string.IsNullOrEmpty(methodName)) return null;

        var routeVal = "/";
        var argList = node.Children.FirstOrDefault(c => c.Type == "argument_list");
        if (argList != null)
        {
            foreach (var arg in argList.Children.Where(c => c.Type == "argument"))
            {
                var strNode = arg.Children.FirstOrDefault(c => c.Type.Contains("string"));
                if (strNode != null)
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
        var func = invocationNode.GetChildForField("function") ?? (invocationNode.Children.Count > 0 ? invocationNode.Children[0] : null);
        if (func == null || func.Type != "member_access_expression") return null;

        var expr = func.GetChildForField("expression");
        if (expr == null || expr.Id == IntPtr.Zero) return null;

        // 1. Direct chaining: app.MapGroup("/api/v1").MapGet(...)
        if (expr.Type == "invocation_expression")
        {
            var nestedGroup = ExtractGroupRouteFromInvocation(expr);
            if (!string.IsNullOrEmpty(nestedGroup))
            {
                var parentGroup = FindGroupPrefix(expr);
                return !string.IsNullOrEmpty(parentGroup) ? CombineRoutes(parentGroup, nestedGroup) : nestedGroup;
            }
        }

        // 2. Variable reference: group.MapGet(...)
        if (expr.Type == "identifier")
        {
            var varName = expr.Text;
            var groupRoute = TryResolveVariableGroupRoute(invocationNode, varName);
            if (!string.IsNullOrEmpty(groupRoute)) return groupRoute;
        }

        return null;
    }

    private static string? ExtractGroupRouteFromInvocation(Node invocation)
    {
        var func = invocation.GetChildForField("function") ?? (invocation.Children.Count > 0 ? invocation.Children[0] : null);
        if (func != null && func.Type == "member_access_expression")
        {
            var nameNode = func.GetChildForField("name");
            if (nameNode != null && nameNode.Text == "MapGroup")
            {
                var argList = invocation.Children.FirstOrDefault(c => c.Type == "argument_list");
                var firstArg = argList?.Children.FirstOrDefault(c => c.Type == "argument");
                var strNode = firstArg?.Children.FirstOrDefault(c => c.Type.Contains("string"));
                if (strNode != null)
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
        while (current != null && current.Id != IntPtr.Zero)
        {
            if (current.Type is "block" or "method_declaration" or "local_function_statement" or "compilation_unit")
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
            if (child.Type is "local_declaration_statement" or "variable_declaration" or "global_statement")
            {
                var decls = FindNodesOfType(child, "variable_declarator");
                foreach (var decl in decls)
                {
                    var nameNode = decl.GetChildForField("name") ?? decl.Children.FirstOrDefault(c => c.Type is "identifier");
                    if (nameNode.IsValid() && nameNode.Text == targetVar)
                    {
                        var valueNode = decl.GetChildForField("value");
                        if (!valueNode.IsValid())
                        {
                            var eqClause = decl.Children.FirstOrDefault(c => c.Type == "equals_value_clause");
                            if (eqClause != null && eqClause.Children.Count > 1)
                            {
                                valueNode = eqClause.Children[1];
                            }
                            else if (decl.Children.Count >= 3 && decl.Children[1].Text == "=")
                            {
                                valueNode = decl.Children[2];
                            }
                        }

                        if (valueNode.IsValid() && valueNode.Type == "invocation_expression")
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
            if (current.Type == targetType)
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
        var nameNode = node.Children.FirstOrDefault(c => c.Type == "identifier");
        if (nameNode == null) return null;
        var name = nameNode.Text;
        if (!RouteAttributes.Contains(name)) return null;

        var argList = node.Children.FirstOrDefault(c => c.Type == "attribute_argument_list");
        string? explicitRoute = null;
        if (argList != null)
        {
            var arg = argList.Children.FirstOrDefault(c => c.Type == "attribute_argument");
            if (arg != null)
            {
                var strNode = arg.Children.FirstOrDefault(c => c.Type.Contains("string"));
                if (strNode != null) explicitRoute = strNode.Text.Trim('"');
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
        if (attrList == null || attrList.Type != "attribute_list") return $"{method}:{explicitRoute ?? "/"}";

        var parentDecl = attrList.Parent;
        if (parentDecl == null) return $"{method}:{explicitRoute ?? "/"}";

        // Case A: Attribute directly on class/struct/record
        if (parentDecl.Type is "class_declaration" or "struct_declaration" or "record_declaration")
        {
            var classNameNode = parentDecl.GetChildForField("name");
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
        if (parentDecl.Type == "method_declaration")
        {
            var classDecl = parentDecl.Parent;
            while (classDecl != null && classDecl.Id != IntPtr.Zero)
            {
                if (classDecl.Type is "class_declaration" or "struct_declaration" or "record_declaration")
                    break;
                classDecl = classDecl.Parent;
            }

            string className = "";
            if (classDecl != null && classDecl.Id != IntPtr.Zero)
            {
                var classNameNode = classDecl.GetChildForField("name");
                if (classNameNode.IsValid())
                {
                    className = classNameNode.Text;
                    if (className.EndsWith("Controller", StringComparison.OrdinalIgnoreCase))
                    {
                        className = className[..^"Controller".Length];
                    }
                }
            }

            var methodNameNode = parentDecl.GetChildForField("name");
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
        if (attrList == null || attrList.Type != "attribute_list") return null;

        var current = attrList.Parent;
        Node? classDecl = null;

        if (current != null && current.Type is "class_declaration" or "struct_declaration" or "record_declaration")
        {
            classDecl = current;
        }
        else
        {
            while (current != null && current.Id != IntPtr.Zero)
            {
                if (current.Type is "class_declaration" or "struct_declaration" or "record_declaration")
                {
                    classDecl = current;
                    break;
                }
                current = current.Parent;
            }
        }

        if (classDecl == null || classDecl.Id == IntPtr.Zero) return null;

        foreach (var child in classDecl.Children)
        {
            if (child.Type == "attribute_list")
            {
                foreach (var attr in child.Children)
                {
                    if (attr.Type == "attribute")
                    {
                        var nameNode = attr.Children.FirstOrDefault(c => c.Type == "identifier");
                        if (nameNode != null && (nameNode.Text == "Route" || nameNode.Text.StartsWith("Http")))
                        {
                            var argList = attr.Children.FirstOrDefault(c => c.Type == "attribute_argument_list");
                            if (argList != null)
                            {
                                var arg = argList.Children.FirstOrDefault(c => c.Type == "attribute_argument");
                                if (arg != null)
                                {
                                    var strNode = arg.Children.FirstOrDefault(c => c.Type.Contains("string"));
                                    if (strNode != null)
                                    {
                                        var prefix = strNode.Text.Trim('"');
                                        var classNameNode = classDecl.GetChildForField("name");
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
            }
        }

        return null;
    }
}