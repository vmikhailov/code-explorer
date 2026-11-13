using CodeExplorer.Core.Database;

[assembly: ParserAssembly]

namespace CodeExplorer.Parser.TypeScript;

[ProjectParser(Tier = ParserTier.Secondary, Supersedes = ["javascript"])]
public partial class TypeScriptProjectParser : IProjectParser
{
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

    public bool IsConfigurationFile(string fileName)
    {
        var lower = fileName.ToLowerInvariant();
        return lower.StartsWith(".env");
    }

    public IReadOnlyList<PackageDescriptor> Packages => TypeScriptFileParser.DefaultPackages;

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

        var projectJson = filesInDirectory.FirstOrDefault(f => Path.GetFileName(f).Equals("project.json", StringComparison.OrdinalIgnoreCase));
        if (projectJson != null)
        {
            if (IsInsideParentWebOrHybridProject(directoryPath) ||
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
        var projectJsonPath = Path.Combine(directoryPath, "project.json");
        if (File.Exists(projectJsonPath))
        {
            return false;
        }

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
                    if (pRoot.Kind == CypherValueKind.Object)
                    {
                        var isMonorepoRoot = pRoot.TryGetProperty(propWorkspaces, out _) ||
                                             File.Exists(Path.Combine(parentDir, "nx.json")) ||
                                             File.Exists(Path.Combine(parentDir, "pnpm-workspace.yaml")) ||
                                             File.Exists(Path.Combine(parentDir, "lerna.json")) ||
                                             File.Exists(Path.Combine(parentDir, "turbo.json"));
                        if (!isMonorepoRoot)
                        {
                            return true;
                        }
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

        var projectJsonPath = Path.Combine(directoryPath, "project.json");
        if (File.Exists(projectJsonPath))
        {
            try
            {
                const string propName = "name";
                var content = PhysicalFileStore.ReadAllText(projectJsonPath);
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
                // Fallback to package.json or directory name
            }
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
            var projectJsonPath = Path.Combine(projectDirectory, "project.json");
            if (File.Exists(projectJsonPath))
            {
                var parent = Path.GetDirectoryName(projectDirectory);
                while (parent != null && parent.Length >= 3)
                {
                    var rootPkg = Path.Combine(parent, "package.json");
                    if (File.Exists(rootPkg) && File.Exists(Path.Combine(parent, "nx.json")))
                    {
                        packageJsonPath = rootPkg;
                        break;
                    }
                    parent = Path.GetDirectoryName(parent);
                }
            }

            if (!File.Exists(packageJsonPath))
            {
                return new ProjectDependencyInfo(localProjectPaths, externalPackages);
            }
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

        var localProjectJson = Path.Combine(projectDirectory, "project.json");
        if (File.Exists(localProjectJson))
        {
            try
            {
                var pContent = await PhysicalFileStore.ReadAllTextAsync(localProjectJson);
                var pRoot = CypherValue.TryParseJsonText(pContent);

                if (pRoot.TryGetProperty("implicitDependencies", out var impProp) && impProp.Kind == CypherValueKind.Array)
                {
                    foreach (var imp in impProp.AsArray())
                    {
                        var depName = imp.AsNullableString();
                        if (!string.IsNullOrWhiteSpace(depName))
                        {
                            externalPackages.Add(new ProducedPackageInfo(depName.Trim(), "*", "npm", "runtime"));
                        }
                    }
                }

                if (pRoot.TryGetProperty("targets", out var targetsProp) && targetsProp.Kind == CypherValueKind.Object)
                {
                    foreach (var target in targetsProp.AsObject())
                    {
                        if (target.Value.Kind == CypherValueKind.Object &&
                            target.Value.TryGetProperty("dependsOn", out var depOnProp) &&
                            depOnProp.Kind == CypherValueKind.Array)
                        {
                            foreach (var depItem in depOnProp.AsArray())
                            {
                                if (depItem.Kind == CypherValueKind.String)
                                {
                                    var str = depItem.AsString();
                                    var colon = str.IndexOf(':');
                                    var pName = colon > 0 ? str[..colon] : str;
                                    if (!string.IsNullOrWhiteSpace(pName) && !pName.StartsWith('^'))
                                    {
                                        externalPackages.Add(new ProducedPackageInfo(pName.Trim(), "*", "npm", "runtime"));
                                    }
                                }
                                else if (depItem.Kind == CypherValueKind.Object &&
                                         depItem.TryGetProperty("projects", out var projsProp))
                                {
                                    if (projsProp.Kind == CypherValueKind.String)
                                    {
                                        var pName = projsProp.AsString();
                                        if (!string.IsNullOrWhiteSpace(pName))
                                        {
                                            externalPackages.Add(new ProducedPackageInfo(pName.Trim(), "*", "npm", "runtime"));
                                        }
                                    }
                                    else if (projsProp.Kind == CypherValueKind.Array)
                                    {
                                        foreach (var pi in projsProp.AsArray())
                                        {
                                            var pName = pi.AsNullableString();
                                            if (!string.IsNullOrWhiteSpace(pName))
                                            {
                                                externalPackages.Add(new ProducedPackageInfo(pName.Trim(), "*", "npm", "runtime"));
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                ExtractTsConfigPathDependencies(projectDirectory, localProjectPaths, externalPackages);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
            {
                // Best effort
            }
        }

        return new ProjectDependencyInfo(localProjectPaths, externalPackages);
    }

    private static void ExtractTsConfigPathDependencies(
        string projectDirectory,
        List<string> localProjectPaths,
        List<ProducedPackageInfo> externalPackages)
    {
        string? tsConfigPath = null;
        var parent = projectDirectory;
        while (parent != null && parent.Length >= 3)
        {
            var candidateBase = Path.Combine(parent, "tsconfig.base.json");
            if (File.Exists(candidateBase))
            {
                tsConfigPath = candidateBase;
                break;
            }
            var candidate = Path.Combine(parent, "tsconfig.json");
            if (File.Exists(candidate))
            {
                tsConfigPath = candidate;
                break;
            }
            parent = Path.GetDirectoryName(parent);
        }

        if (tsConfigPath == null) return;

        try
        {
            var content = PhysicalFileStore.ReadAllText(tsConfigPath);
            var root = CypherValue.TryParseJsonText(content);
            if (!root.TryGetProperty("compilerOptions", out var coProp) ||
                coProp.Kind != CypherValueKind.Object ||
                !coProp.TryGetProperty("paths", out var pathsProp) ||
                pathsProp.Kind != CypherValueKind.Object)
            {
                return;
            }

            var pathMappings = new List<(string AliasPrefix, string TargetRelPath)>();

            foreach (var mapping in pathsProp.AsObject())
            {
                var alias = mapping.Key;
                var targets = mapping.Value;
                string? firstTarget = null;
                if (targets.Kind == CypherValueKind.Array && targets.AsArray().Count > 0)
                {
                    firstTarget = targets.AsArray()[0].AsNullableString();
                }
                else if (targets.Kind == CypherValueKind.String)
                {
                    firstTarget = targets.AsString();
                }

                if (string.IsNullOrEmpty(firstTarget)) continue;

                var cleanAlias = alias.EndsWith("/*") ? alias[..^2] : alias;
                var cleanTarget = firstTarget.EndsWith("/*") ? firstTarget[..^2] : firstTarget;

                pathMappings.Add((cleanAlias, cleanTarget));
            }

            if (pathMappings.Count == 0) return;

            var tsFiles = Directory.EnumerateFiles(projectDirectory, "*.*", SearchOption.AllDirectories)
                .Where(f =>
                {
                    var ext = Path.GetExtension(f);
                    return ext is ".ts" or ".tsx" or ".js" or ".jsx" or ".mts" or ".cts";
                })
                .Take(200);

            var matchedTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var file in tsFiles)
            {
                string text;
                try
                {
                    text = PhysicalFileStore.ReadAllText(file);
                }
                catch
                {
                    continue;
                }

                foreach (var (alias, target) in pathMappings)
                {
                    if (matchedTargets.Contains(target)) continue;

                    if (text.Contains($"'{alias}") || text.Contains($"\"{alias}"))
                    {
                        matchedTargets.Add(target);

                        var shortName = alias.Contains('/') ? alias.Split('/')[^1].TrimEnd('*') : alias;
                        if (!string.IsNullOrEmpty(shortName) && !externalPackages.Any(p => p.Name.Equals(shortName, StringComparison.OrdinalIgnoreCase)))
                        {
                            externalPackages.Add(new ProducedPackageInfo(shortName, "*", "npm", "runtime"));
                        }
                    }
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            // Ignore
        }
    }
}
