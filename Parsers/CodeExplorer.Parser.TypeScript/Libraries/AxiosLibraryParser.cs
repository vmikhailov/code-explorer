using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public class AxiosLibraryParser : ILibraryParser
{
    public string Name => "AxiosLibraryParser";

    public string Category => "api";

    public IEnumerable<string> SupportedLibraries => ["axios"];

    public bool IsImplemented => true;

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (IsAxiosCall(node))
        {
            return OntologyConstants.NodeLabels.ExternalService;
        }
        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx)
    {
        if (IsAxiosCall(node))
        {
            return ExtractAxiosTarget(node);
        }
        return null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        // Axios calls represent external services, no custom inner references are required
    }

    private static bool IsAxiosCall(Node node)
    {
        if (node.Type != "call_expression") return false;

        var func = node.GetChildForField("function");
        if (func == null || (func.Id == IntPtr.Zero && node.Children.Count > 0)) func = node.Children[0];
        if (func == null || func.Id == IntPtr.Zero) return false;

        if (func.Type == "identifier")
        {
            return func.Text == "axios";
        }

        if (func.Type == "member_expression")
        {
            var obj = func.GetChildForField("object");
            if (obj != null)
            {
                var objName = obj.Text;
                var prop = func.GetChildForField("property");
                if (prop != null)
                {
                    var propName = prop.Text;
                    if (objName == "axios")
                    {
                        return propName is "get" or "post" or "put" or "delete" or "request" or "patch" or "head";
                    }
                }
            }
        }
        return false;
    }

    private static string? ExtractAxiosTarget(Node node)
    {
        var args = node.Children.FirstOrDefault(c => c.Type == "arguments");
        if (args != null)
        {
            var firstArg = args.Children.FirstOrDefault(c => c.Type is "string" or "template_string");
            if (firstArg != null)
            {
                var url = firstArg.Text.Trim('\'', '"', '`');
                if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
                {
                    return uri.Host;
                }
                return url;
            }
        }
        return "axios-call";
    }
}
