using System.Text.Json;
using CodeExplorer.Core.Database;

namespace CodeExplorer.Parsers.TypeScript;

public partial class TypeScriptProjectParser
{
    private const string PropIsLibrary = "is_library";
    private const string PropManifestType = "manifest_type";
    private const string PropTags = "tags";
    private const string PropActivationEvents = "activationEvents";
    private const string PropExports = "exports";

    private static readonly HashSet<string> InstallHookNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "preinstall", "install", "postinstall", "preuninstall", "postuninstall", "prepublish"
    };

    private static readonly HashSet<string> BuildHookNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "build", "prepare", "prepack", "compile"
    };

    private static string? TryExtractWranglerNameFromLines(string directoryPath, string[]? wranglerLines)
    {
        if (wranglerLines is not { Length: > 0 }) return null;
        var folderName = Path.GetFileName(directoryPath.TrimEnd('/', '\\'));

        foreach (var rawLine in wranglerLines)
        {
            var line = rawLine.Trim();
            if (line.StartsWith('#') || line.StartsWith('[')) continue;
            if (!line.StartsWith("name", StringComparison.OrdinalIgnoreCase)) continue;

            var eqIdx = line.IndexOf('=');
            if (eqIdx <= 0) continue;

            var val = line[(eqIdx + 1)..].Trim().Trim('"', '\'');
            if (string.IsNullOrWhiteSpace(val)) continue;

            foreach (var envSuffix in new[] { "-dev", "-prod", "-production", "-staging", "-stage", "-test" })
            {
                if (val.EndsWith(envSuffix, StringComparison.OrdinalIgnoreCase) && val.Length > envSuffix.Length)
                {
                    val = val[..^envSuffix.Length];
                    break;
                }
            }

            return !string.IsNullOrWhiteSpace(folderName) && folderName.Equals(val, StringComparison.OrdinalIgnoreCase)
                ? folderName
                : val;
        }

        return null;
    }

    private static void InspectWranglerLayoutFromLines(
        string directoryPath,
        string[]? wranglerLines,
        List<string> absorbedSubpaths,
        List<string> excludedSubpaths,
        List<string> entryPointPaths,
        List<string> sourceRoots)
    {
        if (wranglerLines is { Length: > 0 })
        {
            foreach (var rawLine in wranglerLines)
            {
                var line = rawLine.Trim();
                if (line.StartsWith('#')) continue;

                if (line.StartsWith("main", StringComparison.OrdinalIgnoreCase) && line.Contains('='))
                {
                    var mainRel = line[(line.IndexOf('=') + 1)..].Trim().Trim('"', '\'').Replace('\\', '/').TrimStart('.', '/');
                    if (!string.IsNullOrEmpty(mainRel))
                    {
                        var fullMain = Path.GetFullPath(Path.Combine(directoryPath, mainRel));
                        if (File.Exists(fullMain))
                        {
                            entryPointPaths.Add(fullMain);
                        }
                        var firstSeg = mainRel.Split('/')[0];
                        if (!string.IsNullOrEmpty(firstSeg) && Directory.Exists(Path.Combine(directoryPath, firstSeg)))
                        {
                            if (!absorbedSubpaths.Contains(firstSeg, StringComparer.OrdinalIgnoreCase))
                                absorbedSubpaths.Add(firstSeg);
                            if (!sourceRoots.Contains(firstSeg, StringComparer.OrdinalIgnoreCase))
                                sourceRoots.Add(firstSeg);
                        }
                    }
                }
                else if (line.Contains("bucket", StringComparison.OrdinalIgnoreCase) && line.Contains('='))
                {
                    var bucketIdx = line.IndexOf("bucket", StringComparison.OrdinalIgnoreCase);
                    var eqIdx = line.IndexOf('=', bucketIdx);
                    if (eqIdx > 0)
                    {
                        var afterEq = line[(eqIdx + 1)..].Trim();
                        var quoteChar = afterEq.FirstOrDefault(c => c is '"' or '\'');
                        if (quoteChar != '\0')
                        {
                            var firstQuote = afterEq.IndexOf(quoteChar);
                            var secondQuote = afterEq.IndexOf(quoteChar, firstQuote + 1);
                            if (secondQuote > firstQuote)
                            {
                                var bucketPath = afterEq[(firstQuote + 1)..secondQuote].Replace('\\', '/').TrimStart('.', '/').Trim('/');
                                if (!string.IsNullOrEmpty(bucketPath) && !excludedSubpaths.Contains(bucketPath, StringComparer.OrdinalIgnoreCase))
                                {
                                    excludedSubpaths.Add(bucketPath);
                                }
                            }
                        }
                    }
                }
            }
        }

        const string workersSiteFolder = "workers-site";
        var workersSiteDir = Path.Combine(directoryPath, workersSiteFolder);
        if (Directory.Exists(workersSiteDir))
        {
            if (!absorbedSubpaths.Contains(workersSiteFolder, StringComparer.OrdinalIgnoreCase))
            {
                absorbedSubpaths.Add(workersSiteFolder);
            }
            if (!sourceRoots.Contains(workersSiteFolder, StringComparer.OrdinalIgnoreCase))
            {
                sourceRoots.Add(workersSiteFolder);
            }
        }
    }

    private void ExtractWorkspaceManifestSubProjects(
        string directoryPath,
        List<string> sourceRoots,
        List<string> excludedSubpaths,
        List<ParsedProjectDescriptor> subProjects)
    {
        Components.AngularComponentParser.ExtractWorkspaceSubProjects(directoryPath, this, sourceRoots, excludedSubpaths, subProjects);
        Components.NestJsComponentParser.ExtractWorkspaceSubProjects(directoryPath, this, sourceRoots, excludedSubpaths, subProjects);
        Components.NxComponentParser.ExtractWorkspaceSubProjects(directoryPath, this, sourceRoots, excludedSubpaths, subProjects);
    }

    private static void DetectBuildOrchestratorPropertiesFromBundle(
        string directoryPath,
        string[] filesInDirectory,
        CypherValue? pkgJsonRoot,
        Dictionary<string, string> props)
    {
        if (pkgJsonRoot is not { Kind: CypherValueKind.Object } root) return;
        if (HasWranglerManifest(filesInDirectory)) return;

        const string gulpPrefix = "gulp";
        const string webpackPrefix = "webpack";
        const string wranglerGlob = "wrangler.*";
        var hasMainOrExports = root.TryGetProperty(OntologyConstants.ManifestFields.Main, out _) ||
                               root.TryGetProperty(OntologyConstants.ManifestFields.Module, out _) ||
                               root.TryGetProperty(PropExports, out _) ||
                               root.TryGetProperty(OntologyConstants.ManifestFields.Bin, out _);
        if (hasMainOrExports) return;

        var allDeps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var depSec in new[] { "dependencies", "devDependencies" })
        {
            if (root.TryGetProperty(depSec, out var dObj) && dObj.Kind == CypherValueKind.Object)
            {
                foreach (var p in dObj.AsObject()) allDeps.Add(p.Key);
            }
        }

        var hasBuildTools = allDeps.Any(d =>
            d.StartWithIC(gulpPrefix) || d.StartWithIC(webpackPrefix) || d is "husky" or "lerna" or "turbo" or "nx" or "prettier" or "eslint");
        if (!hasBuildTools) return;

        var workersSubdir = Path.Combine(directoryPath, "workers");
        var hasNestedWorkers = Directory.Exists(workersSubdir) &&
                               Directory.EnumerateFiles(workersSubdir, wranglerGlob, SearchOption.AllDirectories).Any();

        if (hasNestedWorkers)
        {
            props["is_build_orchestrator"] = "true";
            props["manifest_type"] = "worker";
            props["framework_type"] = "worker";
            props["cloud_runtime"] = "cloudflare-worker";
        }
    }

    public Dictionary<string, string> ExtractManifestProperties(string directoryPath, string[] filesInDirectory)
    {
        var bundle = ReadDirectoryManifestBundleOnce(directoryPath, filesInDirectory);
        return ExtractManifestPropertiesFromBundle(directoryPath, filesInDirectory, bundle);
    }

    private Dictionary<string, string> ExtractManifestPropertiesFromBundle(
        string directoryPath,
        string[] filesInDirectory,
        TsDirectoryManifestBundle bundle)
    {
        var props = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var hasWrangler = bundle.WranglerLines != null ||
                          filesInDirectory.Any(f => Path.GetFileName(f).Equals("wrangler.toml", StringComparison.OrdinalIgnoreCase)) ||
                          (Path.GetDirectoryName(directoryPath) is { } wranglerParent && File.Exists(Path.Combine(wranglerParent, "wrangler.toml")));
        if (hasWrangler)
        {
            props["manifest_type"] = "worker";
            props["framework_type"] = "worker";
            props["cloud_runtime"] = "cloudflare-worker";
        }

        if (bundle.ProjectJson is { Kind: CypherValueKind.Object } pRoot)
        {
            const string propProjectType = "projectType";
            if (pRoot.TryGetProperty(propProjectType, out var ptProp) && ptProp.Kind == CypherValueKind.String)
            {
                var pt = ptProp.AsNullableString()?.ToLowerInvariant();
                if (pt == "library")
                {
                    props["manifest_type"] = "library";
                    props["is_library"] = "true";
                    props["role"] = "SharedLibrary";
                    props["primary_role"] = "Library";
                }
                else if (pt == "application")
                {
                    props["manifest_type"] = "application";
                }
            }

            if (pRoot.TryGetProperty(PropTags, out var tagsProp) && tagsProp.Kind == CypherValueKind.Array)
            {
                foreach (var tagItem in tagsProp.AsArray())
                {
                    var tagStr = tagItem.AsNullableString();
                    if (string.Equals(tagStr, "scope:docs", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(tagStr, "type:docs", StringComparison.OrdinalIgnoreCase))
                    {
                        props["is_docs_project"] = "true";
                        props["is_library"] = "true";
                        props["manifest_type"] = "library";
                    }
                }
            }
        }

        Components.AngularComponentParser.EnrichManifestProperties(directoryPath, filesInDirectory, props, bundle.PackageJson);

        if (bundle.PackageJson is { Kind: CypherValueKind.Object } root)
        {
            const string propDependencies = "dependencies";
            const string propDevDependencies = "devDependencies";
            const string propWorkspaces = "workspaces";
            const string propScripts = "scripts";
            const string propPeerDependencies = "peerDependencies";
            const string propPublishConfig = "publishConfig";
            const string propTypes = "types";
            const string propTypings = "typings";
            const string keyIsDocsProject = "is_docs_project";
            const string prefixCloudflareHyphen = "cloudflare-";
            const string prefixCloudflareScope = "@cloudflare/";

            try
            {
                // Check "bin" property
                if (root.TryGetProperty(OntologyConstants.ManifestFields.Bin, out var binProp))
                {
                    if (binProp.Kind == CypherValueKind.String &&
                        !string.IsNullOrWhiteSpace(binProp.AsNullableString()))
                    {
                        props["has_cli_bin"] = "true";
                        if (!props.ContainsKey(PropManifestType)) props[PropManifestType] = "cli";
                    }
                    else if (binProp.Kind == CypherValueKind.Object &&
                             binProp.AsObject().Count > 0)
                    {
                        props["has_cli_bin"] = "true";
                        if (!props.ContainsKey(PropManifestType)) props[PropManifestType] = "cli";
                    }
                }

                // Collect dependencies
                var allDeps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                if (root.TryGetProperty(propDependencies, out var depsObj) &&
                    depsObj.Kind == CypherValueKind.Object)
                {
                    foreach (var prop in depsObj.AsObject()) allDeps.Add(prop.Key);
                }

                if (root.TryGetProperty(propDevDependencies, out var devDepsObj) &&
                    devDepsObj.Kind == CypherValueKind.Object)
                {
                    foreach (var prop in devDepsObj.AsObject()) allDeps.Add(prop.Key);
                }

                var hasIndexHtml = filesInDirectory.Any(f =>
                    Path.GetFileName(f).Equals("index.html", StringComparison.OrdinalIgnoreCase))
                    || File.Exists(Path.Combine(directoryPath, "src", "index.html"))
                    || File.Exists(Path.Combine(directoryPath, "public", "index.html"));

                // Framework and workload detection via registered LibraryRole descriptors + runtime indicators
                var hasGrpcDeps = allDeps.Any(d => d is "@grpc/grpc-js" or "grpc" || d.StartsWith("@grpc/"));
                if (hasGrpcDeps) props["has_grpc"] = "true";

                var hasWebFramework =
                    hasGrpcDeps ||
                    ((IProjectParser)this).HasPackageWithRole(allDeps, LibraryRole.WebService) ||
                    allDeps.Contains("hono");

                var hasWorkerDeps =
                    ((IProjectParser)this).HasPackageWithRole(allDeps, LibraryRole.WorkerService) ||
                    ((IProjectParser)this).HasPackageWithRole(allDeps, LibraryRole.MessageBroker) ||
                    allDeps.Any(d =>
                        d is "@nestjs/schedule" or "@nestjs/bull" or "@nestjs/bullmq" or "@cloudflare/workers-types"
                            or "wrangler" or "node-cron" or "cron" or "agenda"
                            || d.StartWithIC(prefixCloudflareHyphen) || d.StartWithIC(prefixCloudflareScope)) ||
                    hasWrangler;

                var hasMobileDeps = allDeps.Any(d => d is "react-native" or "expo" || d.StartsWith("expo-") || d.StartsWith("@expo/")) ||
                                    filesInDirectory.Any(f => Path.GetFileName(f).Equals("app.json", StringComparison.OrdinalIgnoreCase));

                var hasDesktopDeps = allDeps.Any(d =>
                    d is "electron" or "@electron-forge/cli" or "electron-builder" or "@tauri-apps/api");

                var hasFrontendDeps =
                    hasMobileDeps ||
                    hasDesktopDeps ||
                    ((IProjectParser)this).HasPackageWithRole(allDeps, LibraryRole.FrontendFramework) ||
                    allDeps.Any(d => d is "solid-js" or "nuxt");

                // Monorepo root manifest (npm/yarn "workspaces" or pnpm/Nx/Lerna/Turbo workspace files): it orchestrates
                // member packages and is never itself a deployable service.
                var isWorkspaceRoot = root.TryGetProperty(propWorkspaces, out _) ||
                                      filesInDirectory.Any(f => Path.GetFileName(f) is "pnpm-workspace.yaml" or "lerna.json" or "turbo.json" or "nx.json");
                if (isWorkspaceRoot && !Directory.Exists(Path.Combine(directoryPath, "src")))
                {
                    props["is_orchestrator"] = "true";
                    props["is_build_orchestrator"] = "true";
                }

                // End-to-end / browser test suites (Playwright, Cypress ...) without a server or UI of their own are test
                // projects, even when an Nx project.json marks them as "application".
                var hasE2eRunner = allDeps.Any(d => d is "@playwright/test" or "playwright" or "cypress" or "testcafe" or "webdriverio" or "@wdio/cli" or "nightwatch");
                if (hasE2eRunner && !hasWebFramework && !hasFrontendDeps && !isWorkspaceRoot)
                {
                    props["manifest_type"] = "test";
                    props["is_test_project"] = "true";
                }

                var isDocsSite = props.ContainsKey(keyIsDocsProject) ||
                                 allDeps.Any(d => d is "mintlify" or "@docusaurus/core" or "vitepress" or "nextra" or "vocs" or "rspress" || d.StartsWith("@mdx-js/")) ||
                                 (root.TryGetProperty(propScripts, out var docScrObj) &&
                                  docScrObj.Kind == CypherValueKind.Object &&
                                  docScrObj.AsObject().Any(s => s.Key.Equals("mintlify", StringComparison.OrdinalIgnoreCase) ||
                                                                s.Value.AsString().Contains("mintlify", StringComparison.OrdinalIgnoreCase) ||
                                                                s.Value.AsString().Contains("docusaurus", StringComparison.OrdinalIgnoreCase) ||
                                                                s.Value.AsString().Contains("vitepress", StringComparison.OrdinalIgnoreCase)));
                if (isDocsSite && !hasWebFramework && !isWorkspaceRoot)
                {
                    props["is_library"] = "true";
                    props["manifest_type"] = "library";
                }

                var pkgName =
                    root.TryGetProperty(OntologyConstants.ManifestFields.Name, out var np) && np.Kind == CypherValueKind.String
                        ? np.AsString().ToLowerInvariant()
                        : "";
                var projDirName = Path.GetFileName(directoryPath).ToLowerInvariant();

                var isDedicatedWorkerName = projDirName.Contains("worker") || projDirName.Contains("scheduler") ||
                                            projDirName.Contains("consumer") || pkgName.Contains("worker") ||
                                            pkgName.Contains("scheduler") || pkgName.Contains("consumer");

                const string workerScriptPrefix = "worker:";
                var hasWorkerScripts = root.TryGetProperty(propScripts, out var scrObj) &&
                                       scrObj.Kind == CypherValueKind.Object &&
                                       scrObj.AsObject().Any(s =>
                                           s.Key.StartWithIC(workerScriptPrefix) ||
                                           s.Key.Contains("wrangler", StringComparison.OrdinalIgnoreCase) ||
                                           s.Value.AsString().Contains("wrangler", StringComparison.OrdinalIgnoreCase));

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
                    if (!props.ContainsKey(PropManifestType)) props[PropManifestType] = "worker";
                }

                var hasPeerDeps = root.TryGetProperty(propPeerDependencies, out _);
                var hasPublishConfig = root.TryGetProperty(propPublishConfig, out _);
                var hasPublishScript = root.TryGetProperty(propScripts, out var sObj) &&
                                       sObj.Kind == CypherValueKind.Object &&
                                       sObj.AsObject().Any(s => s.Value.AsString().Contains("npm publish", StringComparison.OrdinalIgnoreCase));

                var hasExplicitTypesOrExports = root.TryGetProperty(PropExports, out _) ||
                                                root.TryGetProperty(propTypes, out _) ||
                                                root.TryGetProperty(propTypings, out _);

                var hasExplicitExports = hasExplicitTypesOrExports ||
                                         root.TryGetProperty(OntologyConstants.ManifestFields.Main, out _);

                var hasPrimaryAppStartScript = root.TryGetProperty(propScripts, out var scriptsObj) &&
                                               scriptsObj.Kind == CypherValueKind.Object &&
                                               scriptsObj.AsObject().Any(s =>
                                                   s.Key.Equals("start", StringComparison.OrdinalIgnoreCase) ||
                                                   s.Key.Equals("start:prod", StringComparison.OrdinalIgnoreCase) ||
                                                   s.Key.Equals("serve", StringComparison.OrdinalIgnoreCase));

                var isVsCodeExtension = (root.TryGetProperty(OntologyConstants.ManifestFields.Engines, out var enginesProp) &&
                                         enginesProp.Kind == CypherValueKind.Object &&
                                         enginesProp.TryGetProperty(OntologyConstants.ManifestFields.VsCode, out _)) ||
                                        (root.TryGetProperty(OntologyConstants.ManifestFields.Contributes, out var contributesProp) &&
                                         contributesProp.Kind == CypherValueKind.Object) ||
                                        (root.TryGetProperty(PropActivationEvents, out var actProp) &&
                                         actProp.Kind == CypherValueKind.Array) ||
                                        allDeps.Contains("@types/vscode");

                var hasStrongLibraryContract = !isWorkerProject && !isVsCodeExtension &&
                                               (hasPeerDeps || hasPublishConfig || hasPublishScript ||
                                                (hasExplicitTypesOrExports && !hasPrimaryAppStartScript));
                var isLibraryByContract = hasStrongLibraryContract ||
                                          (!isWorkerProject && !isVsCodeExtension && hasExplicitExports && !hasPrimaryAppStartScript);
                if (isLibraryByContract)
                {
                    props[PropIsLibrary] = "true";
                    if (!props.ContainsKey(PropManifestType)) props[PropManifestType] = "library";
                }

                if (isVsCodeExtension)
                {
                    props[PropManifestType] = "application";
                    props["framework_type"] = "extension";
                    props["ui_type"] = "extension";
                    props["is_vscode_extension"] = "true";
                    props.Remove(PropIsLibrary);
                }
                else if (hasDesktopDeps)
                {
                    props["framework_type"] = "frontend";
                    props["is_frontend_app"] = "true";
                    props[PropManifestType] = "application";
                    props.Remove(PropIsLibrary);
                }
                else if (hasWebFramework)
                {
                    props["framework_type"] = "web";
                    if (!hasStrongLibraryContract)
                    {
                        props[PropManifestType] = isDedicatedWorkerName && hasWorkerDeps ? "worker" : "application";
                        props.Remove(PropIsLibrary);
                    }
                    else if (!props.ContainsKey(PropManifestType))
                    {
                        props[PropManifestType] = "library";
                    }
                }
                else if (hasWorkerDeps)
                {
                    props["framework_type"] = "worker";
                    if (!props.ContainsKey(PropManifestType)) props[PropManifestType] = "worker";
                }
                else if (hasMobileDeps)
                {
                    props["framework_type"] = "mobile";
                    props["is_frontend_app"] = "true";
                    if (!props.ContainsKey(PropManifestType)) props[PropManifestType] = "application";
                }
                else if (hasFrontendDeps)
                {
                    props["framework_type"] = "frontend";

                    if (!props.ContainsKey(PropManifestType))
                    {
                        var isExplicitUiLib = !hasIndexHtml && !allDeps.Contains("next") &&
                                              !allDeps.Contains("nuxt") &&
                                              root.TryGetProperty(PropExports, out _);
                        props[PropManifestType] = isExplicitUiLib ? "library" : "application";

                        if (isExplicitUiLib)
                        {
                            props["is_ui_library"] = "true";
                            props[PropIsLibrary] = "true";
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
                    if (!props.ContainsKey(PropManifestType))
                    {
                        props[PropManifestType] = "application";
                        props["framework_type"] = "service";
                    }
                }
                else if (hasExplicitExports)
                {
                    if (!props.ContainsKey(PropManifestType))
                    {
                        props[PropManifestType] = "library";
                        props[PropIsLibrary] = "true";
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or JsonException)
            {
                // Ignore package.json parsing errors
            }
        }

        return props;
    }

    internal static ParsedProjectManifestMetadata? ExtractManifestMetadataFromBundle(
        string directoryPath,
        string[] filesInDirectory,
        CypherValue? pkgJsonRoot,
        Dictionary<string, string>? props)
    {
        string? pkgName = null;
        string? pkgVersion = null;
        string? publisher = null;
        string? mainFile = null;
        string? moduleFile = null;
        string? description = null;
        var hasBin = false;
        var hasContributes = false;
        var hasVsCodeEngine = false;
        const string keyHasNgPackage = "has_ng_package";
        const string propNgPackage = "ngPackage";
        const string propScripts = "scripts";
        var hasNgPackage = props?.GetValueOrDefault(keyHasNgPackage) == "true" ||
                           filesInDirectory.Any(f => Path.GetFileName(f).Equals("ng-package.json", StringComparison.OrdinalIgnoreCase));
        string? keywordsJson = null;
        string? extDepsJson = null;
        string? activationEvents = null;
        List<ManifestHookEntry>? hooks = null;
        HashSet<string>? exportTargets = null;
        string? dbEngine = null;
        string? dbFamily = null;

        if (pkgJsonRoot is { Kind: CypherValueKind.Object } root)
        {
            if (root.TryGetProperty(OntologyConstants.ManifestFields.Name, out var nameProp) && nameProp.Kind == CypherValueKind.String)
            {
                var raw = nameProp.AsNullableString();
                if (!string.IsNullOrWhiteSpace(raw)) pkgName = raw;
            }
            if (root.TryGetProperty(OntologyConstants.ManifestFields.Version, out var verProp) && verProp.Kind == CypherValueKind.String)
            {
                var raw = verProp.AsNullableString();
                if (!string.IsNullOrWhiteSpace(raw)) pkgVersion = raw;
            }
            if (root.TryGetProperty(OntologyConstants.ManifestFields.Publisher, out var pubProp) && pubProp.Kind == CypherValueKind.String)
            {
                publisher = pubProp.AsNullableString();
            }
            else if (!string.IsNullOrEmpty(pkgName) && pkgName.StartsWith('@') && pkgName.Contains('/'))
            {
                publisher = pkgName[1..pkgName.IndexOf('/')];
            }

            if (root.TryGetProperty(OntologyConstants.ManifestFields.Main, out var mainProp) && mainProp.Kind == CypherValueKind.String)
            {
                var mainVal = mainProp.AsNullableString();
                if (!string.IsNullOrWhiteSpace(mainVal))
                {
                    mainFile = mainVal;
                    exportTargets ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    exportTargets.Add(mainVal);
                }
            }
            if (root.TryGetProperty(OntologyConstants.ManifestFields.Module, out var modProp) && modProp.Kind == CypherValueKind.String)
            {
                var modVal = modProp.AsNullableString();
                if (!string.IsNullOrWhiteSpace(modVal))
                {
                    moduleFile = modVal;
                    exportTargets ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    exportTargets.Add(modVal);
                }
            }
            if (root.TryGetProperty(PropExports, out var expProp))
            {
                exportTargets ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                CollectExportTargets(expProp, exportTargets);
            }
            if (root.TryGetProperty(OntologyConstants.ManifestFields.Description, out var descProp) && descProp.Kind == CypherValueKind.String)
            {
                var desc = descProp.AsNullableString();
                if (!string.IsNullOrWhiteSpace(desc)) description = desc;
            }
            if (root.TryGetProperty(OntologyConstants.ManifestFields.Bin, out var binProp) &&
                ((binProp.Kind == CypherValueKind.String && !string.IsNullOrWhiteSpace(binProp.AsNullableString())) ||
                 (binProp.Kind == CypherValueKind.Object && binProp.AsObject().Count > 0)))
            {
                hasBin = true;
            }
            if (root.TryGetProperty(OntologyConstants.ManifestFields.Contributes, out var contribProp) && contribProp.Kind == CypherValueKind.Object)
            {
                hasContributes = true;
            }
            if (root.TryGetProperty(OntologyConstants.ManifestFields.Engines, out var engProp) && engProp.Kind == CypherValueKind.Object &&
                engProp.TryGetProperty(OntologyConstants.ManifestFields.VsCode, out _))
            {
                hasVsCodeEngine = true;
            }
            if (root.TryGetProperty(propNgPackage, out _))
            {
                hasNgPackage = true;
            }
            if (root.TryGetProperty(OntologyConstants.ManifestFields.Keywords, out var kwProp) && kwProp.Kind == CypherValueKind.Array)
            {
                var kws = kwProp.AsStringList().Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();
                if (kws.Length > 0) keywordsJson = JsonSerializer.Serialize(kws);
            }

            var extDeps = new List<string>();
            foreach (var propName in new[] { "extensionDependencies", "extensionPack" })
            {
                if (root.TryGetProperty(propName, out var arr) && arr.Kind == CypherValueKind.Array)
                {
                    foreach (var val in arr.AsStringList())
                    {
                        if (!string.IsNullOrWhiteSpace(val)) extDeps.Add(val);
                    }
                }
            }
            if (extDeps.Count > 0) extDepsJson = JsonSerializer.Serialize(extDeps);

            if (root.TryGetProperty(OntologyConstants.ManifestFields.ActivationEvents, out var actProp) && actProp.Kind == CypherValueKind.Array)
            {
                var acts = actProp.AsStringList().Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();
                if (acts.Length > 0) activationEvents = string.Join(",", acts);
            }

            if (root.TryGetProperty(propScripts, out var scriptsProp) && scriptsProp.Kind == CypherValueKind.Object)
            {
                foreach (var script in scriptsProp.AsObject())
                {
                    var isInstallHook = InstallHookNames.Contains(script.Key);
                    var isBuildHook = !isInstallHook && BuildHookNames.Contains(script.Key);
                    if (!isInstallHook && !isBuildHook) continue;
                    hooks ??= [];
                    hooks.Add(new ManifestHookEntry(
                        Name: script.Key,
                        Command: script.Value.AsString(),
                        EntryType: isInstallHook ? "lifecycle-hook" : "build-hook",
                        HookKind: isInstallHook ? "install" : "build"));
                }
            }

            foreach (var depSec in new[] { "dependencies", "devDependencies" })
            {
                if (root.TryGetProperty(depSec, out var dObj) && dObj.Kind == CypherValueKind.Object)
                {
                    foreach (var dep in dObj.AsObject())
                    {
                        if (dep.Key.Equals("pg", StringComparison.OrdinalIgnoreCase) ||
                            dep.Key.Equals("pg-promise", StringComparison.OrdinalIgnoreCase) ||
                            dep.Key.Equals("postgres", StringComparison.OrdinalIgnoreCase))
                        {
                            dbEngine = "PostgreSQL";
                            dbFamily = "relational";
                            break;
                        }
                    }
                }
                if (dbEngine != null) break;
            }
        }

        Dictionary<string, string>? tsPaths = null;
        foreach (var tsConfigName in new[] { "tsconfig.base.json", "tsconfig.json" })
        {
            var tsConfigPath = Path.Combine(directoryPath, tsConfigName);
            if (!WorkspaceDirectoryIndex.FileExistsInCurrentOrDisk(tsConfigPath) || !File.Exists(tsConfigPath)) continue;

            var tsConfigText = FileStreamReader.ReadTextOnce(tsConfigPath);
            if (string.IsNullOrEmpty(tsConfigText)) continue;

            try
            {
                var tsRoot = CypherValue.TryParseJsonText(tsConfigText);
                if (tsRoot.TryGetProperty(OntologyConstants.ManifestFields.CompilerOptions, out var compOpts) &&
                    compOpts.Kind == CypherValueKind.Object &&
                    compOpts.TryGetProperty(OntologyConstants.ManifestFields.Paths, out var pathsProp) &&
                    pathsProp.Kind == CypherValueKind.Object)
                {
                    foreach (var p in pathsProp.AsObject())
                    {
                        if (p.Value.Kind == CypherValueKind.Array && p.Value.AsArray() is { Count: > 0 } arr)
                        {
                            var rawTarget = arr[0].AsNullableString();
                            if (!string.IsNullOrEmpty(rawTarget))
                            {
                                tsPaths ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                                tsPaths[p.Key] = rawTarget.Replace('\\', '/');
                            }
                        }
                    }
                }
            }
            catch (JsonException)
            {
                // Ignore malformed tsconfig
            }
        }

        if (pkgName == null && pkgVersion == null && publisher == null && mainFile == null && moduleFile == null &&
            description == null && !hasBin && !hasContributes && !hasVsCodeEngine && !hasNgPackage &&
            keywordsJson == null && extDepsJson == null && activationEvents == null &&
            hooks == null && exportTargets == null && tsPaths == null && dbEngine == null)
        {
            return null;
        }

        return new ParsedProjectManifestMetadata(
            PackageName: pkgName,
            PackageVersion: pkgVersion,
            Publisher: publisher,
            MainFile: mainFile,
            ModuleFile: moduleFile,
            Description: description,
            HasBin: hasBin,
            HasContributes: hasContributes,
            HasVsCodeEngine: hasVsCodeEngine,
            HasNgPackage: hasNgPackage,
            KeywordsJson: keywordsJson,
            ExtensionDependenciesJson: extDepsJson,
            ActivationEvents: activationEvents,
            TsConfigPaths: tsPaths,
            LifecycleHooks: hooks,
            ExportTargets: exportTargets?.ToArray(),
            DatabaseDriverEngine: dbEngine,
            DatabaseDriverFamily: dbFamily);
    }

    private static void CollectExportTargets(CypherValue elem, HashSet<string> targets)
    {
        if (elem.Kind == CypherValueKind.String)
        {
            var s = elem.AsNullableString();
            if (!string.IsNullOrWhiteSpace(s) && !s.EndsWith(".d.ts", StringComparison.OrdinalIgnoreCase))
            {
                targets.Add(s);
            }
        }
        else if (elem.Kind == CypherValueKind.Object)
        {
            foreach (var p in elem.AsObject())
            {
                if (p.Key.Equals("types", StringComparison.OrdinalIgnoreCase)) continue;
                CollectExportTargets(p.Value, targets);
            }
        }
        else if (elem.Kind == CypherValueKind.Array)
        {
            foreach (var item in elem.AsArray())
            {
                CollectExportTargets(item, targets);
            }
        }
    }
}
