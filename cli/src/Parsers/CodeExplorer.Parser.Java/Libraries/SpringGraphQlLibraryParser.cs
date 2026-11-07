using System.Collections.Frozen;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.Java.Libraries;

public class SpringGraphQlLibraryParser : DeclarativeSemanticExtension
{
    private static readonly FrozenSet<string> GraphQlAnnotations = new[]
    {
        "QueryMapping", "MutationMapping", "SubscriptionMapping", "SchemaMapping"
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    public SpringGraphQlLibraryParser()
    {
        Id = "spring-graphql";
        Name = "Spring GraphQL";
        Type = SemanticExtensionType.GraphqlFramework;
        LibraryRole = LibraryRole.WebService;
        SupportedPatterns =
        [
            "org.springframework.graphql",
            "org.springframework.graphql.*",
            "org.springframework.graphql.data.method.annotation"
        ];
        CustomAstRules =
        [
            new CustomAstRule
            {
                NodeTypes = new[] { TreeSitterSyntax.Java.Annotation, TreeSitterSyntax.Java.MarkerAnnotation }.ToFrozenSet(),
                MapNodeType = MapNodeTypeCore,
                ExtractIdentifier = (node, ctx, _, _) => ExtractIdentifierCore(node),
                EnrichSymbol = EnrichSymbolCore
            }
        ];
    }

    private static string? MapNodeTypeCore(Node node, ParsingContext ctx) =>
        IsGraphQlAnnotation(node) ? OntologyConstants.NodeLabels.EntryPoint : null;

    private static string? ExtractIdentifierCore(Node node)
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

    private static void EnrichSymbolCore(Node node, SyntacticSymbol symbol, ParsingContext ctx)
    {
        if (!IsGraphQlAnnotation(node)) return;

        symbol.Protocol = "GraphQL";
        var annotName = GetAnnotationName(node);
        symbol.OperationType = annotName switch
        {
            "MutationMapping" => CodeExplorer.Cypher.Common.OperationType.Mutation,
            "SubscriptionMapping" => CodeExplorer.Cypher.Common.OperationType.Subscription,
            _ => CodeExplorer.Cypher.Common.OperationType.Query
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
                    if (!param.Is(TreeSitterSyntax.Java.FormalParameter)) continue;
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
