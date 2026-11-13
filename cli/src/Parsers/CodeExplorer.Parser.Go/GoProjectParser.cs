[assembly: ParserAssembly]

namespace CodeExplorer.Parser.Go;

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

    public async Task<ProducedPackageInfo?> GetProducedPackageAsync(string projectDirectory)
    {
        var goModPath = Path.Combine(projectDirectory, "go.mod");
        if (!File.Exists(goModPath)) return null;

        try
        {
            var lines = PhysicalFileStore.ReadLines(goModPath);
            var moduleLine = lines.FirstOrDefault(l => l.Trim().StartsWith("module ", StringComparison.Ordinal));
            if (moduleLine != null)
            {
                var modName = moduleLine.Trim()["module ".Length..].Trim();
                if (!string.IsNullOrEmpty(modName))
                {
                    var version = "unknown";
                    var versionFilePath = Path.Combine(projectDirectory, "VERSION");
                    if (File.Exists(versionFilePath))
                    {
                        version = (await PhysicalFileStore.ReadAllTextAsync(versionFilePath)).Trim();
                    }
                    return new ProducedPackageInfo(modName, version, "go");
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Ignore
        }

        return null;
    }

    public Dictionary<string, string> ExtractManifestProperties(string directoryPath, string[] filesInDirectory)
    {
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
        var hasMainGo = goFiles.Any(f => Path.GetFileName(f).Equals("main.go", StringComparison.OrdinalIgnoreCase));
        if (!hasMainGo)
        {
            var cmdDir = Path.Combine(directoryPath, "cmd");
            if (Directory.Exists(cmdDir))
            {
                hasMainGo = Directory.EnumerateFiles(cmdDir, "main.go", SearchOption.AllDirectories).Any();
            }
        }

        if (isTestDir || (goFiles.Count > 0 && !hasNonTestGo))
        {
            props["manifest_type"] = "test";
            props["is_test_project"] = "true";
            return props;
        }

        var goModPath = Path.Combine(directoryPath, "go.mod");
        var hasGrpc = false;
        var hasWeb = false;
        var hasCobraOrCli = false;

        if (File.Exists(goModPath))
        {
            try
            {
                var content = PhysicalFileStore.ReadAllText(goModPath);
                if (content.Contains("github.com/spf13/cobra") ||
                    content.Contains("github.com/urfave/cli") ||
                    content.Contains("github.com/spf13/pflag"))
                {
                    hasCobraOrCli = true;
                }
                if (content.Contains("google.golang.org/grpc"))
                {
                    hasGrpc = true;
                    props["has_grpc"] = "true";
                }
                if (content.Contains("github.com/gin-gonic/gin") ||
                    content.Contains("github.com/labstack/echo") ||
                    content.Contains("github.com/gofiber/fiber") ||
                    content.Contains("github.com/go-chi/chi") ||
                    content.Contains("github.com/beego/beego") ||
                    content.Contains("github.com/astaxie/beego"))
                {
                    hasWeb = true;
                }
            }
            catch
            {
                // ignored: unreadable go.mod falls back to directory heuristic
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

    public async Task<ProjectDependencyInfo> ParseDependenciesAsync(string projectDirectory)
    {
        var localProjectPaths = new List<string>();
        var externalPackages = new List<ProducedPackageInfo>();

        var goModPath = Path.Combine(projectDirectory, "go.mod");
        if (!File.Exists(goModPath))
        {
            return new ProjectDependencyInfo(localProjectPaths, externalPackages);
        }

        try
        {
            var lines = PhysicalFileStore.ReadLines(goModPath);
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
                    var content = line["require ".Length..].Trim();
                    var parts = content.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 1)
                    {
                        var name = parts[0];
                        var version = parts.Length >= 2 ? parts[1] : "1.0.0";
                        externalPackages.Add(new ProducedPackageInfo(name, version, "go"));
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
                    // Line inside require block
                    var parts = line.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 1)
                    {
                        var name = parts[0];
                        var version = parts.Length >= 2 ? parts[1] : "1.0.0";
                        externalPackages.Add(new ProducedPackageInfo(name, version, "go"));
                    }
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Ignore
        }

        return await Task.FromResult(new ProjectDependencyInfo(localProjectPaths, externalPackages));
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
                        results.Add(new AdditionalProjectDescriptor(name, entryPoint, props));
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
                    results.Add(new AdditionalProjectDescriptor(name, entryPoint, props));
                }
            }
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
                var text = PhysicalFileStore.ReadAllText(goFile);
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
