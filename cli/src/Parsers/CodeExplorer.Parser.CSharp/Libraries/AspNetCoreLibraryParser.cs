using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.CSharp.Libraries;

public partial class AspNetCoreLibraryParser : ISemanticExtension
{
    public string Name => "ASP.NET Core";
    public string Id => "aspnetcore";
    public string Type => "framework";
    public IReadOnlyList<string> SupportedPatterns => ["Microsoft.AspNetCore", "Microsoft.AspNetCore.*", "Microsoft.AspNetCore.Mvc", "Microsoft.AspNetCore.Mvc.*"];
    public bool IsImplemented => true;
    public bool IsBuiltIn => true;
    public LibraryRole LibraryRole => LibraryRole.WebService;
    public Func<ProjectContext, bool>? CustomMatch => ctx => ctx.ManifestProperties?.GetValueOrDefault("sdk") == "Microsoft.NET.Sdk.Web";

    public bool MatchesProject(ProjectContext project)
    {
        if (project.ManifestProperties != null &&
            project.ManifestProperties.GetValueOrDefault("sdk") == "Microsoft.NET.Sdk.Web")
        {
            return true;
        }

        if (project.Dependencies != null)
        {
            foreach (var dep in project.Dependencies)
            {
                foreach (var pattern in SupportedPatterns)
                {
                    if (ISemanticExtension.IsLibraryMatch(dep, pattern))
                        return true;
                }
            }
        }

        return false;
    }

    private static readonly HashSet<string> RouteAttributes =
    [
        "Route", "RoutePrefix", "HttpGet", "HttpPost", "HttpPut", "HttpDelete", "HttpPatch", "HttpHead", "HttpOptions",
        "Get", "Post", "Put", "Delete", "Patch", "Head", "Options"
    ];

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
                return RefitLibraryParser.ExtractRefitTarget(node) ?? ExtractDeclarativeClientRoute(node);
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
            CollectEndpointReferences(node, scopeSymbolId, references);
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
                                            if (argText.StartWithIC("Roles") && argText.Contains('='))
                                            {
                                                var val = ExtractStringFromArg(arg);
                                                if (!string.IsNullOrEmpty(val))
                                                    symbol.RequiredRoles = string.IsNullOrEmpty(symbol.RequiredRoles) ? val : $"{symbol.RequiredRoles},{val}";
                                            }
                                            else if (argText.StartWithIC("Policy") && argText.Contains('='))
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
        if (text.Contains('=')) text = text[(text.IndexOf('=') + 1)..].Trim();
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
                if (strNode.IsValid())
                {
                    explicitRoute = strNode.Text.Trim('"');
                }
                else if (AstValueResolver.TryResolveExpression(arg, null, null, out var resolvedVal) && !string.IsNullOrWhiteSpace(resolvedVal))
                {
                    explicitRoute = resolvedVal;
                }
                else
                {
                    var rawArg = arg.Text.Trim();
                    if (ConstantRegistry.TryResolve(null, rawArg, out var constVal) && !string.IsNullOrWhiteSpace(constVal))
                    {
                        explicitRoute = constVal;
                    }
                }
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

        var prefix = ExtractRoutePrefixFromClassNode(classDecl);
        if (!string.IsNullOrEmpty(prefix)) return prefix;

        var baseList = classDecl.FindChildOfType(TreeSitterSyntax.CSharp.BaseList);
        if (baseList.IsValid())
        {
            var fileUnit = classDecl;
            while (fileUnit.Parent.IsValid()) fileUnit = fileUnit.Parent;

            foreach (var baseType in baseList.Children)
            {
                var baseName = baseType.FindChildOfType(TreeSitterSyntax.Common.Identifier)?.Text ?? baseType.Text;
                if (!string.IsNullOrEmpty(baseName) && baseName != ":" && baseName != ",")
                {
                    var baseClassDecl = fileUnit.FindDescendantsOfType(TreeSitterSyntax.CSharp.ClassDeclaration)
                        .FirstOrDefault(c => c.GetField(TreeSitterSyntax.Fields.Name)?.Text == baseName);
                    if (baseClassDecl.IsValid())
                    {
                        var basePrefix = ExtractRoutePrefixFromClassNode(baseClassDecl, replaceTokens: false);
                        if (!string.IsNullOrEmpty(basePrefix))
                        {
                            var classNameNode = classDecl.GetField(TreeSitterSyntax.Fields.Name);
                            if (classNameNode.IsValid() && basePrefix.Contains("[controller]"))
                            {
                                var className = classNameNode.Text;
                                if (className.EndsWith("Controller", StringComparison.OrdinalIgnoreCase))
                                {
                                    className = className[..^"Controller".Length];
                                }
                                basePrefix = basePrefix.Replace("[controller]", className, StringComparison.OrdinalIgnoreCase);
                            }
                            return basePrefix;
                        }
                    }
                }
            }
        }

        return null;
    }

    private static string? ExtractRoutePrefixFromClassNode(Node classDecl, bool replaceTokens = true)
    {
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
                            string? prefix = null;
                            var strNode = arg.Children.FirstOrDefault(c => c.Type.Contains("string"));
                            if (strNode.IsValid())
                            {
                                prefix = strNode.Text.Trim('"');
                            }
                            else if (AstValueResolver.TryResolveExpression(arg, null, null, out var resolvedVal) && !string.IsNullOrWhiteSpace(resolvedVal))
                            {
                                prefix = resolvedVal;
                            }
                            else
                            {
                                var rawArg = arg.Text.Trim();
                                if (ConstantRegistry.TryResolve(null, rawArg, out var constVal) && !string.IsNullOrWhiteSpace(constVal))
                                {
                                    prefix = constVal;
                                }
                            }

                            if (!string.IsNullOrEmpty(prefix))
                            {
                                if (replaceTokens)
                                {
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
