using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public class AxiosLibraryParser : ISemanticExtension
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

    public string? ExtractIdentifier(Node node, ParsingContext ctx) => ExtractIdentifier(node, ctx, null);

    public string? ExtractIdentifier(Node node, ParsingContext ctx, string? projectName)
    {
        if (IsHttpCallNode(node))
        {
            var args = AstHelper.GetCallArguments(node);
            if (args.Count > 0 && args[0].IsValid())
            {
                var projectContext = projectName ?? ctx?.WorkspaceId;
                var resolved = AstHelper.ResolveStringOrTemplate(args[0], projectContext);
                if (!string.IsNullOrWhiteSpace(resolved))
                {
                    if (Uri.TryCreate(resolved, UriKind.Absolute, out var uri) && !string.IsNullOrEmpty(uri.Host))
                    {
                        var path = uri.AbsolutePath.TrimStart('/');
                        return string.IsNullOrEmpty(path) ? uri.Host : $"{uri.Host}/{path}";
                    }
                    if (!resolved.Contains('/') && RouteDictionaryRegistry.TryResolve(resolved, out var rPath, out var rSvc))
                    {
                        var cleanPath = rPath.Split('?')[0].TrimStart('/');
                        return !string.IsNullOrEmpty(rSvc) ? $"{rSvc}/{cleanPath}" : cleanPath;
                    }

                    // Try to resolve client's baseURL from containing class/service
                    var baseUrl = FindBaseUrl(node, projectContext);
                    if (!string.IsNullOrEmpty(baseUrl))
                    {
                        var cleanBase = baseUrl.TrimEnd('/');
                        var cleanPath = resolved.TrimStart('/');
                        return $"{cleanBase}/{cleanPath}";
                    }

                    // If resolved starts with '/', preserve it so CreateExternalServiceNode recognizes relative routes
                    if (resolved.StartsWith('/'))
                    {
                        return resolved;
                    }

                    return resolved.TrimStart('/');
                }
            }
            return "http-call";
        }
        return null;
    }

    private static string? FindBaseUrl(Node callNode, string? projectContext)
    {
        var curr = callNode.Parent;
        Node? classNode = null;
        while (curr.IsValid())
        {
            if (curr.Is(TreeSitterSyntax.TypeScript.ClassDeclaration))
            {
                classNode = curr;
                break;
            }
            curr = curr.Parent;
        }

        if (classNode == null || !classNode.IsValid()) return null;

        var classText = classNode.Text;

        // 1. Check for axios.create({ baseURL: ... })
        var createMatch = System.Text.RegularExpressions.Regex.Match(
            classText,
            @"axios\.create\s*\(\s*\{[^}]*?base(?:URL|Url)\s*:\s*([^,}]+)");
        if (createMatch.Success)
        {
            var rawExpr = createMatch.Groups[1].Value.Trim().Trim('\'', '"', '`');
            var resolved = ResolveBaseUrlExpression(classNode, rawExpr, projectContext);
            if (!string.IsNullOrEmpty(resolved)) return resolved;
        }

        // 2. Check for this._baseUrl = ... or this.baseUrl = ... in constructor or field initializers
        var baseFieldMatch = System.Text.RegularExpressions.Regex.Match(
            classText,
            @"(?:this\.)?(?:_baseUrl|baseUrl|baseURL|_apiUrl|apiUrl|apiBaseUrl)\s*[:=]\s*([^;\n]+)");
        if (baseFieldMatch.Success)
        {
            var rawExpr = baseFieldMatch.Groups[1].Value.Trim().Trim('\'', '"', '`');
            var resolved = ResolveBaseUrlExpression(classNode, rawExpr, projectContext);
            if (!string.IsNullOrEmpty(resolved)) return resolved;
        }

        return null;
    }

    private static string? ResolveBaseUrlExpression(Node classNode, string rawExpr, string? projectContext)
    {
        if (string.IsNullOrWhiteSpace(rawExpr)) return null;

        var clean = rawExpr.StartsWith("this.", StringComparison.OrdinalIgnoreCase) ? rawExpr[5..].Trim() : rawExpr.Trim();

        // If it's a direct URL
        if (Uri.TryCreate(clean, UriKind.Absolute, out var uri) && !string.IsNullOrEmpty(uri.Host))
        {
            var p = uri.AbsolutePath.Trim('/');
            return string.IsNullOrEmpty(p) ? uri.Host : $"{uri.Host}/{p}";
        }

        // If it's a field name like _baseUrl, check if constructor assigns it: this._baseUrl = config.get('ID_HELPER_NEST')
        if (clean.StartsWith('_') || clean.Equals("baseUrl", StringComparison.OrdinalIgnoreCase) || clean.Equals("apiUrl", StringComparison.OrdinalIgnoreCase))
        {
            var classText = classNode.Text;
            var assignMatch = System.Text.RegularExpressions.Regex.Match(
                classText,
                $@"(?:this\.)?{System.Text.RegularExpressions.Regex.Escape(clean)}\s*=\s*([^;\n]+)");
            if (assignMatch.Success)
            {
                var assignedVal = assignMatch.Groups[1].Value.Trim().Trim('\'', '"', '`');
                if (!string.Equals(assignedVal, clean, StringComparison.OrdinalIgnoreCase))
                {
                    var res = ResolveBaseUrlExpression(classNode, assignedVal, projectContext);
                    if (!string.IsNullOrEmpty(res)) return res;
                }
            }
        }

        // Check for config.get('KEY') or configService.get('KEY')
        var cfgMatch = System.Text.RegularExpressions.Regex.Match(clean, @"(?:getString|get)\s*\(\s*['""]([^'""]+)['""]\s*\)");
        if (cfgMatch.Success)
        {
            var key = cfgMatch.Groups[1].Value.Trim();
            if (ConstantRegistry.TryResolve(projectContext, key, out var resKey) && !string.IsNullOrEmpty(resKey))
            {
                return resKey;
            }
            return WorkspaceConventions.NormalizeServiceName(key);
        }

        // Check if clean itself is an env/config key in ConstantRegistry
        if (ConstantRegistry.TryResolve(projectContext, clean, out var regVal) && !string.IsNullOrEmpty(regVal))
        {
            return regVal;
        }

        // Check if clean looks like a config property: config.idHelperUrl -> idHelper
        if (clean.Contains('.'))
        {
            var propPart = clean[(clean.LastIndexOf('.') + 1)..];
            if (ConstantRegistry.TryResolve(projectContext, propPart, out var propVal) && !string.IsNullOrEmpty(propVal))
            {
                return propVal;
            }
            var stripped = CleanSuffix(propPart);
            if (!string.IsNullOrEmpty(stripped) && stripped.Length >= 2)
            {
                return WorkspaceConventions.NormalizeServiceName(stripped);
            }
        }

        var normalized = WorkspaceConventions.NormalizeServiceName(clean);
        return !string.IsNullOrEmpty(normalized) ? normalized : clean;
    }

    private static string CleanSuffix(string ident)
    {
        var suffixes = new[] { "BaseUrl", "BaseURL", "Url", "URL", "Host", "Domain", "Client", "Endpoint", "Service" };
        foreach (var s in suffixes)
        {
            if (ident.EndsWith(s, StringComparison.OrdinalIgnoreCase) && ident.Length > s.Length)
            {
                return ident[..^s.Length].TrimEnd('_', '-');
            }
        }
        return ident;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        // Axios calls represent external services, no custom inner references are required
    }
}
