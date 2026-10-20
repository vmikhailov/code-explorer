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
        new Libraries.JestVitestLibraryParser(),
        new Libraries.MochaLibraryParser(),
        new Libraries.CypressLibraryParser(),
        new Libraries.PlaywrightLibraryParser()
    ];

    public bool UsesTreeSitter => true;

    public LanguageSyntaxProfile SyntaxProfile => TypeScriptSyntaxProfile.Instance;

    public bool CanParse(string fileExtension)
    {
        return fileExtension.Equals(".ts", StringComparison.OrdinalIgnoreCase) ||
               fileExtension.Equals(".tsx", StringComparison.OrdinalIgnoreCase);
    }

    public void ExtractDeclarations(Node rootNode, Action<string, Node?, string?> registerDeclaration)
    {
        TypeScriptDeclarationExtractor.Extract(rootNode, registerDeclaration);
    }

    public bool IsProjectDirectory(string directoryPath, string[] filesInDirectory)
    {
        var packageJson = filesInDirectory.FirstOrDefault(f => Path.GetFileName(f).Equals("package.json", StringComparison.OrdinalIgnoreCase));
        if (packageJson != null)
        {
            if (IsNgSecondaryEntryPoint(packageJson, directoryPath, filesInDirectory) ||
                IsInsideParentWebOrHybridProject(directoryPath))
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
                if (IsInsideNgPackage(directoryPath) || IsInsideParentProject(directoryPath) || HasOnlyAmbientDeclarationFiles(directoryPath))
                {
                    return false;
                }
                return true;
            }
        }
        return false;
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
        while (parentDir != null && parentDir.Length >= 3 && depth < 4)
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
                    var content = File.ReadAllText(parentPkgJson);
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
            var allFiles = WorkspaceFileFilter.EnumerateFiles(directoryPath, "*").ToList();
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

    public static bool IsNgSecondaryEntryPoint(string? packageJsonPath, string directoryPath, string[]? filesInDirectory = null)
    {
        filesInDirectory ??= Directory.GetFiles(directoryPath);
        var hasNgPackageJson = filesInDirectory.Any(f => Path.GetFileName(f).Equals("ng-package.json", StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrEmpty(packageJsonPath) && File.Exists(packageJsonPath))
        {
            try
            {
                var content = File.ReadAllText(packageJsonPath);
                using var doc = System.Text.Json.JsonDocument.Parse(content);
                var root = doc.RootElement;
                // If it has ngPackage (or sibling ng-package.json) and does not have a valid "name" property, it's an Angular secondary entry point
                if ((root.TryGetProperty("ngPackage", out _) || hasNgPackageJson) &&
                    (!root.TryGetProperty("name", out var nameProp) || string.IsNullOrWhiteSpace(nameProp.GetString())))
                {
                    return true;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
            {
                // Ignore parse errors
            }
        }
        else if (hasNgPackageJson)
        {
            return true;
        }

        return false;
    }

    private static bool IsInsideNgPackage(string directoryPath)
    {
        var parentDir = Path.GetDirectoryName(directoryPath);
        while (parentDir != null && parentDir.Length >= 3)
        {
            if (File.Exists(Path.Combine(parentDir, "ng-package.json")) ||
                File.Exists(Path.Combine(parentDir, "angular.json")))
            {
                return true;
            }
            parentDir = Path.GetDirectoryName(parentDir);
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

    public Dictionary<string, string> ExtractManifestProperties(string directoryPath, string[] filesInDirectory)
    {
        var props = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // 1. Check wrangler.toml (Cloudflare Workers / Serverless)
        var hasWrangler = filesInDirectory.Any(f => Path.GetFileName(f).Equals("wrangler.toml", StringComparison.OrdinalIgnoreCase))
                          || File.Exists(Path.Combine(directoryPath, "wrangler.toml"))
                          || (Path.GetDirectoryName(directoryPath) is { } wranglerParent && File.Exists(Path.Combine(wranglerParent, "wrangler.toml")));
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

                if (doc.RootElement.TryGetProperty("projectType", out var ptProp) &&
                    ptProp.ValueKind == System.Text.Json.JsonValueKind.String)
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
            catch (Exception ex) when (ex is IOException or System.Text.Json.JsonException)
            {
                // Ignore parse errors on best-effort manifest reading
            }
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
                    if (binProp.ValueKind == System.Text.Json.JsonValueKind.String &&
                        !string.IsNullOrWhiteSpace(binProp.GetString()))
                    {
                        props["has_cli_bin"] = "true";
                        if (!props.ContainsKey("manifest_type")) props["manifest_type"] = "cli";
                    }
                    else if (binProp.ValueKind == System.Text.Json.JsonValueKind.Object &&
                             binProp.EnumerateObject().Any())
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

                if (root.TryGetProperty("dependencies", out var depsObj) &&
                    depsObj.ValueKind == System.Text.Json.JsonValueKind.Object)
                {
                    foreach (var prop in depsObj.EnumerateObject()) allDeps.Add(prop.Name);
                }

                if (root.TryGetProperty("devDependencies", out var devDepsObj) &&
                    devDepsObj.ValueKind == System.Text.Json.JsonValueKind.Object)
                {
                    foreach (var prop in devDepsObj.EnumerateObject()) allDeps.Add(prop.Name);
                }

                var hasNgPackage = root.TryGetProperty("ngPackage", out _) || filesInDirectory.Any(f =>
                    Path.GetFileName(f).Equals("ng-package.json", StringComparison.OrdinalIgnoreCase));

                var hasIndexHtml = filesInDirectory.Any(f =>
                    Path.GetFileName(f).Equals("index.html", StringComparison.OrdinalIgnoreCase))
                    || File.Exists(Path.Combine(directoryPath, "src", "index.html"))
                    || File.Exists(Path.Combine(directoryPath, "public", "index.html"));

                // If inside a monorepo, check ancestor angular.json or nx.json
                var parentDir = Path.GetDirectoryName(directoryPath);
                while (parentDir != null && parentDir.Length >= 3)
                {
                    var ancestorAngularJson = Path.Combine(parentDir, "angular.json");
                    if (File.Exists(ancestorAngularJson))
                    {
                        try
                        {
                            var json = File.ReadAllText(ancestorAngularJson);
                            using var angDoc = System.Text.Json.JsonDocument.Parse(json);
                            if (angDoc.RootElement.TryGetProperty("projects", out var projectsObj))
                            {
                                foreach (var pProp in projectsObj.EnumerateObject())
                                {
                                    var projVal = pProp.Value;
                                    var rootPath = projVal.TryGetProperty("root", out var r) ? r.GetString() : null;
                                    if (!string.IsNullOrEmpty(rootPath) && directoryPath.Replace('\\', '/').EndsWith(rootPath.Replace('\\', '/').Trim('/')))
                                    {
                                        if (projVal.TryGetProperty("projectType", out var ptProp))
                                        {
                                            var pt = ptProp.GetString()?.ToLowerInvariant();
                                            if (pt == "application")
                                            {
                                                props["manifest_type"] = "application";
                                                props["framework_type"] = "frontend";
                                                hasIndexHtml = true;
                                            }
                                            else if (pt == "library")
                                            {
                                                props["manifest_type"] = "library";
                                                props["framework_type"] = "frontend";
                                                props["is_ui_library"] = "true";
                                                props["is_library"] = "true";
                                            }
                                        }
                                        break;
                                    }
                                }
                            }
                        }
                        catch (Exception ex) when (ex is IOException or System.Text.Json.JsonException)
                        {
                            // Ignore monorepo ancestor parse errors
                        }
                        break;
                    }
                    parentDir = Path.GetDirectoryName(parentDir);
                }

                if (hasNgPackage)
                {
                    props["manifest_type"] = "library";
                    props["framework_type"] = "frontend";
                    props["is_ui_library"] = "true";
                    props["is_library"] = "true";
                }

                // Framework and workload detection
                var hasGrpcDeps = allDeps.Any(d => d is "@grpc/grpc-js" or "grpc" || d.StartsWith("@grpc/"));
                if (hasGrpcDeps) props["has_grpc"] = "true";

                var hasWebFramework =
                    hasGrpcDeps || allDeps.Any(d => d is "express" or "@nestjs/core" or "fastify" or "koa" or "hono");

                var hasWorkerDeps = allDeps.Any(d =>
                    d is "@nestjs/schedule" or "@nestjs/bull" or "@nestjs/bullmq" or "bullmq" or "bull" or "amqplib"
                        or "kafkajs" or "@cloudflare/workers-types" or "wrangler" or "node-cron" or "cron"
                        or "agenda" || d.StartsWith("cloudflare-", StringComparison.OrdinalIgnoreCase)
                        || d.StartsWith("@cloudflare/", StringComparison.OrdinalIgnoreCase)) || hasWrangler;

                var hasMobileDeps = allDeps.Any(d => d is "react-native" or "expo" || d.StartsWith("expo-") || d.StartsWith("@expo/")) ||
                                    filesInDirectory.Any(f => Path.GetFileName(f).Equals("app.json", StringComparison.OrdinalIgnoreCase));

                var hasDesktopDeps = allDeps.Any(d =>
                    d is "electron" or "@electron-forge/cli" or "electron-builder" or "@tauri-apps/api");

                var hasFrontendDeps = hasMobileDeps || hasDesktopDeps || allDeps.Any(d =>
                    d is "react" or "react-dom" or "@angular/core" or "vue" or "svelte" or "solid-js" or "next"
                        or "nuxt");

                var pkgName =
                    root.TryGetProperty("name", out var np) && np.ValueKind == System.Text.Json.JsonValueKind.String
                        ? (np.GetString() ?? "").ToLowerInvariant()
                        : "";
                var projDirName = Path.GetFileName(directoryPath).ToLowerInvariant();

                var isDedicatedWorkerName = projDirName.Contains("worker") || projDirName.Contains("scheduler") ||
                                            projDirName.Contains("consumer") || pkgName.Contains("worker") ||
                                            pkgName.Contains("scheduler") || pkgName.Contains("consumer");

                var hasWorkerScripts = root.TryGetProperty("scripts", out var scrObj) &&
                                       scrObj.ValueKind == System.Text.Json.JsonValueKind.Object &&
                                       scrObj.EnumerateObject().Any(s =>
                                           s.Name.StartsWith("worker:", StringComparison.OrdinalIgnoreCase) ||
                                           s.Name.Contains("wrangler", StringComparison.OrdinalIgnoreCase) ||
                                           s.Value.ToString().Contains("wrangler", StringComparison.OrdinalIgnoreCase));

                var isWorkerProject = hasWrangler || (hasWorkerDeps && (isDedicatedWorkerName || hasWorkerScripts));

                if (hasWorkerDeps)
                {
                    if (allDeps.Any(d => d is "@nestjs/schedule" or "node-cron" or "cron" or "agenda"))
                        props["has_schedule"] = "true";

                    if (allDeps.Any(d =>
                            d is "@nestjs/bull" or "@nestjs/bullmq" or "bullmq" or "bull" or "amqplib"
                                or "kafkajs")) props["has_queue_worker"] = "true";
                }

                if (isWorkerProject)
                {
                    props["framework_type"] = "worker";
                    if (!props.ContainsKey("manifest_type")) props["manifest_type"] = "worker";
                }

                // Determine if package is an explicit library by standard npm contract:
                // 1. Explicit peerDependencies (only libraries declare peerDependencies for consumers)
                // 2. Explicit publishConfig (declared npm registry publication target)
                // 3. Publish scripts (build/version scripts targeting npm publish)
                // 4. Exports/types/main contract without runnable application launch scripts (start / start:prod / serve)
                var hasPeerDeps = root.TryGetProperty("peerDependencies", out _);
                var hasPublishConfig = root.TryGetProperty("publishConfig", out _);
                var hasPublishScript = root.TryGetProperty("scripts", out var sObj) &&
                                       sObj.ValueKind == System.Text.Json.JsonValueKind.Object &&
                                       sObj.EnumerateObject().Any(s => s.Value.ToString().Contains("npm publish", StringComparison.OrdinalIgnoreCase));

                var hasExplicitExports = root.TryGetProperty("exports", out _) ||
                                         root.TryGetProperty("types", out _) ||
                                         root.TryGetProperty("typings", out _) ||
                                         root.TryGetProperty("main", out _);

                var hasPrimaryAppStartScript = root.TryGetProperty("scripts", out var scriptsObj) &&
                                               scriptsObj.ValueKind == System.Text.Json.JsonValueKind.Object &&
                                               scriptsObj.EnumerateObject().Any(s =>
                                                   s.Name.Equals("start", StringComparison.OrdinalIgnoreCase) ||
                                                   s.Name.Equals("start:prod", StringComparison.OrdinalIgnoreCase) ||
                                                   s.Name.Equals("serve", StringComparison.OrdinalIgnoreCase));

                var isVsCodeExtension = (root.TryGetProperty("engines", out var enginesProp) &&
                                         enginesProp.ValueKind == System.Text.Json.JsonValueKind.Object &&
                                         enginesProp.TryGetProperty("vscode", out _)) ||
                                        (root.TryGetProperty("contributes", out var contributesProp) &&
                                         contributesProp.ValueKind == System.Text.Json.JsonValueKind.Object) ||
                                        (root.TryGetProperty("activationEvents", out var actProp) &&
                                         actProp.ValueKind == System.Text.Json.JsonValueKind.Array) ||
                                        allDeps.Contains("@types/vscode");

                var isLibraryByContract = !isWorkerProject && !isVsCodeExtension && (hasPeerDeps || hasPublishConfig || hasPublishScript || (hasExplicitExports && !hasPrimaryAppStartScript));
                if (isLibraryByContract)
                {
                    props["is_library"] = "true";
                    if (!props.ContainsKey("manifest_type")) props["manifest_type"] = "library";
                }

                if (isVsCodeExtension)
                {
                    props["manifest_type"] = "application";
                    props["framework_type"] = "extension";
                    props["ui_type"] = "extension";
                    props["is_vscode_extension"] = "true";
                    props.Remove("is_library");
                }
                else if (hasDesktopDeps)
                {
                    props["framework_type"] = "frontend";
                    props["is_frontend_app"] = "true";
                    props["manifest_type"] = "application";
                    props.Remove("is_library");
                }
                else if (hasWebFramework)
                {
                    props["framework_type"] = "web";
                    if (!props.ContainsKey("manifest_type"))
                    {
                        props["manifest_type"] = isDedicatedWorkerName && hasWorkerDeps ? "worker" : "application";
                    }
                }
                else if (hasWorkerDeps)
                {
                    props["framework_type"] = "worker";
                    if (!props.ContainsKey("manifest_type")) props["manifest_type"] = "worker";
                }
                else if (hasMobileDeps)
                {
                    props["framework_type"] = "mobile";
                    props["is_frontend_app"] = "true";
                    if (!props.ContainsKey("manifest_type")) props["manifest_type"] = "application";
                }
                else if (hasFrontendDeps)
                {
                    props["framework_type"] = "frontend";

                    if (!props.ContainsKey("manifest_type"))
                    {
                        var isExplicitUiLib = hasNgPackage || (!hasIndexHtml && !allDeps.Contains("next") &&
                                                               !allDeps.Contains("nuxt") &&
                                                               root.TryGetProperty("exports", out _));
                        props["manifest_type"] = isExplicitUiLib ? "library" : "application";

                        if (isExplicitUiLib)
                        {
                            props["is_ui_library"] = "true";
                            props["is_library"] = "true";
                        }
                    }
                }
                else if (allDeps.Any(d =>
                             d.StartsWith("@aws-cdk/") || d.StartsWith("@pulumi/") || d == "serverless"))
                {
                    props["framework_type"] = "cloud";
                }
                else if (hasPrimaryAppStartScript)
                {
                    if (!props.ContainsKey("manifest_type"))
                    {
                        props["manifest_type"] = "application";
                        props["framework_type"] = "service";
                    }
                }
                else if (hasExplicitExports)
                {
                    if (!props.ContainsKey("manifest_type"))
                    {
                        props["manifest_type"] = "library";
                        props["is_library"] = "true";
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or System.Text.Json.JsonException)
            {
                // Ignore package.json parsing errors
            }
        }

        return props;
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
            var content = await File.ReadAllTextAsync(packageJsonPath);

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
            var content = await File.ReadAllTextAsync(packageJsonPath);

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
                                        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(packageJsonPath)!,
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
                        externalPackages.Add(new ProducedPackageInfo(packageName, packageVersion, "npm"));
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

    public ISyntaxEnricher GetSyntaxEnricher(SyntaxTree syntaxTree) => new SyntaxEnricher(SemanticExtensions, syntaxTree, Packages);

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
            pat.StartsWith(firstPart + "/", StringComparison.OrdinalIgnoreCase))))
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
            var content = File.ReadAllText(path);

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
