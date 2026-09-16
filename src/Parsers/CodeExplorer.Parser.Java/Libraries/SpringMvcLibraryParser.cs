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
        if (name.Contains('.')) name = name[(name.LastIndexOf('.') + 1)..];

        return HttpMappingAnnotations.Contains(name);
    }

    public static string? ExtractRoute(Node annotationNode)
    {
        var nameNode = annotationNode.FindChildOfType(TreeSitterSyntax.Java.Identifier)
                       ?? annotationNode.FindChildOfType(TreeSitterSyntax.Java.ScopedIdentifier);

        if (!nameNode.IsValid()) return null;

        var annotName = nameNode.Text;
        if (annotName.Contains('.')) annotName = annotName[(annotName.LastIndexOf('.') + 1)..];

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

        var routePath = ExtractStringFromJavaAnnotation(annotationNode) ?? "";

        if (string.IsNullOrEmpty(routePath))
        {
            routePath = "/";
        }
        else if (!routePath.StartsWith('/'))
        {
            routePath = "/" + routePath;
        }

        return $"{httpMethod}:{routePath}";
    }

    public void EnrichSymbol(Node node, SyntacticSymbol symbol, ParsingContext ctx)
    {
        if (IsSpringRouteAnnotation(node))
        {
            var targetDecl = node.Parent;
            while (targetDecl.IsValid())
            {
                if (targetDecl.IsAny(TreeSitterSyntax.Java.MethodDeclaration, TreeSitterSyntax.Java.ClassDeclaration))
                {
                    EnrichFromJavaAnnotations(targetDecl, symbol);
                    break;
                }
                targetDecl = targetDecl.Parent;
            }

            if (targetDecl.IsValid() && targetDecl.Is(TreeSitterSyntax.Java.MethodDeclaration))
            {
                ExtractJavaPayloadSchemas(targetDecl, symbol);

                var classDecl = targetDecl.Parent;
                while (classDecl.IsValid())
                {
                    if (classDecl.Is(TreeSitterSyntax.Java.ClassDeclaration))
                    {
                        EnrichFromJavaAnnotations(classDecl, symbol);
                        break;
                    }
                    classDecl = classDecl.Parent;
                }
            }
        }
    }

    private static void ExtractJavaPayloadSchemas(Node methodDecl, SyntacticSymbol symbol)
    {
        var typeNode = methodDecl.GetField(TreeSitterSyntax.Fields.Type);
        if (typeNode.IsValid())
        {
            var ret = CleanJavaTypeName(typeNode.Text);
            if (!string.IsNullOrEmpty(ret) && ret != "void")
            {
                symbol.ResponseType = ret;
            }
        }

        var paramList = methodDecl.GetField(TreeSitterSyntax.Fields.Parameters);
        if (paramList.IsValid())
        {
            foreach (var param in paramList.Children)
            {
                if (!param.Type.Contains("formal_parameter")) continue;

                var isRequestBody = param.Text.Contains("@RequestBody") || param.Text.Contains("@ModelAttribute");
                var pType = param.GetField(TreeSitterSyntax.Fields.Type);
                if (pType.IsValid())
                {
                    var pTypeName = CleanJavaTypeName(pType.Text);
                    if (isRequestBody || (!IsJavaPrimitiveOrSystemType(pTypeName) && symbol.RequestType == null))
                    {
                        symbol.RequestType = pTypeName;
                        if (isRequestBody) break;
                    }
                }
            }
        }
    }

    private static string CleanJavaTypeName(string rawType)
    {
        if (string.IsNullOrWhiteSpace(rawType)) return "";
        var type = rawType.Trim();
        while (true)
        {
            var genericIdx = type.IndexOf('<');
            if (genericIdx > 0 && type.EndsWith('>'))
            {
                var outer = type[..genericIdx].Trim();
                if (outer is "ResponseEntity" or "CompletableFuture" or "Mono" or "Flux" or "List" or "Set" or "Collection" or "Optional" or "HttpEntity")
                {
                    type = type[(genericIdx + 1)..^1].Trim();
                    continue;
                }
            }
            break;
        }
        return type;
    }

    private static bool IsJavaPrimitiveOrSystemType(string type)
    {
        return type is "int" or "long" or "String" or "boolean" or "double" or "float" or "byte" or "short" or "char" or "Integer" or "Long" or "Boolean" or "Double" or "Float" or "UUID" or "Date" or "Instant" or "LocalDate" or "LocalDateTime" or "HttpServletRequest" or "HttpServletResponse" or "Principal" or "Authentication" or "HttpSession" or "Object";
    }

    private static void EnrichFromJavaAnnotations(Node targetDecl, SyntacticSymbol symbol)
    {
        var annotations = new List<Node>();
        foreach (var child in targetDecl.Children)
        {
            if (child.IsAny(TreeSitterSyntax.Java.Annotation, TreeSitterSyntax.Java.MarkerAnnotation))
            {
                annotations.Add(child);
            }
            else if (child.Is(TreeSitterSyntax.Java.Modifiers))
            {
                foreach (var modChild in child.Children)
                {
                    if (modChild.IsAny(TreeSitterSyntax.Java.Annotation, TreeSitterSyntax.Java.MarkerAnnotation))
                    {
                        annotations.Add(modChild);
                    }
                }
            }
        }

        foreach (var annot in annotations)
        {
            var nameNode = annot.FindChildOfType(TreeSitterSyntax.Java.Identifier)
                           ?? annot.FindChildOfType(TreeSitterSyntax.Java.ScopedIdentifier);
            if (!nameNode.IsValid()) continue;

            var name = nameNode.Text;
            if (name.Contains('.')) name = name[(name.LastIndexOf('.') + 1)..];

            if (name is "PermitAll" or "AnonymousAllowed")
            {
                symbol.IsAnonymous = true;
            }
            else if (name is "RolesAllowed" or "Secured")
            {
                var role = ExtractStringFromJavaAnnotation(annot);
                if (!string.IsNullOrEmpty(role))
                {
                    symbol.RequiredRoles = string.IsNullOrEmpty(symbol.RequiredRoles) ? role : $"{symbol.RequiredRoles},{role}";
                }
            }
            else if (name is "PreAuthorize")
            {
                var expr = ExtractStringFromJavaAnnotation(annot);
                if (!string.IsNullOrEmpty(expr))
                {
                    symbol.Policies = string.IsNullOrEmpty(symbol.Policies) ? expr : $"{symbol.Policies},{expr}";
                    if (expr.Contains("hasRole", StringComparison.OrdinalIgnoreCase) || expr.Contains("hasAuthority", StringComparison.OrdinalIgnoreCase))
                    {
                        var startQuote = expr.IndexOf('\'');
                        var endQuote = expr.LastIndexOf('\'');
                        if (startQuote >= 0 && endQuote > startQuote)
                        {
                            var role = expr[(startQuote + 1)..endQuote];
                            symbol.RequiredRoles = string.IsNullOrEmpty(symbol.RequiredRoles) ? role : $"{symbol.RequiredRoles},{role}";
                        }
                    }
                }
            }
            else if (name is "QueryMapping" or "MutationMapping" or "SubscriptionMapping")
            {
                symbol.Protocol = "GraphQL";
            }
            else if (name is "GrpcService")
            {
                symbol.Protocol = "gRPC";
            }
        }
    }

    private static string? ExtractStringFromJavaAnnotation(Node annot)
    {
        foreach (var child in annot.Children)
        {
            if (child.Type.Contains("string"))
                return child.Text.Trim('"');
            foreach (var sub in child.Children)
            {
                if (sub.Type.Contains("string"))
                    return sub.Text.Trim('"');
            }
        }
        var text = annot.Text;
        if (text.Contains('"'))
        {
            var first = text.IndexOf('"');
            var last = text.LastIndexOf('"');
            if (last > first) return text[(first + 1)..last];
        }
        return null;
    }
}
