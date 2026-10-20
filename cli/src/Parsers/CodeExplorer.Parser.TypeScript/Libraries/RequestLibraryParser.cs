using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public class RequestLibraryParser : ISemanticExtension
{
    public string Type => "api";
    public string Name => "request";
    public string Id => "request";
    public IReadOnlyList<string> SupportedPatterns => ["request", "request-promise", "request-promise-native"];
    public bool IsImplemented => true;
    public LibraryRole LibraryRole => LibraryRole.General;

    private static readonly HashSet<string> HttpMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "get", "post", "put", "delete", "patch", "head", "options"
    };

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (IsRequestCall(node))
        {
            return OntologyConstants.NodeLabels.ExternalService;
        }
        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx) => ExtractIdentifier(node, ctx, null);

    public string? ExtractIdentifier(Node node, ParsingContext ctx, string? projectName)
    {
        if (IsRequestCall(node))
        {
            var url = ExtractUrl(node, projectName ?? ctx?.WorkspaceId);
            if (!string.IsNullOrEmpty(url))
            {
                return url;
            }
            return "http-client";
        }
        return null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (IsRequestCall(node))
        {
            var url = ExtractUrl(node, ctx?.WorkspaceId);
            if (!string.IsNullOrEmpty(url))
            {
                var target = Uri.TryCreate(url, UriKind.Absolute, out var uri) && !string.IsNullOrEmpty(uri.Host)
                    ? uri.Host
                    : url.Split('/')[0];
                references.Add(new Reference(scopeSymbolId, target, OntologyConstants.Relationships.Calls));
            }
        }
    }

    private static bool IsRequestCall(Node node)
    {
        if (!node.Is(TreeSitterSyntax.TypeScript.CallExpression)) return false;

        var func = node.GetFunctionNode();
        if (!func.IsValid()) return false;

        // 1. Direct call: request(...) or rp(...)
        if (func.Is(TreeSitterSyntax.TypeScript.Identifier))
        {
            var name = func.Text;
            if (name is "request" or "rp" or "requestPromise")
            {
                return HasUrlArgument(node);
            }
        }

        // 2. Member call: request.get(...), rp.post(...), requestPromise.get(...)
        if (func.Is(TreeSitterSyntax.TypeScript.MemberExpression))
        {
            var objNode = func.GetField(TreeSitterSyntax.Fields.Object);
            var propNode = func.GetField(TreeSitterSyntax.Fields.Property);

            if (objNode.IsValid() && propNode.IsValid())
            {
                var objText = objNode.Text;
                var propText = propNode.Text;

                if ((objText is "request" or "rp" or "requestPromise") && HttpMethods.Contains(propText))
                {
                    return HasUrlArgument(node);
                }
            }
        }

        return false;
    }

    private static bool HasUrlArgument(Node callNode)
    {
        var args = AstHelper.GetCallArguments(callNode);
        if (args.Count == 0) return false;

        var firstArg = args[0];
        if (!firstArg.IsValid()) return false;

        if (firstArg.Is(TreeSitterSyntax.TypeScript.Object))
        {
            return AstHelper.TryGetObjectProperty(firstArg, "url", out _) ||
                   AstHelper.TryGetObjectProperty(firstArg, "uri", out _);
        }

        return LooksLikeUrlOrIdentifier(firstArg);
    }

    private static bool LooksLikeUrlOrIdentifier(Node node)
    {
        if (node.IsAny(TreeSitterSyntax.TypeScript.String, TreeSitterSyntax.TypeScript.TemplateString))
        {
            var text = node.Text.Trim('\'', '"', '`');
            return text.StartWithIC("http://") ||
                   text.StartWithIC("https://") ||
                   text.StartsWith('/') ||
                   text.Contains('/');
        }

        return node.Is(TreeSitterSyntax.TypeScript.Identifier);
    }

    private static string? ExtractUrl(Node callNode, string? projectContext)
    {
        var args = AstHelper.GetCallArguments(callNode);
        if (args.Count == 0) return null;

        var firstArg = args[0];
        if (!firstArg.IsValid()) return null;

        if (firstArg.Is(TreeSitterSyntax.TypeScript.Object))
        {
            if (AstHelper.TryGetObjectProperty(firstArg, "url", out var urlNode) ||
                AstHelper.TryGetObjectProperty(firstArg, "uri", out urlNode))
            {
                return AstHelper.ResolveStringOrTemplate(urlNode, projectContext);
            }
        }

        return AstHelper.ResolveStringOrTemplate(firstArg, projectContext);
    }
}
