using System.Text.RegularExpressions;
using CodeExplorer.Common;
using CodeExplorer.Core.Parser;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.Rust.Libraries;

/// <summary>
/// Semantic extension for the Actix-Web framework in Rust.
/// Supports attribute-based routing (#[get("/path")], #[post("/path")])
/// and fluent routing builders (web::resource, web::scope, web::get().to(handler)).
/// </summary>
public class ActixWebLibraryParser : ISemanticExtension
{
    public string Id => "actix-web";
    public string Name => "Actix-Web";
    public string Type => "framework";
    public bool IsImplemented => true;
    public LibraryRole LibraryRole => LibraryRole.WebService;
    public IReadOnlyList<string> SupportedPatterns => ["actix-web*", "actix_web*"];

    private static readonly Regex ActixAttributeRegex = new(
        @"#\[\s*(get|post|put|delete|patch|head|options)\s*\(\s*""([^""]+)""\s*\)\s*\]",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (IsActixRoute(node, out _, out _, out _))
        {
            return OntologyConstants.NodeLabels.EntryPoint;
        }
        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx) => ExtractIdentifier(node, ctx, null, null);
    public string? ExtractIdentifier(Node node, ParsingContext ctx, string? projectName) => ExtractIdentifier(node, ctx, projectName, null);

    public string? ExtractIdentifier(Node node, ParsingContext ctx, string? projectName, string? relativePath)
    {
        if (IsActixRoute(node, out var httpMethod, out var fullRoute, out _))
        {
            return $"{httpMethod}:{fullRoute}";
        }
        return null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (IsActixRoute(node, out _, out var fullRoute, out var handlerName) && !string.IsNullOrEmpty(handlerName))
        {
            references.Add(new Reference(handlerName, fullRoute.Replace(":", " "), OntologyConstants.Relationships.Implements));
            references.Add(new Reference(scopeSymbolId, handlerName, OntologyConstants.Relationships.Triggers));
        }
    }

    public static bool IsActixRoute(
        Node node,
        out string httpMethod,
        out string fullRoute,
        out string? handlerName)
    {
        httpMethod = "GET";
        fullRoute = "/";
        handlerName = null;

        // 1. Attribute routing: #[get("/path")]
        if (node.Type == TreeSitterSyntax.Rust.AttributeItem)
        {
            var match = ActixAttributeRegex.Match(node.Text);
            if (match.Success)
            {
                httpMethod = match.Groups[1].Value.ToUpperInvariant();
                fullRoute = RustAstHelper.CombineRoutes(null, match.Groups[2].Value);

                var parent = node.Parent;
                if (parent.IsValid() && parent.Type == TreeSitterSyntax.Rust.FunctionItem)
                {
                    var nameNode = parent.GetChildForField(TreeSitterSyntax.Fields.Name) ??
                                   parent.Children.FirstOrDefault(c => c.Type == TreeSitterSyntax.Rust.Identifier);
                    if (nameNode.IsValid())
                    {
                        handlerName = nameNode.Text;
                    }
                }
                return true;
            }
        }

        // 2. Fluent routing: web::get().to(handler) or .route("/path", web::get().to(handler))
        if (node.Type == TreeSitterSyntax.Rust.CallExpression)
        {
            if (RustAstHelper.GetMethodCallInfo(node, out var receiver, out var methodName, out var args))
            {
                if (methodName == "to" && args.Count > 0 && receiver != null)
                {
                    var recText = receiver.Text;
                    var detectedMethod = "GET";
                    if (recText.Contains("get", StringComparison.OrdinalIgnoreCase)) detectedMethod = "GET";
                    else if (recText.Contains("post", StringComparison.OrdinalIgnoreCase)) detectedMethod = "POST";
                    else if (recText.Contains("put", StringComparison.OrdinalIgnoreCase)) detectedMethod = "PUT";
                    else if (recText.Contains("delete", StringComparison.OrdinalIgnoreCase)) detectedMethod = "DELETE";

                    httpMethod = detectedMethod;
                    handlerName = args[0].Text.Split("::").Last().Trim();
                    fullRoute = "/";
                    return true;
                }
            }
        }

        return false;
    }
}
