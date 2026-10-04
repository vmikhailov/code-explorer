using System.Text.RegularExpressions;
using System.Collections.Concurrent;
using CodeExplorer.Core.Parser;
using TreeSitter;

[assembly: ParserAssembly]

namespace CodeExplorer.Parser.Python;

public class PythonParser : IProjectParser, IFileParser
{
    public string LanguageName => "python";

    public string ProjectType => "python";

    public IReadOnlyCollection<string> ExcludedFolders => ["venv", ".venv", "__pycache__"];

    public bool IsConfigurationFile(string fileName)
    {
        var lower = fileName.ToLowerInvariant();
        return lower.StartsWith(".env");
    }

    public IReadOnlyList<PackageDescriptor> Packages { get; } =
    [
        // Frameworks
        new("django", "Django", LibraryRole.WebFramework, "pypi", ["django"]),
        new("flask", "Flask", LibraryRole.WebFramework, "pypi", ["flask"]),
        new("fastapi", "FastAPI", LibraryRole.WebFramework, "pypi", ["fastapi"]),

        // Databases / ORMs
        new("sqlalchemy", "SQLAlchemy", LibraryRole.OrmOrDatabase, "pypi", ["sqlalchemy"]),
        new("peewee", "Peewee", LibraryRole.OrmOrDatabase, "pypi", ["peewee"]),
        new("psycopg2", "psycopg2", LibraryRole.OrmOrDatabase, "pypi", ["psycopg2", "psycopg"]),
        new("pymysql", "PyMySQL", LibraryRole.OrmOrDatabase, "pypi", ["pymysql"]),
        new("mysql-connector", "MySQL Connector", LibraryRole.OrmOrDatabase, "pypi", ["mysql.connector"]),
        new("pymongo", "PyMongo", LibraryRole.OrmOrDatabase, "pypi", ["pymongo"]),
        new("redis", "Redis", LibraryRole.OrmOrDatabase, "pypi", ["redis"]),
        new("sqlite3", "sqlite3", LibraryRole.OrmOrDatabase, "pypi", ["sqlite3"]),
        new("elasticsearch", "Elasticsearch", LibraryRole.OrmOrDatabase, "pypi", ["elasticsearch"]),
        new("couchdb", "CouchDB", LibraryRole.OrmOrDatabase, "pypi", ["couchdb"]),
        new("chromadb", "ChromaDB", LibraryRole.OrmOrDatabase, "pypi", ["chromadb"]),
        new("pinecone", "Pinecone", LibraryRole.OrmOrDatabase, "pypi", ["pinecone-client", "pinecone"]),
        new("bigquery", "BigQuery", LibraryRole.OrmOrDatabase, "pypi", ["google-cloud-bigquery", "google.cloud.bigquery"]),
        new("clickhouse", "ClickHouse", LibraryRole.OrmOrDatabase, "pypi", ["clickhouse-connect", "clickhouse-driver"]),

        // Generic Cloud Services
        new("stripe", "Stripe", LibraryRole.CloudSdk, "pypi", ["stripe"]),
        new("aws", "AWS", LibraryRole.CloudSdk, "pypi", ["boto3"]),
        new("gcp", "GCP", LibraryRole.CloudSdk, "pypi", ["google-cloud-", "google.cloud", "firebase-admin"]),
        new("azure", "Azure", LibraryRole.CloudSdk, "pypi", ["azure-", "azure."]),

        // Test Frameworks
        new("pytest", "Pytest", LibraryRole.TestFramework, "pypi", ["pytest", "pytest-*"]),
        new("unittest", "Unittest", LibraryRole.TestFramework, "pypi", ["unittest"]),

        // Generic API Clients
        new("requests", "requests", LibraryRole.General, "pypi", ["requests"]),
        new("urllib", "urllib", LibraryRole.General, "pypi", ["urllib.request", "urllib3", "urllib"]),
        new("httpx", "httpx", LibraryRole.General, "pypi", ["httpx"]),
        new("aiohttp", "aiohttp", LibraryRole.General, "pypi", ["aiohttp"])
    ];

    public IReadOnlyList<ISemanticExtension> SemanticExtensions { get; } =
    [
        new Libraries.ChromaDbLibraryParser(),
        new Libraries.CouchDbPythonLibraryParser(),
        new Libraries.ElasticsearchPythonLibraryParser(),
        new Libraries.FastApiLibraryParser(),
        new Libraries.MysqlConnectorPythonLibraryParser(),
        new Libraries.PeeweeLibraryParser(),
        new Libraries.PineconeLibraryParser(),
        new Libraries.Psycopg2LibraryParser(),
        new Libraries.PyMongoLibraryParser(),
        new Libraries.PyMysqlLibraryParser(),
        new Libraries.PythonRedisLibraryParser(),
        new Libraries.PythonSqlite3LibraryParser(),
        new Libraries.SqlAlchemyLibraryParser()
    ];

    public bool UsesTreeSitter => true;

    public LanguageSyntaxProfile SyntaxProfile => PythonSyntaxProfile.Instance;

    public bool CanParse(string fileExtension)
    {
        return fileExtension.Equals(".py", StringComparison.OrdinalIgnoreCase);
    }

    public void ExtractDeclarations(Node rootNode, Action<string, Node?, string?> registerDeclaration)
    {
        PythonDeclarationExtractor.Extract(rootNode, registerDeclaration);
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
            this,
            relativePath,
            absoluteWorkspacePath,
            fileParser,
            extensionRegistry
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

                    if (line.StartsWith("[") && line.EndsWith("]"))
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

    public async Task<SyntaxTree> ParseAsync(string filePath, string parentNodeId, string workspaceId, string absoluteWorkspacePath)
    {
        var relativePath = Path.GetRelativePath(absoluteWorkspacePath, filePath).Replace('\\', '/');
        return await SyntaxTree.ParseAsync(filePath, relativePath, parentNodeId, this, workspaceId, absoluteWorkspacePath);
    }

    public ISyntaxEnricher GetSyntaxEnricher(SyntaxTree syntaxTree) => new SyntaxEnricher(SemanticExtensions, syntaxTree, Packages);

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
