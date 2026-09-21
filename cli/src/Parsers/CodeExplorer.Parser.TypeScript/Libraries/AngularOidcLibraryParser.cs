using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public class AngularOidcLibraryParser : ILibraryParser
{
    public string Type => "api";

    public string Name => "Angular OAuth2 OIDC";

    public string Id => "angular-oidc";

    public IReadOnlyList<string> SupportedPatterns =>
    [
        "angular-oauth2-oidc",
        "oidc-client-ts",
        "oidc-client",
        "@auth0/auth0-angular",
        "@auth0/auth0-spa-js"
    ];

    public bool IsImplemented => true;

    private static readonly HashSet<string> OidcCallMethods =
    [
        "loaddiscoverydocumentandlogin",
        "loaddiscoverydocumentandtrylogin",
        "loaddiscoverydocument",
        "fetchtokenusingpasswordflow",
        "initcodeflow",
        "initimplicitflow",
        "refreshtoken",
        "configure"
    ];

    private static bool IsOidcTarget(Node node)
    {
        if (!node.IsValid()) return false;

        // 1. AuthConfig object: { issuer: 'https://...' }
        if (node.Is(TreeSitterSyntax.TypeScript.Object))
        {
            if (AstHelper.TryGetObjectProperty(node, "issuer", out var issuerNode) && issuerNode.IsValid())
            {
                return true;
            }
            if (AstHelper.TryGetObjectProperty(node, "authority", out var authNode) && authNode.IsValid())
            {
                return true;
            }
        }

        // 2. OIDC Service method calls: this.oauthService.loadDiscoveryDocumentAndTryLogin()
        if (node.Is(TreeSitterSyntax.TypeScript.CallExpression))
        {
            var func = node.GetFunctionNode();
            if (func.IsValid() && func.Is(TreeSitterSyntax.TypeScript.MemberExpression))
            {
                var prop = func.GetField(TreeSitterSyntax.Fields.Property);
                if (prop.IsValid())
                {
                    var verb = prop.Text.ToLowerInvariant();
                    if (OidcCallMethods.Contains(verb))
                    {
                        var obj = func.GetField(TreeSitterSyntax.Fields.Object);
                        if (obj.IsValid())
                        {
                            var objText = obj.Text.ToLowerInvariant();
                            if (objText.Contains("oauth") || objText.Contains("auth") || objText.Contains("oidc"))
                            {
                                return true;
                            }
                        }
                    }
                }
            }
        }

        return false;
    }

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (IsOidcTarget(node)) return OntologyConstants.NodeLabels.ExternalService;
        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx)
    {
        if (!IsOidcTarget(node)) return null;

        // 1. Object with issuer/authority property
        if (node.Is(TreeSitterSyntax.TypeScript.Object))
        {
            Node? targetNode = null;
            if (AstHelper.TryGetObjectProperty(node, "issuer", out var issuerNode) && issuerNode.IsValid())
            {
                targetNode = issuerNode;
            }
            else if (AstHelper.TryGetObjectProperty(node, "authority", out var authNode) && authNode.IsValid())
            {
                targetNode = authNode;
            }

            if (targetNode.IsValid())
            {
                var resolved = AstHelper.ResolveStringOrTemplate(targetNode);
                if (!string.IsNullOrWhiteSpace(resolved))
                {
                    return FormatOidcTarget(resolved);
                }
            }
        }

        // 2. Method invocation: oauthService.loadDiscoveryDocument*()
        if (node.Is(TreeSitterSyntax.TypeScript.CallExpression))
        {
            var args = AstHelper.GetCallArguments(node);
            if (args.Count > 0 && args[0].IsValid())
            {
                var resolved = AstHelper.ResolveStringOrTemplate(args[0]);
                if (!string.IsNullOrWhiteSpace(resolved))
                {
                    return FormatOidcTarget(resolved);
                }
            }

            // Check if RouteDictionaryRegistry has an identity or auth service registered
            if (RouteDictionaryRegistry.TryResolve("identity", out var idPath, out var idSvc))
            {
                var target = !string.IsNullOrEmpty(idSvc) ? $"{idSvc}{idPath}" : idPath;
                return FormatOidcTarget(target);
            }
            if (RouteDictionaryRegistry.TryResolve("auth", out var authPath, out var authSvc))
            {
                var target = !string.IsNullOrEmpty(authSvc) ? $"{authSvc}{authPath}" : authPath;
                return FormatOidcTarget(target);
            }

            return "identity/.well-known/openid-configuration";
        }

        return null;
    }

    private static string FormatOidcTarget(string raw)
    {
        if (Uri.TryCreate(raw, UriKind.Absolute, out var uri) && !string.IsNullOrEmpty(uri.Host))
        {
            var basePath = uri.AbsolutePath.Trim('/');
            var fullPath = string.IsNullOrEmpty(basePath)
                ? ".well-known/openid-configuration"
                : $"{basePath}/.well-known/openid-configuration";

            return $"{uri.Host}/{fullPath}";
        }

        var clean = raw.Trim('/');
        return string.IsNullOrEmpty(clean)
            ? "identity/.well-known/openid-configuration"
            : $"{clean}/.well-known/openid-configuration";
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
    }
}
