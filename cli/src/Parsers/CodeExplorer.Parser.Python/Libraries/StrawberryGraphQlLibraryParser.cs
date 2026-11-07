using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.Python.Libraries;

public class StrawberryGraphQlLibraryParser : ISemanticExtension
{
    public string Name => "Strawberry & Graphene GraphQL";
    public string Id => "strawberry-graphql";
    public SemanticExtensionType Type => SemanticExtensionType.Framework;
    public IReadOnlyList<string> SupportedPatterns => ["strawberry", "strawberry.*", "graphene", "graphene.*"];
    public bool IsImplemented => true;
    public LibraryRole LibraryRole => LibraryRole.WebService;

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        return TryExtractGraphQlField(node, out _, out _) ? OntologyConstants.NodeLabels.EntryPoint : null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx)
    {
        return TryExtractGraphQlField(node, out var kind, out var fieldName) ? $"GRAPHQL:{kind}:{fieldName}" : null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (TryExtractGraphQlField(node, out _, out var fieldName))
        {
            references.Add(new Reference(scopeSymbolId, fieldName, OntologyConstants.Relationships.Triggers));
        }
    }

    private static bool TryExtractGraphQlField(Node node, out string kind, out string fieldName)
    {
        kind = "QUERY";
        fieldName = string.Empty;

        if (!node.Is(TreeSitterSyntax.Python.Decorator)) return false;

        var call = node.FindChildOfType(TreeSitterSyntax.Python.Call);
        var target = call.IsValid()
            ? (call.GetChildForField(TreeSitterSyntax.Fields.Function) ?? (call.Children.Count > 0 ? call.Children[0] : default))
            : node.Children.FirstOrDefault(c => c.Type != "@");
        if (!target.IsValid()) return false;

        var raw = target.Text.TrimStart('@').Trim();
        var isMutation = raw.EndsWith(".mutation", StringComparison.Ordinal) || raw == "mutation";
        var isField = raw.EndsWith("strawberry.field", StringComparison.Ordinal) ||
                      raw.EndsWith("strawberry.subscription", StringComparison.Ordinal);

        if (!isMutation && !isField) return false;

        kind = isMutation ? "MUTATION" : "FIELD";
        var parent = node.Parent;
        var fn = parent.IsValid() ? parent.FindChildOfType(TreeSitterSyntax.Python.FunctionDefinition) : default;
        var nameNode = fn.IsValid() ? fn.GetChildForField(TreeSitterSyntax.Fields.Name) : default;
        if (!nameNode.IsValid() || string.IsNullOrEmpty(nameNode.Text)) return false;

        fieldName = nameNode.Text;
        return true;
    }
}
