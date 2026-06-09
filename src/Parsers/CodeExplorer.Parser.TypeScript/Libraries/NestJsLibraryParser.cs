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
            return $"{(name == "Controller" ? "GET" : name.ToUpperInvariant())}:{routeVal}";
        }
        return null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        // When visiting a method_definition, check if the previous sibling is a NestJS decorator
        // and emit IMPLEMENTS to link the function to its EntryPoint
        if (node.Type == "method_definition")
        {
            var prev = GetPreviousNamedSibling(node);
            if (prev != null && _decoratorEntryPointSelector.Matches(prev))
            {
                var route = ExtractIdentifier(prev, ctx);
                if (!string.IsNullOrEmpty(route))
                {
                    references.Add(new Reference(scopeSymbolId, route.Replace(":", " "), OntologyConstants.Relationships.Implements));
                }
            }
        }
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
