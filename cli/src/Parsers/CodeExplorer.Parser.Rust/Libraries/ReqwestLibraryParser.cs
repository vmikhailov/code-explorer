using CodeExplorer.Common;
using CodeExplorer.Core.Parser;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.Rust.Libraries;

/// <summary>
/// Semantic extension for the Reqwest HTTP client in Rust.
/// Detects outbound HTTP requests such as reqwest::get(url) or client.get(url).send().await.
/// Emits ExternalService nodes and CALLS relationships.
/// </summary>
public class ReqwestLibraryParser : ISemanticExtension
{
    public string Id => "reqwest";
    public string Name => "Reqwest";
    public string Type => "http-client";
    public bool IsImplemented => true;
    public LibraryRole LibraryRole => LibraryRole.General;
    public IReadOnlyList<string> SupportedPatterns => ["reqwest*"];

    private static readonly HashSet<string> HttpMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "get", "post", "put", "delete", "patch", "head"
    };

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (IsReqwestCall(node, out _))
        {
            return OntologyConstants.NodeLabels.ExternalService;
        }
        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx) => ExtractIdentifier(node, ctx, null, null);
    public string? ExtractIdentifier(Node node, ParsingContext ctx, string? projectName) => ExtractIdentifier(node, ctx, projectName, null);

    public string? ExtractIdentifier(Node node, ParsingContext ctx, string? projectName, string? relativePath)
    {
        if (IsReqwestCall(node, out var target))
        {
            return target;
        }
        return null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (IsReqwestCall(node, out var target) && !string.IsNullOrEmpty(target))
        {
            references.Add(new Reference(scopeSymbolId, target, OntologyConstants.Relationships.Calls));
        }
    }

    public static bool IsReqwestCall(Node node, out string target)
    {
        target = string.Empty;

        if (node.Type != TreeSitterSyntax.Rust.CallExpression)
        {
            return false;
        }

        if (!RustAstHelper.GetMethodCallInfo(node, out var receiver, out var methodName, out var args))
        {
            return false;
        }

        if (string.IsNullOrEmpty(methodName)) return false;

        // 1. Standalone reqwest::get(url) or reqwest::post(url)
        if (methodName.StartWithIC("reqwest::"))
        {
            var shortMethod = methodName["reqwest::".Length..];
            if (HttpMethods.Contains(shortMethod) && args.Count > 0)
            {
                var url = RustAstHelper.ExtractStringLiteral(args[0]);
                if (!string.IsNullOrEmpty(url))
                {
                    target = RustAstHelper.ExtractHostOrUrl(url);
                    return true;
                }
            }
        }

        // 2. Chained method call on client: client.get(url), client.post(url)
        if (HttpMethods.Contains(methodName) && args.Count > 0 && receiver != null)
        {
            var recText = receiver.Text.ToLowerInvariant();
            if (recText.Contains("client") || recText.Contains("reqwest") || recText.Contains("http"))
            {
                var url = RustAstHelper.ExtractStringLiteral(args[0]);
                if (!string.IsNullOrEmpty(url))
                {
                    target = RustAstHelper.ExtractHostOrUrl(url);
                    return true;
                }
            }
        }

        return false;
    }
}
