using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public class AngularHttpLibraryParser : ILibraryParser
{
    public string Type => "api";

    public string Name => "Angular HttpClient";

    public string Id => "angular-http";

    public IReadOnlyList<string> SupportedPatterns =>
    [
        "@angular/common/http",
        "@angular/common/http/*",
        "@angular/common",
        "@angular/core"
    ];

    public bool IsImplemented => true;

    private static bool IsHttpCallNode(Node node)
    {
        if (!node.IsValid() || !node.Is(TreeSitterSyntax.TypeScript.CallExpression))
            return false;

        var func = node.GetFunctionNode();
        if (!func.IsValid())
            return false;

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

            // Exclude common false positives like Map.get, Map.delete, Headers.get, etc.
            if (objText is "map" or "set" or "headers" or "params" or "searchparams" or "cache" or "store")
            {
                return false;
            }

            if (objText.Contains("http") ||
                objText.Contains("client") ||
                objText.Contains("service") ||
                objText.Contains("api"))
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
                var func = node.GetFunctionNode();
                var prop = func?.GetField(TreeSitterSyntax.Fields.Property);
                var verb = prop?.Text.ToLowerInvariant();

                var urlArg = args[0];
                if (verb == "request" && args.Count > 1 && args[1].IsValid())
                {
                    var firstText = args[0].Text.Trim('\'', '"', '`').ToUpperInvariant();
                    if (firstText is "GET" or "POST" or "PUT" or "DELETE" or "PATCH" or "HEAD" or "OPTIONS")
                    {
                        urlArg = args[1];
                    }
                }

                var resolved = AstHelper.ResolveStringOrTemplate(urlArg);
                if (!string.IsNullOrWhiteSpace(resolved))
                {
                    if (Uri.TryCreate(resolved, UriKind.Absolute, out var uri) && !string.IsNullOrEmpty(uri.Host))
                    {
                        var path = uri.AbsolutePath.TrimStart('/');
                        if (uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
                            uri.Host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
                            uri.Host.Contains("hostname", StringComparison.OrdinalIgnoreCase))
                        {
                            var argKey = urlArg.Text.Trim('\'', '"', '`', '(', ')');
                            if (RouteDictionaryRegistry.TryResolve(argKey, out _, out var knownSvc) && !string.IsNullOrEmpty(knownSvc))
                            {
                                if (path.StartsWith(knownSvc + "/", StringComparison.OrdinalIgnoreCase))
                                {
                                    return $"{knownSvc}/{path[(knownSvc.Length + 1)..]}";
                                }
                                return string.IsNullOrEmpty(path) ? knownSvc : $"{knownSvc}/{path}";
                            }

                            var slashIdx = path.IndexOf('/');
                            if (slashIdx > 0)
                            {
                                var firstPart = path[..slashIdx].ToLowerInvariant();
                                if (firstPart is "identity" or "player" or "tournament" or "media" or "notification" ||
                                    RouteDictionaryRegistry.GetAllKnownServices().Any(s => string.Equals(s, firstPart, StringComparison.OrdinalIgnoreCase)))
                                {
                                    return $"{firstPart}/{path[slashIdx..].TrimStart('/')}";
                                }
                            }

                            return string.IsNullOrEmpty(path) ? uri.Host : $"{uri.Host}/{path}";
                        }
                        return string.IsNullOrEmpty(path) ? uri.Host : $"{uri.Host}/{path}";
                    }
                    return resolved.StartsWith('/') ? resolved : $"/{resolved}";
                }
            }
            return "http-call";
        }
        return null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
    }
}
