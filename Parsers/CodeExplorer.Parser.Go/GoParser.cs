using System.Collections.Concurrent;
using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.Go;

public class GoParser : IProjectParser, IFileParser
{
    public string LanguageName => "go";

    public string ProjectType => "go";

    public IReadOnlyCollection<string> ExcludedFolders => ["vendor"];

    public IReadOnlyList<ILibraryParser> LibraryParsers { get; } =
    [
        new Libraries.ElasticsearchGoLibraryParser(),
        new Libraries.GoRedisLegacyLibraryParser(),
        new Libraries.GoRedisLibraryParser(),
        new Libraries.GormLibraryParser(),
        new Libraries.GoSqlDriverMysqlLibraryParser(),
        new Libraries.GoSqlite3LibraryParser(),
        new Libraries.GoSqlLibraryParser(),
        new Libraries.LibPqLibraryParser(),
        new Libraries.MongoGoLibraryParser(),

        // Generic Cloud Services
        new GenericLibraryParser("stripe", "Stripe", "cloud", ["github.com/stripe/stripe-go"]),
        new GenericLibraryParser("aws", "AWS", "cloud", ["github.com/aws/aws-sdk-go"]),
        new GenericLibraryParser("gcp", "GCP", "cloud", ["cloud.google.com/", "firebase.google.com/"]),
        new GenericLibraryParser("azure", "Azure", "cloud", ["/Azure/", "/azure-sdk-for-go"]),

        // Generic Frameworks
        new GenericLibraryParser("gin", "Gin", "framework", ["github.com/gin-gonic/gin"]),
        new GenericLibraryParser("echo", "Echo", "framework", ["github.com/labstack/echo"]),
        new GenericLibraryParser("fiber", "Fiber", "framework", ["github.com/gofiber/fiber"]),

        // Generic API Clients
        new GenericLibraryParser("net/http", "http/https", "api", ["net/http"], isBuiltIn: true),
        new GenericLibraryParser("resty", "Resty", "api", ["github.com/go-resty/resty"]),
        new GenericLibraryParser("req", "req", "api", ["github.com/imroc/req"]),
        new GenericLibraryParser("grequests", "grequests", "api", ["github.com/levigross/grequests"]),
        new GenericLibraryParser("gorequest", "gorequest", "api", ["github.com/parnurzeal/gorequest"]),
        new GenericLibraryParser("surf", "surf", "api", ["github.com/go-surf/surf"]),
    ];

    public bool CanParse(string fileExtension)
    {
        return fileExtension.Equals(".go", StringComparison.OrdinalIgnoreCase);
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
        TreeSitter.Node rootNode,
        List<ILibraryParser> activeLibraryParsers,
        string relativePath,
        string absoluteWorkspacePath,
        IFileParser fileParser,
        LibraryTrieRegistry libraryRegistry)
    {
        return new GoFileVisitor(
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
        var goModPath = Path.Combine(projectDirectory, "go.mod");
        if (!File.Exists(goModPath)) return null;

        try
        {
            var lines = await File.ReadAllLinesAsync(goModPath);
            var moduleLine = lines.FirstOrDefault(l => l.Trim().StartsWith("module "));
            if (moduleLine != null)
            {
                var modName = moduleLine.Trim().Substring("module ".Length).Trim();
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
        catch
        {
            // Ignore
        }

        return null;
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

            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();
                if (string.IsNullOrEmpty(line)) continue;

                // Handle single-line require
                if (line.StartsWith("require ") && !line.EndsWith("("))
                {
                    var content = line.Substring("require ".Length).Trim();
                    var parts = content.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 1)
                    {
                        var name = parts[0];
                        var version = parts.Length >= 2 ? parts[1] : "1.0.0";
                        externalPackages.Add(new ProducedPackageInfo(name, version, "go"));
                    }
                }
                else if (line.StartsWith("require ("))
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
        catch
        {
            // Ignore
        }

        return new ProjectDependencyInfo(localProjectPaths, externalPackages);
    }

    public bool UsesTreeSitter => true;
    public async Task<SyntaxTree> ParseAsync(string filePath, string parentNodeId, string workspaceId, string absoluteWorkspacePath)
    {
        var relativePath = Path.GetRelativePath(absoluteWorkspacePath, filePath).Replace('\\', '/');
        return await SyntaxTree.ParseAsync(filePath, relativePath, parentNodeId, this, workspaceId, absoluteWorkspacePath);
    }

    public ISyntaxEnricher GetSyntaxEnricher(SyntaxTree syntaxTree) => new GoSyntaxEnricher(LibraryParsers, syntaxTree);

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
                    importPath.StartsWith(moduleName + "/", StringComparison.OrdinalIgnoreCase))
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
                if (trimmed.StartsWith("module "))
                {
                    return trimmed.Substring("module ".Length).Trim();
                }
            }
        }
        catch
        {
            // Ignore
        }
        return string.Empty;
    }

}
