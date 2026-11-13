using CodeExplorer.Core.Common;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Parser;
using CodeExplorer.Core.Parser.Syntax;
using CodeExplorer.Parser.Terraform.Hcl;

namespace CodeExplorer.Parser.Terraform;

/// <summary>
/// Stateless Layer 3 file parser for Terraform (<c>.tf</c>, <c>.tfvars</c>), OpenTofu (<c>.tofu</c>),
/// and Terragrunt (<c>.hcl</c>) files.
/// </summary>
public sealed class TerraformFileParser : IFileParser
{
    private const string TerraformLanguage = "terraform";
    private const string ExtTf = ".tf";
    private const string ExtTofu = ".tofu";
    private const string ExtTfvars = ".tfvars";
    private const string ExtHcl = ".hcl";
    private const string LockFileSuffix = ".terraform.lock.hcl";
    private const string TflintFileSuffix = ".tflint.hcl";

    public string LanguageName => TerraformLanguage;

    public IReadOnlyList<ISemanticExtension> SemanticExtensions => [];

    public bool UsesTreeSitter => false;

    public bool CanParse(string fileExtension)
    {
        return fileExtension.Equals(ExtTf, StringComparison.OrdinalIgnoreCase) ||
               fileExtension.Equals(ExtTofu, StringComparison.OrdinalIgnoreCase) ||
               fileExtension.Equals(ExtTfvars, StringComparison.OrdinalIgnoreCase);
    }

    public bool CanParseFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return false;
        var ext = Path.GetExtension(filePath);
        if (CanParse(ext)) return true;

        if (ext.Equals(ExtHcl, StringComparison.OrdinalIgnoreCase))
        {
            var fileName = Path.GetFileName(filePath);
            return !fileName.Equals(LockFileSuffix, StringComparison.OrdinalIgnoreCase) &&
                   !fileName.Equals(TflintFileSuffix, StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    public BaseParserVisitor CreateVisitor(
        SyntaxNode rootNode,
        List<ISemanticExtension> activeExtensions,
        string relativePath,
        string absoluteWorkspacePath,
        IFileParser fileParser,
        SemanticExtensionRegistry extensionRegistry)
    {
        throw new NotSupportedException("TerraformFileParser uses the Superpower HCL combinator parser instead of TreeSitter visitors.");
    }

    public ImportType ResolveImportType(string importPath, string filePath, string? absoluteWorkspacePath) =>
        TerraformPathResolver.IsLocalModuleSource(importPath) ? ImportType.Internal : ImportType.External;

    public async Task<SyntaxTree> ParseAsync(
        string filePath,
        string parentNodeId,
        string workspaceId,
        string absoluteWorkspacePath)
    {
        var relativePath = Path.GetRelativePath(absoluteWorkspacePath, filePath).Replace('\\', '/');
        var fileNodeId = $"{workspaceId}:{OntologyConstants.IdPrefixes.File}:{relativePath}";
        var fileNode = new FileNode(fileNodeId, Path.GetFileName(filePath), relativePath, filePath);

        var sourceText = await PhysicalFileStore.ConsumeFileTextAsync(filePath);
        var hclFile = HclParser.Parse(sourceText, relativePath);
        var moduleDir = TerraformDeclarationMapper.GetModuleDir(relativePath);

        var declarations = TerraformDeclarationMapper.MapDeclarations(
            hclFile,
            workspaceId,
            fileNodeId,
            relativePath,
            moduleDir,
            fileNode.Children);

        var endLine = hclFile.Blocks.Count > 0 ? hclFile.Blocks[^1].EndLine : 1;
        var hclDocNode = new HclDocumentSyntaxNode(hclFile, declarations, endLine);
        var doc = SyntaxDocument.Create(sourceText, LanguageName, filePath, relativePath, [hclDocNode]);

        return new SyntaxTree(filePath, relativePath, doc, fileNode, this, [], [], []);
    }
}
