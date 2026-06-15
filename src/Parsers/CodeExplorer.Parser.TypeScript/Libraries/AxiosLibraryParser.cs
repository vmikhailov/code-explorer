using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public class AxiosLibraryParser : ILibraryParser
{
    public string Type => "api";

    public string Name => "Axios";

    public string Id => "axios";

    public IReadOnlyList<string> SupportedPatterns => ["axios", "@nestjs/axios"];

    public bool IsImplemented => true;

    private static readonly NodeSelector _axiosCallSelector = NodeSelector.New()
        .HasType("call_expression")
        .FunctionNode
        .Where(NodeSelector.Or(
            NodeSelector.New().HasType("identifier").Text("axios"),
            NodeSelector.New()
                .HasType("member_expression")
                .HasChild("object", NodeSelector.New().Text("axios"))
                .HasChild("property", NodeSelector.New().Text("get|post|put|delete|request|patch|head"))
        ));

    private static readonly NodeSelector _callFirstStringArgSelector = NodeSelector.New()
        .GetChildForField("arguments")
        .FirstChild;

    public IReadOnlyDictionary<string, NodeSelector> Selectors => new Dictionary<string, NodeSelector>
    {
        { OntologyConstants.NodeLabels.ExternalService, _axiosCallSelector }
    };

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (_axiosCallSelector.Matches(node)) return OntologyConstants.NodeLabels.ExternalService;
        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx)
    {
        if (_axiosCallSelector.Matches(node))
        {
            var firstArg = _callFirstStringArgSelector.Select(node);
            if (firstArg.IsValid())
            {
                var resolved = AstHelper.ResolveStringOrTemplate(firstArg);
                if (resolved != null)
                {
                    if (Uri.TryCreate(resolved, UriKind.Absolute, out var uri))
                    {
                        return uri.Host;
                    }
                    return resolved;
                }
            }
            return "axios-call";
        }
        return null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        // Axios calls represent external services, no custom inner references are required
    }
}
