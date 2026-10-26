using CodeExplorer.Core.Common;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using TreeSitter;

namespace CodeExplorer.Core.Parser;

public class SyntaxTree : IDisposable
{
    public string FilePath { get; }
    public string RelativePath { get; }
    public Tree? Tree { get; }
    public TreeSitter.Parser? Parser { get; }
    public Language? Language { get; }
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
        Tree? tree,
        TreeSitter.Parser? parser,
        Language? language,
        FileNode fileNode,
        IFileParser fileParser,
        List<RawImport> rawImports,
        List<RawVariable> rawVariables,
        List<RawTypeBinding> rawTypeBindings)
    {
        FilePath = filePath;
        RelativePath = relativePath;
        Tree = tree;
        Parser = parser;
        Language = language;
        FileNode = fileNode;
        FileParser = fileParser;
        RawImports = rawImports;
        RawVariables = rawVariables;
        RawTypeBindings = rawTypeBindings;
    }

    private bool _disposed;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Tree?.Dispose();
        Parser?.Dispose();
        // Language is cached in _languageCache and reused across all files
    }

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, Language> _languageCache = new(StringComparer.OrdinalIgnoreCase);

        [ThreadStatic]
    private static Dictionary<string, TreeSitter.Parser>? _threadParsers;

    public static TreeSitter.Parser GetParserForThread(string languageName)
    {
        _threadParsers ??= new Dictionary<string, TreeSitter.Parser>(StringComparer.OrdinalIgnoreCase);
        if (!_threadParsers.TryGetValue(languageName, out var parser))
        {
            var language = GetLanguage(languageName);
            parser = new TreeSitter.Parser(language);
            _threadParsers[languageName] = parser;
        }
        return parser;
    }
    public static Language GetLanguage(string languageName) =>
        _languageCache.GetOrAdd(languageName, name => new Language(name));

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
        var sourceText = await File.ReadAllTextAsync(filePath);
        var language = GetLanguage(fileParser.LanguageName);
        var parser = GetParserForThread(fileParser.LanguageName);
        var tree = parser.Parse(sourceText);

        var fileNodeId = $"{workspaceId}:{OntologyConstants.IdPrefixes.File}:{relativePath}";
        var fileNode = new FileNode(fileNodeId, Path.GetFileName(filePath), relativePath, filePath);

        return new SyntaxTree(
            filePath,
            relativePath,
            tree,
            null,
            language,
            fileNode,
            fileParser, [], [], []);
    }
}
