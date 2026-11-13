using CodeExplorer.Core.Parser.Syntax;

namespace CodeExplorer.Parser.TypeScript;

public class JavaScriptFileParser : IFileParser
{
    private readonly TypeScriptFileParser _tsParser = new();

    public string LanguageName => "javascript";

    public IReadOnlyList<ISemanticExtension> SemanticExtensions => TypeScriptFileParser.DefaultExtensions;

    public bool UsesTreeSitter => true;

    public SyntaxDocument? ParseDocument(string sourceText, string filePath = "", string relativePath = "") =>
        TypeScriptSyntaxConverter.Instance.Parse(sourceText, LanguageName, filePath, relativePath);

    public LanguageSyntaxProfile SyntaxProfile => TypeScriptSyntaxProfile.Instance;

    public bool CanParse(string fileExtension)
    {
        return fileExtension.Equals(".js", StringComparison.OrdinalIgnoreCase) ||
               fileExtension.Equals(".jsx", StringComparison.OrdinalIgnoreCase) ||
               fileExtension.Equals(".mjs", StringComparison.OrdinalIgnoreCase) ||
               fileExtension.Equals(".cjs", StringComparison.OrdinalIgnoreCase);
    }

    public void ExtractDeclarations(CodeExplorer.Core.Parser.Syntax.SyntaxNode rootNode, Action<string, CodeExplorer.Core.Parser.Syntax.SyntaxNode?, string?> registerDeclaration)
    {
        TypeScriptDeclarationExtractor.Extract(rootNode, registerDeclaration);
    }

    public bool ShouldSkipFileName(string fileName) => _tsParser.ShouldSkipFileName(fileName);
    public bool ShouldSkipFileBytes(string fileName, ReadOnlySpan<byte> contentBytes) => _tsParser.ShouldSkipFileBytes(fileName, contentBytes);
    public bool ShouldSkipFile(FileInfo fileInfo) => _tsParser.ShouldSkipFile(fileInfo);
    public string ClassifyArtifactRole(string relativePath, string fileName, ReadOnlySpan<byte> contentBytes = default) =>
        _tsParser.ClassifyArtifactRole(relativePath, fileName, contentBytes);
    public void EnrichFileSignals(
        CodeExplorer.Core.Common.Nodes.Layer1_Physical.FileNode fileNode,
        SyntaxTree syntaxTree,
        CodeExplorer.Core.Common.Nodes.Layer2_Boundaries.ProjectNode? project,
        string sourceText,
        ParsingContext? ctx) =>
        _tsParser.EnrichFileSignals(fileNode, syntaxTree, project, sourceText, ctx);

    public async Task<SyntaxTree> ParseAsync(string filePath, string parentNodeId, string workspaceId, string absoluteWorkspacePath)
    {
        var relativePath = Path.GetRelativePath(absoluteWorkspacePath, filePath).Replace('\\', '/');
        return await SyntaxTree.ParseAsync(filePath, relativePath, parentNodeId, this, workspaceId, absoluteWorkspacePath);
    }

    public BaseParserVisitor CreateVisitor(
        CodeExplorer.Core.Parser.Syntax.SyntaxNode rootNode,
        List<ISemanticExtension> activeExtensions,
        string relativePath,
        string absoluteWorkspacePath,
        IFileParser fileParser,
        SemanticExtensionRegistry extensionRegistry)
    {
        return _tsParser.CreateVisitor(
            rootNode,
            activeExtensions,
            relativePath,
            absoluteWorkspacePath,
            fileParser,
            extensionRegistry
        );
    }

    public ImportType ResolveImportType(string importPath, string filePath, string? absoluteWorkspacePath)
    {
        return _tsParser.ResolveImportType(importPath, filePath, absoluteWorkspacePath);
    }
}
