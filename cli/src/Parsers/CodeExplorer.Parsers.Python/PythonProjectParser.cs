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

    private static readonly HashSet<string> InternalPythonSubfolders = new(StringComparer.OrdinalIgnoreCase)
    {
        "requirements", "api", "wsb", "auth", "cron", "cronjobs", "cronjobs_old", "CronJobs",
        "poyd", "sync", "cache", "homer", "order", "keydns", "rabbit", "bi_tool", "hubspot",
        "request", "bulkmail", "deletions", "migration", "migrations", "titan_api", "deployment",
        "monitoring", "newsletter", "two_factor", "aftermarket", "geolocation", "productinfo",
        "smartsearch", "step_up_2fa", "dd24_sockets", "dd24Socket", "pdfGeneration", "minimonitoring",
        "dd24_exceptions", "error_management", "notificationCenter", "premium", "bundling",
        "controlling", "jirainterface", "activeCampaign",
        "db", "date", "math", "mreg", "tools", "badman", "events", "promos", "socket", "billing",
        "payment", "pricing", "external", "renewals", "positions", "riskRating", "action_trigger",
        "dd24_plausib_checks", "opmode", "logs-analyzer", "common", "wlconfig", "configParser", "gunter",
        "src", "app", "lib", "libs", "core", "utils", "helpers", "services", "models", "controllers",
        "handlers", "workers", "tasks", "jobs", "scripts", "bin", "config", "configs", "settings",
        "tests", "test", "html", "public", "www", "site", "vendor"
    };

    public bool CanContainNestedProject(
        ParsedProjectDescriptor enclosingProject,
        string childDirectoryPath,
        string[] childFiles,
        IProjectParser candidateChildParser)
    {
        if (PhysicalFileStore.TryGetConfiguredProject(childDirectoryPath, out _) ||
            (PhysicalFileStore.IsGitRepositoryRoot(childDirectoryPath) && !PhysicalFileStore.HasChildGitRepositories(childDirectoryPath)))
        {
            return true;
        }

        if (!IProjectParser.DefaultCanContainNestedProject(enclosingProject, childDirectoryPath, candidateChildParser))
        {
            return false;
        }

        if (candidateChildParser.ProjectType is "php" or "perl" or "script")
        {
            return false;
        }

        var hasExplicitManifest = candidateChildParser.ManifestFileNames.Any(m =>
            childFiles.Any(f => Path.GetFileName(f).Equals(m, StringComparison.OrdinalIgnoreCase)));
        if (!hasExplicitManifest)
        {
            return false;
        }

        if (!string.Equals(candidateChildParser.ProjectType, "python", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var childFolderName = Path.GetFileName(childDirectoryPath.TrimEnd('/', '\\'));
        if (childFolderName.Equals("requirements", StringComparison.OrdinalIgnoreCase) &&
            !HasPythonFilesInSubtree(childDirectoryPath))
        {
            return false;
        }

        var relToParent = Path.GetRelativePath(enclosingProject.DirectoryPath, childDirectoryPath).Replace('\\', '/').Trim('/');
        var segments = relToParent.Split('/', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < segments.Length; i++)
        {
            if (InternalPythonSubfolders.Contains(segments[i]))
            {
                return false;
            }
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

        var hasOtherManifest = HasOtherPrimaryManifest(directoryPath, filesInDirectory);

        if (hasPrimaryManifest)
        {
            if (HasNonTerraformApplicationManifest(filesInDirectory) && !HasSetupOrManageManifest(filesInDirectory))
            {
                var (pyFiles, otherFiles) = CountPythonVsOtherSourceFiles(directoryPath, filesInDirectory);
                if (pyFiles == 0 && otherFiles > 0)
                {
                    return false;
                }
            }
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

            if (hasOtherManifest || PhysicalFileStore.HasCachedDirectory(directoryPath))
            {
                var (pyFiles, otherFiles) = CountPythonVsOtherSourceFiles(directoryPath, filesInDirectory);
                if (pyFiles == 0 && (hasOtherManifest || otherFiles > 0))
                {
                    return false;
                }
                if (hasOtherManifest && otherFiles > pyFiles)
                {
                    return false;
                }
            }

            return true;
        }

        if (PhysicalFileStore.HasChildGitRepositories(directoryPath) ||
            PhysicalFileStore.HasChildConfiguredProjects(directoryPath))
        {
            return false;
        }

        if (HasNestedRequirementsManifest(directoryPath) && HasPythonFilesInSubtree(directoryPath))
        {
            return !HasAncestorPythonProject(directoryPath);
        }

        if (IsPythonContainerRoot(directoryPath, filesInDirectory))
        {
            return !HasAncestorPythonProject(directoryPath);
        }

        return false;
    }

    private static bool HasOtherPrimaryManifest(string directoryPath, string[] filesInDirectory)
    {
        for (var i = 0; i < filesInDirectory.Length; i++)
        {
            var fn = Path.GetFileName(filesInDirectory[i]);
            var lowerFn = fn.ToLowerInvariant();
            var lowerExt = Path.GetExtension(lowerFn);
            if (lowerFn is "terragrunt.hcl" or "root.hcl" ||
                lowerExt is ".tf" or ".tofu" ||
                fn.Equals("composer.json", StringComparison.OrdinalIgnoreCase) ||
                fn.Equals("composer.lock", StringComparison.OrdinalIgnoreCase) ||
                fn.Equals("package.json", StringComparison.OrdinalIgnoreCase) ||
                fn.Equals("tsconfig.json", StringComparison.OrdinalIgnoreCase) ||
                fn.Equals("build.gradle", StringComparison.OrdinalIgnoreCase) ||
                fn.Equals("build.gradle.kts", StringComparison.OrdinalIgnoreCase) ||
                fn.Equals("settings.gradle", StringComparison.OrdinalIgnoreCase) ||
                fn.Equals("settings.gradle.kts", StringComparison.OrdinalIgnoreCase) ||
                fn.Equals("Makefile.PL", StringComparison.OrdinalIgnoreCase) ||
                fn.Equals("cpanfile", StringComparison.OrdinalIgnoreCase) ||
                fn.Equals("go.mod", StringComparison.OrdinalIgnoreCase) ||
                fn.Equals("Cargo.toml", StringComparison.OrdinalIgnoreCase) ||
                fn.Equals("pom.xml", StringComparison.OrdinalIgnoreCase) ||
                fn.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) ||
                fn.EndsWith(".sln", StringComparison.OrdinalIgnoreCase) ||
                fn.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase) ||
                fn.EndsWith(".php", StringComparison.OrdinalIgnoreCase) ||
                fn.EndsWith(".pl", StringComparison.OrdinalIgnoreCase) ||
                fn.EndsWith(".pm", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return PhysicalFileStore.FileExistsInCachedDirectory(Path.Combine(directoryPath, "public", "index.php")) ||
               PhysicalFileStore.FileExistsInCachedDirectory(Path.Combine(directoryPath, "www", "index.php")) ||
               PhysicalFileStore.FileExistsInCachedDirectory(Path.Combine(directoryPath, "application", "Bootstrap.php"));
    }

    private static bool SubtreeHasOtherEcosystemManifests(string directoryPath)
    {
        foreach (var file in PhysicalFileStore.EnumerateCachedFilesInSubtree(directoryPath))
        {
            var fn = Path.GetFileName(file);
            var lowerFn = fn.ToLowerInvariant();
            var lowerExt = Path.GetExtension(lowerFn);
            if (lowerFn is "terragrunt.hcl" or "root.hcl" or "box.json" or "application.cfc" or "application.cfm" ||
                lowerExt is ".tf" or ".tofu" ||
                fn.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) ||
                fn.EndsWith(".fsproj", StringComparison.OrdinalIgnoreCase) ||
                fn.EndsWith(".vbproj", StringComparison.OrdinalIgnoreCase) ||
                fn.EndsWith(".sln", StringComparison.OrdinalIgnoreCase) ||
                fn.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase) ||
                fn.Equals("composer.json", StringComparison.OrdinalIgnoreCase) ||
                fn.Equals("go.mod", StringComparison.OrdinalIgnoreCase) ||
                fn.Equals("Cargo.toml", StringComparison.OrdinalIgnoreCase) ||
                fn.Equals("pom.xml", StringComparison.OrdinalIgnoreCase) ||
                fn.Equals("build.gradle", StringComparison.OrdinalIgnoreCase) ||
                fn.Equals("build.gradle.kts", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsOtherCompiledOrTypedSourceFile(string file) =>
        Path.GetExtension(file).ToLowerInvariant() is
            ".cs" or ".ts" or ".tsx" or ".js" or ".jsx" or ".mjs" or ".cjs" or ".go" or ".rs" or ".java" or ".kt" or ".tf" or ".tofu" or ".cfc" or ".cfm";

    private static bool HasSetupOrManageManifest(string[] filesInDirectory)
    {
        for (var i = 0; i < filesInDirectory.Length; i++)
        {
            var lowerFn = Path.GetFileName(filesInDirectory[i]).ToLowerInvariant();
            if (lowerFn is "setup.py" or "manage.py" or "alembic.ini")
            {
                return true;
            }
        }
        return false;
    }

    private static bool HasNonTerraformApplicationManifest(string[] filesInDirectory)
    {
        for (var i = 0; i < filesInDirectory.Length; i++)
        {
            var lowerFn = Path.GetFileName(filesInDirectory[i]).ToLowerInvariant();
            if (lowerFn is "package.json" or "tsconfig.json" or "pom.xml" or "build.gradle" or "build.gradle.kts"
                or "cargo.toml" or "go.mod" or "composer.json"
                || lowerFn.EndsWith(".csproj", StringComparison.Ordinal)
                || lowerFn.EndsWith(".sln", StringComparison.Ordinal)
                || lowerFn.EndsWith(".slnx", StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }

    private static (int PyCount, int OtherCodeCount) CountPythonVsOtherSourceFiles(string directoryPath, string[] filesInDirectory)
    {
        var pyCount = 0;
        var otherCount = 0;
        if (PhysicalFileStore.HasCachedDirectory(directoryPath))
        {
            foreach (var file in PhysicalFileStore.EnumerateCachedFilesInSubtree(directoryPath))
            {
                var ext = Path.GetExtension(file).ToLowerInvariant();
                if (ext is ".py" or ".pyi") pyCount++;
                else if (IsOtherCompiledOrTypedSourceFile(file)) otherCount++;
            }
            return (pyCount, otherCount);
        }

        for (var i = 0; i < filesInDirectory.Length; i++)
        {
            var ext = Path.GetExtension(filesInDirectory[i]).ToLowerInvariant();
            if (ext is ".py" or ".pyi") pyCount++;
            else if (IsOtherCompiledOrTypedSourceFile(filesInDirectory[i])) otherCount++;
        }

        ReadOnlySpan<string> commonSourceDirs = ["src", "app", "apps", "packages", "lib"];
        foreach (var sub in commonSourceDirs)
        {
            var subFiles = PhysicalFileStore.GetDirectoryFiles(Path.Combine(directoryPath, sub));
            for (var i = 0; i < subFiles.Length; i++)
            {
                var ext = Path.GetExtension(subFiles[i]).ToLowerInvariant();
                if (ext is ".py" or ".pyi") pyCount++;
                else if (IsOtherCompiledOrTypedSourceFile(subFiles[i])) otherCount++;
            }
        }
        return (pyCount, otherCount);
    }

    private static bool IsPythonContainerRoot(string directoryPath, string[] filesInDirectory)
    {
        if (!PhysicalFileStore.HasCachedDirectory(directoryPath))
        {
            return false;
        }

        var folderName = Path.GetFileName(directoryPath.TrimEnd('/', '\\'));
        var parentDir = Directory.GetParent(directoryPath);
        if (parentDir != null &&
            PhysicalFileStore.HasCachedDirectory(parentDir.FullName) &&
            InternalPythonSubfolders.Contains(folderName))
        {
            return false;
        }

        if (HasOtherPrimaryManifest(directoryPath, filesInDirectory) ||
            SubtreeHasOtherEcosystemManifests(directoryPath))
        {
            return false;
        }

        var hasDockerOrCoverageMarker = filesInDirectory.Any(f =>
        {
            var fn = Path.GetFileName(f);
            return fn.Equals("Dockerfile", StringComparison.OrdinalIgnoreCase) ||
                   fn.Equals("compose.yaml", StringComparison.OrdinalIgnoreCase) ||
                   fn.Equals("compose.yml", StringComparison.OrdinalIgnoreCase) ||
                   fn.Equals("docker-compose.yml", StringComparison.OrdinalIgnoreCase) ||
                   fn.Equals("docker-compose.yaml", StringComparison.OrdinalIgnoreCase) ||
                   fn.Equals(".coveragerc", StringComparison.OrdinalIgnoreCase) ||
                   fn.Equals("pytest.ini", StringComparison.OrdinalIgnoreCase) ||
                   fn.Equals("tox.ini", StringComparison.OrdinalIgnoreCase);
        });

        if (PhysicalFileStore.IsGitRepositoryRoot(directoryPath) &&
            !PhysicalFileStore.HasChildGitRepositories(directoryPath))
        {
            var phpCount = 0;
            var pyCount = 0;
            var perlCount = 0;
            var otherCodeCount = 0;
            foreach (var file in PhysicalFileStore.EnumerateCachedFilesInSubtree(directoryPath))
            {
                if (file.EndsWith(".py", StringComparison.OrdinalIgnoreCase)) pyCount++;
                else if (file.EndsWith(".php", StringComparison.OrdinalIgnoreCase) || file.EndsWith(".phtml", StringComparison.OrdinalIgnoreCase) || file.EndsWith(".inc", StringComparison.OrdinalIgnoreCase)) phpCount++;
                else if (file.EndsWith(".pl", StringComparison.OrdinalIgnoreCase) || file.EndsWith(".pm", StringComparison.OrdinalIgnoreCase)) perlCount++;
                else if (IsOtherCompiledOrTypedSourceFile(file)) otherCodeCount++;
            }
            if (pyCount > 0 && pyCount > phpCount && pyCount >= perlCount && pyCount >= otherCodeCount)
            {
                return true;
            }
        }

        var childDirs = PhysicalFileStore.GetCachedDirectChildDirectories(directoryPath);
        var pyChildCount = 0;
        for (var i = 0; i < childDirs.Count; i++)
        {
            var childName = Path.GetFileName(childDirs[i].DirectoryPath.TrimEnd('/', '\\'));
            if (!InternalPythonSubfolders.Contains(childName))
            {
                continue;
            }

            if (PhysicalFileStore.EnumerateCachedFilesInSubtree(childDirs[i].DirectoryPath)
                .Any(f => f.EndsWith(".py", StringComparison.OrdinalIgnoreCase)))
            {
                pyChildCount++;
                if (pyChildCount >= 2 || (hasDockerOrCoverageMarker && pyChildCount >= 1))
                {
                    return true;
                }
            }
        }

        if (hasDockerOrCoverageMarker &&
            filesInDirectory.Any(f => f.EndsWith(".py", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return false;
    }

    private static bool HasAncestorPythonProject(string directoryPath)
    {
        var useCacheBound = PhysicalFileStore.HasCachedDirectory(directoryPath);
        var current = Directory.GetParent(directoryPath);
        var depth = 0;
        while (current != null && depth < 16)
        {
            depth++;
            if (useCacheBound && !PhysicalFileStore.HasCachedDirectory(current.FullName))
            {
                break;
            }

            var parentFiles = PhysicalFileStore.GetDirectoryFiles(current.FullName);
            if (parentFiles.Any(f =>
                {
                    var fn = Path.GetFileName(f).ToLowerInvariant();
                    return fn is "pyproject.toml" or "setup.py" or "setup.cfg" or "pipfile" or "alembic.ini" or "manage.py" or "requirements.txt" or ".coveragerc"
                           || fn.EndsWith(".py", StringComparison.Ordinal);
                }) ||
                HasNestedRequirementsManifest(current.FullName) ||
                IsPythonContainerRoot(current.FullName, parentFiles))
            {
                return true;
            }

            current = current.Parent;
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
