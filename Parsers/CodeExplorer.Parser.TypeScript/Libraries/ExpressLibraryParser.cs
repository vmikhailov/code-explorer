using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public class ExpressLibraryParser : ILibraryParser
{
    public string Name => "Express";
    public string Id => "express";
    public string Type => "framework";
    public IReadOnlyList<string> SupportedPatterns => ["express", "@types/express"];
    public bool IsImplemented => true;

    private static readonly NodeSelector _expressRouteSelector = NodeSelector.New()
        .HasType("call_expression")
        .FunctionNode
        .HasType("member_expression")
        .HasChild("object", NodeSelector.New().TextContains("app|router|express"))
        .HasChild("property", NodeSelector.New().Text("get|post|put|delete"));

    private static readonly NodeSelector _expressRouteMethodSelector = NodeSelector.New()
        .FunctionNode
        .GetChildForField("property");

    private static readonly NodeSelector _callFirstStringArgSelector = NodeSelector.New()
        .GetChildForField("arguments")
        .FirstChild;

    public IReadOnlyDictionary<string, NodeSelector> Selectors => new Dictionary<string, NodeSelector>
    {
        { OntologyConstants.NodeLabels.EntryPoint, _expressRouteSelector }
    };

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (_expressRouteSelector.Matches(node)) return OntologyConstants.NodeLabels.EntryPoint;
        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx)
    {
        if (_expressRouteSelector.Matches(node))
        {
            var prop = _expressRouteMethodSelector.Select(node);
            if (!prop.IsValid()) return null;

            var method = prop!.Text.ToUpperInvariant();
            var routeVal = "/";

            var firstArg = _callFirstStringArgSelector.Select(node);
            if (firstArg.IsValid() && (firstArg!.Type == "string" || firstArg.Type == "template_string"))
            {
                routeVal = firstArg.Text.Trim('\'', '"', '`');
            }

            return $"{method}:{routeVal}";
        }
        return null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx) { }
}
