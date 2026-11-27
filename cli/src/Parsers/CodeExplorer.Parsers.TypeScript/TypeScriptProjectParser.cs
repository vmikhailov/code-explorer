using System.Text.Json;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Database;

[assembly: ParserAssembly]

namespace CodeExplorer.Parsers.TypeScript;

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

    public void InspectDirectoryFiles(string directoryPath, FileInfo[] files, Core.Common.Nodes.WorkspaceNode workspace)
    {
        foreach (var file in files)
        {
            var ext = file.Extension;
            if (ext.Equals(".node", StringComparison.OrdinalIgnoreCase))
            {
                var sig = Core.Common.Nodes.AnalysisSignalStore.Signals(workspace);
                sig.HasNativeNode = true;
                sig.HasNativeAddon = true;
            }
            else if (ext.Equals(".wasm", StringComparison.OrdinalIgnoreCase) ||
                     ext.Equals(".dylib", StringComparison.OrdinalIgnoreCase) ||
                     ext.Equals(".so", StringComparison.OrdinalIgnoreCase))
            {
                Core.Common.Nodes.AnalysisSignalStore.Signals(workspace).HasNativeAddon = true;
            }
        }
    }

    public void InspectDirectoryFilePaths(string directoryPath, string[] filePaths, Core.Common.Nodes.WorkspaceNode workspace)
    {
        for (var i = 0; i < filePaths.Length; i++)
        {
            var fp = filePaths[i];
            if (fp.EndsWith(".node", StringComparison.OrdinalIgnoreCase))
            {
                var sig = Core.Common.Nodes.AnalysisSignalStore.Signals(workspace);
                sig.HasNativeNode = true;
                sig.HasNativeAddon = true;
            }
            else if (fp.EndsWith(".wasm", StringComparison.OrdinalIgnoreCase) ||
                     fp.EndsWith(".dylib", StringComparison.OrdinalIgnoreCase) ||
                     fp.EndsWith(".so", StringComparison.OrdinalIgnoreCase))
            {
                Core.Common.Nodes.AnalysisSignalStore.Signals(workspace).HasNativeAddon = true;
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
            var fullPackageJson = Path.IsPathRooted(packageJson) ? packageJson : Path.Combine(directoryPath, packageJson);
            if (IsSubpathEntryPointPackageJson(fullPackageJson, directoryPath) ||
                IsInsideParentWebOrHybridProject(directoryPath) ||
                IsAbsorbedWorkersSiteDirectory(directoryPath) ||
                IsVendoredFrontendAssetDirectory(directoryPath))
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

    public static bool IsVendoredFrontendAssetDirectory(string directoryPath)
    {
        var dirName = Path.GetFileName(directoryPath.TrimEnd('/', '\\'));
        if (string.IsNullOrEmpty(dirName)) return false;

        return dirName.StartsWith("jquery-ui", StringComparison.OrdinalIgnoreCase) ||
               dirName.StartsWith("jquery.", StringComparison.OrdinalIgnoreCase) ||
               dirName.StartsWith("Stuk-jszip", StringComparison.OrdinalIgnoreCase) ||
               dirName.StartsWith("tinymce", StringComparison.OrdinalIgnoreCase) ||
               dirName.StartsWith("ckeditor", StringComparison.OrdinalIgnoreCase) ||
               dirName.EndsWith(".custom", StringComparison.OrdinalIgnoreCase);
    }

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
        var depth = 0;
        while (parentDir != null && parentDir.Length >= 3 && depth < 3)
        {
            depth++;
            try
            {
                var parentFiles = WorkspaceDirectoryIndex.GetDirectoryFilesOrFallback(parentDir);
                var hasParentCsproj = parentFiles.Any(f => f.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase));
                if (hasParentCsproj)
                {
                    var hasParentPkgOrWebConfig = parentFiles.Any(f =>
                    {
                        var fn = Path.GetFileName(f);
                        return fn.Equals("package.json", StringComparison.OrdinalIgnoreCase) ||
                               fn.Equals("web.config", StringComparison.OrdinalIgnoreCase);
                    });
                    if (hasParentPkgOrWebConfig)
                    {
                        return true;
                    }
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
        var dirFiles = WorkspaceDirectoryIndex.GetDirectoryFilesOrFallback(directoryPath);
        if (dirFiles.Any(f => Path.GetFileName(f).Equals("project.json", StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        var parentDir = Path.GetDirectoryName(directoryPath);
        var depth = 0;
        while (parentDir != null && parentDir.Length >= 3 && depth < 8)
        {
            depth++;
            try
            {
                var parentFiles = WorkspaceDirectoryIndex.GetDirectoryFilesOrFallback(parentDir);
                if (parentFiles.Any(f => f.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }

                var parentPkgJson = parentFiles.FirstOrDefault(f => Path.GetFileName(f).Equals("package.json", StringComparison.OrdinalIgnoreCase));
                if (parentPkgJson != null)
                {
                    const string propWorkspaces = "workspaces";
                    var content = FileStreamReader.ReadTextOnce(parentPkgJson);
                    var pRoot = CypherValue.TryParseJsonText(content);
                    if (pRoot.Kind == CypherValueKind.Object)
                    {
                        var isMonorepoRoot = pRoot.TryGetProperty(propWorkspaces, out _) ||
                                             parentFiles.Any(f => Path.GetFileName(f) is "nx.json" or "pnpm-workspace.yaml" or "lerna.json" or "turbo.json");
                        if (!isMonorepoRoot)
                        {
                            return true;
                        }
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
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
            var allFiles = WorkspaceDirectoryIndex.HasCachedDirectoryInCurrent(directoryPath)
                ? WorkspaceDirectoryIndex.EnumerateSubtreeInCurrent(directoryPath)
                : WorkspaceFileFilter.Default.EnumerateFiles(directoryPath, "*");
            var hasAnyDts = false;
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

    public static bool IsSubpathEntryPointPackageJson(string? packageJsonPath, string directoryPath, CypherValue? preParsedPkgJson = null)
    {
        if (string.IsNullOrEmpty(packageJsonPath) || !File.Exists(packageJsonPath))
        {
            return false;
        }

        try
        {
            var root = preParsedPkgJson ?? CypherValue.TryParseJsonText(FileStreamReader.ReadTextOnce(packageJsonPath));
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
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return false;
        }
    }

    private sealed record TsDirectoryManifestBundle(
        string? PackageJsonPath,
        CypherValue? PackageJson,
        CypherValue? ProjectJson,
        string[]? WranglerLines);

    private static TsDirectoryManifestBundle ReadDirectoryManifestBundleOnce(string directoryPath, string[] filesInDirectory)
    {
        var pkgPath = filesInDirectory.FirstOrDefault(f => Path.GetFileName(f).Equals("package.json", StringComparison.OrdinalIgnoreCase));
        if (pkgPath != null && !Path.IsPathRooted(pkgPath))
        {
            pkgPath = Path.Combine(directoryPath, pkgPath);
        }
        else if (pkgPath == null)
        {
            var fallbackPkg = Path.Combine(directoryPath, "package.json");
            if (WorkspaceDirectoryIndex.FileExistsInCurrentOrDisk(fallbackPkg)) pkgPath = fallbackPkg;
        }

        CypherValue? pkgRoot = null;
        if (pkgPath != null && File.Exists(pkgPath))
        {
            var pkgContent = FileStreamReader.ReadTextOnce(pkgPath);
            if (!string.IsNullOrEmpty(pkgContent))
            {
                pkgRoot = CypherValue.TryParseJsonText(pkgContent);
            }
        }

        CypherValue? projRoot = null;
        var rawProjPath = filesInDirectory.FirstOrDefault(f => Path.GetFileName(f).Equals("project.json", StringComparison.OrdinalIgnoreCase));
        var projPath = rawProjPath != null
            ? (Path.IsPathRooted(rawProjPath) ? rawProjPath : Path.Combine(directoryPath, rawProjPath))
            : Path.Combine(directoryPath, "project.json");
        if (WorkspaceDirectoryIndex.FileExistsInCurrentOrDisk(projPath) && File.Exists(projPath))
        {
            var projContent = FileStreamReader.ReadTextOnce(projPath);
            if (!string.IsNullOrEmpty(projContent))
            {
                projRoot = CypherValue.TryParseJsonText(projContent);
            }
        }

        string[]? wranglerLines = null;
        var rawWranglerPath = filesInDirectory.FirstOrDefault(f => Path.GetFileName(f).Equals("wrangler.toml", StringComparison.OrdinalIgnoreCase));
        var wranglerPath = rawWranglerPath != null
            ? (Path.IsPathRooted(rawWranglerPath) ? rawWranglerPath : Path.Combine(directoryPath, rawWranglerPath))
            : Path.Combine(directoryPath, "wrangler.toml");
        if (WorkspaceDirectoryIndex.FileExistsInCurrentOrDisk(wranglerPath) && File.Exists(wranglerPath))
        {
            var wranglerText = FileStreamReader.ReadTextOnce(wranglerPath);
            if (!string.IsNullOrEmpty(wranglerText))
            {
                wranglerLines = wranglerText.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
            }
        }

        return new TsDirectoryManifestBundle(pkgPath, pkgRoot, projRoot, wranglerLines);
    }

    public string GetProjectName(string directoryPath, string[] filesInDirectory)
    {
        var bundle = ReadDirectoryManifestBundleOnce(directoryPath, filesInDirectory);
        return ExtractProjectNameFromBundle(directoryPath, bundle);
    }

    private static string ExtractProjectNameFromBundle(string directoryPath, TsDirectoryManifestBundle bundle)
    {
        var wranglerName = TryExtractWranglerNameFromLines(directoryPath, bundle.WranglerLines);
        if (!string.IsNullOrEmpty(wranglerName))
        {
            return wranglerName;
        }

        const string propName = "name";
        if (bundle.ProjectJson is { Kind: CypherValueKind.Object } pRoot &&
            pRoot.TryGetProperty(propName, out var pNameProp) &&
            pNameProp.Kind == CypherValueKind.String)
        {
            var normalized = NormalizeTsManifestName(pNameProp.AsNullableString());
            if (!string.IsNullOrEmpty(normalized)) return normalized;
        }

        if (bundle.PackageJson is { Kind: CypherValueKind.Object } pkgRoot &&
            pkgRoot.TryGetProperty(propName, out var nameProp) &&
            nameProp.Kind == CypherValueKind.String)
        {
            var normalized = NormalizeTsManifestName(nameProp.AsNullableString());
            if (!string.IsNullOrEmpty(normalized)) return normalized;
        }

        return Path.GetFileName(directoryPath.TrimEnd('/', '\\'));
    }

    private static string? NormalizeTsManifestName(string? rawName)
    {
        if (string.IsNullOrWhiteSpace(rawName)) return null;
        var name = rawName.Trim();
        if (name.StartsWith('@') && name.Contains('/'))
        {
            name = name[(name.IndexOf('/') + 1)..].Trim();
        }
        if (string.IsNullOrEmpty(name)) return null;
        var stripped = WorkspaceConventions.StripServicePrefix(name);
        return !string.IsNullOrEmpty(stripped) ? stripped : name;
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

        var bundle = ReadDirectoryManifestBundleOnce(dir, files);
        var name = ExtractProjectNameFromBundle(dir, bundle);
        if (string.IsNullOrEmpty(name))
        {
            name = Path.GetFileName(dir.TrimEnd('/', '\\'));
        }

        var props = ExtractManifestPropertiesFromBundle(dir, files, bundle);
        var absorbedSubpaths = new List<string>();
        var excludedSubpaths = new List<string>();
        var entryPointPaths = new List<string>();
        var sourceRoots = new List<string>();
        var subProjects = new List<ParsedProjectDescriptor>();

        InspectWranglerLayoutFromLines(dir, bundle.WranglerLines, absorbedSubpaths, excludedSubpaths, entryPointPaths, sourceRoots);

        foreach (var staticCandidate in new[] { "public_src", "public", "workers/public" })
        {
            var fullCandidate = Path.Combine(dir, staticCandidate);
            if (Directory.Exists(fullCandidate) && !excludedSubpaths.Contains(staticCandidate, StringComparer.OrdinalIgnoreCase))
            {
                excludedSubpaths.Add(staticCandidate);
            }
        }

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

        ExtractWorkspaceManifestSubProjects(dir, sourceRoots, excludedSubpaths, subProjects);
        DetectBuildOrchestratorPropertiesFromBundle(dir, files, bundle.PackageJson, props);

        if (absorbedSubpaths.Count > 0)
        {
            props["absorbed_subpaths"] = JsonSerializer.Serialize(absorbedSubpaths);
        }
        if (excludedSubpaths.Count > 0)
        {
            props["excluded_subpaths"] = JsonSerializer.Serialize(excludedSubpaths);
        }

        var produced = ExtractProducedPackageFromBundle(bundle.PackageJson);
        var deps = ExtractDependenciesFromBundle(dir, bundle);
        var meta = ExtractManifestMetadataFromBundle(dir, files, bundle.PackageJson, props);

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
                SubProjects: subProjects.Count > 0 ? subProjects : null,
                ProducedPackage: produced,
                Dependencies: deps,
                ManifestMetadata: meta),
            ClaimMode: ParserClaimMode.AbsorbSecondaryInDirectory);
        activeIndex?.StoreProjectParse(this, dir, true, result);
        return true;
    }

    public ParsedProjectManifestMetadata? InspectDirectoryManifestMetadata(ProjectParseContext context)
    {
        var bundle = ReadDirectoryManifestBundleOnce(context.DirectoryPath, context.FilesInDirectory);
        return ExtractManifestMetadataFromBundle(context.DirectoryPath, context.FilesInDirectory, bundle.PackageJson, null);
    }

    private const string PropIsBuildOrchestrator = "is_build_orchestrator";
    private const string PropIsOrchestrator = "is_orchestrator";

    public bool CanContainNestedProject(
        ParsedProjectDescriptor enclosingProject,
        string childDirectoryPath,
        string[] childFiles,
        IProjectParser candidateChildParser)
    {
        if (!IProjectParser.DefaultCanContainNestedProject(enclosingProject, childDirectoryPath, candidateChildParser))
        {
            return false;
        }

        if (string.Equals(enclosingProject.ManifestProperties.GetValueOrDefault(PropIsBuildOrchestrator), "true", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(enclosingProject.ManifestProperties.GetValueOrDefault(PropIsOrchestrator), "true", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var normChild = Path.GetFullPath(childDirectoryPath).Replace('\\', '/').TrimEnd('/');
        if (enclosingProject.SubProjects != null)
        {
            foreach (var sub in enclosingProject.SubProjects)
            {
                var fullSub = Path.GetFullPath(sub.DirectoryPath).Replace('\\', '/').TrimEnd('/');
                if (normChild.Equals(fullSub, StringComparison.OrdinalIgnoreCase) ||
                    normChild.StartsWith(fullSub + "/", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        if (candidateChildParser.ProjectType is "typescript" or "javascript")
        {
            var relToParent = Path.GetRelativePath(enclosingProject.DirectoryPath, childDirectoryPath).Replace('\\', '/').Trim('/');
            var firstSegment = relToParent.Split('/')[0];
            if (firstSegment.Equals("src", StringComparison.OrdinalIgnoreCase) ||
                firstSegment.Equals("templates", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    public Task<ProducedPackageInfo?> GetProducedPackageAsync(string projectDirectory)
    {
        var files = WorkspaceDirectoryIndex.GetDirectoryFilesOrFallback(projectDirectory);
        var bundle = ReadDirectoryManifestBundleOnce(projectDirectory, files);
        return Task.FromResult(ExtractProducedPackageFromBundle(bundle.PackageJson));
    }

    internal static ProducedPackageInfo? ExtractProducedPackageFromBundle(CypherValue? pkgRoot)
    {
        const string ecoNpm = "npm";
        if (pkgRoot is not { Kind: CypherValueKind.Object } root) return null;
        if (!root.TryGetProperty(OntologyConstants.ManifestFields.Name, out var nameProp) || nameProp.Kind != CypherValueKind.String)
        {
            return null;
        }

        var name = nameProp.AsNullableString();
        if (string.IsNullOrEmpty(name)) return null;

        const string defaultVersion = "1.0.0";
        var version = root.TryGetProperty(OntologyConstants.ManifestFields.Version, out var versionProp) && versionProp.Kind == CypherValueKind.String
            ? versionProp.AsString(defaultVersion)
            : defaultVersion;

        return new ProducedPackageInfo(name, version, ecoNpm);
    }

    public Task<ProjectDependencyInfo> ParseDependenciesAsync(string projectDirectory)
    {
        var files = WorkspaceDirectoryIndex.GetDirectoryFilesOrFallback(projectDirectory);
        var bundle = ReadDirectoryManifestBundleOnce(projectDirectory, files);
        return Task.FromResult(ExtractDependenciesFromBundle(projectDirectory, bundle));
    }

    private static ProjectDependencyInfo ExtractDependenciesFromBundle(string projectDirectory, TsDirectoryManifestBundle bundle)
    {
        var localProjectPaths = new List<string>();
        var externalPackages = new List<ProducedPackageInfo>();

        var pkgJsonPath = bundle.PackageJsonPath;
        var pkgRoot = bundle.PackageJson;

        if (pkgRoot == null && bundle.ProjectJson != null)
        {
            var parent = Path.GetDirectoryName(projectDirectory);
            while (parent != null && parent.Length >= 3)
            {
                var rootPkg = Path.Combine(parent, "package.json");
                if (File.Exists(rootPkg) && File.Exists(Path.Combine(parent, "nx.json")))
                {
                    pkgJsonPath = rootPkg;
                    var rootContent = FileStreamReader.ReadTextOnce(rootPkg);
                    if (!string.IsNullOrEmpty(rootContent))
                    {
                        pkgRoot = CypherValue.TryParseJsonText(rootContent);
                    }
                    break;
                }
                parent = Path.GetDirectoryName(parent);
            }
        }

        if (pkgRoot is { Kind: CypherValueKind.Object } root && pkgJsonPath != null)
        {
            CollectNpmDependencies(pkgJsonPath, root, localProjectPaths, externalPackages);
        }

        if (bundle.ProjectJson is { Kind: CypherValueKind.Object } pRoot)
        {
            CollectNxProjectJsonDependencies(pRoot, externalPackages);
            ExtractTsConfigPathDependencies(projectDirectory, localProjectPaths, externalPackages);
        }

        return new ProjectDependencyInfo(localProjectPaths, externalPackages);
    }

    internal static void CollectNpmDependencies(
        string packageJsonPath,
        CypherValue root,
        List<string> localProjectPaths,
        List<ProducedPackageInfo> externalPackages)
    {
        foreach (var propName in new[] { "dependencies", "devDependencies" })
        {
            if (!root.TryGetProperty(propName, out var depsObj) || depsObj.Kind != CypherValueKind.Object) continue;

            const string defaultUnknownVersion = "unknown";
            foreach (var prop in depsObj.AsObject())
            {
                var packageName = prop.Key;
                var packageVersion = prop.Value.AsString(defaultUnknownVersion);

                if (packageVersion.StartsWith("file:", StringComparison.Ordinal) ||
                    (packageVersion.StartsWith("workspace:", StringComparison.Ordinal) &&
                     (packageVersion.Contains('/') || packageVersion.Contains('\\'))))
                {
                    var relativePath = packageVersion[(packageVersion.IndexOf(':') + 1)..];
                    if (!string.IsNullOrEmpty(relativePath) &&
                        (relativePath.StartsWith('.') || relativePath.StartsWith('/') || relativePath.StartsWith('\\')))
                    {
                        try
                        {
                            var referencedDir = Path.GetFullPath(Path.Combine(
                                Path.GetDirectoryName(packageJsonPath) ?? string.Empty,
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

                const string ecoNpm = "npm";
                const string scopeDev = "dev";
                const string scopeRuntime = "runtime";
                externalPackages.Add(new ProducedPackageInfo(packageName, packageVersion, ecoNpm, propName == "devDependencies" ? scopeDev : scopeRuntime));
            }
        }
    }

    private static void CollectNxProjectJsonDependencies(CypherValue pRoot, List<ProducedPackageInfo> externalPackages)
    {
        const string propImplicitDependencies = "implicitDependencies";
        const string propTargets = "targets";
        const string propDependsOn = "dependsOn";
        const string propProjects = "projects";
        const string wildcardVer = "*";
        const string ecoNpm = "npm";
        const string scopeRuntime = "runtime";

        if (pRoot.TryGetProperty(propImplicitDependencies, out var impProp) && impProp.Kind == CypherValueKind.Array)
        {
            foreach (var imp in impProp.AsArray())
            {
                var depName = imp.AsNullableString();
                if (!string.IsNullOrWhiteSpace(depName))
                {
                    externalPackages.Add(new ProducedPackageInfo(depName.Trim(), wildcardVer, ecoNpm, scopeRuntime));
                }
            }
        }

        if (!pRoot.TryGetProperty(propTargets, out var targetsProp) || targetsProp.Kind != CypherValueKind.Object) return;

        foreach (var target in targetsProp.AsObject())
        {
            if (target.Value.Kind != CypherValueKind.Object ||
                !target.Value.TryGetProperty(propDependsOn, out var depOnProp) ||
                depOnProp.Kind != CypherValueKind.Array)
            {
                continue;
            }

            foreach (var depItem in depOnProp.AsArray())
            {
                if (depItem.Kind == CypherValueKind.String)
                {
                    var str = depItem.AsString();
                    var colon = str.IndexOf(':');
                    var pName = colon > 0 ? str[..colon] : str;
                    if (!string.IsNullOrWhiteSpace(pName) && !pName.StartsWith('^'))
                    {
                        externalPackages.Add(new ProducedPackageInfo(pName.Trim(), wildcardVer, ecoNpm, scopeRuntime));
                    }
                }
                else if (depItem.Kind == CypherValueKind.Object && depItem.TryGetProperty(propProjects, out var projsProp))
                {
                    if (projsProp.Kind == CypherValueKind.String)
                    {
                        var pName = projsProp.AsString();
                        if (!string.IsNullOrWhiteSpace(pName))
                        {
                            externalPackages.Add(new ProducedPackageInfo(pName.Trim(), wildcardVer, ecoNpm, scopeRuntime));
                        }
                    }
                    else if (projsProp.Kind == CypherValueKind.Array)
                    {
                        foreach (var pi in projsProp.AsArray())
                        {
                            var pName = pi.AsNullableString();
                            if (!string.IsNullOrWhiteSpace(pName))
                            {
                                externalPackages.Add(new ProducedPackageInfo(pName.Trim(), wildcardVer, ecoNpm, scopeRuntime));
                            }
                        }
                    }
                }
            }
        }
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
            var content = FileStreamReader.ReadTextOnce(tsConfigPath);
            var root = CypherValue.TryParseJsonText(content);
            if (!root.TryGetProperty("compilerOptions", out var coProp) ||
                coProp.Kind != CypherValueKind.Object ||
                !coProp.TryGetProperty("paths", out var pathsProp) ||
                pathsProp.Kind != CypherValueKind.Object)
            {
                return;
            }

            var existingNames = externalPackages
                .Select(p => p.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var pathMappings = new List<(string AliasPrefix, string TargetRelPath, string ShortName)>();

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

                if (cleanAlias is "~" or "@" ||
                    cleanTarget.Equals("src", StringComparison.OrdinalIgnoreCase) ||
                    cleanTarget.Equals("./src", StringComparison.OrdinalIgnoreCase) ||
                    cleanTarget.StartsWith("src/", StringComparison.OrdinalIgnoreCase) ||
                    cleanTarget.StartsWith("./src/", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var shortName = cleanAlias.Contains('/') ? cleanAlias.Split('/')[^1].TrimEnd('*') : cleanAlias;
                if (string.IsNullOrEmpty(shortName) || existingNames.Contains(shortName))
                {
                    continue;
                }

                pathMappings.Add((cleanAlias, cleanTarget, shortName));
            }

            if (pathMappings.Count == 0) return;

            var candidateFiles = WorkspaceDirectoryIndex.HasCachedDirectoryInCurrent(projectDirectory)
                ? WorkspaceDirectoryIndex.EnumerateSubtreeInCurrent(projectDirectory)
                : Directory.EnumerateFiles(projectDirectory, "*.*", SearchOption.AllDirectories);

            var tsFiles = candidateFiles
                .Where(f =>
                {
                    var ext = Path.GetExtension(f);
                    return ext is ".ts" or ".tsx" or ".js" or ".jsx" or ".mts" or ".cts";
                })
                .Take(200);

            var tsConfigDir = Path.GetDirectoryName(tsConfigPath) ?? projectDirectory;
            var normSelfDir = Path.GetFullPath(projectDirectory).Replace('\\', '/').TrimEnd('/');
            var normRootDir = Path.GetFullPath(tsConfigDir).Replace('\\', '/').TrimEnd('/');
            var matchedTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var file in tsFiles)
            {
                if (matchedTargets.Count >= pathMappings.Count)
                {
                    break;
                }

                var text = FileStreamReader.ReadTextOnce(file);
                if (string.IsNullOrEmpty(text)) continue;

                foreach (var (alias, target, _) in pathMappings)
                {
                    if (matchedTargets.Contains(target)) continue;

                    if (text.Contains($"'{alias}") || text.Contains($"\"{alias}"))
                    {
                        const string wildcardVer = "*", ecoNpm = "npm", scopeRuntime = "runtime";
                        matchedTargets.Add(target);

                        var ownerDir = TryResolveTsConfigTargetProjectDir(tsConfigDir, normRootDir, target);
                        if (ownerDir != null)
                        {
                            if (!ownerDir.Equals(normSelfDir, StringComparison.OrdinalIgnoreCase) &&
                                !localProjectPaths.Contains(ownerDir, StringComparer.OrdinalIgnoreCase))
                            {
                                localProjectPaths.Add(ownerDir);
                            }
                        }
                        else if (existingNames.Add(alias))
                        {
                            externalPackages.Add(new ProducedPackageInfo(alias, wildcardVer, ecoNpm, scopeRuntime));
                        }
                    }
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // Ignore
        }
    }

    private static string? TryResolveTsConfigTargetProjectDir(string tsConfigDir, string normRootDir, string targetRelPath)
    {
        const string projJsonName = "project.json", pkgJsonName = "package.json", slash = "/";
        try
        {
            var fullTarget = Path.GetFullPath(Path.Combine(tsConfigDir, targetRelPath));
            var curr = Directory.Exists(fullTarget) ? fullTarget : Path.GetDirectoryName(fullTarget);
            while (!string.IsNullOrEmpty(curr))
            {
                var normCurr = curr.Replace('\\', '/').TrimEnd('/');
                if (normCurr.Equals(normRootDir, StringComparison.OrdinalIgnoreCase) ||
                    !normCurr.StartsWith(normRootDir + slash, StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                if (File.Exists(Path.Combine(curr, projJsonName)) ||
                    File.Exists(Path.Combine(curr, pkgJsonName)))
                {
                    return normCurr;
                }

                curr = Path.GetDirectoryName(curr);
            }
        }
        catch (Exception ex) when (ex is ArgumentException or PathTooLongException)
        {
            // Ignore malformed path
        }
        return null;
    }
}
