using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Parser;
using CodeExplorer.Core.Parser.Syntax;

namespace CodeExplorer.Parser.Terraform;

/// <summary>
/// Syntax tree for one parsed HCL file. Carries the file's declarations, harvested traversal
/// references, and <c>.tfvars</c> assignments to the project-scoped enricher, which resolves them
/// across the sibling files of the module directory.
///
/// The references are kept here rather than on <c>FileNode.References</c> because node
/// references feed the global name-based resolver, which would bind them to unrelated symbols.
/// </summary>
public sealed class TerraformSyntaxTree : SyntaxTree
{
    public IReadOnlyList<TerraformSymbol> Symbols { get; }
    public IReadOnlyList<TerraformReference> References { get; }
    public IReadOnlyList<TerraformConfigure> Configures { get; }

    public TerraformSyntaxTree(
        string filePath,
        string relativePath,
        SyntaxDocument? document,
        FileNode fileNode,
        IFileParser fileParser,
        IReadOnlyList<TerraformSymbol> symbols,
        IReadOnlyList<TerraformReference> references,
        IReadOnlyList<TerraformConfigure> configures)
        : base(filePath, relativePath, document, fileNode, fileParser, [], [], [])
    {
        Symbols = symbols;
        References = references;
        Configures = configures;
    }
}
