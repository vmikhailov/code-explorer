using System.Collections.Concurrent;
using CodeExplorer.Core.Parser;
using TreeSitter;

[assembly: ParserAssembly]

namespace CodeExplorer.Parser.Go;

[ProjectParser(Tier = ParserTier.Primary)]
public class GoParser : IProjectParser, IFileParser
{
    public string LanguageName => "go";

    public string ProjectType => "go";

    public IReadOnlyCollection<string> ExcludedFolders => ["vendor", ".go"];
    public IReadOnlyCollection<string> ManifestFileNames => ["go.mod"];

    public bool IsConfigurationFile(string fileName)
    {
        var lower = fileName.ToLowerInvariant();
        return lower.StartsWith(".env");
    }

    public IReadOnlyList<PackageDescriptor> Packages { get; }

    public IReadOnlyList<ISemanticExtension> SemanticExtensions { get; } =
    [
        new Libraries.BigQueryGoLibraryParser(),
        new Libraries.ClickHouseGoLibraryParser(),
        new Libraries.ElasticsearchGoLibraryParser(),
        new Libraries.GinLibraryParser(),
        new Libraries.FiberLibraryParser(),
        new Libraries.EchoLibraryParser(),
        new Libraries.GoRedisLegacyLibraryParser(),
        new Libraries.GoRedisLibraryParser(),
        new Libraries.GormLibraryParser(),
        new Libraries.GoSqlDriverMysqlLibraryParser(),
        new Libraries.GoSqlite3LibraryParser(),
        new Libraries.GoSqlLibraryParser(),
        new Libraries.LibPqLibraryParser(),
        new Libraries.MongoGoLibraryParser(),
        new Libraries.PubSubGoLibraryParser(),
        new Libraries.RabbitMqGoLibraryParser(),
        new Libraries.PgxLibraryParser(),
        new Libraries.RestyLibraryParser(),
        new Libraries.GoHttpClientLibraryParser()
    ];

    public bool UsesTreeSitter => true;

    public LanguageSyntaxProfile SyntaxProfile => GoSyntaxProfile.Instance;

    public GoParser()
    {
        Packages = [.. SemanticExtensions.SelectMany(e => e.GetPackageDescriptors())];

        /*
        // Unimplemented / Backlog packages (commented out until AST semantic extensions are implemented):


        // Generic Cloud Services
        new PackageDescriptor("stripe", "Stripe", LibraryRole.CloudSdk, "library", ["github.com/stripe/stripe-go*"]),
        new PackageDescriptor("aws", "AWS", LibraryRole.CloudSdk, "library", ["github.com/aws/aws-sdk-go*"]),
        new PackageDescriptor("gcp", "GCP", LibraryRole.CloudSdk, "library", ["cloud.google.com/*", "firebase.google.com/*"]),
        new PackageDescriptor("azure", "Azure", LibraryRole.CloudSdk, "library", ["*Azure*", "*azure-sdk-for-go*"]),

        // Test Frameworks
        new PackageDescriptor("testify", "Testify", LibraryRole.TestFramework, "library", ["github.com/stretchr/testify*"]),

        // API Clients
        new PackageDescriptor("req", "req", LibraryRole.General, "library", ["github.com/imroc/req*"]),
        new PackageDescriptor("grequests", "grequests", LibraryRole.General, "library", ["github.com/levigross/grequests*"]),
        new PackageDescriptor("gorequest", "gorequest", LibraryRole.General, "library", ["github.com/parnurzeal/gorequest*"]),
        new PackageDescriptor("surf", "surf", LibraryRole.General, "library", ["github.com/go-surf/surf*"])
        */
    }

    public bool CanParse(string fileExtension)
    {
        return fileExtension.Equals(".go", StringComparison.OrdinalIgnoreCase);
    }

    public void ExtractDeclarations(Node rootNode, Action<string, Node?, string?> registerDeclaration)
    {
        GoDeclarationExtractor.Extract(rootNode, registerDeclaration);
    }

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

    public BaseParserVisitor CreateVisitor(
        Node rootNode,
        List<ISemanticExtension> activeExtensions,
        string relativePath,
        string absoluteWorkspacePath,
        IFileParser fileParser,
        SemanticExtensionRegistry extensionRegistry)
    {
        return new GoFileVisitor(
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
        var goModPath = Path.Combine(projectDirectory, "go.mod");
        if (!File.Exists(goModPath)) return null;

        try
        {
            var lines = await File.ReadAllLinesAsync(goModPath);
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
                        version = (await File.ReadAllTextAsync(versionFilePath)).Trim();
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
                var content = File.ReadAllText(goModPath);
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
                    content.Contains("github.com/gofiber/fiber"))
                {
                    hasWeb = true;
                }
            }
            catch { }
        }

        var isWorker = dirName.EndsWith("-worker") || dirName.EndsWith("_worker") || dirName.EndsWith(".worker") ||
                       dirName.EndsWith("-consumer") || dirName.EndsWith("_consumer") || dirName.EndsWith(".consumer") ||
                       dirName.EndsWith("-scheduler") || dirName.EndsWith("_scheduler") ||
                       goFiles.Any(f => Path.GetFileName(f).ToLowerInvariant().Contains("worker"));

        if (isWorker)
        {
            props["manifest_type"] = "worker";
            props["framework_type"] = "worker";
        }
        else if (hasMainGo)
        {
            if (isInternalTool)
            {
                props["manifest_type"] = "tool";
            }
            else if (hasGrpc || hasWeb)
            {
                props["manifest_type"] = "application";
                props["framework_type"] = "web";
            }
            else if (hasCobraOrCli || normPath.EndsWith("/cmd/") || normPath.EndsWith("/cli/") || dirName.EndsWith("-cli") || dirName.EndsWith("_cli") || dirName == "cli")
            {
                props["manifest_type"] = "cli";
                props["has_cli_bin"] = "true";
            }
            else
            {
                props["manifest_type"] = "application";
                props["framework_type"] = "web";
            }
        }
        else if (hasGrpc || hasWeb)
        {
            props["manifest_type"] = "application";
            props["framework_type"] = "web";
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
            var lines = await File.ReadAllLinesAsync(goModPath);
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

        return new ProjectDependencyInfo(localProjectPaths, externalPackages);
    }

    public async Task<SyntaxTree> ParseAsync(string filePath, string parentNodeId, string workspaceId, string absoluteWorkspacePath)
    {
        var relativePath = Path.GetRelativePath(absoluteWorkspacePath, filePath).Replace('\\', '/');
        return await SyntaxTree.ParseAsync(filePath, relativePath, parentNodeId, this, workspaceId, absoluteWorkspacePath);
    }

    public ISyntaxEnricher GetSyntaxEnricher(SyntaxTree syntaxTree) => new SyntaxEnricher(SemanticExtensions, syntaxTree, Packages);

    private readonly ConcurrentDictionary<string, string> _goModCache = new(StringComparer.OrdinalIgnoreCase);

    public ImportType ResolveImportType(string importPath, string filePath, string? absoluteWorkspacePath)
    {
        return ResolveGoImportType(importPath, filePath);
    }

    public ImportType ResolveGoImportType(string importPath, string filePath)
    {
        if (string.IsNullOrEmpty(importPath)) return ImportType.External;

        if (importPath.StartsWith('.') || importPath.StartsWith('/') || importPath.StartsWith('\\'))
            return ImportType.Internal;

        var dir = Path.GetDirectoryName(filePath);
        var goModFile = FindGoModFile(dir);
        if (goModFile != null)
        {
            var moduleName = _goModCache.GetOrAdd(goModFile, f => LoadGoModuleName(f));
            if (!string.IsNullOrEmpty(moduleName))
            {
                if (importPath.Equals(moduleName, StringComparison.OrdinalIgnoreCase) ||
                    importPath.StartWithIC(moduleName + "/"))
                {
                    return ImportType.Internal;
                }
            }
        }

        return ImportType.External;
    }

    private string? FindGoModFile(string? dir)
    {
        while (dir != null)
        {
            var path = Path.Combine(dir, "go.mod");
            if (File.Exists(path))
                return path;
            dir = Path.GetDirectoryName(dir);
        }
        return null;
    }

    private string LoadGoModuleName(string goModFile)
    {
        try
        {
            var lines = File.ReadLines(goModFile);
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("module ", StringComparison.Ordinal))
                {
                    return trimmed["module ".Length..].Trim();
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Ignore
        }
        return string.Empty;
    }

}
