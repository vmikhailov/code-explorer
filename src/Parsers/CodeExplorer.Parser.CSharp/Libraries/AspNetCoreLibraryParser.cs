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

    private static readonly HashSet<string> RouteAttributes = ["Route", "HttpGet", "HttpPost", "HttpPut", "HttpDelete", "HttpPatch"];

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (IsRouteAttribute(node)) return OntologyConstants.NodeLabels.EntryPoint;
        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx)
    {
        if (IsRouteAttribute(node)) return ExtractRoute(node);
        return null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx) { }

    private static bool IsRouteAttribute(Node node)
    {
        if (node.Type != "attribute") return false;
        var nameNode = node.Children.FirstOrDefault(c => c.Type == "identifier");
        return nameNode != null && RouteAttributes.Contains(nameNode.Text);
    }

    private static string? ExtractRoute(Node node)
    {
        var nameNode = node.Children.FirstOrDefault(c => c.Type == "identifier");
        if (nameNode == null) return null;
        var name = nameNode.Text;
        if (!RouteAttributes.Contains(name)) return null;

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

        var method = (name == "Route" ? "GET" : name.Replace("Http", "").ToUpperInvariant());

        var classPrefix = GetControllerRoutePrefix(node);
        if (!string.IsNullOrEmpty(classPrefix))
        {
            routeVal = CombineRoutes(classPrefix, routeVal);
        }

        return $"{method}:{routeVal}";
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

        var methodDecl = attrList.Parent;
        if (methodDecl == null || methodDecl.Type != "method_declaration") return null;

        var classDecl = methodDecl.Parent;
        while (classDecl != null && classDecl.Id != IntPtr.Zero)
        {
            if (classDecl.Type is "class_declaration" or "struct_declaration" or "record_declaration")
            {
                break;
            }
            classDecl = classDecl.Parent;
        }
        if (classDecl == null) return null;

        foreach (var child in classDecl.Children)
        {
            if (child.Type == "attribute_list")
            {
                foreach (var attr in child.Children)
                {
                    if (attr.Type == "attribute")
                    {
                        var nameNode = attr.Children.FirstOrDefault(c => c.Type == "identifier");
                        if (nameNode != null && nameNode.Text == "Route")
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
                                            var className = classNameNode!.Text;
                                            if (className.EndsWith("Controller", StringComparison.OrdinalIgnoreCase))
                                            {
                                                className = className.Substring(0, className.Length - "Controller".Length);
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
