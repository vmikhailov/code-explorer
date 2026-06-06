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
        return $"{(name == "Route" ? "GET" : name.Replace("Http", "").ToUpperInvariant())}:{routeVal}";
    }
}
