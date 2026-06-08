using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public class FetchLibraryParser : ILibraryParser
{
    public string Name => "Fetch";
    public string Id => "fetch";
    public string Type => "api";
    // fetch is built-in; node-fetch/got/superagent are npm packages
    public IReadOnlyList<string> SupportedPatterns => ["node-fetch", "got", "superagent", "cross-fetch", "isomorphic-fetch"];
    public bool IsImplemented => true;
    public bool IsBuiltIn => true;   // fetch is available without import in browsers/Node 18+

    private static readonly NodeSelector _fetchCallSelector = NodeSelector.New()
        .HasType("call_expression")
        .FunctionNode
        .Where(NodeSelector.Or(
            NodeSelector.New().HasType("identifier").Text("fetch|nodeFetch|got|superagent"),
            NodeSelector.New()
                .HasType("member_expression")
                .HasChild("object", NodeSelector.New().Text("got|superagent|request|http|https"))
                .HasChild("property", NodeSelector.New().Text("get|post|put|delete|request|patch|head"))
        ));

    private static readonly NodeSelector _callFirstStringArgSelector = NodeSelector.New()
        .GetChildForField("arguments")
        .FirstChild;

    public IReadOnlyDictionary<string, NodeSelector> Selectors => new Dictionary<string, NodeSelector>
    {
        { OntologyConstants.NodeLabels.ExternalService, _fetchCallSelector }
    };

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (_fetchCallSelector.Matches(node)) return OntologyConstants.NodeLabels.ExternalService;
        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx)
    {
        if (_fetchCallSelector.Matches(node))
        {
            var firstArg = _callFirstStringArgSelector.Select(node);
            if (firstArg.IsValid() && (firstArg!.Type == "string" || firstArg.Type == "template_string"))
            {
                var url = firstArg.Text.Trim('\'', '"', '`');
                if (Uri.TryCreate(url, UriKind.Absolute, out var uri)) return uri.Host;
                return url;
            }
            return "http:unknown-service";
        }
        return null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx) { }
}
