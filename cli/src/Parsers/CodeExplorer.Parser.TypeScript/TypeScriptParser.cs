using System.Collections.Concurrent;
using CodeExplorer.Core.Parser.Syntax;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

[assembly: ParserAssembly]

namespace CodeExplorer.Parser.TypeScript;

[ProjectParser(Tier = ParserTier.Secondary, Supersedes = ["javascript"])]
public partial class TypeScriptParser : IProjectParser, IFileParser
{

    public string LanguageName => "typescript";

    public string ProjectType => "typescript";

    public IReadOnlyCollection<string> ExcludedFolders =>
    [
        "node_modules", "dist", "build", ".build", ".next", ".nuxt",
        ".turbo", ".cache", ".output", "out", "coverage", "bower_components"
    ];

    public IReadOnlyCollection<string> ManifestFileNames => ["package.json", "wrangler.toml", "wrangler.jsonc", "wrangler.json", "angular.json", "nest-cli.json"];
    public IReadOnlyCollection<string> ManifestExtensions => [];

    public bool ShouldSkipFileName(string fileName)
    {
        var fn = fileName.ToLowerInvariant();
        if (fn.EndsWith(".d.ts") ||
            fn.EndsWith(".min.js") || fn.EndsWith(".min.mjs") || fn.EndsWith(".min.cjs") ||
            fn.EndsWith(".bundle.js") || fn.EndsWith(".bundle.min.js") ||
            fn.Contains(".min.") ||
            fn.EndsWith("_debug.ts") || fn.EndsWith("_debug.js"))
        {
            return true;
        }

        return IProjectParser.DefaultShouldSkipFileName(fileName);
    }

    public bool ShouldSkipFile(FileInfo fileInfo)
    {
        var ext = fileInfo.Extension;
        if (ext.Equals(".js", StringComparison.OrdinalIgnoreCase) ||
            ext.Equals(".ts", StringComparison.OrdinalIgnoreCase) ||
            ext.Equals(".css", StringComparison.OrdinalIgnoreCase))
        {
            if (WorkspaceFileFilter.IsMinifiedContent(fileInfo.FullName))
            {
                return true;
            }
        }

        return false;
    }

    public bool IsConfigurationFile(string fileName)
    {
        var lower = fileName.ToLowerInvariant();
        return lower.StartsWith(".env");
    }

    public IReadOnlyList<PackageDescriptor> Packages { get; }

    public TypeScriptParser()
    {
        Packages = [.. SemanticExtensions.SelectMany(e => e.GetPackageDescriptors())];
    }

    public IReadOnlyList<ISemanticExtension> SemanticExtensions { get; } =
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
        new Libraries.GcpLibraryParser(),
        new Libraries.RabbitMqLibraryParser(),
        new Libraries.KafkaJsLibraryParser(),
        new Libraries.BullMqLibraryParser(),
        new Libraries.NestJsLibraryParser(),
        new Libraries.NestJsCqrsLibraryParser(),
        new Libraries.RoutingControllersLibraryParser(),
        new Libraries.ExpressLibraryParser(),
        new Libraries.FastifyLibraryParser(),
        new Libraries.KoaLibraryParser(),
        new Libraries.NextJsLibraryParser(),
        new Libraries.FetchLibraryParser(),
        new Libraries.GotKyLibraryParser(),
        new Libraries.RequestLibraryParser(),
        new Libraries.UndiciLibraryParser(),
        new Libraries.UrllibLibraryParser(),
        new Libraries.SocketIoLibraryParser(),
        new Libraries.AngularHttpLibraryParser(),
        new Libraries.AngularOidcLibraryParser(),
        new Libraries.SignalRLibraryParser(),
        new Libraries.GraphQLClientLibraryParser(),
        new Libraries.PothosLibraryParser(),
        new Libraries.ClickHouseLibraryParser(),
        new Libraries.BigQueryLibraryParser(),
        new Libraries.StripeLibraryParser(),
        new Libraries.AwsSdkLibraryParser(),
        new Libraries.AzureSdkLibraryParser(),
        new Libraries.ReactLibraryParser(),
        new Libraries.VueLibraryParser(),
        new Libraries.AngularCoreLibraryParser(),
        new Libraries.SvelteLibraryParser(),
        new Libraries.ViteLibraryParser(),
        new Libraries.VsCodeLibraryParser(),
        new Libraries.JestVitestLibraryParser(),
        new Libraries.MochaLibraryParser(),
        new Libraries.CypressLibraryParser(),
        new Libraries.PlaywrightLibraryParser()
    ];

    public bool UsesTreeSitter => true;

    public SyntaxDocument? ParseDocument(string sourceText, string filePath = "", string relativePath = "") =>
        TypeScriptSyntaxConverter.Instance.Parse(
            sourceText,
            filePath.EndsWith(".tsx", StringComparison.OrdinalIgnoreCase) || relativePath.EndsWith(".tsx", StringComparison.OrdinalIgnoreCase) ? "tsx" : LanguageName,
            filePath,
            relativePath);

    public LanguageSyntaxProfile SyntaxProfile => TypeScriptSyntaxProfile.Instance;

    public bool CanParse(string fileExtension)
    {
        return fileExtension.Equals(".ts", StringComparison.OrdinalIgnoreCase) ||
               fileExtension.Equals(".tsx", StringComparison.OrdinalIgnoreCase) ||
               fileExtension.Equals(".mts", StringComparison.OrdinalIgnoreCase) ||
               fileExtension.Equals(".cts", StringComparison.OrdinalIgnoreCase);
    }

    public void ExtractDeclarations(Node rootNode, Action<string, Node?, string?> registerDeclaration)
    {
        TypeScriptDeclarationExtractor.Extract(rootNode, registerDeclaration);
    }

    public bool IsProjectDirectory(string directoryPath, string[] filesInDirectory)
    {
        if (HasWranglerManifest(filesInDirectory))
        {
            return true;
        }

        var packageJson = filesInDirectory.FirstOrDefault(f => Path.GetFileName(f).Equals("package.json", StringComparison.OrdinalIgnoreCase));
        if (packageJson != null)
        {
            if (IsSubpathEntryPointPackageJson(packageJson, directoryPath) ||
                IsInsideParentWebOrHybridProject(directoryPath) ||
                IsAbsorbedWorkersSiteDirectory(directoryPath))
            {
                return false;
            }
            return true;
        }

        foreach (var file in filesInDirectory)
        {
            var fileName = Path.GetFileName(file).ToLowerInvariant();
            if (fileName == "tsconfig.json")
            {
                if (IsInsideParentProject(directoryPath) || HasOnlyAmbientDeclarationFiles(directoryPath))
                {
                    return false;
                }
                return true;
            }
        }
        return false;
    }

    public static bool HasWranglerManifest(string[] filesInDirectory) =>
        filesInDirectory.Any(f =>
        {
            var fn = Path.GetFileName(f);
            return fn.Equals("wrangler.toml", StringComparison.OrdinalIgnoreCase) ||
                   fn.Equals("wrangler.jsonc", StringComparison.OrdinalIgnoreCase) ||
                   fn.Equals("wrangler.json", StringComparison.OrdinalIgnoreCase);
        });

    public static bool IsAbsorbedWorkersSiteDirectory(string directoryPath)
    {
        var dirName = Path.GetFileName(directoryPath.TrimEnd('/', '\\'));
        if (!dirName.Equals("workers-site", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var parentDir = Path.GetDirectoryName(directoryPath.TrimEnd('/', '\\'));
        if (string.IsNullOrEmpty(parentDir)) return false;

        return File.Exists(Path.Combine(parentDir, "wrangler.toml")) ||
               File.Exists(Path.Combine(parentDir, "wrangler.jsonc")) ||
               File.Exists(Path.Combine(parentDir, "wrangler.json"));
    }

    private static bool IsInsideParentWebOrHybridProject(string directoryPath)
    {
        var parentDir = Path.GetDirectoryName(directoryPath);
        int depth = 0;
        while (parentDir != null && parentDir.Length >= 3 && depth < 3)
        {
            depth++;
            try
            {
                var hasParentCsproj = Directory.EnumerateFiles(parentDir, "*.csproj").Any();
                var parentPkgJson = Path.Combine(parentDir, "package.json");
                var hasParentWebConfig = Directory.EnumerateFiles(parentDir, "*").Any(f =>
                    Path.GetFileName(f).Equals("web.config", StringComparison.OrdinalIgnoreCase));

                if (hasParentCsproj && (File.Exists(parentPkgJson) || hasParentWebConfig))
                {
                    return true;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Ignore
            }
            parentDir = Path.GetDirectoryName(parentDir);
        }
        return false;
    }

    private static bool IsInsideParentProject(string directoryPath)
    {
        var parentDir = Path.GetDirectoryName(directoryPath);
        int depth = 0;
        while (parentDir != null && parentDir.Length >= 3 && depth < 8)
        {
            depth++;
            try
            {
                if (Directory.EnumerateFiles(parentDir, "*.csproj").Any())
                {
                    return true;
                }

                var parentPkgJson = Path.Combine(parentDir, "package.json");
                if (File.Exists(parentPkgJson))
                {
                    var content = PhysicalFileStore.ReadAllText(parentPkgJson);
                    using var doc = System.Text.Json.JsonDocument.Parse(content);
                    if (!doc.RootElement.TryGetProperty("workspaces", out _))
                    {
                        return true;
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
            {
                // Ignore
            }
            parentDir = Path.GetDirectoryName(parentDir);
        }
        return false;
    }

    private static bool HasOnlyAmbientDeclarationFiles(string directoryPath)
    {
        try
        {
            var allFiles = WorkspaceFileFilter.Default.EnumerateFiles(directoryPath, "*").ToList();
            bool hasAnyDts = false;
            foreach (var f in allFiles)
            {
                var lower = f.ToLowerInvariant();
                if (lower.EndsWith(".d.ts") || lower.EndsWith(".d.mts") || lower.EndsWith(".d.cts"))
                {
                    hasAnyDts = true;
                    continue;
                }
                if (lower.EndsWith(".ts") || lower.EndsWith(".tsx") || lower.EndsWith(".js") || lower.EndsWith(".jsx") || lower.EndsWith(".mts") || lower.EndsWith(".cts"))
                {
                    return false;
                }
            }
            return hasAnyDts;
        }
        catch
        {
            return false;
        }
    }

    public static bool IsSubpathEntryPointPackageJson(string? packageJsonPath, string directoryPath)
    {
        if (string.IsNullOrEmpty(packageJsonPath) || !File.Exists(packageJsonPath))
        {
            return false;
        }

        try
        {
            var content = PhysicalFileStore.ReadAllText(packageJsonPath);
            using var doc = System.Text.Json.JsonDocument.Parse(content);
            var root = doc.RootElement;
            if (root.ValueKind != System.Text.Json.JsonValueKind.Object || !root.EnumerateObject().Any())
            {
                return false;
            }

            if (root.TryGetProperty("name", out var nameProp) && !string.IsNullOrWhiteSpace(nameProp.GetString()))
            {
                return false;
            }

            if (IsInsideParentProject(directoryPath))
            {
                return true;
            }

            var hasRootManifestFields =
                root.TryGetProperty("version", out _) ||
                root.TryGetProperty("dependencies", out _) ||
                root.TryGetProperty("devDependencies", out _) ||
                root.TryGetProperty("peerDependencies", out _) ||
                root.TryGetProperty("scripts", out _) ||
                root.TryGetProperty("workspaces", out _) ||
                root.TryGetProperty("private", out _);

            return !hasRootManifestFields;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            return false;
        }
    }

    public string GetProjectName(string directoryPath, string[] filesInDirectory)
    {
        var wranglerName = TryExtractWranglerName(directoryPath, filesInDirectory);
        if (!string.IsNullOrEmpty(wranglerName))
        {
            return wranglerName;
        }

        var packageJsonPath = Path.Combine(directoryPath, "package.json");
        if (File.Exists(packageJsonPath))
        {
            try
            {
                var content = PhysicalFileStore.ReadAllText(packageJsonPath);

                using var doc = System.Text.Json.JsonDocument.Parse(content);

                if (doc.RootElement.TryGetProperty("name", out var nameProp) &&
                    nameProp.ValueKind == System.Text.Json.JsonValueKind.String)
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
                            var stripped = WorkspaceConventions.StripServicePrefix(name);
                            return !string.IsNullOrEmpty(stripped) ? stripped : name;
                        }
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
            {
                // Fallback to directory name
            }
        }

        return Path.GetFileName(directoryPath.TrimEnd('/', '\\'));
    }

    public bool TryParse(ProjectParseContext context, out ProjectParseResult? result)
    {
        var dir = context.DirectoryPath;
        var files = context.FilesInDirectory;

        if (!IsProjectDirectory(dir, files))
        {
            result = null;
            return false;
        }

        var name = GetProjectName(dir, files);
        if (string.IsNullOrEmpty(name))
        {
            name = Path.GetFileName(dir.TrimEnd('/', '\\'));
        }

        var props = ExtractManifestProperties(dir, files);
        var absorbedSubpaths = new List<string>();
        var excludedSubpaths = new List<string>();
        var entryPointPaths = new List<string>();
        var sourceRoots = new List<string>();
        var subProjects = new List<ParsedProjectDescriptor>();

        // 1. Wrangler (Cloudflare Worker) embedded workers-site and static bucket handling
        InspectWranglerLayout(dir, files, absorbedSubpaths, excludedSubpaths, entryPointPaths, sourceRoots);

        // 2. Conventional static asset subfolders (public_src, public) when inside a worker or build orchestrator
        foreach (var staticCandidate in new[] { "public_src", "public", "workers/public" })
        {
            var fullCandidate = Path.Combine(dir, staticCandidate);
            if (Directory.Exists(fullCandidate) && !excludedSubpaths.Contains(staticCandidate, StringComparer.OrdinalIgnoreCase))
            {
                excludedSubpaths.Add(staticCandidate);
            }
        }

        // 3. VS Code Extension embedded webview folders (Primary/Secondary relationship -> absorbed into parent extension)
        if (props.GetValueOrDefault("is_vscode_extension") == "true")
        {
            foreach (var webviewDirName in new[] { "webview", "webview-ui", "media", "views" })
            {
                var fullWebviewDir = Path.Combine(dir, webviewDirName);
                if (Directory.Exists(fullWebviewDir) && !absorbedSubpaths.Contains(webviewDirName, StringComparer.OrdinalIgnoreCase))
                {
                    absorbedSubpaths.Add(webviewDirName);
                }
            }
        }

        // 4. Declarative SubProjects in angular.json or nest-cli.json
        ExtractWorkspaceManifestSubProjects(dir, sourceRoots, excludedSubpaths, subProjects);

        // 5. Detect build-orchestrator root package.json (e.g. Gulp/Webpack static builder wrapping child workers in subfolders)
        DetectBuildOrchestratorProperties(dir, files, props);

        if (absorbedSubpaths.Count > 0)
        {
            props["absorbed_subpaths"] = System.Text.Json.JsonSerializer.Serialize(absorbedSubpaths);
        }
        if (excludedSubpaths.Count > 0)
        {
            props["excluded_subpaths"] = System.Text.Json.JsonSerializer.Serialize(excludedSubpaths);
        }

        result = new ProjectParseResult(
            Project: new ParsedProjectDescriptor(
                Name: name,
                DirectoryPath: dir,
                ProjectType: ProjectType,
                ManifestProperties: props,
                SourceRoots: sourceRoots.Count > 0 ? sourceRoots : null,
                AbsorbedSubpaths: absorbedSubpaths.Count > 0 ? absorbedSubpaths : null,
                ExcludedSubpaths: excludedSubpaths.Count > 0 ? excludedSubpaths : null,
                EntryPointPaths: entryPointPaths.Count > 0 ? entryPointPaths : null,
                SubProjects: subProjects.Count > 0 ? subProjects : null),
            ClaimMode: ParserClaimMode.AbsorbSecondaryInDirectory);
        return true;
    }

    public BaseParserVisitor CreateVisitor(
        Node rootNode,
        List<ISemanticExtension> activeExtensions,
        string relativePath,
        string absoluteWorkspacePath,
        IFileParser fileParser,
        SemanticExtensionRegistry extensionRegistry)
    {
        return new TypeScriptFileVisitor(
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
        var packageJsonPath = Path.Combine(projectDirectory, "package.json");
        if (!File.Exists(packageJsonPath)) return null;

        try
        {
            var content = await PhysicalFileStore.ReadAllTextAsync(packageJsonPath);

            using var doc = System.Text.Json.JsonDocument.Parse(content);

            var root = doc.RootElement;

            if (root.TryGetProperty("name", out var nameProp) &&
                nameProp.ValueKind == System.Text.Json.JsonValueKind.String)
            {
                var name = nameProp.GetString();
                if (string.IsNullOrEmpty(name)) return null;

                var version = "1.0.0";

                if (root.TryGetProperty("version", out var versionProp) &&
                    versionProp.ValueKind == System.Text.Json.JsonValueKind.String)
                {
                    version = versionProp.GetString() ?? "1.0.0";
                }

                return new ProducedPackageInfo(name, version, "npm");
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
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
            var content = await PhysicalFileStore.ReadAllTextAsync(packageJsonPath);

            using var doc = System.Text.Json.JsonDocument.Parse(content);

            var root = doc.RootElement;

            var depProperties = new[] { "dependencies", "devDependencies" };

            foreach (var propName in depProperties)
            {
                if (root.TryGetProperty(propName, out var depsObj) &&
                    depsObj.ValueKind == System.Text.Json.JsonValueKind.Object)
                {
                    foreach (var prop in depsObj.EnumerateObject())
                    {
                        var packageName = prop.Name;
                        var packageVersion = prop.Value.GetString() ?? "unknown";

                        // Check if it is a local file/path reference (e.g. file:../lib or workspace:../lib)
                        if (packageVersion.StartsWith("file:", StringComparison.Ordinal) ||
                            (packageVersion.StartsWith("workspace:", StringComparison.Ordinal) &&
                             (packageVersion.Contains('/') || packageVersion.Contains('\\'))))
                        {
                            var relativePath = packageVersion[(packageVersion.IndexOf(':') + 1)..];

                            if (!string.IsNullOrEmpty(relativePath) && (relativePath.StartsWith('.') ||
                                                                        relativePath.StartsWith('/') ||
                                                                        relativePath.StartsWith('\\')))
                            {
                                try
                                {
                                    var referencedDir =
                                        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(packageJsonPath) ?? string.Empty,
                                            relativePath)).Replace('\\', '/');

                                    if (Directory.Exists(referencedDir) || File.Exists(referencedDir))
                                    {
                                        localProjectPaths.Add(referencedDir);
                                        continue;
                                    }
                                }
                                catch (Exception ex) when (ex is ArgumentException or PathTooLongException)
                                {
                                    // Fallback to external package
                                }
                            }
                        }

                        // Treat as npm package reference
                        externalPackages.Add(new ProducedPackageInfo(packageName, packageVersion, "npm", propName == "devDependencies" ? "dev" : "runtime"));
                    }
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
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

    private readonly ConcurrentDictionary<string, HashSet<string>> _tsDepsCache = new(StringComparer.OrdinalIgnoreCase);

    public ImportType ResolveImportType(string importPath, string filePath, string? absoluteWorkspacePath)
    {
        return ResolveTsImportType(importPath, filePath, absoluteWorkspacePath);
    }

    public ImportType ResolveTsImportType(string importPath, string filePath, string? absoluteWorkspacePath = null)
    {
        if (string.IsNullOrEmpty(importPath)) return ImportType.External;

        if (importPath.StartsWith('.') || importPath.StartsWith('/') || importPath.StartsWith('\\'))
            return ImportType.Internal;

        if (importPath.StartsWith("@/"))
            return ImportType.Internal;

        // If it matches any registered library parser, it is an external library
        var firstPart = importPath.Contains('/') ? importPath.Split('/')[0] : importPath;
        if (Packages.Any(p => p.SupportedPatterns.Any(pat =>
            pat.Equals(importPath, StringComparison.OrdinalIgnoreCase) ||
            pat.Equals(firstPart, StringComparison.OrdinalIgnoreCase) ||
            pat.StartWithIC(firstPart + "/"))))
        {
            return ImportType.External;
        }

        var fullFilePath = Path.IsPathRooted(filePath)
            ? filePath
            : (!string.IsNullOrEmpty(absoluteWorkspacePath) ? Path.Combine(absoluteWorkspacePath, filePath).Replace('\\', '/') : filePath);

        var dir = Path.GetDirectoryName(fullFilePath);
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

                if (deps.Count == 0)
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

                if (deps.Count == 0)
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
            var content = PhysicalFileStore.ReadAllText(path);

            using var doc = System.Text.Json.JsonDocument.Parse(content);

            var root = doc.RootElement;
            var depProperties = new[] { "dependencies", "devDependencies" };

            foreach (var propName in depProperties)
            {
                if (root.TryGetProperty(propName, out var depsObj) &&
                    depsObj.ValueKind == System.Text.Json.JsonValueKind.Object)
                {
                    foreach (var prop in depsObj.EnumerateObject())
                    {
                        deps.Add(prop.Name);
                    }
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
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
            if (curr.IsAny(TreeSitterSyntax.TypeScript.ClassDeclaration, TreeSitterSyntax.TypeScript.InterfaceDeclaration))
            {
                var nameNode = curr.GetChildForField(TreeSitterSyntax.Fields.Name);
                if (nameNode.IsValid()) return nameNode.Text;
            }
            else if (curr.IsAny(TreeSitterSyntax.TypeScript.FunctionDeclaration, TreeSitterSyntax.TypeScript.MethodDefinition))
            {
                var nameNode = curr.GetChildForField(TreeSitterSyntax.Fields.Name);
                if (nameNode.IsValid())
                {
                    var nameText = nameNode.Text;
                    if (nameText == "constructor")
                    {
                        var classNode = curr.Parent;
                        while (classNode.IsValid())
                        {
                            if (classNode.IsAny(TreeSitterSyntax.TypeScript.ClassDeclaration, TreeSitterSyntax.TypeScript.InterfaceDeclaration))
                            {
                                var classNameNode = classNode.GetChildForField(TreeSitterSyntax.Fields.Name);
                                if (classNameNode.IsValid()) return classNameNode.Text;
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
