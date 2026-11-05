using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Parser.Syntax;

namespace CodeExplorer.Core.Parser;

public class SyntaxTree : IDisposable
{
    public string FilePath { get; }
    public string RelativePath { get; }
    public SyntaxDocument? Document { get; private set; }
    public SyntaxDocument? Tree => Document;
    public SyntaxNode? RootNode => Document?.RootNode;
    public FileNode FileNode { get; }
    public IFileParser FileParser { get; }
    public List<RawImport> RawImports { get; }
    public List<RawVariable> RawVariables { get; }
    public List<RawTypeBinding> RawTypeBindings { get; }
    public SyntacticSymbol? RootSymbol { get; set; }
    public List<SyntacticSymbol> SemanticSymbols { get; } = [];

    public SyntaxTree(
        string filePath,
        string relativePath,
        SyntaxDocument? document,
        FileNode fileNode,
        IFileParser fileParser,
        List<RawImport> rawImports,
        List<RawVariable> rawVariables,
        List<RawTypeBinding> rawTypeBindings)
    {
        FilePath = filePath;
        RelativePath = relativePath;
        Document = document;
        FileNode = fileNode;
        FileParser = fileParser;
        RawImports = rawImports;
        RawVariables = rawVariables;
        RawTypeBindings = rawTypeBindings;
        CodeExplorer.Core.Parser.Layers.Layer3FileSignalEnricher.EnrichFileDatabaseSignals(fileNode, document?.SourceText, filePath);
        if (!string.IsNullOrEmpty(document?.SourceText))
        {
            fileParser?.EnrichFileSignals(fileNode, this, null, document.SourceText, null);
        }
    }

    public SyntaxTree(
        string filePath,
        string relativePath,
        SyntaxDocument? document,
        object? unusedParser,
        object? unusedLanguage,
        FileNode fileNode,
        IFileParser fileParser,
        List<RawImport> rawImports,
        List<RawVariable> rawVariables,
        List<RawTypeBinding> rawTypeBindings)
        : this(filePath, relativePath, document, fileNode, fileParser, rawImports, rawVariables, rawTypeBindings)
    {
    }

    public void Dispose()
    {
        Document?.Dispose();
        Document = null;
        RootSymbol = null;
        SemanticSymbols.Clear();
    }

    public static SyntaxDocument? ParseText(
        string sourceText,
        string languageName,
        string filePath = "",
        string relativePath = "",
        LanguageSyntaxProfile? profile = null)
    {
        var parser = WorkspaceIndexer.GetParserByLanguage(languageName)
                     ?? (!string.IsNullOrEmpty(filePath) ? WorkspaceIndexer.GetParserForFile(filePath) : null);
        if (parser != null)
        {
            return parser.ParseDocument(sourceText, filePath, relativePath);
        }
        return SyntaxDocument.Create(sourceText, languageName, filePath, relativePath);
    }

    public static async Task<SyntaxTree> ParseAsync(
        string filePath,
        string relativePath,
        string parentNodeId,
        IFileParser fileParser,
        string workspaceId,
        string absoluteWorkspacePath)
    {
        filePath = filePath.Replace('\\', '/');
        relativePath = relativePath.Replace('\\', '/');
        var sourceText = await PhysicalFileStore.ConsumeFileTextAsync(filePath);
        var document = fileParser.ParseDocument(sourceText, filePath, relativePath);

        var fileNodeId = $"{workspaceId}:{OntologyConstants.IdPrefixes.File}:{relativePath}";
        var fileNode = new FileNode(fileNodeId, Path.GetFileName(filePath), relativePath, filePath);

        return new SyntaxTree(
            filePath,
            relativePath,
            document,
            fileNode,
            fileParser,
            [],
            [],
            []);
    }
}
