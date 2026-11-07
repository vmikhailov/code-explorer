using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public class AngularHttpLibraryParser : DeclarativeSemanticExtension
{
    public override SemanticExtensionType Type => SemanticExtensionType.ApiClient;

    public override string Name => "Angular HttpClient";

    public override string Id => "angular-http";

    public override IReadOnlyList<string> SupportedPatterns =>
    [
        "@angular/common/http",
        "@angular/common/http/*",
        "@angular/common"
    ];

    private static readonly IReadOnlyList<CustomAstRule> Rules =
    [
        new CustomAstRule
        {
            NodeTypes = [TreeSitterSyntax.TypeScript.CallExpression],
            MapNodeType = static (node, ctx) => IsHttpCallNode(node) ? OntologyConstants.NodeLabels.ExternalService : null,
            ExtractIdentifier = static (node, ctx, proj, rel) => ExtractIdentifierStatic(node, ctx, proj)
        }
    ];

    public override IReadOnlyList<CustomAstRule> CustomAstRules => Rules;

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

    private static string? ExtractIdentifierStatic(Node node, ParsingContext ctx, string? projectName)
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

                var projectContext = projectName ?? ctx?.WorkspaceId;
                var resolved = AstHelper.ResolveStringOrTemplate(urlArg, projectContext);
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
                                if (path.StartWithIC(knownSvc + "/"))
                                {
                                    return $"{knownSvc}/{path[(knownSvc.Length + 1)..]}";
                                }
                                return string.IsNullOrEmpty(path) ? knownSvc : $"{knownSvc}/{path}";
                            }

                            var slashIdx = path.IndexOf('/');
                            if (slashIdx > 0)
                            {
                                var firstPart = path[..slashIdx].ToLowerInvariant();
                                var rest = path[slashIdx..];
                                if (!firstPart.Equals(ApiSegment, StringComparison.OrdinalIgnoreCase) &&
                                    !firstPart.Equals(V1Segment, StringComparison.OrdinalIgnoreCase) &&
                                    !firstPart.Equals(V2Segment, StringComparison.OrdinalIgnoreCase) &&
                                    !firstPart.Equals(V3Segment, StringComparison.OrdinalIgnoreCase) &&
                                    (rest.StartWithIC(ApiSlashPrefix) ||
                                     rest.StartWithIC(V1SlashPrefix) ||
                                     rest.StartWithIC(V2SlashPrefix) ||
                                     rest.StartWithIC(V3SlashPrefix) ||
                                     RouteDictionaryRegistry.GetAllKnownServices().Any(s => string.Equals(s, firstPart, StringComparison.OrdinalIgnoreCase))))
                                {
                                    return $"{firstPart}/{rest.TrimStart('/')}";
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

    private const string ApiSegment = "api";
    private const string V1Segment = "v1";
    private const string V2Segment = "v2";
    private const string V3Segment = "v3";
    private const string ApiSlashPrefix = "/api/";
    private const string V1SlashPrefix = "/v1/";
    private const string V2SlashPrefix = "/v2/";
    private const string V3SlashPrefix = "/v3/";
}
