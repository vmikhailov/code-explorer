using CodeExplorer.Core.Parser.Syntax;

namespace CodeExplorer.Parser.Terraform.Hcl;

/// <summary>
/// In-memory syntax node attached to <see cref="SyntaxDocument"/> in Layer 3
/// so Layer 4 semantic enrichment can inspect the parsed HCL AST and declarations statelessly.
/// </summary>
public sealed class HclDocumentSyntaxNode : SyntaxNode
{
    private const string DocumentSyntaxKind = "hcl_document";

    public override SyntaxKind SyntaxKind => SyntaxKind.Document;

    public HclFile HclFile { get; }
    public IReadOnlyList<TerraformSymbol> Declarations { get; }

    public HclDocumentSyntaxNode(
        HclFile hclFile,
        IReadOnlyList<TerraformSymbol> declarations,
        int endLine)
    {
        HclFile = hclFile;
        Declarations = declarations;
        Span = new TextSpan(0, 0, 0, 0, Math.Max(0, endLine - 1), 0);
    }

    protected override string GetCanonicalKindString() => DocumentSyntaxKind;
}
