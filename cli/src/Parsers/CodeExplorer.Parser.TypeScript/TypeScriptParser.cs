using System.Collections.Concurrent;
using CodeExplorer.Core.Database;
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

    public IReadOnlyCollection<string> ManifestFileNames =>
    [
        "package.json", "tsconfig.json", "tsconfig.base.json", "jsconfig.json",
        "project.json", "ng-package.json", "wrangler.toml", "wrangler.jsonc",
        "wrangler.json", "angular.json", "nest-cli.json"
    ];
    public IReadOnlyCollection<string> ManifestExtensions => [];

    public bool IsDistOutputDirectory(string dirName) =>
        dirName.Equals("dist", StringComparison.OrdinalIgnoreCase) ||
        dirName.Equals("build", StringComparison.OrdinalIgnoreCase) ||
        dirName.Equals("out", StringComparison.OrdinalIgnoreCase) ||
        dirName.Equals("umd", StringComparison.OrdinalIgnoreCase) ||
        dirName.Equals("amd", StringComparison.OrdinalIgnoreCase) ||
        dirName.Equals("esm", StringComparison.OrdinalIgnoreCase) ||
        dirName.Equals("cjs", StringComparison.OrdinalIgnoreCase);

    public void InspectDirectoryFiles(string directoryPath, FileInfo[] files, CodeExplorer.Core.Common.Nodes.WorkspaceNode workspace)
    {
        foreach (var file in files)
        {
            var ext = file.Extension;
            if (ext.Equals(".node", StringComparison.OrdinalIgnoreCase))
            {
                var sig = CodeExplorer.Core.Common.Nodes.AnalysisSignalStore.Signals(workspace);
                sig.HasNativeNode = true;
                sig.HasNativeAddon = true;
            }
            else if (ext.Equals(".wasm", StringComparison.OrdinalIgnoreCase) ||
                     ext.Equals(".dylib", StringComparison.OrdinalIgnoreCase) ||
                     ext.Equals(".so", StringComparison.OrdinalIgnoreCase))
            {
                CodeExplorer.Core.Common.Nodes.AnalysisSignalStore.Signals(workspace).HasNativeAddon = true;
            }
        }
    }

    public bool ShouldSkipFileName(string fileName)
    {
        var fn = fileName.ToLowerInvariant();
        if (fn.EndsWith(".d.ts") || fn.EndsWith(".d.mts") || fn.EndsWith(".d.cts") ||
            fn.EndsWith(".min.js") || fn.EndsWith(".min.mjs") || fn.EndsWith(".min.cjs") ||
            fn.EndsWith(".bundle.js") || fn.EndsWith(".bundle.min.js") ||
            fn.Contains(".min.") ||
            fn.EndsWith("_debug.ts") || fn.EndsWith("_debug.js"))
        {
            return true;
        }

        return IProjectParser.DefaultShouldSkipFileName(fileName);
    }

    public bool ShouldSkipFileBytes(string fileName, ReadOnlySpan<byte> contentBytes) =>
        WorkspaceFileFilter.IsMinifiedBytes(contentBytes, maxLineLength: 5000);

    public bool ShouldSkipFile(FileInfo fileInfo)
    {
        if (PhysicalFileStore.TryGetFileText(fileInfo.FullName, out var cachedText))
        {
            return WorkspaceFileFilter.IsMinifiedText(cachedText, maxLineLength: 5000);
        }
        return false;
    }

    public string ClassifyArtifactRole(string relativePath, string fileName, ReadOnlySpan<byte> contentBytes = default)
    {
        var normPath = relativePath.NormalizeSlashes();
        var lowerFile = fileName.ToLowerInvariant();
        if (lowerFile.EndsWith(".d.ts") || lowerFile.EndsWith(".d.mts") || lowerFile.EndsWith(".d.cts"))
        {
            return "declaration";
        }
        if (lowerFile.Contains(".min.") || lowerFile.Contains(".bundle.") || WorkspaceFileFilter.IsMinifiedBytes(contentBytes, maxLineLength: 500))
        {
            return "minified";
        }
        var padded = "/" + normPath.TrimStart('/');
        if (padded.Contains("/dist/", StringComparison.OrdinalIgnoreCase) ||
            padded.Contains("/build/", StringComparison.OrdinalIgnoreCase) ||
            padded.Contains("/out/", StringComparison.OrdinalIgnoreCase) ||
            padded.Contains("/umd/", StringComparison.OrdinalIgnoreCase) ||
            padded.Contains("/amd/", StringComparison.OrdinalIgnoreCase))
        {
            return "dist";
        }
        return "source";
    }

    public void EnrichFileSignals(
        CodeExplorer.Core.Common.Nodes.Layer1_Physical.FileNode fileNode,
        SyntaxTree syntaxTree,
        CodeExplorer.Core.Common.Nodes.Layer2_Boundaries.ProjectNode? project,
        string sourceText,
        ParsingContext? ctx)
    {
        if (project == null ||
            (!fileNode.Name.Contains("config", StringComparison.OrdinalIgnoreCase) &&
             !fileNode.Name.Contains("datasource", StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        const string FamilyRelational = "relational";
        var projSignals = CodeExplorer.Core.Common.Nodes.AnalysisSignalStore.Signals(project);
        if (!string.IsNullOrEmpty(projSignals.DbDriverEngine)) return;

        var lower = sourceText.ToLowerInvariant();
        if (lower.Contains("type: 'postgres'") || lower.Contains("type: \"postgres\"") ||
            lower.Contains("dialect: 'postgres'") || lower.Contains("dialect: \"postgres\""))
        {
            projSignals.DbDriverEngine = "PostgreSQL";
            projSignals.DbDriverFamily = FamilyRelational;
        }
        else if (lower.Contains("type: 'mysql'") || lower.Contains("type: \"mysql\"") ||
                 lower.Contains("dialect: 'mysql'") || lower.Contains("dialect: \"mysql\""))
        {
            projSignals.DbDriverEngine = "MySQL";
            projSignals.DbDriverFamily = FamilyRelational;
        }
        else if (lower.Contains("type: 'sqlite'") || lower.Contains("type: \"sqlite\"") ||
                 lower.Contains("dialect: 'sqlite'") || lower.Contains("dialect: \"sqlite\""))
        {
            projSignals.DbDriverEngine = "SQLite";
            projSignals.DbDriverFamily = FamilyRelational;
        }
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
        new Libraries.MikroOrmKyselyLibraryParser(),
        new Libraries.TemporalTypeScriptLibraryParser(),
        new Libraries.AiSdkTypeScriptLibraryParser(),
        new Libraries.TestingLibraryParser(),
        new Libraries.NatsMqttTsLibraryParser(),
        new Libraries.EncoreSstLibraryParser(),
        new Libraries.MetaFrameworkRoutesLibraryParser(),
        new Libraries.CloudflareWorkersLibraryParser(),
        new Libraries.EffectTsLibraryParser(),
        new Libraries.GraphQlClientTsLibraryParser(),
        new Libraries.TanStackQuerySwrLibraryParser(),
        new Libraries.AuthTsLibraryParser(),
        new Libraries.SupabaseFirebaseTsLibraryParser(),
        new Libraries.PlaywrightCypressLibraryParser(),
        new Libraries.RedisLibraryParser(),
        new Libraries.GrpcTypeScriptLibraryParser(),
        new Libraries.SequelizeLibraryParser(),
        new Libraries.Sqlite3LibraryParser(),
        new Libraries.TypeOrmLibraryParser(),
        new Libraries.GcpLibraryParser(),
        new Libraries.RabbitMqLibraryParser(),
        new Libraries.KafkaJsLibraryParser(),
        new Libraries.BullMqLibraryParser(),
        new Libraries.BullLegacyLibraryParser(),
        new Libraries.InngestLibraryParser(),
        new Libraries.NestJsLibraryParser(),
        new Libraries.NestJsCqrsLibraryParser(),
        new Libraries.RoutingControllersLibraryParser(),
        new Libraries.ExpressLibraryParser(),
        new Libraries.HonoLibraryParser(),
        new Libraries.TrpcLibraryParser(),
        new Libraries.RemixLibraryParser(),
        new Libraries.GraphQlYogaApolloLibraryParser(),
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
                    const string propWorkspaces = "workspaces";
                    var content = PhysicalFileStore.ReadAllText(parentPkgJson);
                    var pRoot = CypherValue.TryParseJsonText(content);
                    if (pRoot.Kind == CypherValueKind.Object && !pRoot.TryGetProperty(propWorkspaces, out _))
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
            var root = CypherValue.TryParseJsonText(content);
            if (root.Kind != CypherValueKind.Object || root.AsObject().Count == 0)
            {
                return false;
            }

            if (root.TryGetProperty("name", out var nameProp) && !string.IsNullOrWhiteSpace(nameProp.AsNullableString()))
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
                const string propName = "name";
                var content = PhysicalFileStore.ReadAllText(packageJsonPath);
                var root = CypherValue.TryParseJsonText(content);

                if (root.TryGetProperty(propName, out var nameProp) &&
                    nameProp.Kind == CypherValueKind.String)
                {
                    var rawName = nameProp.AsNullableString();

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
            var root = CypherValue.TryParseJsonText(content);

            if (root.TryGetProperty("name", out var nameProp) &&
                nameProp.Kind == CypherValueKind.String)
            {
                var name = nameProp.AsNullableString();
                if (string.IsNullOrEmpty(name)) return null;

                const string defaultVersion = "1.0.0";
                var version = defaultVersion;

                if (root.TryGetProperty("version", out var versionProp) &&
                    versionProp.Kind == CypherValueKind.String)
                {
                    version = versionProp.AsString(defaultVersion);
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
            var root = CypherValue.TryParseJsonText(content);

            var depProperties = new[] { "dependencies", "devDependencies" };

            foreach (var propName in depProperties)
            {
                if (root.TryGetProperty(propName, out var depsObj) &&
                    depsObj.Kind == CypherValueKind.Object)
                {
                    const string defaultUnknownVersion = "unknown";
                    foreach (var prop in depsObj.AsObject())
                    {
                        var packageName = prop.Key;
                        var packageVersion = prop.Value.AsString(defaultUnknownVersion);

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
    private readonly ConcurrentDictionary<string, string?> _dirToProjectDirCache = new(StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> NodeBuiltInModules = new(StringComparer.OrdinalIgnoreCase)
    {
        "fs", "path", "os", "http", "https", "crypto", "child_process", "dns", "events", "net", "stream", "util", "url", "zlib"
    };

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

                if (NodeBuiltInModules.Contains(firstSegment))
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
        if (string.IsNullOrEmpty(dir)) return null;
        return _dirToProjectDirCache.GetOrAdd(dir, static startDir =>
        {
            var current = startDir;
            while (current != null)
            {
                if (File.Exists(Path.Combine(current, "package.json")))
                    return current;
                current = Path.GetDirectoryName(current);
            }
            return null;
        });
    }

    private HashSet<string> LoadPackageJsonDependencies(string projectDir)
    {
        var deps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var path = Path.Combine(projectDir, "package.json");
        if (!File.Exists(path)) return deps;

        try
        {
            var content = PhysicalFileStore.ReadAllText(path);
            var root = CypherValue.TryParseJsonText(content);
            var depProperties = new[] { "dependencies", "devDependencies" };

            foreach (var propName in depProperties)
            {
                if (root.TryGetProperty(propName, out var depsObj) &&
                    depsObj.Kind == CypherValueKind.Object)
                {
                    foreach (var prop in depsObj.AsObject())
                    {
                        deps.Add(prop.Key);
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
