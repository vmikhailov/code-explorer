using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using CodeExplorer.Core.Parser;
using CodeExplorer.Core.Parser.Syntax;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

[assembly: ParserAssembly]

namespace CodeExplorer.Parser.Python;

[ProjectParser(Tier = ParserTier.Primary)]
public class PythonParser : IProjectParser, IFileParser
{
    public string LanguageName => "python";

    public string ProjectType => "python";

    public IReadOnlyCollection<string> ExcludedFolders => ["venv", ".venv", "__pycache__", ".pytest_cache", ".mypy_cache"];
    public IReadOnlyCollection<string> ManifestFileNames => ["requirements.txt", "pyproject.toml", "setup.py", "Pipfile"];

    public bool IsConfigurationFile(string fileName)
    {
        var lower = fileName.ToLowerInvariant();
        return lower.StartsWith(".env");
    }

    public IReadOnlyList<PackageDescriptor> Packages { get; }

    public PythonParser()
    {
        Packages = [.. SemanticExtensions.SelectMany(e => e.GetPackageDescriptors())];

        /*
        // Unimplemented / Backlog packages (commented out until AST semantic extensions are implemented):
        // Test Frameworks
        new("pytest", "Pytest", LibraryRole.TestFramework, "library", ["pytest", "pytest-*"]),
        new("unittest", "Unittest", LibraryRole.TestFramework, "library", ["unittest"]),

        // Databases / ORMs
        new("bigquery", "BigQuery", LibraryRole.OrmOrDatabase, "library", ["google-cloud-bigquery", "google.cloud.bigquery"]),
        new("clickhouse", "ClickHouse", LibraryRole.OrmOrDatabase, "library", ["clickhouse-connect", "clickhouse-driver"]),

        // Generic Cloud Services
        new("stripe", "Stripe", LibraryRole.CloudSdk, "library", ["stripe"]),
        new("aws", "AWS", LibraryRole.CloudSdk, "library", ["boto3"]),
        new("gcp", "GCP", LibraryRole.CloudSdk, "library", ["google-cloud-", "google.cloud", "firebase-admin"]),
        new("azure", "Azure", LibraryRole.CloudSdk, "library", ["azure-", "azure."]),

        // Generic API Clients
        new("urllib", "urllib", LibraryRole.General, "library", ["urllib.request", "urllib3", "urllib"])
        */
    }

    public IReadOnlyList<ISemanticExtension> SemanticExtensions { get; } =
    [
        new Libraries.AioHttpPythonLibraryParser(),
        new Libraries.ChromaDbLibraryParser(),
        new Libraries.CouchDbPythonLibraryParser(),
        new Libraries.DjangoLibraryParser(),
        new Libraries.ElasticsearchPythonLibraryParser(),
        new Libraries.FastApiLibraryParser(),
        new Libraries.FlaskLibraryParser(),
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
        new Libraries.SqlAlchemyLibraryParser()
    ];

    public bool UsesTreeSitter => true;

    public SyntaxDocument? ParseDocument(string sourceText, string filePath = "", string relativePath = "") =>
        UniversalSyntaxBuilder.Parse(sourceText, LanguageName, filePath, relativePath, SyntaxProfile, this as ILanguageSyntaxHooks);

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
            if (fileName is "requirements.txt" or "pyproject.toml" or "setup.py" or "setup.cfg" or "pipfile" or "alembic.ini" or "manage.py")
            {
                return true;
            }
        }
        return false;
    }

    public Dictionary<string, string> ExtractManifestProperties(string directoryPath, string[] filesInDirectory)
    {
        var props = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // 1. Alembic migrations
        if (filesInDirectory.Any(f => Path.GetFileName(f).Equals("alembic.ini", StringComparison.OrdinalIgnoreCase)))
        {
            props["manifest_type"] = "migration";
            return props;
        }

        // 2. Django manage.py
        if (filesInDirectory.Any(f => Path.GetFileName(f).Equals("manage.py", StringComparison.OrdinalIgnoreCase)))
        {
            props["manifest_type"] = "application";
            props["framework_type"] = "web";
            return props;
        }

        // 2. Read dependencies / content from manifest files
        var allText = new System.Text.StringBuilder();
        foreach (var file in filesInDirectory)
        {
            var fn = Path.GetFileName(file).ToLowerInvariant();
            if (fn is "pyproject.toml" or "requirements.txt" or "setup.py" or "setup.cfg" or "pipfile")
            {
                try
                {
                    allText.AppendLine(File.ReadAllText(file));
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // Ignore unreadable manifest files during best-effort property extraction
                }
            }
        }

        var text = allText.ToString().ToLowerInvariant();

        // Check for Web frameworks
        if (text.Contains("fastapi") || text.Contains("flask") || text.Contains("django") ||
            text.Contains("uvicorn") || text.Contains("gunicorn") || text.Contains("tornado") || text.Contains("sanic"))
        {
            props["manifest_type"] = "application";
            props["framework_type"] = "web";
            return props;
        }

        // Check for Workers
        if (text.Contains("celery") || text.Contains("rq") || text.Contains("dramatiq") || text.Contains("huey"))
        {
            props["manifest_type"] = "worker";
            props["framework_type"] = "worker";
            return props;
        }

        // Check for CLI
        if (text.Contains("[project.scripts]") || text.Contains("click") || text.Contains("typer") || text.Contains("argparse"))
        {
            props["manifest_type"] = "cli";
            props["has_cli_bin"] = "true";
            return props;
        }

        // Default Python standalone package without web frameworks is a Library
        props["manifest_type"] = "library";
        return props;
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
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
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
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
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
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
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
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
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
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Ignore
        }
        return names;
    }

}
