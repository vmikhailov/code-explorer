using System.Text.RegularExpressions;
using System.Collections.Concurrent;
using CodeExplorer.Core.Parser;

[assembly: ParserAssembly]

namespace CodeExplorer.Parser.Python;

public class PythonParser : IProjectParser, IFileParser
{
    public string LanguageName => "python";

    public string ProjectType => "python";

    public IReadOnlyCollection<string> ExcludedFolders => ["venv", ".venv", "__pycache__"];

    public IReadOnlyList<ILibraryParser> LibraryParsers { get; } =
    [
        new Libraries.ChromaDbLibraryParser(),
        new Libraries.CouchDbPythonLibraryParser(),
        new Libraries.ElasticsearchPythonLibraryParser(),
        new Libraries.MysqlConnectorPythonLibraryParser(),
        new Libraries.PeeweeLibraryParser(),
        new Libraries.PineconeLibraryParser(),
        new Libraries.Psycopg2LibraryParser(),
        new Libraries.PyMongoLibraryParser(),
        new Libraries.PyMysqlLibraryParser(),
        new Libraries.PythonRedisLibraryParser(),
        new Libraries.PythonSqlite3LibraryParser(),
        new Libraries.SqlAlchemyLibraryParser(),

        // Generic Cloud Services
        new GenericLibraryParser("stripe", "Stripe", "cloud", ["stripe"]),
        new GenericLibraryParser("aws", "AWS", "cloud", ["boto3"]),
        new GenericLibraryParser("gcp", "GCP", "cloud", ["google-cloud-", "google.cloud", "firebase-admin"]),
        new GenericLibraryParser("azure", "Azure", "cloud", ["azure-", "azure."]),

        // Generic Frameworks
        new GenericLibraryParser("django", "Django", "framework", ["django"]),
        new GenericLibraryParser("flask", "Flask", "framework", ["flask"]),
        new GenericLibraryParser("fastapi", "FastAPI", "framework", ["fastapi"]),

        // Generic API Clients
        new GenericLibraryParser("requests", "requests", "api", ["requests"]),
        new GenericLibraryParser("urllib", "requests", "api", ["urllib.request", "urllib3", "urllib"], isBuiltIn: true),
        new GenericLibraryParser("httpx", "httpx", "api", ["httpx"]),
        new GenericLibraryParser("aiohttp", "aiohttp", "api", ["aiohttp"]),
    ];

    public PythonParser()
    {
    }

    public bool CanParse(string fileExtension)
    {
        return fileExtension.Equals(".py", StringComparison.OrdinalIgnoreCase);
    }

    public bool IsProjectDirectory(string directoryPath, string[] filesInDirectory)
    {
        foreach (var file in filesInDirectory)
        {
            var fileName = Path.GetFileName(file).ToLowerInvariant();
            if (fileName is "requirements.txt" or "pyproject.toml" or "setup.py" or "setup.cfg")
            {
                return true;
            }
        }
        return false;
    }

    public BaseParserVisitor CreateVisitor(
        TreeSitter.Node rootNode,
        List<ILibraryParser> activeLibraryParsers,
        string relativePath,
        string absoluteWorkspacePath,
        IFileParser fileParser,
        LibraryTrieRegistry libraryRegistry)
    {
        return new PythonFileVisitor(
            rootNode,
            activeLibraryParsers,
            this,
            relativePath,
            absoluteWorkspacePath,
            fileParser,
            libraryRegistry
        );
    }

    public async Task<ProducedPackageInfo?> GetProducedPackageAsync(string projectDirectory)
    {
        var pyprojectPath = Path.Combine(projectDirectory, "pyproject.toml");
        if (File.Exists(pyprojectPath))
        {
            try
            {
                var lines = await File.ReadAllLinesAsync(pyprojectPath);
                string? name = null;
                var version = "1.0.0";

                var inProjectSection = false;
                foreach (var rawLine in lines)
                {
                    var line = rawLine.Trim();
                    if (line.StartsWith("[project]") || line.StartsWith("[tool.poetry]"))
                    {
                        inProjectSection = true;
                        continue;
                    }
                    else if (line.StartsWith("[") && line.EndsWith("]"))
                    {
                        inProjectSection = false;
                    }

                    if (inProjectSection)
                      {
                        if (line.StartsWith("name"))
                        {
                            var parts = line.Split('=', 2);
                            if (parts.Length == 2)
                            {
                                name = parts[1].Trim(' ', '"', '\'');
                            }
                        }
                        else if (line.StartsWith("version"))
                        {
                            var parts = line.Split('=', 2);
                            if (parts.Length == 2)
                            {
                                version = parts[1].Trim(' ', '"', '\'');
                            }
                        }
                    }
                }

                if (!string.IsNullOrEmpty(name))
                {
                    return new ProducedPackageInfo(name, version, "pip");
                }
            }
            catch
            {
                // Ignore
            }
        }

        var setupPyPath = Path.Combine(projectDirectory, "setup.py");
        if (File.Exists(setupPyPath))
        {
            try
            {
                var content = await File.ReadAllTextAsync(setupPyPath);
                var nameMatch = Regex.Match(content, @"name\s*=\s*['""]([^'""]+)['""]");
                if (nameMatch.Success)
                {
                    var name = nameMatch.Groups[1].Value;
                    var versionMatch = Regex.Match(content, @"version\s*=\s*['""]([^'""]+)['""]");
                    var version = versionMatch.Success ? versionMatch.Groups[1].Value : "1.0.0";

                    return new ProducedPackageInfo(name, version, "pip");
                }
            }
            catch
            {
                // Ignore
            }
        }

        return null;
    }

    public async Task<ProjectDependencyInfo> ParseDependenciesAsync(string projectDirectory)
    {
        var localProjectPaths = new List<string>();
        var externalPackages = new List<ProducedPackageInfo>();

        // 1. Try parsing pyproject.toml dependencies
        var pyprojectPath = Path.Combine(projectDirectory, "pyproject.toml");
        if (File.Exists(pyprojectPath))
        {
            try
            {
                var lines = await File.ReadAllLinesAsync(pyprojectPath);
                var inDependencies = false;
                foreach (var rawLine in lines)
                {
                    var line = rawLine.Trim();
                    if (string.IsNullOrEmpty(line)) continue;

                    if (line.StartsWith("[project.dependencies]") || line.StartsWith("[tool.poetry.dependencies]"))
                    {
                        inDependencies = true;
                        continue;
                    }
                    else if (line.StartsWith("[") && line.EndsWith("]"))
                    {
                        inDependencies = false;
                    }

                    if (inDependencies)
                    {
                        var parts = line.Split('=', 2);
                        if (parts.Length >= 1)
                        {
                            var name = parts[0].Trim();
                            if (name.ToLowerInvariant() == "python") continue; // skip python version constraint

                            var version = parts.Length == 2 ? parts[1].Trim(' ', '"', '\'') : "unknown";
                            externalPackages.Add(new ProducedPackageInfo(name, version, "pip"));
                        }
                    }
                }
            }
            catch
            {
                // Ignore
            }
        }

        // 2. Try parsing requirements.txt dependencies if externalPackages is empty
        if (externalPackages.Count == 0)
        {
            var reqPath = Path.Combine(projectDirectory, "requirements.txt");
            if (File.Exists(reqPath))
            {
                try
                {
                    var lines = await File.ReadAllLinesAsync(reqPath);
                    foreach (var rawLine in lines)
                    {
                        var line = rawLine.Trim();
                        if (string.IsNullOrEmpty(line) || line.StartsWith("#") || line.StartsWith("-")) continue;

                        // Parse package name and specifier, e.g. requests>=2.25.1 -> name = requests, version = >=2.25.1
                        var match = Regex.Match(line, @"^([a-zA-Z0-9_\-\[\]]+)(.*)$");
                        if (match.Success)
                        {
                            var name = match.Groups[1].Value;
                            var versionSpec = match.Groups[2].Value.Trim();
                            var version = string.IsNullOrEmpty(versionSpec) ? "unknown" : versionSpec;
                            externalPackages.Add(new ProducedPackageInfo(name, version, "pip"));
                        }
                    }
                }
                catch
                {
                    // Ignore
                }
            }
        }

        return new ProjectDependencyInfo(localProjectPaths, externalPackages);
    }

    public bool UsesTreeSitter => true;
    public async Task<SyntaxTree> ParseAsync(string filePath, string parentNodeId, string workspaceId, string absoluteWorkspacePath)
    {
        var relativePath = Path.GetRelativePath(absoluteWorkspacePath, filePath).Replace('\\', '/');
        return await SyntaxTree.ParseAsync(filePath, relativePath, parentNodeId, this, workspaceId, absoluteWorkspacePath);
    }

    public ISyntaxEnricher GetSyntaxEnricher(SyntaxTree syntaxTree) => new SyntaxEnricher(LibraryParsers, syntaxTree);

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
            var internalNames = _pyRootCache.GetOrAdd(projectRoot, r => LoadLocalPythonNames(r));
            var parts = importPath.Split('.');
            var firstSegment = parts[0];

            if (internalNames.Contains(firstSegment))
            {
                return ImportType.Internal;
            }
        }

        return ImportType.External;
    }

    private string? FindPythonProjectRoot(string? dir, string? workspaceRoot)
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

    private HashSet<string> LoadLocalPythonNames(string projectRoot)
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
        catch
        {
            // Ignore
        }
        return names;
    }

}
