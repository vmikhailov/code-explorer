using System.Collections.Concurrent;
using CodeExplorer.Core.Parser.Syntax;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.Python;

public class PythonFileParser : IFileParser
{
    public string LanguageName => "python";

    internal static readonly IReadOnlyList<ISemanticExtension> DefaultExtensions =
    [
        new Libraries.AioHttpPythonLibraryParser(),
        new Libraries.CeleryLibraryParser(),
        new Libraries.DramatiqLibraryParser(),
        new Libraries.RqRedisStreamsLibraryParser(),
        new Libraries.ChromaDbLibraryParser(),
        new Libraries.CouchDbPythonLibraryParser(),
        new Libraries.DjangoLibraryParser(),
        new Libraries.ElasticsearchPythonLibraryParser(),
        new Libraries.FastApiLibraryParser(),
        new Libraries.LitestarLibraryParser(),
        new Libraries.StrawberryGraphQlLibraryParser(),
        new Libraries.FlaskLibraryParser(),
        new Libraries.GrpcPythonLibraryParser(),
        new Libraries.HttpxPythonLibraryParser(),
        new Libraries.MysqlConnectorPythonLibraryParser(),
        new Libraries.PeeweeLibraryParser(),
        new Libraries.PineconeLibraryParser(),
        new Libraries.Psycopg2LibraryParser(),
        new Libraries.PyMongoLibraryParser(),
        new Libraries.PyMysqlLibraryParser(),
        new Libraries.PythonRedisLibraryParser(),
        new Libraries.PythonSqlite3LibraryParser(),
        new Libraries.RequestsPythonLibraryParser(),
        new Libraries.SqlAlchemyLibraryParser(),
        new Libraries.SqlModelLibraryParser(),
        new Libraries.TortoiseOrmLibraryParser(),
        new Libraries.MotorMongoLibraryParser(),
        new Libraries.Boto3LibraryParser(),
        new Libraries.TemporalPythonLibraryParser(),
        new Libraries.AiSdkPythonLibraryParser(),
        new Libraries.FastStreamLibraryParser(),
        new Libraries.KafkaPythonLibraryParser(),
        new Libraries.PikaRabbitMqLibraryParser(),
        new Libraries.SchedulerOrchestrationPythonLibraryParser(),
        new Libraries.AsyncWebPythonLibraryParser(),
        new Libraries.AsyncPgPsycopg3LibraryParser(),
        new Libraries.ClickHousePythonLibraryParser(),
        new Libraries.VectorDbPythonLibraryParser(),
        new Libraries.AgenticAiPythonLibraryParser(),
        new Libraries.DiContainerPythonLibraryParser(),
        Libraries.PythonTestLibraryParser.Instance
    ];

    internal static readonly IReadOnlyList<PackageDescriptor> DefaultPackages =
        [.. DefaultExtensions.SelectMany(e => e.GetPackageDescriptors())];

    public IReadOnlyList<ISemanticExtension> SemanticExtensions => DefaultExtensions;

    public bool UsesTreeSitter => true;

    public SyntaxDocument? ParseDocument(string sourceText, string filePath = "", string relativePath = "") =>
        PythonSyntaxConverter.Instance.Parse(sourceText, LanguageName, filePath, relativePath);

    public LanguageSyntaxProfile SyntaxProfile => PythonSyntaxProfile.Instance;

    public bool CanParse(string fileExtension)
    {
        return fileExtension.Equals(".py", StringComparison.OrdinalIgnoreCase);
    }

    public void ExtractDeclarations(Node rootNode, Action<string, Node?, string?> registerDeclaration)
    {
        PythonDeclarationExtractor.Extract(rootNode, registerDeclaration);
    }

    public BaseParserVisitor CreateVisitor(
        Node rootNode,
        List<ISemanticExtension> activeExtensions,
        string relativePath,
        string absoluteWorkspacePath,
        IFileParser fileParser,
        SemanticExtensionRegistry extensionRegistry)
    {
        return new PythonFileVisitor(
            rootNode,
            activeExtensions,
            relativePath,
            absoluteWorkspacePath,
            fileParser,
            extensionRegistry
        );
    }

    public async Task<SyntaxTree> ParseAsync(string filePath, string parentNodeId, string workspaceId, string absoluteWorkspacePath)
    {
        var relativePath = Path.GetRelativePath(absoluteWorkspacePath, filePath).Replace('\\', '/');
        return await SyntaxTree.ParseAsync(filePath, relativePath, parentNodeId, this, workspaceId, absoluteWorkspacePath);
    }

    private readonly ConcurrentDictionary<string, HashSet<string>> _pyRootCache = new(StringComparer.OrdinalIgnoreCase);

    public ImportType ResolveImportType(string importPath, string filePath, string? absoluteWorkspacePath)
    {
        return ResolvePyImportType(importPath, filePath, absoluteWorkspacePath);
    }

    public ImportType ResolvePyImportType(string importPath, string filePath, string? absoluteWorkspacePath)
    {
        if (string.IsNullOrEmpty(importPath)) return ImportType.External;

        // Python relative imports start with '.'
        if (importPath.StartsWith('.'))
            return ImportType.Internal;

        var dir = Path.GetDirectoryName(filePath);
        var projectRoot = FindPythonProjectRoot(dir, absoluteWorkspacePath);
        if (projectRoot != null)
        {
            var internalNames = _pyRootCache.GetOrAdd(projectRoot, LoadLocalPythonNames);
            var parts = importPath.Split('.');
            var firstSegment = parts[0];

            if (internalNames.Contains(firstSegment))
            {
                return ImportType.Internal;
            }
        }

        return ImportType.External;
    }

    private static string? FindPythonProjectRoot(string? dir, string? workspaceRoot)
    {
        var current = dir;
        string? bestRoot = null;
        while (current != null)
        {
            if (File.Exists(Path.Combine(current, "requirements.txt")) ||
                File.Exists(Path.Combine(current, "pyproject.toml")) ||
                File.Exists(Path.Combine(current, "setup.py")) ||
                Directory.Exists(Path.Combine(current, ".git")))
            {
                return current;
            }
            if (workspaceRoot != null && current.Replace('\\', '/').Equals(workspaceRoot.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase))
            {
                bestRoot = current;
            }
            current = Path.GetDirectoryName(current);
        }
        return bestRoot ?? dir;
    }

    private static HashSet<string> LoadLocalPythonNames(string projectRoot)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            if (Directory.Exists(projectRoot))
            {
                foreach (var d in Directory.GetDirectories(projectRoot))
                {
                    var name = Path.GetFileName(d);
                    var lower = name.ToLowerInvariant();
                    if (lower == "venv" || lower == "env" || lower == ".venv" || lower == "build" || lower == "dist" || lower == ".git")
                        continue;
                    names.Add(name);
                }
                foreach (var f in Directory.GetFiles(projectRoot, "*.py"))
                {
                    var name = Path.GetFileNameWithoutExtension(f);
                    names.Add(name);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Ignore
        }
        return names;
    }
}
