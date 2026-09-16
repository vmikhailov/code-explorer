using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.Java.Libraries;

public class SpringGraphQlLibraryParser : ILibraryParser
{
    public string Type => "framework:graphql";
    public string Name => "Spring GraphQL";
    public string Id => "spring-graphql";

    public IReadOnlyList<string> SupportedPatterns =>
    [
        "org.springframework.graphql",
        "org.springframework.graphql.data.method.annotation"
    ];

    public bool IsImplemented => true;

    private static readonly HashSet<string> GraphQlAnnotations = new(StringComparer.OrdinalIgnoreCase)
    {
        "QueryMapping", "MutationMapping", "SubscriptionMapping", "SchemaMapping"
    };

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (IsGraphQlAnnotation(node)) return OntologyConstants.NodeLabels.EntryPoint;
        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx)
    {
        if (IsGraphQlAnnotation(node))
        {
            var name = GetAnnotationName(node);
            var opType = name switch
            {
                "MutationMapping" => "MUTATION",
                "SubscriptionMapping" => "SUBSCRIPTION",
                _ => "QUERY"
            };

            var parentDecl = node.Parent;
            while (parentDecl.IsValid() && !parentDecl.Is(TreeSitterSyntax.Java.MethodDeclaration))
            {
                parentDecl = parentDecl.Parent;
            }

            if (parentDecl.IsValid())
            {
                var nameNode = parentDecl.FindChildOfType(TreeSitterSyntax.Java.Identifier);
                var methodName = nameNode.IsValid() ? nameNode.Text : "anonymous";
                return $"{opType}:{methodName}";
            }

            return $"{opType}:unknown";
        }
        return null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx) { }

    public void EnrichSymbol(Node node, SyntacticSymbol symbol, ParsingContext ctx)
    {
        if (!IsGraphQlAnnotation(node)) return;

        symbol.Protocol = "GraphQL";
        var annotName = GetAnnotationName(node);
        symbol.OperationType = annotName switch
        {
            "MutationMapping" => "Mutation",
            "SubscriptionMapping" => "Subscription",
            _ => "Query"
        };

        var parentDecl = node.Parent;
        while (parentDecl.IsValid() && !parentDecl.Is(TreeSitterSyntax.Java.MethodDeclaration))
        {
            parentDecl = parentDecl.Parent;
        }

        if (parentDecl.IsValid())
        {
            // Response type
            var typeNode = parentDecl.GetField(TreeSitterSyntax.Fields.Type);
            if (typeNode.IsValid())
            {
                var ret = CleanJavaTypeName(typeNode.Text);
                if (!string.IsNullOrEmpty(ret) && ret != "void")
                {
                    symbol.ResponseType = ret;
                }
            }

            // Request type from @Argument parameter
            var paramList = parentDecl.GetField(TreeSitterSyntax.Fields.Parameters);
            if (paramList.IsValid())
            {
                foreach (var param in paramList.Children)
                {
                    if (!param.Type.Contains("formal_parameter")) continue;
                    var pType = param.GetField(TreeSitterSyntax.Fields.Type);
                    if (pType.IsValid())
                    {
                        var pTypeName = CleanJavaTypeName(pType.Text);
                        if (!IsJavaPrimitiveOrSystemType(pTypeName) && symbol.RequestType == null)
                        {
                            symbol.RequestType = pTypeName;
                            break;
                        }
                    }
                }
            }
        }
    }

    private static bool IsGraphQlAnnotation(Node node)
    {
        if (!node.IsAny(TreeSitterSyntax.Java.Annotation, TreeSitterSyntax.Java.MarkerAnnotation)) return false;
        var name = GetAnnotationName(node);
        return GraphQlAnnotations.Contains(name);
    }

    private static string GetAnnotationName(Node node)
    {
        var nameNode = node.FindChildOfType(TreeSitterSyntax.Java.Identifier)
                       ?? node.FindChildOfType(TreeSitterSyntax.Java.ScopedIdentifier);
        if (!nameNode.IsValid()) return "";
        var name = nameNode.Text;
        return name.Contains('.') ? name[(name.LastIndexOf('.') + 1)..] : name;
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
                if (outer is "CompletableFuture" or "Mono" or "Flux" or "List" or "Set" or "Collection" or "Optional")
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
        return type is "int" or "long" or "String" or "boolean" or "double" or "float" or "byte" or "short" or "char" or "Integer" or "Long" or "Boolean" or "Double" or "Float" or "UUID" or "Date" or "Instant" or "LocalDate" or "LocalDateTime" or "Object";
    }
}
