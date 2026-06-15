using System.Collections.Concurrent;
using CodeExplorer.Core.Parser;
using TreeSitter;

[assembly: ParserAssembly]

namespace CodeExplorer.Parser.TypeScript;

public class TypeScriptParser : IProjectParser, IFileParser
{

    public string LanguageName => "typescript";

    public string ProjectType => "typescript";

    public IReadOnlyCollection<string> ExcludedFolders => ["node_modules", "dist", "build", ".next", "out"];

    public IReadOnlyList<ILibraryParser> LibraryParsers { get; } =
    [
        new Libraries.AxiosLibraryParser(),
        new Libraries.ElasticsearchTsLibraryParser(),
        new Libraries.InfluxDbLibraryParser(),
        new Libraries.KnexLibraryParser(),
        new Libraries.MongodbLibraryParser(),
        new Libraries.MongooseLibraryParser(),
        new Libraries.Mysql2LibraryParser(),
        new Libraries.Neo4jLibraryParser(),
        new Libraries.PgLibraryParser(),
        new Libraries.RedisLibraryParser(),
        new Libraries.SequelizeLibraryParser(),
        new Libraries.Sqlite3LibraryParser(),
        new Libraries.TypeOrmLibraryParser(),
        new Libraries.GcpLibraryParser(),
        new Libraries.RabbitMqLibraryParser(),

        // Generic Cloud Services
        new GenericLibraryParser("stripe", "Stripe", "cloud", ["stripe"]),
        new GenericLibraryParser("aws", "AWS", "cloud", ["aws-sdk", "@aws-sdk/*"]),
        new GenericLibraryParser("azure", "Azure", "cloud", ["@azure/*"]),

        new Libraries.NestJsLibraryParser(),
        new Libraries.ExpressLibraryParser(),
        new Libraries.FetchLibraryParser(),
        new Libraries.SocketIoLibraryParser(),

        // Generic Frameworks
        new GenericLibraryParser("nextjs", "Next.js", "framework", ["next"]),
        new GenericLibraryParser("react", "React", "framework", ["react"]),
        new GenericLibraryParser("angular", "Angular", "framework", ["@angular/core"]),

        // Generic API Clients
        new GenericLibraryParser("request", "request", "api", ["request"]),
        new GenericLibraryParser("undici", "undici", "api", ["undici"]),
        new GenericLibraryParser("ky", "ky", "api", ["ky"]),
        new GenericLibraryParser("bent", "bent", "api", ["bent"]),
        new GenericLibraryParser("urllib", "urllib", "api", ["urllib"]),
    ];

    public bool CanParse(string fileExtension)
    {
        return fileExtension.Equals(".ts", StringComparison.OrdinalIgnoreCase) ||
               fileExtension.Equals(".tsx", StringComparison.OrdinalIgnoreCase);
    }

    public bool IsProjectDirectory(string directoryPath, string[] filesInDirectory)
    {
        foreach (var file in filesInDirectory)
        {
            var fileName = Path.GetFileName(file).ToLowerInvariant();
            if (fileName == "package.json" || fileName == "tsconfig.json")
            {
                return true;
            }
        }
        return false;
    }

    public BaseParserVisitor CreateVisitor(
        Node rootNode,
        List<ILibraryParser> activeLibraryParsers,
        string relativePath,
        string absoluteWorkspacePath,
        IFileParser fileParser,
        LibraryTrieRegistry libraryRegistry)
    {
        return new TypeScriptFileVisitor(
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
        var packageJsonPath = Path.Combine(projectDirectory, "package.json");
        if (!File.Exists(packageJsonPath)) return null;

        try
        {
            var content = await File.ReadAllTextAsync(packageJsonPath);
            using var doc = System.Text.Json.JsonDocument.Parse(content);
            var root = doc.RootElement;

            // Check private attribute for npm publishing
            if (root.TryGetProperty("private", out var privateProp))
            {
                if (privateProp.ValueKind == System.Text.Json.JsonValueKind.True ||
                    (privateProp.ValueKind == System.Text.Json.JsonValueKind.String &&
                     string.Equals(privateProp.GetString(), "true", StringComparison.OrdinalIgnoreCase)))
                {
                    return null;
                }
            }

            if (root.TryGetProperty("name", out var nameProp) && nameProp.ValueKind == System.Text.Json.JsonValueKind.String)
            {
                var name = nameProp.GetString();
                if (string.IsNullOrEmpty(name)) return null;

                var version = "1.0.0";
                if (root.TryGetProperty("version", out var versionProp) && versionProp.ValueKind == System.Text.Json.JsonValueKind.String)
                {
                    version = versionProp.GetString() ?? "1.0.0";
                }

                return new ProducedPackageInfo(name, version, "npm");
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

        var packageJsonPath = Path.Combine(projectDirectory, "package.json");
        if (!File.Exists(packageJsonPath))
        {
            return new ProjectDependencyInfo(localProjectPaths, externalPackages);
        }

        try
        {
            var content = await File.ReadAllTextAsync(packageJsonPath);
            using var doc = System.Text.Json.JsonDocument.Parse(content);
            var root = doc.RootElement;

            var depProperties = new[] { "dependencies", "devDependencies" };
            foreach (var propName in depProperties)
            {
                if (root.TryGetProperty(propName, out var depsObj) && depsObj.ValueKind == System.Text.Json.JsonValueKind.Object)
                {
                    foreach (var prop in depsObj.EnumerateObject())
                    {
                        var packageName = prop.Name;
                        var packageVersion = prop.Value.GetString() ?? "unknown";

                        // Check if it is a local workspace project reference
                        if (packageVersion.StartsWith("file:") || packageVersion.StartsWith("workspace:"))
                        {
                            var relativePath = packageVersion.Substring(packageVersion.IndexOf(':') + 1);
                            if (!string.IsNullOrEmpty(relativePath))
                            {
                                var referencedDir = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(packageJsonPath)!, relativePath)).Replace('\\', '/');
                                localProjectPaths.Add(referencedDir);
                                continue;
                            }
                        }

                        // Treat as npm package reference
                        externalPackages.Add(new ProducedPackageInfo(packageName, packageVersion, "npm"));
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

    public ISyntaxEnricher GetSyntaxEnricher(SyntaxTree syntaxTree) => new SyntaxEnricher(LibraryParsers, syntaxTree);

    private readonly ConcurrentDictionary<string, HashSet<string>> _tsDepsCache = new(StringComparer.OrdinalIgnoreCase);

    public ImportType ResolveImportType(string importPath, string filePath, string? absoluteWorkspacePath)
    {
        return ResolveTsImportType(importPath, filePath);
    }

    public ImportType ResolveTsImportType(string importPath, string filePath)
    {
        if (string.IsNullOrEmpty(importPath)) return ImportType.External;

        if (importPath.StartsWith('.') || importPath.StartsWith('/') || importPath.StartsWith('\\'))
            return ImportType.Internal;

        if (importPath.StartsWith("@/"))
            return ImportType.Internal;

        var dir = Path.GetDirectoryName(filePath);
        var projectDir = FindProjectDirectoryWithPackageJson(dir);
        if (projectDir != null)
        {
            var deps = _tsDepsCache.GetOrAdd(projectDir, _ => LoadPackageJsonDependencies(projectDir));

            if (importPath.StartsWith("@"))
            {
                var parts = importPath.Split('/');
                if (parts.Length < 2) return ImportType.Internal;

                var scopeAndPackage = $"{parts[0]}/{parts[1]}";
                if (deps.Contains(scopeAndPackage) || deps.Contains(importPath))
                    return ImportType.External;

                if (deps.Any(d => d.StartsWith(parts[0] + "/")))
                    return ImportType.External;

                return ImportType.Internal;
            }
            else
            {
                var parts = importPath.Split('/');
                var firstSegment = parts[0];
                if (deps.Contains(firstSegment) || deps.Contains(importPath))
                    return ImportType.External;

                var builtIns = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "fs", "path", "os", "http", "https", "crypto", "child_process", "dns", "events", "net", "stream", "util", "url", "zlib"
                };
                if (builtIns.Contains(firstSegment))
                    return ImportType.External;

                return ImportType.Internal;
            }
        }

        return ImportType.External;
    }

    private string? FindProjectDirectoryWithPackageJson(string? dir)
    {
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir, "package.json")))
                return dir;
            dir = Path.GetDirectoryName(dir);
        }
        return null;
    }

    private HashSet<string> LoadPackageJsonDependencies(string projectDir)
    {
        var deps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var path = Path.Combine(projectDir, "package.json");
        if (!File.Exists(path)) return deps;

        try
        {
            var content = File.ReadAllText(path);
            using var doc = System.Text.Json.JsonDocument.Parse(content);
            var root = doc.RootElement;
            var depProperties = new[] { "dependencies", "devDependencies" };
            foreach (var propName in depProperties)
            {
                if (root.TryGetProperty(propName, out var depsObj) && depsObj.ValueKind == System.Text.Json.JsonValueKind.Object)
                {
                    foreach (var prop in depsObj.EnumerateObject())
                    {
                        deps.Add(prop.Name);
                    }
                }
            }
        }
        catch
        {
            // Ignore
        }
        return deps;
    }

    private static string GetContainingScopeName(Node node)
    {
        var curr = node.Parent;
        while (curr != null && curr.Id != IntPtr.Zero)
        {
            if (curr.Type is "class_declaration" or "interface_declaration")
            {
                var nameNode = curr.GetChildForField("name");
                if (nameNode != null && nameNode.Id != IntPtr.Zero) return nameNode.Text;
            }
            else if (curr.Type is "function_declaration" or "method_definition")
            {
                var nameNode = curr.GetChildForField("name");
                if (nameNode != null && nameNode.Id != IntPtr.Zero)
                {
                    var nameText = nameNode.Text;
                    if (nameText == "constructor")
                    {
                        var classNode = curr.Parent;
                        while (classNode != null && classNode.Id != IntPtr.Zero)
                        {
                            if (classNode.Type is "class_declaration" or "interface_declaration")
                            {
                                var classNameNode = classNode.GetChildForField("name");
                                if (classNameNode != null && classNameNode.Id != IntPtr.Zero) return classNameNode.Text;
                            }
                            classNode = classNode.Parent;
                        }
                    }
                    return nameText;
                }
            }
            curr = curr.Parent;
        }
        return "global";
    }

}
