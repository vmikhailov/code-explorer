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

    public IReadOnlyList<string> SupportedPatterns => ["axios", "@nestjs/axios", "@nestjs/common"];

    public bool IsImplemented => true;

    private static bool IsHttpCallNode(Node node)
    {
        if (!node.IsValid() || !node.Is(TreeSitterSyntax.TypeScript.CallExpression))
            return false;

        var func = node.GetFunctionNode();
        if (!func.IsValid())
            return false;

        // 1. Direct call: axios(url, ...)
        if (func.Is(TreeSitterSyntax.TypeScript.Identifier))
        {
            return string.Equals(func.Text, "axios", StringComparison.OrdinalIgnoreCase);
        }

        // 2. Member call: this.http.get(...), this.httpService.post(...), axios.get(...), apiClient.get(...)
        if (func.Is(TreeSitterSyntax.TypeScript.MemberExpression))
        {
            var prop = func.GetField(TreeSitterSyntax.Fields.Property);
            if (!prop.IsValid()) return false;

            var verb = prop.Text.ToLowerInvariant();
            if (verb is not ("get" or "post" or "put" or "delete" or "request" or "patch" or "head" or "options"))
            {
                return false;
            }

            var obj = func.GetField(TreeSitterSyntax.Fields.Object);
            if (!obj.IsValid()) return false;

            var objText = obj.Text.ToLowerInvariant();

            // Exclude common false positives like Map.get, Map.delete, Headers.get, URLSearchParams.get, etc.
            if (objText is "map" or "set" or "headers" or "params" or "searchparams" or "cache" or "store")
            {
                return false;
            }

            if (objText.Contains("axios") ||
                objText.Contains("http") ||
                objText.Contains("apiclient") ||
                objText.EndsWith("client") ||
                objText.Equals("client") ||
                objText.Equals("this.client") ||
                objText.Equals("api") ||
                objText.Equals("this.api"))
            {
                return true;
            }
        }

        return false;
    }

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (IsHttpCallNode(node)) return OntologyConstants.NodeLabels.ExternalService;
        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx)
    {
        if (IsHttpCallNode(node))
        {
            var args = AstHelper.GetCallArguments(node);
            if (args.Count > 0 && args[0].IsValid())
            {
                var resolved = AstHelper.ResolveStringOrTemplate(args[0]);
                if (!string.IsNullOrWhiteSpace(resolved))
                {
                    if (Uri.TryCreate(resolved, UriKind.Absolute, out var uri) && !string.IsNullOrEmpty(uri.Host))
                    {
                        var path = uri.AbsolutePath.TrimStart('/');
                        return string.IsNullOrEmpty(path) ? uri.Host : $"{uri.Host}/{path}";
                    }
                    return resolved.TrimStart('/');
                }
            }
            return "http-call";
        }
        return null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        // Axios calls represent external services, no custom inner references are required
    }
}
