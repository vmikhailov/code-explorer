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
    public IReadOnlyList<string> SupportedPatterns => ["@nestjs/common", "@nestjs/core", "@nestjs/microservices", "@nestjs/websockets"];
    public bool IsImplemented => true;

    private static readonly NodeSelector _decoratorEntryPointSelector = NodeSelector.New()
        .HasType("decorator")
        .FirstChild
        .HasType("call_expression")
        .GetChildForField("function")
        .Text("Controller|Get|Post|Put|Delete|Patch|SubscribeMessage");

    private static readonly NodeSelector _decoratorCallFunctionSelector = NodeSelector.New()
        .FirstChild
        .FunctionNode;

    private static readonly NodeSelector _decoratorCallFirstStringArgSelector = NodeSelector.New()
        .FirstChild
        .GetChildForField("arguments")
        .FirstChild;

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

            var name = func!.Text;
            var routeVal = "/";

            var firstArg = _decoratorCallFirstStringArgSelector.Select(node);
            if (firstArg.IsValid() && (firstArg!.Type == "string" || firstArg.Type == "template_string"))
            {
                routeVal = firstArg.Text.Trim('\'', '"', '`');
            }

            if (name == "SubscribeMessage") return $"ws:{routeVal}";

            if (name != "Controller")
            {
                var classPrefix = GetControllerPrefixForNode(node);
                if (!string.IsNullOrEmpty(classPrefix))
                {
                    routeVal = CombineRoutes(classPrefix, routeVal);
                }
            }

            return $"{(name == "Controller" ? "GET" : name.ToUpperInvariant())}:{routeVal}";
        }
        return null;
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
        if (classBody == null || classBody.Type != "class_body") return null;

        var classDecl = classBody.Parent;
        if (classDecl == null || (classDecl.Type != "class_declaration" && classDecl.Type != "class_expression")) return null;

        var candidates = new List<Node>();
        candidates.AddRange(classDecl.Children);

        var parent = classDecl.Parent;
        if (parent != null && parent.Type == "export_statement")
        {
            candidates.AddRange(parent.Children);
        }

        foreach (var c in candidates)
        {
            if (c.Type == "decorator")
            {
                var func = _decoratorCallFunctionSelector.Select(c);
                if (func.IsValid() && func!.Text == "Controller")
                {
                    var firstArg = _decoratorCallFirstStringArgSelector.Select(c);
                    if (firstArg.IsValid() && (firstArg!.Type == "string" || firstArg.Type == "template_string"))
                    {
                        return firstArg.Text.Trim('\'', '"', '`');
                    }
                    return "/";
                }
            }
        }

        return null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (node.Type == "method_definition")
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
        if (parent == null || parent.Id == IntPtr.Zero) return result;
        var children = parent.Children;
        var idx = children.ToList().FindIndex(c => c.Id == node.Id);
        if (idx <= 0) return result;

        for (var i = idx - 1; i >= 0; i--)
        {
            var sibling = children[i];
            if (sibling.Type == "decorator")
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
}
