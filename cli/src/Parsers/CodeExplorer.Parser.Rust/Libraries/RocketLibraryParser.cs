using System.Text.RegularExpressions;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.Rust.Libraries;

/// <summary>
/// Semantic extension for the Rocket web framework in Rust.
/// Detects attribute routes (#[get("/path")], #[post("/path")]) and routes![...] macro mounts.
/// </summary>
public class RocketLibraryParser : DeclarativeSemanticExtension
{
    private static readonly Regex RocketAttributeRegex = new(
        @"#\[\s*(get|post|put|delete|patch|head|options)\s*\(\s*""([^""]+)""",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public RocketLibraryParser()
    {
        Id = "rocket";
        Name = "Rocket";
        Type = SemanticExtensionType.Framework;
        LibraryRole = LibraryRole.WebService;
        SupportedPatterns = ["rocket*"];
        CustomAstRules =
        [
            new CustomAstRule
            {
                NodeTypes = System.Collections.Frozen.FrozenSet.ToFrozenSet([TreeSitterSyntax.Rust.AttributeItem]),
                MapNodeType = (node, ctx) => IsRocketRoute(node, out _, out _, out _) ? OntologyConstants.NodeLabels.EntryPoint : null,
                ExtractIdentifier = (node, _, _, _) => IsRocketRoute(node, out var m, out var r, out _) ? $"{m}:{r}" : null,
                CollectReferences = CollectReferencesCore
            }
        ];
    }

    private static void CollectReferencesCore(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
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

        if (node.Is(TreeSitterSyntax.Rust.AttributeItem))
        {
            var match = RocketAttributeRegex.Match(node.Text);
            if (match.Success)
            {
                httpMethod = match.Groups[1].Value.ToUpperInvariant();
                fullRoute = RustAstHelper.CombineRoutes(null, match.Groups[2].Value);

                var parent = node.Parent;
                if (parent.IsValid() && parent.Is(TreeSitterSyntax.Rust.FunctionItem))
                {
                    var nameNode = parent.GetChildForField(TreeSitterSyntax.Fields.Name) ??
                                   parent.Children.FirstOrDefault(c => c.Is(TreeSitterSyntax.Rust.Identifier));
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
