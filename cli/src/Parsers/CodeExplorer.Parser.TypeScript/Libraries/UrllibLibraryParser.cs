using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using CodeExplorer.Core.Parser.Syntax;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public class UrllibLibraryParser : ISemanticExtension
{
    public string Type => "api";
    public string Name => "urllib";
    public string Id => "urllib";
    public IReadOnlyList<string> SupportedPatterns => ["urllib"];
    public bool IsImplemented => true;
    public LibraryRole LibraryRole => LibraryRole.General;

    private static readonly HashSet<string> UrllibMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "request", "curl", "requestWithCallback"
    };

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (IsUrllibCall(node))
        {
            return OntologyConstants.NodeLabels.ExternalService;
        }
        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx) => ExtractIdentifier(node, ctx, null);

    public string? ExtractIdentifier(Node node, ParsingContext ctx, string? projectName)
    {
        if (IsUrllibCall(node))
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
        if (IsUrllibCall(node))
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

    private static bool IsUrllibCall(Node node)
    {
        if (!node.Is(TreeSitterSyntax.TypeScript.CallExpression)) return false;

        var func = node.GetFunctionNode();
        if (!func.IsValid()) return false;

        // 1. Direct identifier call: urllib(...)
        if (func.Is(TreeSitterSyntax.TypeScript.Identifier))
        {
            var name = func.Text;
            if (name == "urllib") return HasUrlArgument(node);
        }

        // 2. Member call: urllib.request(...), urllib.curl(...), client.request(...)
        if (func.Is(TreeSitterSyntax.TypeScript.MemberExpression))
        {
            var objNode = func.GetField(TreeSitterSyntax.Fields.Object);
            var propNode = func.GetField(TreeSitterSyntax.Fields.Property);

            if (objNode.IsValid() && propNode.IsValid())
            {
                var objText = objNode.Text;
                var propText = propNode.Text;

                if (objText.Equals("urllib", StringComparison.OrdinalIgnoreCase) && UrllibMethods.Contains(propText))
                {
                    return HasUrlArgument(node);
                }

                if ((objText.Contains("client", StringComparison.OrdinalIgnoreCase) ||
                     objText.Contains("http", StringComparison.OrdinalIgnoreCase)) &&
                    UrllibMethods.Contains(propText))
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
