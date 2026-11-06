using System.Text.Json;
using System.Text.RegularExpressions;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Parser;

namespace CodeExplorer.Parser.TypeScript;

public partial class TypeScriptParser
{
    private static string? TryExtractWranglerName(string directoryPath, string[] filesInDirectory)
    {
        var folderName = Path.GetFileName(directoryPath.TrimEnd('/', '\\'));
        var wranglerToml = filesInDirectory.FirstOrDefault(f => Path.GetFileName(f).Equals("wrangler.toml", StringComparison.OrdinalIgnoreCase))
                           ?? (File.Exists(Path.Combine(directoryPath, "wrangler.toml")) ? Path.Combine(directoryPath, "wrangler.toml") : null);
        if (wranglerToml != null && File.Exists(wranglerToml))
        {
            try
            {
                foreach (var rawLine in PhysicalFileStore.ReadLines(wranglerToml))
                {
                    var line = rawLine.Trim();
                    if (line.StartsWith('#') || line.StartsWith('[')) continue;
                    if (line.StartsWith("name", StringComparison.OrdinalIgnoreCase))
                    {
                        var eqIdx = line.IndexOf('=');
                        if (eqIdx > 0)
                        {
                            var val = line[(eqIdx + 1)..].Trim().Trim('"', '\'');
                            if (!string.IsNullOrWhiteSpace(val))
                            {
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
                        }
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Ignore
            }
        }
        return null;
    }

    private static void InspectWranglerLayout(
        string directoryPath,
        string[] filesInDirectory,
        List<string> absorbedSubpaths,
        List<string> excludedSubpaths,
        List<string> entryPointPaths,
        List<string> sourceRoots)
    {
        var wranglerToml = filesInDirectory.FirstOrDefault(f => Path.GetFileName(f).Equals("wrangler.toml", StringComparison.OrdinalIgnoreCase))
                           ?? (File.Exists(Path.Combine(directoryPath, "wrangler.toml")) ? Path.Combine(directoryPath, "wrangler.toml") : null);
        if (wranglerToml != null && File.Exists(wranglerToml))
        {
            try
            {
                var content = PhysicalFileStore.ReadAllText(wranglerToml);
                foreach (var rawLine in content.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
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
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Ignore
            }
        }

        var workersSiteDir = Path.Combine(directoryPath, "workers-site");
        if (Directory.Exists(workersSiteDir))
        {
            if (!absorbedSubpaths.Contains("workers-site", StringComparer.OrdinalIgnoreCase))
            {
                absorbedSubpaths.Add("workers-site");
            }
            if (!sourceRoots.Contains("workers-site", StringComparer.OrdinalIgnoreCase))
            {
                sourceRoots.Add("workers-site");
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
    }

    private static void DetectBuildOrchestratorProperties(
        string directoryPath,
        string[] filesInDirectory,
        Dictionary<string, string> props)
    {
        var packageJsonPath = Path.Combine(directoryPath, "package.json");
        if (!File.Exists(packageJsonPath)) return;

        if (HasWranglerManifest(filesInDirectory)) return;

        try
        {
            var content = PhysicalFileStore.ReadAllText(packageJsonPath);
            var root = CypherValue.TryParseJsonText(content);

            var hasMainOrExports = root.TryGetProperty("main", out _) ||
                                   root.TryGetProperty("module", out _) ||
                                   root.TryGetProperty("exports", out _) ||
                                   root.TryGetProperty("bin", out _);
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
                d.StartWithIC("gulp") || d.StartWithIC("webpack") || d is "husky" or "lerna" or "turbo" or "nx" or "prettier" or "eslint");
            if (!hasBuildTools) return;

            var workersSubdir = Path.Combine(directoryPath, "workers");
            var hasNestedWorkers = Directory.Exists(workersSubdir) &&
                                   Directory.EnumerateFiles(workersSubdir, "wrangler.*", SearchOption.AllDirectories).Any();

            if (hasNestedWorkers)
            {
                props["is_build_orchestrator"] = "true";
                props["manifest_type"] = "worker";
                props["framework_type"] = "worker";
                props["cloud_runtime"] = "cloudflare-worker";
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            // Ignore
        }
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
                var content = PhysicalFileStore.ReadAllText(projectJsonPath);
                var pRoot = CypherValue.TryParseJsonText(content);

                if (pRoot.TryGetProperty("projectType", out var ptProp) &&
                    ptProp.Kind == CypherValueKind.String)
                {
                    var pt = ptProp.AsNullableString()?.ToLowerInvariant();

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

        // 3. Enrich with Angular workspace/package manifest properties if present
        Components.AngularComponentParser.EnrichManifestProperties(directoryPath, filesInDirectory, props);

        // 4. Inspect package.json
        var packageJsonPath = Path.Combine(directoryPath, "package.json");
        if (File.Exists(packageJsonPath))
        {
            try
            {
                var content = PhysicalFileStore.ReadAllText(packageJsonPath);
                var root = CypherValue.TryParseJsonText(content);

                // Check "bin" property
                if (root.TryGetProperty("bin", out var binProp))
                {
                    if (binProp.Kind == CypherValueKind.String &&
                        !string.IsNullOrWhiteSpace(binProp.AsNullableString()))
                    {
                        props["has_cli_bin"] = "true";
                        if (!props.ContainsKey("manifest_type")) props["manifest_type"] = "cli";
                    }
                    else if (binProp.Kind == CypherValueKind.Object &&
                             binProp.AsObject().Count > 0)
                    {
                        props["has_cli_bin"] = "true";
                        if (!props.ContainsKey("manifest_type")) props["manifest_type"] = "cli";
                    }
                }

                // Collect dependencies
                var allDeps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                if (root.TryGetProperty("dependencies", out var depsObj) &&
                    depsObj.Kind == CypherValueKind.Object)
                {
                    foreach (var prop in depsObj.AsObject()) allDeps.Add(prop.Key);
                }

                if (root.TryGetProperty("devDependencies", out var devDepsObj) &&
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
                            || d.StartWithIC("cloudflare-") || d.StartWithIC("@cloudflare/")) ||
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

                var pkgName =
                    root.TryGetProperty("name", out var np) && np.Kind == CypherValueKind.String
                        ? np.AsString().ToLowerInvariant()
                        : "";
                var projDirName = Path.GetFileName(directoryPath).ToLowerInvariant();

                var isDedicatedWorkerName = projDirName.Contains("worker") || projDirName.Contains("scheduler") ||
                                            projDirName.Contains("consumer") || pkgName.Contains("worker") ||
                                            pkgName.Contains("scheduler") || pkgName.Contains("consumer");

                var hasWorkerScripts = root.TryGetProperty("scripts", out var scrObj) &&
                                       scrObj.Kind == CypherValueKind.Object &&
                                       scrObj.AsObject().Any(s =>
                                           s.Key.StartWithIC("worker:") ||
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
                                       sObj.Kind == CypherValueKind.Object &&
                                       sObj.AsObject().Any(s => s.Value.AsString().Contains("npm publish", StringComparison.OrdinalIgnoreCase));

                var hasExplicitExports = root.TryGetProperty("exports", out _) ||
                                         root.TryGetProperty("types", out _) ||
                                         root.TryGetProperty("typings", out _) ||
                                         root.TryGetProperty("main", out _);

                var hasPrimaryAppStartScript = root.TryGetProperty("scripts", out var scriptsObj) &&
                                               scriptsObj.Kind == CypherValueKind.Object &&
                                               scriptsObj.AsObject().Any(s =>
                                                   s.Key.Equals("start", StringComparison.OrdinalIgnoreCase) ||
                                                   s.Key.Equals("start:prod", StringComparison.OrdinalIgnoreCase) ||
                                                   s.Key.Equals("serve", StringComparison.OrdinalIgnoreCase));

                var isVsCodeExtension = (root.TryGetProperty("engines", out var enginesProp) &&
                                         enginesProp.Kind == CypherValueKind.Object &&
                                         enginesProp.TryGetProperty("vscode", out _)) ||
                                        (root.TryGetProperty("contributes", out var contributesProp) &&
                                         contributesProp.Kind == CypherValueKind.Object) ||
                                        (root.TryGetProperty("activationEvents", out var actProp) &&
                                         actProp.Kind == CypherValueKind.Array) ||
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
                        var isExplicitUiLib = !hasIndexHtml && !allDeps.Contains("next") &&
                                              !allDeps.Contains("nuxt") &&
                                              root.TryGetProperty("exports", out _);
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

}
