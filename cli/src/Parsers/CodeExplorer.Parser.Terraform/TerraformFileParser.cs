using CodeExplorer.Core.Common;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Parser;
using CodeExplorer.Core.Parser.Syntax;
using CodeExplorer.Cypher.Common;
using CodeExplorer.Parser.Terraform.Hcl;

namespace CodeExplorer.Parser.Terraform;

/// <summary>
/// Layer 3 file parser for Terraform / OpenTofu / Terragrunt (HCL). Pure C# (UsesTreeSitter =>
/// false): it parses HCL in ParseAsync and builds graph nodes directly, rather than using a
/// tree-sitter grammar/visitor.
/// </summary>
public class TerraformFileParser : IFileParser
{
    public string LanguageName => "hcl";

    public IReadOnlyList<ISemanticExtension> SemanticExtensions => [];

    // HCL is parsed in ParseAsync, never via a tree-sitter visitor.
    public BaseParserVisitor CreateVisitor(
        CodeExplorer.Core.Parser.Syntax.SyntaxNode rootNode,
        List<ISemanticExtension> activeExtensions,
        string relativePath,
        string absoluteWorkspacePath,
        IFileParser fileParser,
        SemanticExtensionRegistry extensionRegistry)
        => throw new NotSupportedException("Terraform parser uses pure C# HCL analysis (no TreeSitter visitor).");

    public bool UsesTreeSitter => false;

    public bool CanParse(string fileExtension) => TerraformFiles.IsTerraformExtension(fileExtension);

    // `.terraform.lock.hcl` is generated, not Terraform source; rejected by name. The `.hcl`
    // extension is otherwise owned for Terragrunt (terragrunt.hcl and included *.hcl fragments).
    public bool CanParseFile(string filePath) =>
        CanParse(Path.GetExtension(filePath)) && !TerraformFiles.IsLockFile(filePath);

    // Parses the HCL file, emits TypeNode/MemberNode declarations into the FileNode with
    // workspace-scoped URN IDs, and returns a TerraformSyntaxTree carrying the declared symbols and
    // harvested references for the enricher (cross-file resolution cannot happen per file).
    public async Task<SyntaxTree> ParseAsync(string filePath, string parentNodeId, string workspaceId, string absoluteWorkspacePath)
    {
        var relativePath = Path.GetRelativePath(absoluteWorkspacePath, filePath).NormalizeSlashes();
        var fileName = Path.GetFileName(filePath);
        var fileNodeId = $"{workspaceId}:{OntologyConstants.IdPrefixes.File}:{relativePath}";
        var fileNode = new FileNode(fileNodeId, fileName, relativePath, filePath);

        // A lock file may still reach here via the extension-only gate; keep its File node for
        // physical topology but do not parse its contents into declarations.
        if (TerraformFiles.IsLockFile(filePath))
        {
            return new TerraformSyntaxTree(filePath, relativePath, null, fileNode, this, [], [], []);
        }

        // Read failures propagate to Layer 3, which logs them and records a scan issue on the file.
        // Syntax errors do not throw: the parser skips past them and reports them as diagnostics,
        // which Layer 3 records as syntax-error scan issues from the document.
        var content = await PhysicalFileStore.ReadAllTextAsync(filePath);
        var ast = HclParser.Parse(content);

        var isTfvars = TerraformDeclarationMapper.IsTfvars(relativePath);
        var symbols = TerraformDeclarationMapper.Map(ast, fileNode, workspaceId, relativePath);
        var collected = TerraformReferenceCollector.Collect(ast, isTfvars);
        symbols.AddRange(TerraformDeclarationMapper.MapAnchors(collected.Anchors, fileNode, workspaceId, relativePath));
        var configures = TerraformDeclarationMapper.MapConfigures(ast, fileNode, workspaceId, relativePath);

        var document = SyntaxDocument.Create(content, LanguageName, filePath, relativePath);
        document.Diagnostics = ast.Diagnostics.Select(ToSyntaxDiagnostic).ToList();

        return new TerraformSyntaxTree(filePath, relativePath, document, fileNode, this, symbols, collected.References, configures);
    }

    // An HCL diagnostic as a document diagnostic: 0-based line, spanning the reported line.
    private static SyntaxDiagnostic ToSyntaxDiagnostic(HclDiagnostic diagnostic)
    {
        var line = Math.Max(0, diagnostic.Line - 1);
        return new SyntaxDiagnostic(SyntaxDiagnosticKind.SyntaxError, diagnostic.Message,
            new TextSpan(0, 0, line, 0, line, 0));
    }

    // Local module sources ("./", "../", "/") are internal; registry/git sources are external.
    public ImportType ResolveImportType(string importPath, string filePath, string? absoluteWorkspacePath)
    {
        if (string.IsNullOrEmpty(importPath)) return ImportType.External;
        return importPath.StartsWith('.') || importPath.StartsWith('/')
            ? ImportType.Internal
            : ImportType.External;
    }
}
