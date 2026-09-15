using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.CSharp.Libraries;

public class HotChocolateLibraryParser : ILibraryParser
{
    public string Name => "HotChocolate GraphQL";
    public string Id => "hotchocolate";
    public string Type => "framework";
    public IReadOnlyList<string> SupportedPatterns => ["HotChocolate", "HotChocolate.Types", "HotChocolate.AspNetCore"];
    public bool IsImplemented => true;
    public bool IsBuiltIn => true;

    private static readonly HashSet<string> GraphQlAttributes =
    [
        "Query", "Mutation", "Subscription", "ExtendObjectType", "GraphQLName",
        "QueryAttribute", "MutationAttribute", "SubscriptionAttribute", "ExtendObjectTypeAttribute"
    ];

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (IsGraphQlAttribute(node)) return OntologyConstants.NodeLabels.EntryPoint;
        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx)
    {
        if (IsGraphQlAttribute(node))
        {
            var attrName = GetAttributeName(node);
            var opType = attrName switch
            {
                "Mutation" or "MutationAttribute" => "MUTATION",
                "Subscription" or "SubscriptionAttribute" => "SUBSCRIPTION",
                _ => "QUERY"
            };

            var parentDecl = node.Parent?.Parent;
            if (parentDecl.IsValid() && parentDecl.Is(TreeSitterSyntax.CSharp.MethodDeclaration))
            {
                var nameNode = parentDecl.GetField(TreeSitterSyntax.Fields.Name);
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
        if (!IsGraphQlAttribute(node)) return;

        symbol.Protocol = "GraphQL";
        var attrName = GetAttributeName(node);
        symbol.OperationType = attrName switch
        {
            "Mutation" or "MutationAttribute" => "Mutation",
            "Subscription" or "SubscriptionAttribute" => "Subscription",
            _ => "Query"
        };

        var parentDecl = node.Parent?.Parent;
        if (parentDecl.IsValid() && parentDecl.Is(TreeSitterSyntax.CSharp.MethodDeclaration))
        {
            // Response type
            var typeNode = parentDecl.GetField("returns")
                           ?? parentDecl.GetField(TreeSitterSyntax.Fields.Type)
                           ?? parentDecl.GetField("return_type")
                           ?? parentDecl.Children.FirstOrDefault(c => c.IsAny(TreeSitterSyntax.CSharp.GenericName, TreeSitterSyntax.CSharp.TypeIdentifier, TreeSitterSyntax.CSharp.QualifiedName, "predefined_type", "nullable_type", "generic_name", "type_identifier"));
            if (typeNode.IsValid())
            {
                var ret = CleanTypeName(typeNode.Text);
                if (!string.IsNullOrEmpty(ret) && ret != "void" && ret != "Task" && ret != "ValueTask")
                {
                    symbol.ResponseType = ret;
                }
            }

            // Request type
            var paramList = parentDecl.GetField(TreeSitterSyntax.Fields.Parameters)
                            ?? parentDecl.FindChildOfType(TreeSitterSyntax.CSharp.ParameterList);
            if (paramList.IsValid())
            {
                foreach (var param in paramList.Children)
                {
                    if (!param.Is(TreeSitterSyntax.CSharp.Parameter)) continue;
                    var pType = param.GetField(TreeSitterSyntax.Fields.Type)
                                ?? param.Children.FirstOrDefault(c => c.IsAny(TreeSitterSyntax.CSharp.GenericName, TreeSitterSyntax.CSharp.TypeIdentifier, TreeSitterSyntax.CSharp.QualifiedName, "predefined_type", "nullable_type", "generic_name", "type_identifier"));
                    if (pType.IsValid())
                    {
                        var pTypeName = CleanTypeName(pType.Text);
                        if (!IsPrimitiveOrSystemType(pTypeName) && symbol.RequestType == null)
                        {
                            symbol.RequestType = pTypeName;
                            break;
                        }
                    }
                }
            }
        }
    }

    private static bool IsGraphQlAttribute(Node node)
    {
        if (node.Is(TreeSitterSyntax.CSharp.Attribute))
        {
            var name = GetAttributeName(node);
            return GraphQlAttributes.Contains(name);
        }
        return false;
    }

    private static string GetAttributeName(Node node)
    {
        var nameNode = node.GetField(TreeSitterSyntax.Fields.Name)
                       ?? node.FindChildOfType(TreeSitterSyntax.Common.Identifier);
        return nameNode.IsValid() ? nameNode.Text : "";
    }

    private static string CleanTypeName(string rawType)
    {
        if (string.IsNullOrWhiteSpace(rawType)) return "";
        var type = rawType.Trim();
        while (true)
        {
            var genericIdx = type.IndexOf('<');
            if (genericIdx > 0 && type.EndsWith('>'))
            {
                var outer = type.Substring(0, genericIdx).Trim();
                if (outer is "Task" or "ValueTask" or "IQueryable" or "IEnumerable" or "List" or "IList")
                {
                    type = type.Substring(genericIdx + 1, type.Length - genericIdx - 2).Trim();
                    continue;
                }
            }
            break;
        }
        return type;
    }

    private static bool IsPrimitiveOrSystemType(string type)
    {
        return type is "int" or "long" or "string" or "bool" or "double" or "float" or "decimal" or "Guid" or "DateTime" or "DateTimeOffset" or "CancellationToken" or "ClaimsPrincipal" or "object";
    }
}
