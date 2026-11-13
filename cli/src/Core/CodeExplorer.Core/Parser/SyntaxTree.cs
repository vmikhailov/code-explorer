using System.Text.RegularExpressions;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Parser.Syntax;

namespace CodeExplorer.Core.Parser;

public class SyntaxTree : IDisposable
{
    private static readonly Regex SchemaCallRegex = new(@"HasDefaultSchema\s*\(\s*[""']?([^""'\)]+)[""']?\s*\)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex SchemaConstRegex = new(@"(?:Schema_?Name|Default_?Schema_?Name|Default_?Schema)\s*(?::\s*[^=]+)?=\s*[""']?([^""';]+)[""']?", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex TypeOrmSchemaRegex = new(@"@(?:Entity|ViewEntity|Table)\s*\(\s*\{[^}]*schema\s*:\s*[""']?([A-Za-z0-9_.]+)[""']?", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex ToTableSchemaRegex = new(@"ToTable\s*\(\s*[^,]+\s*,\s*(?:schema:\s*)?[""']?([A-Za-z0-9_.]+)[""']?\s*\)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex DataSourceSchemaRegex = new(@"\b(?:schema|defaultSchema)\s*:\s*[""']([A-Za-z0-9_-]+)[""']", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex EntityOrTableDeclRegex = new(@"(?::\s*(?:[A-Za-z0-9_.]+\.)?(?:Identity)?DbContext\b|\bI?DbSet\s*<|\b__tablename__\b|@(?:Entity|Table|ViewEntity)\b|\[\s*Table\s*\(|\bdeclarative_base\b|\bmodelBuilder\.Entity\b)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
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
        EnrichFileDatabaseSignals(fileNode, document?.SourceText, filePath);
        if (!string.IsNullOrEmpty(document?.SourceText))
        {
            fileParser?.EnrichFileSignals(fileNode, this, null, document.SourceText);
        }
    }

    public static void EnrichFileDatabaseSignals(FileNode? fileNode, string? sourceText, string filePath)
    {
        if (fileNode == null || string.IsNullOrEmpty(sourceText)) return;

        var signals = fileNode.Signals();
        if (EntityOrTableDeclRegex.IsMatch(sourceText))
        {
            signals.HasEntityOrTable = true;
            fileNode.Flags |= SymbolFlags.HasEntityOrTable;
        }

        var rawSchema = ExtractRawSchemaToken(sourceText);
        if (!string.IsNullOrEmpty(rawSchema))
        {
            signals.DeclaredSchemaRaw = rawSchema;
        }
    }

    private static string? ExtractRawSchemaToken(string text)
    {
        if (!text.Contains("schema", StringComparison.OrdinalIgnoreCase) &&
            !text.Contains("ToTable", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var m1 = SchemaCallRegex.Match(text);
        if (m1.Success) return m1.Groups[1].Value.Trim().Trim('\'', '"');

        var m2 = SchemaConstRegex.Match(text);
        if (m2.Success) return m2.Groups[1].Value.Trim().Trim('\'', '"');

        var m3 = TypeOrmSchemaRegex.Match(text);
        if (m3.Success) return m3.Groups[1].Value.Trim().Trim('\'', '"');

        var m4 = ToTableSchemaRegex.Match(text);
        if (m4.Success) return m4.Groups[1].Value.Trim().Trim('\'', '"');

        var m5 = DataSourceSchemaRegex.Match(text);
        if (m5.Success)
        {
            var val = m5.Groups[1].Value.Trim().Trim('\'', '"');
            if (!val.Equals("true", StringComparison.OrdinalIgnoreCase) &&
                !val.Equals("false", StringComparison.OrdinalIgnoreCase))
            {
                return val;
            }
        }

        return null;
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
        var parser = ParserRegistry.GetParserByLanguage(languageName)
                     ?? (!string.IsNullOrEmpty(filePath) ? ParserRegistry.GetParserForFile(filePath) : null);
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
