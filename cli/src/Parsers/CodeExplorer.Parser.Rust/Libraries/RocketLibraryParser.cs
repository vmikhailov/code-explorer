using System.Text.RegularExpressions;
using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.Rust.Libraries;

/// <summary>
/// Semantic extension for the Rocket web framework in Rust.
/// Detects attribute routes (#[get("/path")], #[post("/path")]) and routes![...] macro mounts.
/// </summary>
public class RocketLibraryParser : ISemanticExtension
{
    public string Id => "rocket";
    public string Name => "Rocket";
    public string Type => "framework";
    public bool IsImplemented => true;
    public LibraryRole LibraryRole => LibraryRole.WebService;
    public IReadOnlyList<string> SupportedPatterns => ["rocket*"];

    private static readonly Regex RocketAttributeRegex = new(
        @"#\[\s*(get|post|put|delete|patch|head|options)\s*\(\s*""([^""]+)""",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (IsRocketRoute(node, out _, out _, out _))
        {
            return OntologyConstants.NodeLabels.EntryPoint;
        }
        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx) => ExtractIdentifier(node, ctx, null, null);
    public string? ExtractIdentifier(Node node, ParsingContext ctx, string? projectName) => ExtractIdentifier(node, ctx, projectName, null);

    public string? ExtractIdentifier(Node node, ParsingContext ctx, string? projectName, string? relativePath)
    {
        if (IsRocketRoute(node, out var httpMethod, out var fullRoute, out _))
        {
            return $"{httpMethod}:{fullRoute}";
        }
        return null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (IsRocketRoute(node, out _, out var fullRoute, out var handlerName) && !string.IsNullOrEmpty(handlerName))
        {
            references.Add(new Reference(handlerName, fullRoute.Replace(":", " "), OntologyConstants.Relationships.Implements));
            references.Add(new Reference(scopeSymbolId, handlerName, OntologyConstants.Relationships.Triggers));
        }
    }

    public static bool IsRocketRoute(
        Node node,
        out string httpMethod,
        out string fullRoute,
        out string? handlerName)
    {
        httpMethod = "GET";
        fullRoute = "/";
        handlerName = null;

        if (node.Type == TreeSitterSyntax.Rust.AttributeItem)
        {
            var match = RocketAttributeRegex.Match(node.Text);
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

        return false;
    }
}
