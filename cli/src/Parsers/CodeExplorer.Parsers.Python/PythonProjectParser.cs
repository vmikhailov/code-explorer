using System.Text.RegularExpressions;

[assembly: ParserAssembly]

namespace CodeExplorer.Parsers.Python;

[ProjectParser(Tier = ParserTier.Primary)]
public class PythonProjectParser : IProjectParser
{
    public string ProjectType => "python";

    public IReadOnlyCollection<string> ExcludedFolders => ["venv", ".venv", "__pycache__", ".pytest_cache", ".mypy_cache"];
    public IReadOnlyCollection<string> ManifestFileNames => ["requirements.txt", "pyproject.toml", "setup.py", "Pipfile"];

    public bool IsConfigurationFile(string fileName)
    {
        var lower = fileName.ToLowerInvariant();
        return lower.StartsWith(".env");
    }

    public IReadOnlyList<PackageDescriptor> Packages => PythonFileParser.DefaultPackages;

    public bool CanContainNestedProject(
        ParsedProjectDescriptor enclosingProject,
        string childDirectoryPath,
        string[] childFiles,
        IProjectParser candidateChildParser)
    {
        if (!IProjectParser.DefaultCanContainNestedProject(enclosingProject, childDirectoryPath, candidateChildParser))
        {
            return false;
        }

        var hasExplicitManifest = candidateChildParser.ManifestFileNames.Any(m =>
            childFiles.Any(f => Path.GetFileName(f).Equals(m, StringComparison.OrdinalIgnoreCase)));
        if (!hasExplicitManifest)
        {
            return false;
        }

        var childFolderName = Path.GetFileName(childDirectoryPath.TrimEnd('/', '\\'));
        if (childFolderName.Equals("requirements", StringComparison.OrdinalIgnoreCase) &&
            !HasPythonFilesInSubtree(childDirectoryPath))
        {
            return false;
        }

        return true;
    }

    public bool IsProjectDirectory(string directoryPath, string[] filesInDirectory)
    {
        var hasPrimaryManifest = false;
        var hasRequirementsTxt = false;
        foreach (var file in filesInDirectory)
        {
            var fileName = Path.GetFileName(file).ToLowerInvariant();
            if (fileName is "pyproject.toml" or "setup.py" or "setup.cfg" or "pipfile" or "alembic.ini" or "manage.py")
            {
                hasPrimaryManifest = true;
                break;
            }
            if (fileName == "requirements.txt")
            {
                hasRequirementsTxt = true;
            }
        }

        if (hasPrimaryManifest)
        {
            return true;
        }

        if (hasRequirementsTxt)
        {
            var folderName = Path.GetFileName(directoryPath.TrimEnd('/', '\\'));
            if (folderName.Equals("requirements", StringComparison.OrdinalIgnoreCase) &&
                PhysicalFileStore.HasCachedDirectory(directoryPath) &&
                !HasPythonFilesInSubtree(directoryPath))
            {
                return false;
            }
            return true;
        }

        if (HasNestedRequirementsManifest(directoryPath) && HasPythonFilesInSubtree(directoryPath))
        {
            return true;
        }

        return false;
    }

    private static bool HasNestedRequirementsManifest(string directoryPath)
    {
        var reqDir = Path.Combine(directoryPath, "requirements");
        if (!PhysicalFileStore.TryGetCachedDirectoryFiles(reqDir, out var reqFiles) || reqFiles.Length == 0)
        {
            return false;
        }

        for (var i = 0; i < reqFiles.Length; i++)
        {
            var fn = Path.GetFileName(reqFiles[i]);
            if (fn.Equals("requirements.txt", StringComparison.OrdinalIgnoreCase) ||
                fn.Equals("base.txt", StringComparison.OrdinalIgnoreCase) ||
                fn.Equals("prod.txt", StringComparison.OrdinalIgnoreCase) ||
                fn.Equals("production.txt", StringComparison.OrdinalIgnoreCase) ||
                fn.Equals("common.txt", StringComparison.OrdinalIgnoreCase) ||
                fn.Equals("main.txt", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    private static bool HasPythonFilesInSubtree(string directoryPath)
    {
        if (!PhysicalFileStore.HasCachedDirectory(directoryPath))
        {
            return false;
        }

        return PhysicalFileStore.EnumerateCachedFilesInSubtree(directoryPath)
            .Any(f => f.EndsWith(".py", StringComparison.OrdinalIgnoreCase));
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
                    allText.AppendLine(PhysicalFileStore.ReadAllText(file));
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // Ignore unreadable manifest files during best-effort property extraction
                }
            }
        }

        var text = allText.ToString().ToLowerInvariant();

        // Check for Web / RPC frameworks
        if (text.Contains("fastapi") || text.Contains("flask") || text.Contains("django") ||
            text.Contains("uvicorn") || text.Contains("gunicorn") || text.Contains("tornado") ||
            text.Contains("sanic") || text.Contains("grpcio") || text.Contains("grpc"))
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

    public async Task<ProducedPackageInfo?> GetProducedPackageAsync(string projectDirectory)
    {
        var pyprojectPath = Path.Combine(projectDirectory, "pyproject.toml");
        if (File.Exists(pyprojectPath))
        {
            try
            {
                var lines = PhysicalFileStore.ReadLines(pyprojectPath);
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
                var content = await PhysicalFileStore.ReadAllTextAsync(setupPyPath);
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
                var lines = PhysicalFileStore.ReadLines(pyprojectPath);
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
            if (!PhysicalFileStore.FileExistsInCachedDirectory(reqPath))
            {
                reqPath = Path.Combine(projectDirectory, "requirements", "requirements.txt");
            }
            if (PhysicalFileStore.FileExistsInCachedDirectory(reqPath))
            {
                try
                {
                    var lines = PhysicalFileStore.ReadLines(reqPath);
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

        return await Task.FromResult(new ProjectDependencyInfo(localProjectPaths, externalPackages));
    }
}
