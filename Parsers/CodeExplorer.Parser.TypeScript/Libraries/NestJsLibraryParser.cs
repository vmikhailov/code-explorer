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

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (IsNestDecorator(node)) return OntologyConstants.NodeLabels.EntryPoint;
        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx)
    {
        if (IsNestDecorator(node)) return ExtractRoute(node);
        return null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        // When visiting a method_definition, check if the previous sibling is a NestJS decorator
        // and emit IMPLEMENTS to link the function to its EntryPoint
        if (node.Type == "method_definition")
        {
            var prev = GetPreviousNamedSibling(node);
            if (prev != null && prev.Type == "decorator" && IsNestDecorator(prev))
            {
                var route = ExtractRoute(prev);
                if (!string.IsNullOrEmpty(route))
                {
                    references.Add(new Reference(scopeSymbolId, route.Replace(":", " "), OntologyConstants.Relationships.Implements));
                }
            }
        }
    }

    internal static bool IsNestDecorator(Node node)
    {
        if (node.Type != "decorator") return false;
        var call = node.Children.FirstOrDefault(c => c.Type == "call_expression");
        if (call == null) return false;
        var func = call.GetChildForField("function")
                   ?? (call.Children.Count > 0 ? call.Children[0] : null);
        if (func == null || func.Id == IntPtr.Zero) return false;
        return func.Text is "Controller" or "Get" or "Post" or "Put" or "Delete" or "Patch" or "SubscribeMessage";
    }

    internal static string? ExtractRoute(Node node)
    {
        var call = node.Children.FirstOrDefault(c => c.Type == "call_expression");
        if (call == null) return null;
        var func = call.GetChildForField("function")
                   ?? (call.Children.Count > 0 ? call.Children[0] : null);
        if (func == null || func.Id == IntPtr.Zero) return null;
        var name = func.Text;

        var args = call.Children.FirstOrDefault(c => c.Type == "arguments");
        var routeVal = "/";
        if (args != null && args.Children.Count > 2)
        {
            var firstArg = args.Children.FirstOrDefault(c => c.Type is "string" or "template_string");
            if (firstArg != null) routeVal = firstArg.Text.Trim('\'', '"', '`');
        }

        if (name == "SubscribeMessage") return $"ws:{routeVal}";
        return $"{(name == "Controller" ? "GET" : name.ToUpperInvariant())}:{routeVal}";
    }

    private static Node? GetPreviousNamedSibling(Node node)
    {
        var parent = node.Parent;
        if (parent == null || parent.Id == IntPtr.Zero) return null;
        var children = parent.Children;
        var idx = children.ToList().FindIndex(c => c.Id == node.Id);
        return idx > 0 ? children[idx - 1] : null;
    }
}
