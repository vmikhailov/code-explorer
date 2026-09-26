using System.Collections.Concurrent;
using CodeExplorer.Core.Common;
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
        new Libraries.PrismaLibraryParser(),
        new Libraries.DrizzleLibraryParser(),
        new Libraries.RedisLibraryParser(),
        new Libraries.SequelizeLibraryParser(),
        new Libraries.Sqlite3LibraryParser(),
        new Libraries.TypeOrmLibraryParser(),
        new GenericLibraryParser("bigquery", "BigQuery", OntologyConstants.LibraryTypes.AnalyticsDb, ["@google-cloud/bigquery", "bigquery"]),
        new GenericLibraryParser("clickhouse", "ClickHouse", OntologyConstants.LibraryTypes.AnalyticsDb, ["@clickhouse/client", "@clickhouse/client-web", "clickhouse"]),
        new Libraries.GcpLibraryParser(),
        new Libraries.RabbitMqLibraryParser(),
        new Libraries.KafkaJsLibraryParser(),
        new Libraries.BullMqLibraryParser(),

        // Generic Cloud Services
        new GenericLibraryParser("stripe", "Stripe", "cloud", ["stripe"]),
        new GenericLibraryParser("aws", "AWS", "cloud", ["aws-sdk", "@aws-sdk/*"]),
        new GenericLibraryParser("azure", "Azure", "cloud", ["@azure/*"]),

        new Libraries.NestJsLibraryParser(),
        new Libraries.NestJsCqrsLibraryParser(),
        new Libraries.ExpressLibraryParser(),
        new Libraries.FastifyLibraryParser(),
        new Libraries.KoaLibraryParser(),
        new Libraries.NextJsLibraryParser(),
        new Libraries.FetchLibraryParser(),
        new Libraries.GotKyLibraryParser(),
        new Libraries.SocketIoLibraryParser(),
        new Libraries.AngularHttpLibraryParser(),
        new Libraries.AngularOidcLibraryParser(),
        new Libraries.SignalRLibraryParser(),
        new Libraries.GraphQLClientLibraryParser(),

        // Generic Frameworks
        new GenericLibraryParser("react", "React", "framework", ["react"]),

        // Generic API Clients
        new GenericLibraryParser("request", "request", "api", ["request"]),
        new GenericLibraryParser("undici", "undici", "api", ["undici"]),
        new GenericLibraryParser("bent", "bent", "api", ["bent"]),
        new GenericLibraryParser("urllib", "urllib", "api", ["urllib"]),
    ];

    public bool UsesTreeSitter => true;

    public LanguageSyntaxProfile SyntaxProfile => TypeScriptSyntaxProfile.Instance;

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

    public string GetProjectName(string directoryPath, string[] filesInDirectory)
    {
        var packageJsonPath = Path.Combine(directoryPath, "package.json");
        if (File.Exists(packageJsonPath))
        {
            try
            {
                var content = File.ReadAllText(packageJsonPath);
                using var doc = System.Text.Json.JsonDocument.Parse(content);
                if (doc.RootElement.TryGetProperty("name", out var nameProp) && nameProp.ValueKind == System.Text.Json.JsonValueKind.String)
                {
                    var rawName = nameProp.GetString();
                    if (!string.IsNullOrWhiteSpace(rawName))
                    {
                        var name = rawName.Trim();
                        if (name.StartsWith('@') && name.Contains('/'))
                        {
                            name = name[(name.IndexOf('/') + 1)..].Trim();
                        }
                        if (!string.IsNullOrEmpty(name))
                        {
                            return name;
                        }
                    }
                }
            }
            catch
            {
                // Fallback to directory name
            }
        }

        return Path.GetFileName(directoryPath);
    }

    public Dictionary<string, string> ExtractManifestProperties(string directoryPath, string[] filesInDirectory)
    {
        var props = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // 1. Check wrangler.toml (Cloudflare Workers / Serverless)
        var hasWrangler = filesInDirectory.Any(f => Path.GetFileName(f).Equals("wrangler.toml", StringComparison.OrdinalIgnoreCase))
                          || File.Exists(Path.Combine(directoryPath, "wrangler.toml"));
        if (hasWrangler)
        {
            props["manifest_type"] = "worker";
            props["framework_type"] = "worker";
            props["cloud_runtime"] = "cloudflare-worker";
        }

        // 2. Check project.json (Nx monorepo project definition)
        var projectJsonPath = Path.Combine(directoryPath, "project.json");
        if (File.Exists(projectJsonPath))
        {
            try
            {
                var content = File.ReadAllText(projectJsonPath);
                using var doc = System.Text.Json.JsonDocument.Parse(content);
                if (doc.RootElement.TryGetProperty("projectType", out var ptProp) && ptProp.ValueKind == System.Text.Json.JsonValueKind.String)
                {
                    var pt = ptProp.GetString()?.ToLowerInvariant();
                    if (pt == "library")
                    {
                        props["manifest_type"] = "library";
                    }
                    else if (pt == "application")
                    {
                        props["manifest_type"] = "application";
                    }
                }
            }
            catch { }
        }

        // 3. Check ng-package.json or angular.json
        if (filesInDirectory.Any(f => Path.GetFileName(f).Equals("ng-package.json", StringComparison.OrdinalIgnoreCase)))
        {
            props["manifest_type"] = "library";
            props["framework_type"] = "frontend";
        }
        else if (filesInDirectory.Any(f => Path.GetFileName(f).Equals("angular.json", StringComparison.OrdinalIgnoreCase))
                 || File.Exists(Path.Combine(directoryPath, "angular.json")))
        {
            if (!props.ContainsKey("framework_type")) props["framework_type"] = "frontend";
        }

        // 4. Inspect package.json
        var packageJsonPath = Path.Combine(directoryPath, "package.json");
        if (File.Exists(packageJsonPath))
        {
            try
            {
                var content = File.ReadAllText(packageJsonPath);
                using var doc = System.Text.Json.JsonDocument.Parse(content);
                var root = doc.RootElement;

                // Check "bin" property
                if (root.TryGetProperty("bin", out var binProp))
                {
                    if (binProp.ValueKind == System.Text.Json.JsonValueKind.String && !string.IsNullOrWhiteSpace(binProp.GetString()))
                    {
                        props["has_cli_bin"] = "true";
                        if (!props.ContainsKey("manifest_type")) props["manifest_type"] = "cli";
                    }
                    else if (binProp.ValueKind == System.Text.Json.JsonValueKind.Object && binProp.EnumerateObject().Any())
                    {
                        props["has_cli_bin"] = "true";
                        if (!props.ContainsKey("manifest_type")) props["manifest_type"] = "cli";
                    }
                }

                // Check "ngPackage" in package.json
                if (root.TryGetProperty("ngPackage", out _))
                {
                    props["manifest_type"] = "library";
                    props["framework_type"] = "frontend";
                }

                // Collect dependencies
                var allDeps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (root.TryGetProperty("dependencies", out var depsObj) && depsObj.ValueKind == System.Text.Json.JsonValueKind.Object)
                {
                    foreach (var prop in depsObj.EnumerateObject()) allDeps.Add(prop.Name);
                }
                if (root.TryGetProperty("devDependencies", out var devDepsObj) && devDepsObj.ValueKind == System.Text.Json.JsonValueKind.Object)
                {
                    foreach (var prop in devDepsObj.EnumerateObject()) allDeps.Add(prop.Name);
                }

                // Framework detection
                if (allDeps.Any(d => d is "react" or "react-dom" or "@angular/core" or "vue" or "svelte" or "solid-js" or "next" or "nuxt"))
                {
                    props["framework_type"] = "frontend";
                    if (!props.ContainsKey("manifest_type")) props["manifest_type"] = "application";
                }
                else if (allDeps.Any(d => d is "express" or "@nestjs/core" or "fastify" or "koa" or "hono"))
                {
                    props["framework_type"] = "web";
                    if (!props.ContainsKey("manifest_type")) props["manifest_type"] = "application";
                }
                else if (allDeps.Any(d => d is "bullmq" or "bull" or "amqplib" or "kafkajs" or "@cloudflare/workers-types" or "wrangler"))
                {
                    props["framework_type"] = "worker";
                    if (!props.ContainsKey("manifest_type")) props["manifest_type"] = "worker";
                }
                else if (allDeps.Any(d => d.StartsWith("@aws-cdk/") || d.StartsWith("@pulumi/") || d == "serverless"))
                {
                    props["framework_type"] = "cloud";
                }

                // Check if library by export declarations (main, types, exports without index.html)
                if (!props.ContainsKey("manifest_type"))
                {
                    var hasMainOrTypes = root.TryGetProperty("main", out _) || root.TryGetProperty("types", out _) || root.TryGetProperty("exports", out _);
                    var hasIndexHtml = filesInDirectory.Any(f => Path.GetFileName(f).Equals("index.html", StringComparison.OrdinalIgnoreCase));
                    if (hasMainOrTypes && !hasIndexHtml)
                    {
                        props["manifest_type"] = "library";
                    }
                }
            }
            catch { }
        }

        return props;
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

                        // Check if it is a local file/path reference (e.g. file:../lib or workspace:../lib)
                        if (packageVersion.StartsWith("file:", StringComparison.Ordinal) ||
                            (packageVersion.StartsWith("workspace:", StringComparison.Ordinal) && (packageVersion.Contains('/') || packageVersion.Contains('\\'))))
                        {
                            var relativePath = packageVersion[(packageVersion.IndexOf(':') + 1)..];
                            if (!string.IsNullOrEmpty(relativePath) && (relativePath.StartsWith('.') || relativePath.StartsWith('/') || relativePath.StartsWith('\\')))
                            {
                                try
                                {
                                    var referencedDir = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(packageJsonPath)!, relativePath)).Replace('\\', '/');
                                    if (Directory.Exists(referencedDir) || File.Exists(referencedDir))
                                    {
                                        localProjectPaths.Add(referencedDir);
                                        continue;
                                    }
                                }
                                catch
                                {
                                    // Fallback to external package
                                }
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
        while (curr.IsValid())
        {
            if (curr!.IsAny(TreeSitterSyntax.TypeScript.ClassDeclaration, TreeSitterSyntax.TypeScript.InterfaceDeclaration))
            {
                var nameNode = curr.GetChildForField(TreeSitterSyntax.Fields.Name);
                if (nameNode.IsValid()) return nameNode!.Text;
            }
            else if (curr!.IsAny(TreeSitterSyntax.TypeScript.FunctionDeclaration, TreeSitterSyntax.TypeScript.MethodDefinition))
            {
                var nameNode = curr.GetChildForField(TreeSitterSyntax.Fields.Name);
                if (nameNode.IsValid())
                {
                    var nameText = nameNode!.Text;
                    if (nameText == "constructor")
                    {
                        var classNode = curr.Parent;
                        while (classNode.IsValid())
                        {
                            if (classNode!.IsAny(TreeSitterSyntax.TypeScript.ClassDeclaration, TreeSitterSyntax.TypeScript.InterfaceDeclaration))
                            {
                                var classNameNode = classNode.GetChildForField(TreeSitterSyntax.Fields.Name);
                                if (classNameNode.IsValid()) return classNameNode!.Text;
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
