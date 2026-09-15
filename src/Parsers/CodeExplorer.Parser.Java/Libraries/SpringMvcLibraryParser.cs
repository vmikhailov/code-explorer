using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.Java.Libraries;

public class SpringMvcLibraryParser : ILibraryParser
{
    public string Type => "framework:web";
    public string Name => "Spring Web / MVC";
    public string Id => "spring-web";

    public IReadOnlyList<string> SupportedPatterns =>
    [
        "org.springframework.web",
        "org.springframework.boot.autoconfigure",
        "org.springframework.stereotype.Controller",
        "org.springframework.web.bind.annotation"
    ];

    public bool IsImplemented => true;

    public static readonly HashSet<string> HttpMappingAnnotations = new(StringComparer.OrdinalIgnoreCase)
    {
        "GetMapping", "PostMapping", "PutMapping", "DeleteMapping", "PatchMapping", "RequestMapping"
    };

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (IsSpringRouteAnnotation(node))
        {
            return OntologyConstants.NodeLabels.EntryPoint;
        }
        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx)
    {
        if (IsSpringRouteAnnotation(node))
        {
            return ExtractRoute(node);
        }
        return null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
    }

    public static bool IsSpringRouteAnnotation(Node node)
    {
        if (!node.IsAny(TreeSitterSyntax.Java.MarkerAnnotation, TreeSitterSyntax.Java.Annotation)) return false;

        var nameNode = node.FindChildOfType(TreeSitterSyntax.Java.Identifier)
                       ?? node.FindChildOfType(TreeSitterSyntax.Java.ScopedIdentifier);

        if (!nameNode.IsValid()) return false;

        var name = nameNode.Text;
        if (name.Contains('.')) name = name.Substring(name.LastIndexOf('.') + 1);

        return HttpMappingAnnotations.Contains(name);
    }

    public static string? ExtractRoute(Node annotationNode)
    {
        var nameNode = annotationNode.FindChildOfType(TreeSitterSyntax.Java.Identifier)
                       ?? annotationNode.FindChildOfType(TreeSitterSyntax.Java.ScopedIdentifier);

        if (!nameNode.IsValid()) return null;

        var annotName = nameNode.Text;
        if (annotName.Contains('.')) annotName = annotName.Substring(annotName.LastIndexOf('.') + 1);

        var httpMethod = annotName switch
        {
            "GetMapping" => "GET",
            "PostMapping" => "POST",
            "PutMapping" => "PUT",
            "DeleteMapping" => "DELETE",
            "PatchMapping" => "PATCH",
            "RequestMapping" => "ALL",
            _ => "GET"
        };

        var routePath = "";

        // Check for direct string argument e.g. @GetMapping("/api/users")
        var strNode = annotationNode.FindChildOfType(TreeSitterSyntax.Java.StringLiteral)
                      ?? annotationNode.FindChildOfType(TreeSitterSyntax.Java.TextBlock);

        if (strNode.IsValid())
        {
            routePath = strNode.Text.Trim('"').Trim();
        }
        else
        {
            // Check for element_value_pair e.g. @RequestMapping(value = "/api/users", method = RequestMethod.GET)
            foreach (var pair in annotationNode.Children)
            {
                if (pair.Is(TreeSitterSyntax.Java.ElementValuePair))
                {
                    var keyNode = pair.FindChildOfType(TreeSitterSyntax.Java.Identifier);
                    if (keyNode.IsValid() && keyNode.Text is "value" or "path")
                    {
                        var valStr = pair.FindChildOfType(TreeSitterSyntax.Java.StringLiteral);
                        if (valStr.IsValid())
                        {
                            routePath = valStr.Text.Trim('"').Trim();
                        }
                    }
                }
            }
        }

        if (string.IsNullOrEmpty(routePath))
        {
            routePath = "/";
        }
        else if (!routePath.StartsWith('/'))
        {
            routePath = "/" + routePath;
        }

        return $"{httpMethod} {routePath}";
    }
}
