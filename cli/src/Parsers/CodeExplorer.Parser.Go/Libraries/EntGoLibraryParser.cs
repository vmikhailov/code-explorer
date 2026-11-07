using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.Go.Libraries;

public class EntGoLibraryParser : ISemanticExtension
{
    private const string EntTargetPrefix = "ent:";

    public string Name => "Ent (Go ORM)";
    public string Id => "ent-go";
    public SemanticExtensionType Type => SemanticExtensionType.RelationalDb;
    public IReadOnlyList<string> SupportedPatterns => ["entgo.io/ent*"];
    public bool IsImplemented => true;
    public LibraryRole LibraryRole => LibraryRole.OrmOrDatabase;

    private static readonly HashSet<string> EntTerminalMethods = new(StringComparer.Ordinal)
    {
        "All", "First", "Only", "Count", "Save", "SaveX", "Exec", "ExecX"
    };

    public string? MapNodeType(Node node, ParsingContext ctx) => null;
    public string? ExtractIdentifier(Node node, ParsingContext ctx) => null;

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (!node.Is(TreeSitterSyntax.Go.CallExpression)) return;

        var func = node.GetFunctionNode();
        if (!func.IsValid() || !func.Is(TreeSitterSyntax.Go.SelectorExpression)) return;

        var field = func.GetChildForField(TreeSitterSyntax.Fields.Field);
        var operand = func.GetChildForField(TreeSitterSyntax.Fields.Operand);
        if (!field.IsValid() || !operand.IsValid()) return;

        if (EntTerminalMethods.Contains(field.Text) &&
            (operand.Text.Contains(".Create(", StringComparison.Ordinal) ||
             operand.Text.Contains(".Query(", StringComparison.Ordinal) ||
             operand.Text.Contains(".Update(", StringComparison.Ordinal) ||
             operand.Text.Contains(".Delete(", StringComparison.Ordinal)))
        {
            references.Add(new Reference(scopeSymbolId, EntTargetPrefix + field.Text.ToLowerInvariant(), OntologyConstants.Relationships.UsesDb));
        }
    }
}
