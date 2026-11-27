[assembly: ParserAssembly]

namespace CodeExplorer.Parsers.Go;

[ProjectParser(Tier = ParserTier.Primary)]
public class GoProjectParser : IProjectParser
{
    private const string GoSearchPattern = "*.go";

    public string ProjectType => "go";

    public IReadOnlyCollection<string> ExcludedFolders => ["vendor", ".go"];
    public IReadOnlyCollection<string> ManifestFileNames => ["go.mod"];

    public bool IsConfigurationFile(string fileName)
    {
        var lower = fileName.ToLowerInvariant();
        return lower.StartsWith(".env");
    }

    public IReadOnlyList<PackageDescriptor> Packages => GoFileParser.DefaultPackages;

    public bool IsProjectDirectory(string directoryPath, string[] filesInDirectory)
    {
        foreach (var file in filesInDirectory)
        {
            var fileName = Path.GetFileName(file).ToLowerInvariant();
            if (fileName == "go.mod")
            {
                return true;
            }
        }
        return false;
    }

    public bool TryParse(ProjectParseContext context, out ProjectParseResult? result)
    {
        var activeIndex = context.DirectoryIndex ?? WorkspaceDirectoryIndex.Current;
        if (activeIndex != null && activeIndex.TryGetCachedProjectParse(this, context.DirectoryPath, out var cached))
        {
            result = cached.Result;
            return cached.Matched;
        }

        var dir = context.DirectoryPath;
        var files = context.FilesInDirectory;
        if (!IsProjectDirectory(dir, files))
        {
            activeIndex?.StoreProjectParse(this, dir, false, null);
            result = null;
            return false;
        }

        var goModPath = Path.Combine(dir, "go.mod");
        var goModText = File.Exists(goModPath) ? FileStreamReader.ReadTextOnce(goModPath) : string.Empty;
        var name = Path.GetFileName(dir.TrimEnd('/', '\\'));
        var props = ExtractManifestPropertiesFromContent(dir, files, goModText);
        var produced = ExtractProducedPackageFromContent(dir, goModText);
        var deps = ExtractDependenciesFromContent(goModText);
        var meta = produced != null ? new ParsedProjectManifestMetadata(GoModuleName: produced.Name) : null;
        var additional = GetAdditionalProjects(dir, files);

        List<ParsedProjectDescriptor>? subProjects = null;
        if (additional.Count > 0)
        {
            subProjects = [];
            foreach (var add in additional)
            {
                var entryDir = Path.GetDirectoryName(add.EntryPointPath) ?? dir;
                var relIdOverride = !string.IsNullOrEmpty(context.AbsoluteWorkspacePath)
                    ? Path.GetRelativePath(context.AbsoluteWorkspacePath, Path.GetFullPath(add.EntryPointPath)).Replace('\\', '/')
                    : null;
                subProjects.Add(new ParsedProjectDescriptor(
                    Name: add.Name,
                    DirectoryPath: entryDir,
                    ProjectType: ProjectType,
                    ManifestProperties: add.Properties,
                    EntryPointPaths: [add.EntryPointPath],
                    LocalDependencyPaths: add.LocalDependencyPaths,
                    RelativeIdPathOverride: relIdOverride));
            }
        }

        result = new ProjectParseResult(
            Project: new ParsedProjectDescriptor(
                Name: name,
                DirectoryPath: dir,
                ProjectType: ProjectType,
                ManifestProperties: props,
                SubProjects: subProjects,
                ProducedPackage: produced,
                Dependencies: deps,
                ManifestMetadata: meta,
                AdditionalTargets: additional.Count > 0 ? additional : null),
            ClaimMode: ParserClaimMode.AbsorbSecondaryInDirectory);
        activeIndex?.StoreProjectParse(this, dir, true, result);
        return true;
    }

    public ParsedProjectManifestMetadata? InspectDirectoryManifestMetadata(ProjectParseContext context)
    {
        var activeIndex = context.DirectoryIndex ?? WorkspaceDirectoryIndex.Current;
        if (activeIndex != null && activeIndex.TryGetCachedProjectParse(this, context.DirectoryPath, out var cached) && cached.Result != null)
        {
            return cached.Result.Project.ManifestMetadata;
        }

        var goModPath = Path.Combine(context.DirectoryPath, "go.mod");
        if (!File.Exists(goModPath)) return null;
        var produced = ExtractProducedPackageFromContent(context.DirectoryPath, FileStreamReader.ReadTextOnce(goModPath));
        return produced != null ? new ParsedProjectManifestMetadata(GoModuleName: produced.Name) : null;
    }

    public Task<ProducedPackageInfo?> GetProducedPackageAsync(string projectDirectory)
    {
        var goModPath = Path.Combine(projectDirectory, "go.mod");
        if (!File.Exists(goModPath)) return Task.FromResult<ProducedPackageInfo?>(null);
        return Task.FromResult(ExtractProducedPackageFromContent(projectDirectory, FileStreamReader.ReadTextOnce(goModPath)));
    }

    private static ProducedPackageInfo? ExtractProducedPackageFromContent(string projectDirectory, string goModText)
    {
        if (string.IsNullOrEmpty(goModText)) return null;
        foreach (var rawLine in goModText.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = rawLine.Trim();
            if (!trimmed.StartsWith("module ", StringComparison.Ordinal)) continue;
            var modName = trimmed["module ".Length..].Trim();
            if (string.IsNullOrEmpty(modName)) continue;

            var version = "unknown";
            var versionFilePath = Path.Combine(projectDirectory, "VERSION");
            if (File.Exists(versionFilePath))
            {
                version = FileStreamReader.ReadTextOnce(versionFilePath).Trim();
            }
            const string goEcosystem = "go";
            return new ProducedPackageInfo(modName, version, goEcosystem);
        }
        return null;
    }

    public Dictionary<string, string> ExtractManifestProperties(string directoryPath, string[] filesInDirectory)
    {
        var goModPath = Path.Combine(directoryPath, "go.mod");
        var content = File.Exists(goModPath) ? FileStreamReader.ReadTextOnce(goModPath) : string.Empty;
        return ExtractManifestPropertiesFromContent(directoryPath, filesInDirectory, content);
    }

    private static Dictionary<string, string> ExtractManifestPropertiesFromContent(
        string directoryPath,
        string[] filesInDirectory,
        string goModContent)
    {
        const string mainGoFileName = "main.go";
        var props = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var normPath = "/" + directoryPath.Replace('\\', '/').Trim('/') + "/";
        var dirName = Path.GetFileName(directoryPath.TrimEnd('/', '\\')).ToLowerInvariant();
        var isTestDir = dirName is "test" or "tests" ||
                        normPath.EndsWith("/test/") || normPath.EndsWith("/tests/") ||
                        normPath.EndsWith("/e2e/") || normPath.EndsWith("/qa/");

        var isInternalTool = dirName is "tools" or "tooling" ||
                             normPath.EndsWith("/internal/tools/") || normPath.EndsWith("/internal/tooling/");

        if (isInternalTool)
        {
            props["manifest_type"] = "tool";
            props["is_internal_tool"] = "true";
        }

        var goFiles = filesInDirectory.Where(f => f.EndsWith(".go", StringComparison.OrdinalIgnoreCase)).ToList();
        var hasNonTestGo = goFiles.Any(f => !f.EndsWith("_test.go", StringComparison.OrdinalIgnoreCase));
        var hasMainGo = goFiles.Any(f => Path.GetFileName(f).Equals(mainGoFileName, StringComparison.OrdinalIgnoreCase));
        if (!hasMainGo)
        {
            var cmdDir = Path.Combine(directoryPath, "cmd");
            if (Directory.Exists(cmdDir))
            {
                hasMainGo = Directory.EnumerateFiles(cmdDir, mainGoFileName, SearchOption.AllDirectories).Any();
            }
        }

        if (isTestDir || (goFiles.Count > 0 && !hasNonTestGo))
        {
            props["manifest_type"] = "test";
            props["is_test_project"] = "true";
            return props;
        }

        var hasGrpc = false;
        var hasWeb = false;
        var hasCobraOrCli = false;

        if (!string.IsNullOrEmpty(goModContent))
        {
            if (goModContent.Contains("github.com/spf13/cobra") ||
                goModContent.Contains("github.com/urfave/cli") ||
                goModContent.Contains("github.com/spf13/pflag"))
            {
                hasCobraOrCli = true;
            }
            if (goModContent.Contains("google.golang.org/grpc"))
            {
                hasGrpc = true;
                props["has_grpc"] = "true";
            }
            if (goModContent.Contains("github.com/gin-gonic/gin") ||
                goModContent.Contains("github.com/labstack/echo") ||
                goModContent.Contains("github.com/gofiber/fiber") ||
                goModContent.Contains("github.com/go-chi/chi") ||
                goModContent.Contains("github.com/beego/beego") ||
                goModContent.Contains("github.com/astaxie/beego"))
            {
                hasWeb = true;
            }
        }

        var isWorker = dirName.EndsWith("-worker") || dirName.EndsWith("_worker") || dirName.EndsWith(".worker") ||
                       dirName.EndsWith("-consumer") || dirName.EndsWith("_consumer") || dirName.EndsWith(".consumer") ||
                       dirName.EndsWith("-scheduler") || dirName.EndsWith("_scheduler") ||
                       goFiles.Any(f => Path.GetFileName(f).ToLowerInvariant().Contains("worker"));

        if (isWorker)
        {
            props["manifest_type"] = "worker";
            props["framework_type"] = "worker";
            props["role"] = "Worker";
            props["primary_role"] = "Worker";
        }
        else if (hasMainGo)
        {
            if (isInternalTool)
            {
                props["manifest_type"] = "tool";
                props["role"] = "CliTool";
                props["primary_role"] = "CliTool";
            }
            else if (hasGrpc || hasWeb)
            {
                props["manifest_type"] = "application";
                props["framework_type"] = "web";
                props["role"] = "Service";
                props["primary_role"] = "Service";
            }
            else if (hasCobraOrCli || normPath.EndsWith("/cmd/") || normPath.EndsWith("/cli/") || dirName.EndsWith("-cli") || dirName.EndsWith("_cli") || dirName == "cli")
            {
                props["manifest_type"] = "cli";
                props["has_cli_bin"] = "true";
                props["role"] = "CliTool";
                props["primary_role"] = "CliTool";
            }
            else
            {
                props["manifest_type"] = "application";
                props["framework_type"] = "web";
                props["role"] = "Service";
                props["primary_role"] = "Service";
            }
        }
        else if (hasGrpc || hasWeb)
        {
            props["manifest_type"] = "application";
            props["framework_type"] = "web";
            props["role"] = "Service";
            props["primary_role"] = "Service";
        }

        return props;
    }

    public Task<ProjectDependencyInfo> ParseDependenciesAsync(string projectDirectory)
    {
        var goModPath = Path.Combine(projectDirectory, "go.mod");
        if (!File.Exists(goModPath))
        {
            return Task.FromResult(new ProjectDependencyInfo([], []));
        }
        return Task.FromResult(ExtractDependenciesFromContent(FileStreamReader.ReadTextOnce(goModPath)));
    }

    private static ProjectDependencyInfo ExtractDependenciesFromContent(string goModText)
    {
        var localProjectPaths = new List<string>();
        var externalPackages = new List<ProducedPackageInfo>();
        if (string.IsNullOrEmpty(goModText))
        {
            return new ProjectDependencyInfo(localProjectPaths, externalPackages);
        }

        var lines = goModText.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        var inRequireBlock = false;
        var inReplaceBlock = false;

        foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith("//", StringComparison.Ordinal)) continue;

                // Handle replace directives (e.g. "replace example.com/pkg => ../pkg" or inside "replace (...)")
                if (line.StartsWith("replace (", StringComparison.Ordinal))
                {
                    inReplaceBlock = true;
                    continue;
                }
                if (inReplaceBlock && line == ")")
                {
                    inReplaceBlock = false;
                    continue;
                }

                if (inReplaceBlock || line.StartsWith("replace ", StringComparison.Ordinal))
                {
                    var replaceContent = line.StartsWith("replace ", StringComparison.Ordinal)
                        ? line["replace ".Length..].Trim()
                        : line;

                    if (replaceContent.Contains("=>"))
                    {
                        var arrowIdx = replaceContent.IndexOf("=>", StringComparison.Ordinal);
                        var target = replaceContent[(arrowIdx + 2)..].Trim();
                        var targetParts = target.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
                        if (targetParts.Length >= 1)
                        {
                            var targetPath = targetParts[0];
                            if (targetPath.StartsWith('.') || targetPath.StartsWith('/') || targetPath.StartsWith('\\'))
                            {
                                localProjectPaths.Add(targetPath);
                            }
                        }
                    }
                    continue;
                }

                // Handle single-line require
                if (line.StartsWith("require ", StringComparison.Ordinal) && !line.EndsWith('('))
                {
                    const string goEco = "go";
                    const string defaultVer = "1.0.0";
                    var content = line["require ".Length..].Trim();
                    var parts = content.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 1)
                    {
                        var name = parts[0];
                        var version = parts.Length >= 2 ? parts[1] : defaultVer;
                        externalPackages.Add(new ProducedPackageInfo(name, version, goEco));
                    }
                }
                else if (line.StartsWith("require (", StringComparison.Ordinal))
                {
                    inRequireBlock = true;
                }
                else if (line == ")")
                {
                    inRequireBlock = false;
                }
                else if (inRequireBlock)
                {
                    const string goEco = "go";
                    const string defaultVer = "1.0.0";
                    var parts = line.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 1)
                    {
                        var name = parts[0];
                        var version = parts.Length >= 2 ? parts[1] : defaultVer;
                        externalPackages.Add(new ProducedPackageInfo(name, version, goEco));
                    }
                }
            }

        return new ProjectDependencyInfo(localProjectPaths, externalPackages);
    }

    public IReadOnlyList<AdditionalProjectDescriptor> GetAdditionalProjects(string directoryPath, string[] filesInDirectory)
    {
        var goModPath = Path.Combine(directoryPath, "go.mod");
        if (!File.Exists(goModPath)) return [];

        var results = new List<AdditionalProjectDescriptor>();
        var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var cmdDir = Path.Combine(directoryPath, "cmd");
        if (Directory.Exists(cmdDir))
        {
            foreach (var sub in Directory.EnumerateDirectories(cmdDir))
            {
                var entryPoint = FindMainGoFile(sub);
                if (entryPoint != null)
                {
                    var name = Path.GetFileName(sub);
                    if (seenNames.Add(name))
                    {
                        var props = BuildAdditionalProjectProperties(name, sub, entryPoint);
                        results.Add(new AdditionalProjectDescriptor(name, entryPoint, props, [directoryPath]));
                    }
                }
            }
        }

        foreach (var sub in Directory.EnumerateDirectories(directoryPath))
        {
            var dirName = Path.GetFileName(sub).ToLowerInvariant();
            if (dirName is "cmd" or "vendor" or ".git" or ".codeexplorer" or "tests" or "test" or "testing" or "common" or "lib" or "pkg" or "portal" or "scripts" or "docs")
            {
                continue;
            }

            var entryPoint = FindMainGoFile(sub);
            if (entryPoint != null)
            {
                var name = Path.GetFileName(sub);
                if (seenNames.Add(name))
                {
                    var props = BuildAdditionalProjectProperties(name, sub, entryPoint);
                    results.Add(new AdditionalProjectDescriptor(name, entryPoint, props, [directoryPath]));
                }
            }
        }

        // Standard single-binary Go module layout (go.mod + cmd/<app>/main.go + internal/*, with no root main.go):
        // keep as one unified module project rather than detaching cmd/<app>/main.go from internal/*.
        if (results.Count == 1 && FindMainGoFile(directoryPath) == null)
        {
            return [];
        }

        return results;
    }

    private static string? FindMainGoFile(string dir)
    {
        var mainGo = Path.Combine(dir, "main.go");
        if (File.Exists(mainGo)) return mainGo;

        try
        {
            foreach (var goFile in Directory.EnumerateFiles(dir, GoSearchPattern, SearchOption.TopDirectoryOnly))
            {
                if (goFile.EndsWith("_test.go", StringComparison.OrdinalIgnoreCase)) continue;
                var text = FileStreamReader.ReadTextOnce(goFile);
                if (text.Contains("package main") && text.Contains("func main()"))
                {
                    return goFile;
                }
            }
        }
        catch (Exception ex)
        {
            // Directory traversal or file reading error when inspecting main package
            _ = ex;
        }

        return null;
    }

    private static Dictionary<string, string> BuildAdditionalProjectProperties(string name, string dir, string entryPoint)
    {
        var props = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var lower = name.ToLowerInvariant();

        var isWorker = lower.Contains("worker") || lower.Contains("consumer") || lower.Contains("job") || lower.Contains("queue");
        var isCliTool = lower.Contains("cli") || lower.Contains("tool") || lower.Contains("migrat") || lower.Contains("gen") || lower.Contains("patch") || lower.Contains("swagger");

        if (isCliTool && !lower.Contains("server") && !lower.Contains("service"))
        {
            props["role"] = "CliTool";
            props["primary_role"] = "CliTool";
            props["kind"] = "CliTool";
            props["manifest_type"] = "cli";
        }
        else if (isWorker)
        {
            props["role"] = "Worker";
            props["primary_role"] = "Worker";
            props["kind"] = "Service";
            props["manifest_type"] = "worker";
            props["framework_type"] = "worker";
        }
        else
        {
            props["role"] = "Service";
            props["primary_role"] = "Service";
            props["kind"] = "Service";
            props["manifest_type"] = "application";
            props["framework_type"] = "web";
        }

        return props;
    }
}
