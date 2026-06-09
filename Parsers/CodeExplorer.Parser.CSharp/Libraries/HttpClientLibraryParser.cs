using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.CSharp.Libraries;

public class HttpClientLibraryParser : ILibraryParser
{
    public string Name => "HttpClient";
    public string Id => "httpclient";
    public string Type => "api";
    public IReadOnlyList<string> SupportedPatterns => ["System.Net.Http"];
    public bool IsImplemented => true;
    public bool IsBuiltIn => true;

    private static readonly HashSet<string> HttpMethods =
    [
        "GetAsync", "PostAsync", "PutAsync", "DeleteAsync", "SendAsync",
        "PostAsJsonAsync", "GetFromJsonAsync", "PatchAsync", "PutAsJsonAsync"
    ];

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (IsHttpClientCall(node)) return OntologyConstants.NodeLabels.ExternalService;
        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx)
    {
        if (IsHttpClientCall(node)) return ExtractTarget(node);
        return null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx) { }

    private static bool IsHttpClientCall(Node node)
    {
        if (node.Type != "invocation_expression") return false;
        var func = node.GetChildForField("function")
                   ?? (node.Children.Count > 0 ? node.Children[0] : null);
        if (func == null || func.Id == IntPtr.Zero) return false;

        if (func.Type == "member_access_expression")
        {
            var nameChild = func.GetChildForField("name");
            if (nameChild != null && nameChild.Id != IntPtr.Zero)
                return HttpMethods.Contains(nameChild.Text);
        }
        return false;
    }

    private static string? ExtractTarget(Node node)
    {
        var argList = node.Children.FirstOrDefault(c => c.Type == "argument_list");
        if (argList != null)
        {
            var arg = argList.Children.FirstOrDefault(c => c.Type == "argument");
            if (arg != null)
            {
                var valNode = arg.Children.FirstOrDefault();
                if (valNode != null)
                {
                    var text = valNode.Text.Trim('"');
                    if (Uri.TryCreate(text, UriKind.Absolute, out var uri))
                    {
                        return $"{uri.Scheme}:{uri.Host}{uri.AbsolutePath}";
                    }
                    return text;
                }
            }
        }
        return "http:unknown-service";
    }
}
